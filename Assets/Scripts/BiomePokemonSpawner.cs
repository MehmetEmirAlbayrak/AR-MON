using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
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

    [Tooltip("Tüm vahşi Pokemonlara uygulanan global ölçek (1 = orijinal). Hızlıca büyüklük ayarı için bunu kullan.")]
    [Range(0.1f, 2f)]
    public float globalScale = 0.5f;

    [Tooltip("Pokemon boyut varyasyonu (0.5 = yarı boyut, 1.5 = 1.5 kat) — globalScale ile çarpılır")]
    public Vector2 sizeVariation = new Vector2(0.85f, 1.15f);

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

    [Header("Zemin Doğrulama")]
    [Tooltip("True: spawn yalnızca gerçek plane hit'i veya öğrenilmiş zemin (inferred ground) üzerinde. " +
             "Hiç plane görülmeden depth/fallback tahminiyle spawn (havada kalma bug'ı) engellenir.")]
    public bool requireGroundedSpawn = true;
    [Tooltip("Plane hit'lerinde plane'in her iki boyutu da en az bu kadar olmalı (metre)")]
    public float minPlaneSize = 0.3f;
    [Tooltip("Plane hit'i kenardan en az bu kadar içeride olmalı (metre)")]
    public float planeEdgeMargin = 0.15f;
    [Tooltip("Zeminle z-fighting önlemek için spawn'a eklenen Y ofseti (metre)")]
    public float spawnYOffset = 0.01f;

    [Header("Yaşam Döngüsü")]
    [Tooltip("Aynı anda sahnede olabilecek maksimum vahşi Pokemon (birikmeyi önler)")]
    public int maxConcurrentPokemons = 5;
    [Tooltip("Kameradan bu kadar uzaklaşan Pokemon'lar silinir (0 = kapalı)")]
    public float despawnDistance = 20f;
    public float despawnCheckInterval = 2f;

    // Spawn kaydı: pokemon + anchor + (varsa) üzerine oturduğu plane id'si.
    // Plane AR sistemi tarafından kaldırılırsa üzerindeki Pokemon da silinir.
    class TrackedSpawn
    {
        public GameObject pokemon;
        public ARAnchor anchor;
        public TrackableId planeId = TrackableId.invalidId;
    }

    readonly List<TrackedSpawn> currentPokemons = new List<TrackedSpawn>();

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
        if (despawnDistance > 0f)
            InvokeRepeating(nameof(DespawnTick), despawnCheckInterval, despawnCheckInterval);
    }

    void OnEnable()
    {
        if (planeManager != null)
            planeManager.trackablesChanged.AddListener(OnPlanesChanged);
    }

    void OnDisable()
    {
        if (planeManager != null)
            planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
    }

    /// <summary>
    /// AR sistemi bir plane'i kaldırdığında üzerine spawn edilmiş Pokemon'ları da kaldır.
    /// Aksi halde objeler kaybolan zeminin üstünde havada asılı kalır.
    /// </summary>
    void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
    {
        if (args.removed == null || args.removed.Count == 0) return;
        foreach (var removed in args.removed)
        {
            TrackableId removedId = removed.Key;
            for (int i = currentPokemons.Count - 1; i >= 0; i--)
            {
                var t = currentPokemons[i];
                if (t.planeId != removedId) continue;
                Debug.Log($"[Spawner] Plane {removedId} kaldırıldı → üzerindeki Pokemon siliniyor");
                DestroyTrackedSpawn(t);
                currentPokemons.RemoveAt(i);
            }
        }
    }

    void DespawnTick()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        for (int i = currentPokemons.Count - 1; i >= 0; i--)
        {
            var t = currentPokemons[i];
            if (t.pokemon == null) { currentPokemons.RemoveAt(i); continue; }
            float dist = Vector3.Distance(cam.transform.position, t.pokemon.transform.position);
            if (dist > despawnDistance)
            {
                Debug.Log($"[Spawner] {t.pokemon.name} {dist:F1}m uzakta — despawn");
                DestroyTrackedSpawn(t);
                currentPokemons.RemoveAt(i);
            }
        }
    }

    static void DestroyTrackedSpawn(TrackedSpawn t)
    {
        // Anchor parent'ı yok etmek child pokemon'u da yok eder; anchor yoksa pokemon'u doğrudan sil.
        if (t.anchor != null) Destroy(t.anchor.gameObject);
        else if (t.pokemon != null) Destroy(t.pokemon);
    }

    public void PlacePokemonForBiome(string biome)
    {
        if (registry == null) { Debug.LogError("BiomePokemonSpawner.registry not assigned"); return; }
        if (string.IsNullOrEmpty(biome)) return;
        LastDetectedBiome = biome;

        // İstek havadayken tracking düşmüş olabilir — tracking yokken spawn etme.
        if (ARSession.state != ARSessionState.SessionTracking)
        {
            Debug.Log("[Spawner] AR tracking yok — spawn iptal");
            return;
        }

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

        // Cooldown YALNIZCA başarılı spawn'da tüketilir. Eskiden reddedilen denemeler de
        // 10 sn'lik cooldown'u yiyordu — uzak/geçersiz taramalar oyunu "ölü" hissettiriyordu.
        if (SpawnSpecies(species))
        {
            lastSpawnTime = Time.time;
            totalSpawnedEver++;
        }
    }

    /// <summary>Spawn dener; yalnızca gerçekten Instantiate olduysa true döner.</summary>
    bool SpawnSpecies(PokemonSpecies species)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        // Destroy edilmiş entry'leri temizle (catch / battle defeat sonrası).
        currentPokemons.RemoveAll(t => t.pokemon == null);

        // Birikme önleme: sahnedeki vahşi Pokemon sayısı sınırlı.
        if (currentPokemons.Count >= maxConcurrentPokemons)
        {
            Debug.Log($"[Spawner] Maksimum {maxConcurrentPokemons} Pokemon sahnede — spawn iptal");
            return false;
        }

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
            return false;

        // ZEMİN DOĞRULAMA: hiç plane görülmeden depth/fallback tahminiyle spawn etme.
        // (InferredGround da gerçek bir plane'den öğrenilmiş Y'dir — uzak outdoor spawn'ları korur.)
        if (requireGroundedSpawn && !ARPositionResolver.IsGroundedSource(r.source))
        {
            Debug.Log($"[Spawner] {species.speciesId} reddedildi — zemin yok (src={r.source}). Önce zemini tara.");
            ARMON.UI.AR.ScanStatusUI.NotifyGroundRequired();
            return false;
        }

        Vector3 camPos = cam.transform.position;
        float dist = Vector3.Distance(camPos, r.position);

        if (dist < minSpawnDistance)
        {
            Debug.Log($"[Spawner] {species.speciesId} reddedildi — çok yakın ({dist:F2}m < {minSpawnDistance}m)");
            return false;
        }

        if (dist > maxSpawnDistance)
        {
            // UZAK TARAMA = İPTAL DEĞİL. Gerçek dünyada uzaktaki ormanı taramak yine forest
            // Pokemon'u doğurmalı; ama 20m+ spawn görünmez, ulaşılmaz ve concurrent slot'u
            // boşa yer. Çözüm: spawn'ı AYNI YÖNDE maxSpawnDistance'a çek — öğrenilmiş
            // zeminin üstünde, görünür ve yakalanabilir.
            Vector3 dir = r.position - camPos;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            dir.Normalize();

            Vector3 clamped = camPos + dir * maxSpawnDistance;
            clamped.y = ARPositionResolver.HasInferredGround
                ? ARPositionResolver.inferredGroundY
                : r.position.y;

            Debug.Log($"[Spawner] {species.speciesId} uzak tarama ({dist:F1}m) → {maxSpawnDistance}m'e çekildi (yön korundu)");

            r.position = clamped;
            r.rotation = Quaternion.LookRotation(dir); // resolver konvansiyonu: yalnız yaw, kameradan uzağa
            // Orijinal uzak plane hit'i artık bu noktayı temsil etmiyor —
            // zemin-projeksiyonlu spawn olarak işaretle ki plane doğrulamaları atlansın.
            r.source = ResolveSource.InferredGround;
            r.plane  = null;
            dist = maxSpawnDistance;
        }

        // Plane hit'lerinde ek doğrulama: boyut + kenar payı (yalnız clamp'lenmemiş yakın hit'ler).
        if (r.source == ResolveSource.Plane && r.plane != null)
        {
            if (!ARPositionResolver.IsPlaneLargeEnough(r.plane, minPlaneSize))
            {
                Debug.Log($"[Spawner] {species.speciesId} reddedildi — plane çok küçük ({r.plane.size.x:F2}x{r.plane.size.y:F2}m < {minPlaneSize}m)");
                return false;
            }
            if (!ARPositionResolver.IsAwayFromPlaneEdge(r.plane, r.position, planeEdgeMargin))
            {
                Debug.Log($"[Spawner] {species.speciesId} reddedildi — plane kenarına çok yakın");
                return false;
            }
        }

        Debug.Log($"[Spawner] {species.speciesId} spawn → src={r.source} pos={r.position} dist={dist:F2}m groundY={ARMON.AR.ARPositionResolver.inferredGroundY:F3}");

        if (IsPositionTooClose(r.position, GetExistingPositions())) return false;

        ARAnchor anchor = ARAnchorUtil.CreateAnchor(r, anchorManager, $"PokeAnchor_{species.speciesId}");
        GameObject pokemon = Instantiate(species.basePrefab, anchor.transform);
        // Hafif Y ofseti: model zeminle aynı düzlemde başlarsa z-fighting/yarı gömülme olur.
        pokemon.transform.localPosition = new Vector3(0f, spawnYOffset, 0f);
        pokemon.transform.localRotation = Quaternion.identity;
        float variation = Random.Range(sizeVariation.x, sizeVariation.y);
        pokemon.transform.localScale = pokemon.transform.localScale * (globalScale * variation);

        ApplyBodyTint(pokemon, species.bodyTint);

        var wp = pokemon.GetComponent<WildPokemon>();
        if (wp != null)
        {
            wp.species  = species;
            wp.minLevel = minPokemonLevel;
            wp.maxLevel = maxPokemonLevel;
            wp.level    = Random.Range(minPokemonLevel, maxPokemonLevel + 1);
        }

        currentPokemons.Add(new TrackedSpawn
        {
            pokemon = pokemon,
            anchor  = anchor,
            planeId = r.plane != null ? r.plane.trackableId : TrackableId.invalidId,
        });

        return true;
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
        foreach (var t in currentPokemons)
        {
            if (t.pokemon != null)
                positions.Add(t.pokemon.transform.position);
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
    /// Mevcut Pokemonları temizle (anchor'larıyla birlikte)
    /// </summary>
    void ClearCurrentPokemons()
    {
        foreach (var t in currentPokemons)
        {
            DestroyTrackedSpawn(t);
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
