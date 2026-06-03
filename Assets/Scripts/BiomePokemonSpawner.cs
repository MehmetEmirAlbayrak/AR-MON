using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using ARMON.Data;
using ARMON.AR;

public class BiomePokemonSpawner : MonoBehaviour
{
    [Header("Data")]
    public PokemonSpeciesRegistry registry;

    [Header("AR References")]
    public ARRaycastManager raycastManager;
    public ARAnchorManager  anchorManager;
    public ARPlaneManager   planeManager;
    public ObjectSwapManager objectSwapManager;

    [Header("Yerleşim ayarları")]
    [Tooltip("Kameradan minimum uzaklık (metre)")]
    public float minSpawnDistance = 2f;

    [Tooltip("Kameradan maksimum uzaklık (metre)")]
    public float maxSpawnDistance = 8f;

    [Tooltip("Pokemonlar arası minimum mesafe (metre)")]
    public float minDistanceBetweenPokemons = 0.5f;

    [Tooltip("Merkez noktasından maksimum yayılma mesafesi (metre)")]
    public float spreadRadius = 1.5f;

    [Tooltip("Pokemon boyut varyasyonu (0.5 = yarı boyut, 1.5 = 1.5 kat)")]
    public Vector2 sizeVariation = new Vector2(0.7f, 1.3f);

    [Header("Pokemon Level Ayarları")]
    [Tooltip("Minimum spawn level")]
    public int minPokemonLevel = 1;

    [Tooltip("Maksimum spawn level")]
    public int maxPokemonLevel = 20;

    [Tooltip("Oyuncu ilerledikçe level artışı (her yakalanan Pokemon için +0.5 max level)")]
    public bool progressiveDifficulty = true;

    [Header("Derinlik Tahmini")]
    [Tooltip("Algılanan objelere göre spawn pozisyonu belirle")]
    public bool spawnNearDetectedObjects = true;

    [Tooltip("Algılanan objeye minimum uzaklık (metre)")]
    public float objectProximityMin = 0.5f;

    [Tooltip("Algılanan objeye maksimum uzaklık (metre)")]
    public float objectProximityMax = 2f;

    [Tooltip("Çoklu raycast için arama yarıçapı (piksel)")]
    public float multiRaycastRadius = 150f;

    [Header("Spawn Zamanlama")]
    [Tooltip("Pokemon spawn'ları arasındaki bekleme süresi (saniye)")]
    public float spawnCooldown = 10f;

    // Mevcut Pokemonlar
    List<GameObject> currentPokemons = new List<GameObject>();

    // Toplam spawn edilen Pokemon sayısı (tüm zamanlar)
    int totalSpawnedEver = 0;

    // Son spawn zamanı
    float lastSpawnTime = -999f;

    // AR position resolver
    ARPositionResolver _resolver;

    // Son algılanan biome (UI için)
    public string LastDetectedBiome { get; private set; } = "";

    // Toplam spawn edilen Pokemon sayısı
    public int TotalPokemonCount => currentPokemons.Count;

    // Toplam spawn (tüm zamanlar)
    public int TotalSpawnedEver => totalSpawnedEver;

    // Kalan bekleme süresi
    public float RemainingCooldown => Mathf.Max(0, spawnCooldown - (Time.time - lastSpawnTime));

    void Start()
    {
        // Outdoor ayarları — ObjectSwapManager ile aynı tutuluyor ki uzak Pokemon'lar da
        // inferred ground sayesinde doğru zemine otursun.
        _resolver = new ARPositionResolver
        {
            depthNearMeters = 0.5f,
            depthFarMeters  = 18f,
            fallbackMeters  = 4f,
            groundOffsetY   = 1.5f,
        };
    }

    public void PlacePokemonForBiome(string biome)
    {
        if (registry == null) { Debug.LogError("BiomePokemonSpawner.registry not assigned"); return; }
        if (string.IsNullOrEmpty(biome)) return;
        LastDetectedBiome = biome;

        float since = Time.time - lastSpawnTime;
        if (since < spawnCooldown) return;

        var pool = registry.GetPoolForBiome(biome);
        if (pool == null) { Debug.LogWarning("No pool for biome " + biome); return; }

        bool allowLegendary = pool.legendary != null;
        Rarity rolled = RarityRoll.Roll(Random.value, allowLegendary);
        PokemonSpecies species = pool.GetByRarity(rolled);
        if (species == null || species.basePrefab == null)
        {
            Debug.LogWarning($"Species missing for {biome}/{rolled}");
            return;
        }

        SpawnSpecies(species);
        lastSpawnTime = Time.time;
        totalSpawnedEver++;
    }

    void SpawnSpecies(PokemonSpecies species)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // Destroy edilmiş entry'leri temizle (catch / battle defeat sonrası).
        currentPokemons.RemoveAll(p => p == null);

        // Pick screen pos: detected object center if any, else random in lower half
        Vector2 screenPos;
        float depthHint = 0f;
        if (objectSwapManager != null
            && objectSwapManager.LastDetectedObjects.Count > 0)
        {
            var obj = objectSwapManager.LastDetectedObjects[
                Random.Range(0, objectSwapManager.LastDetectedObjects.Count)];
            if (obj.center != null && obj.center.Length >= 2)
            {
                screenPos = new Vector2(
                    obj.center[0] * Screen.width,
                    (1f - obj.center[1]) * Screen.height);
                depthHint = obj.depth;
            }
            else screenPos = RandomScreenPos();
        }
        else screenPos = RandomScreenPos();

        if (!_resolver.TryResolve(screenPos, depthHint, raycastManager, planeManager, out var r))
            return;

        float dist = Vector3.Distance(cam.transform.position, r.position);
        if (dist < minSpawnDistance || dist > maxSpawnDistance)
        {
            Debug.Log($"[Spawner] {species.speciesId} reddedildi — mesafe {dist:F2}m (min={minSpawnDistance}, max={maxSpawnDistance})");
            return;
        }

        Debug.Log($"[Spawner] {species.speciesId} spawn → src={r.source} pos={r.position} dist={dist:F2}m groundY={ARMON.AR.ARPositionResolver.inferredGroundY:F3}");

        if (IsPositionTooClose(r.position, GetExistingPositions())) return;

        ARAnchor anchor = ARAnchorUtil.CreateAnchor(r, anchorManager, $"PokeAnchor_{species.speciesId}");
        GameObject pokemon = Instantiate(species.basePrefab, anchor.transform);
        pokemon.transform.localPosition = Vector3.zero;
        pokemon.transform.localRotation = Quaternion.identity;

        ApplyBodyTint(pokemon, species.bodyTint);

        var wp = pokemon.GetComponent<WildPokemon>();
        if (wp != null)
        {
            wp.species  = species;
            wp.minLevel = minPokemonLevel;
            wp.maxLevel = maxPokemonLevel;
            wp.level    = Random.Range(minPokemonLevel, maxPokemonLevel + 1);
        }

        currentPokemons.Add(pokemon);
    }

    static Vector2 RandomScreenPos()
    {
        return new Vector2(
            Random.Range(Screen.width * 0.2f, Screen.width * 0.8f),
            Random.Range(Screen.height * 0.2f, Screen.height * 0.6f));
    }

    static void ApplyBodyTint(GameObject root, Color tint)
    {
        if (tint == Color.white) return;
        foreach (var rend in root.GetComponentsInChildren<Renderer>())
        {
            var mat = rend.material; // instance copy
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
        }
    }

    /// <summary>
    /// Mevcut Pokemon pozisyonlarını al
    /// </summary>
    List<Vector3> GetExistingPositions()
    {
        List<Vector3> positions = new List<Vector3>();
        foreach (var p in currentPokemons)
        {
            if (p != null)
                positions.Add(p.transform.position);
        }
        return positions;
    }

    /// <summary>
    /// Pozisyon mevcut pozisyonlara çok yakın mı
    /// </summary>
    bool IsPositionTooClose(Vector3 position, List<Vector3> existingPositions)
    {
        foreach (var existing in existingPositions)
        {
            if (Vector3.Distance(position, existing) < minDistanceBetweenPokemons)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Mevcut Pokemonları temizle
    /// </summary>
    void ClearCurrentPokemons()
    {
        foreach (var pokemon in currentPokemons)
        {
            if (pokemon != null)
                Destroy(pokemon);
        }
        currentPokemons.Clear();
    }

    /// <summary>
    /// Tüm spawn geçmişini temizle
    /// </summary>
    public void ResetSpawnHistory()
    {
        ClearCurrentPokemons();
        LastDetectedBiome = "";
        totalSpawnedEver = 0;
        lastSpawnTime = -999f;
    }

    /// <summary>
    /// Sadece cooldown'ı sıfırla
    /// </summary>
    public void ResetCooldown()
    {
        lastSpawnTime = -999f;
    }

    /// <summary>
    /// Mevcut Pokemonları kaldır
    /// </summary>
    public void RemoveCurrentPokemons()
    {
        ClearCurrentPokemons();
    }
}
