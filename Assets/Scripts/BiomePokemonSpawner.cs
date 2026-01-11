using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class BiomePokemonSpawner : MonoBehaviour
{
    [Header("AR References")]
    public ARRaycastManager raycastManager;
    public ObjectSwapManager objectSwapManager; // Algılanan objelere göre spawn için

    [System.Serializable]
    public class BiomeEntry
    {
        public string biomeName;     // "forest", "water", "urban" gibi
        public GameObject pokemonPrefab;
    }

    [Header("Biome -> Pokemon eşleşmeleri")]
    public List<BiomeEntry> biomeEntries = new List<BiomeEntry>();

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
    string currentBiome = "";
    
    // Toplam spawn edilen Pokemon sayısı (tüm zamanlar)
    int totalSpawnedEver = 0;
    
    // Son spawn zamanı
    float lastSpawnTime = -999f;

    // Son algılanan biome (UI için)
    public string LastDetectedBiome { get; private set; } = "";
    
    // Toplam spawn edilen Pokemon sayısı
    public int TotalPokemonCount => currentPokemons.Count;
    
    // Toplam spawn (tüm zamanlar)
    public int TotalSpawnedEver => totalSpawnedEver;
    
    // Kalan bekleme süresi
    public float RemainingCooldown => Mathf.Max(0, spawnCooldown - (Time.time - lastSpawnTime));

    public void PlacePokemonForBiome(string biome)
    {
        if (string.IsNullOrEmpty(biome))
        {
            Debug.LogWarning("Biome boş geldi.");
            return;
        }

        // Son algılanan biome'u kaydet (UI için)
        LastDetectedBiome = biome;

        string normalizedBiome = biome.ToLower().Trim();

        // Cooldown kontrolü
        float timeSinceLastSpawn = Time.time - lastSpawnTime;
        if (timeSinceLastSpawn < spawnCooldown)
        {
            return;
        }

        // Eşleşen prefabı bul
        GameObject prefab = GetPrefabForBiome(biome);
        if (prefab == null)
        {
            Debug.LogWarning($"Biome için prefab bulunamadı: {biome}");
            return;
        }

        // Farklı bir biome algılandıysa eski Pokemonları temizle
        if (currentBiome != normalizedBiome)
        {
            if (currentPokemons.Count > 0)
            {
                Debug.Log($"Biome değişti: {currentBiome} -> {normalizedBiome}, eski Pokemonlar kaldırılıyor.");
                ClearCurrentPokemons();
            }
            currentBiome = normalizedBiome;
        }

        // Tek bir Pokemon spawn et
        SpawnSinglePokemon(prefab);
        
        // Spawn zamanını güncelle
        lastSpawnTime = Time.time;
        totalSpawnedEver++;
    }

    /// <summary>
    /// Tek bir Pokemon spawn et
    /// </summary>
    void SpawnSinglePokemon(GameObject prefab)
    {
        Vector3 finalPosition;
        Quaternion finalRotation;
        
        // Önce algılanan objelerin yanına spawn etmeyi dene
        if (spawnNearDetectedObjects && TryGetSpawnPositionNearObject(out finalPosition, out finalRotation))
        {
            Debug.Log($"Pokemon algılanan obje yakınına spawn ediliyor: {finalPosition}");
        }
        // Sonra AR plane üzerinde uygun nokta bul
        else if (TryGetSpawnPositionOnPlane(out finalPosition, out finalRotation))
        {
            Debug.Log($"Pokemon AR plane üzerinde spawn ediliyor: {finalPosition}");
        }
        // Son çare: Tahmini derinlikte spawn et
        else
        {
            finalPosition = GetEstimatedSpawnPosition();
            finalRotation = Quaternion.identity;
            Debug.Log($"Pokemon tahmini konuma spawn ediliyor: {finalPosition}");
        }

        // Pokemon oluştur
        GameObject pokemon = Instantiate(prefab, finalPosition, finalRotation);
        
        // Rastgele boyut varyasyonu uygula
        float randomScale = Random.Range(sizeVariation.x, sizeVariation.y);
        pokemon.transform.localScale *= randomScale;
        
        // Level ayarla
        SetPokemonLevel(pokemon);
        
        // Kameraya baksın
        LookAtCamera(pokemon);

        currentPokemons.Add(pokemon);
    }

    /// <summary>
    /// Algılanan objelerin yanında spawn pozisyonu bul
    /// </summary>
    bool TryGetSpawnPositionNearObject(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        
        if (objectSwapManager == null || objectSwapManager.LastDetectedObjects.Count == 0)
            return false;
        
        Camera cam = Camera.main;
        var detectedObjects = objectSwapManager.LastDetectedObjects;
        
        // Rastgele bir algılanan obje seç
        var randomObj = detectedObjects[Random.Range(0, detectedObjects.Count)];
        
        if (randomObj.center == null || randomObj.center.Length < 2)
            return false;
        
        // Objenin ekran pozisyonunu al
        float nx = randomObj.center[0];
        float ny = randomObj.center[1];
        Vector2 screenPos = new Vector2(nx * Screen.width, (1f - ny) * Screen.height);
        
        // Objenin derinliğini tahmin et (bbox boyutundan)
        float estimatedDepth = EstimateDepthFromBbox(randomObj);
        
        // AR plane bul
        var hits = new List<ARRaycastHit>();
        float groundY = cam.transform.position.y - 1.5f; // Varsayılan zemin
        
        if (raycastManager.Raycast(screenPos, hits, TrackableType.PlaneWithinPolygon))
        {
            groundY = hits[0].pose.position.y;
            estimatedDepth = Vector3.Distance(cam.transform.position, hits[0].pose.position);
        }
        else if (TryMultiRaycast(screenPos, out Vector3 planePos, out Quaternion planeRot))
        {
            groundY = planePos.y;
        }
        
        // Objenin dünya pozisyonunu hesapla
        Ray ray = cam.ScreenPointToRay(screenPos);
        Vector3 objectWorldPos = ray.origin + ray.direction * estimatedDepth;
        objectWorldPos.y = groundY;
        
        // Objenin yanında rastgele bir nokta seç
        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float randomDist = Random.Range(objectProximityMin, objectProximityMax);
        
        position = objectWorldPos + new Vector3(
            Mathf.Cos(randomAngle) * randomDist,
            0,
            Mathf.Sin(randomAngle) * randomDist
        );
        
        // Kameraya çok yakın mı kontrol et
        float distToCamera = Vector3.Distance(cam.transform.position, position);
        if (distToCamera < minSpawnDistance)
        {
            // Daha uzağa taşı
            Vector3 dirFromCamera = (position - cam.transform.position).normalized;
            dirFromCamera.y = 0;
            position = cam.transform.position + dirFromCamera * minSpawnDistance;
            position.y = groundY;
        }
        
        // Mevcut Pokemon'lara çok yakın mı kontrol et
        if (IsPositionTooClose(position, GetExistingPositions()))
        {
            return false;
        }
        
        rotation = Quaternion.identity;
        return true;
    }

    /// <summary>
    /// AR plane üzerinde spawn pozisyonu bul
    /// </summary>
    bool TryGetSpawnPositionOnPlane(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        
        Camera cam = Camera.main;
        
        // Ekranda rastgele noktalar dene
        int maxAttempts = 20;
        for (int i = 0; i < maxAttempts; i++)
        {
            // Ekranın alt yarısında rastgele nokta (zemin olma olasılığı daha yüksek)
            float randomX = Random.Range(Screen.width * 0.2f, Screen.width * 0.8f);
            float randomY = Random.Range(Screen.height * 0.2f, Screen.height * 0.6f);
            Vector2 screenPoint = new Vector2(randomX, randomY);
            
            var hits = new List<ARRaycastHit>();
            if (raycastManager.Raycast(screenPoint, hits, TrackableType.PlaneWithinPolygon))
            {
                Vector3 hitPos = hits[0].pose.position;
                float distToCamera = Vector3.Distance(cam.transform.position, hitPos);
                
                // Mesafe kontrolü
                if (distToCamera >= minSpawnDistance && distToCamera <= maxSpawnDistance)
                {
                    // Mevcut Pokemon'lara yakınlık kontrolü
                    if (!IsPositionTooClose(hitPos, GetExistingPositions()))
                    {
                        position = hitPos;
                        rotation = hits[0].pose.rotation;
                        return true;
                    }
                }
            }
        }
        
        return false;
    }

    /// <summary>
    /// Tahmini spawn pozisyonu hesapla (AR plane bulunamazsa)
    /// </summary>
    Vector3 GetEstimatedSpawnPosition()
    {
        Camera cam = Camera.main;
        
        // Rastgele bir derinlik seç
        float randomDepth = Random.Range(minSpawnDistance, maxSpawnDistance);
        
        // Rastgele bir açı seç (kameranın önünde, biraz sağa/sola)
        float randomAngle = Random.Range(-30f, 30f) * Mathf.Deg2Rad;
        
        // Kameranın ileri yönü
        Vector3 forward = cam.transform.forward;
        forward.y = 0;
        forward.Normalize();
        
        // Sağ yön
        Vector3 right = cam.transform.right;
        right.y = 0;
        right.Normalize();
        
        // Yatay pozisyon
        Vector3 horizontalDir = forward * Mathf.Cos(randomAngle) + right * Mathf.Sin(randomAngle);
        Vector3 position = cam.transform.position + horizontalDir * randomDepth;
        
        // Y pozisyonu: kameranın 1.5m altı (yaklaşık zemin)
        position.y = cam.transform.position.y - 1.5f;
        
        return position;
    }

    /// <summary>
    /// Bbox boyutundan derinlik tahmini
    /// </summary>
    float EstimateDepthFromBbox(DetectedObject obj)
    {
        if (obj.bbox == null || obj.bbox.Length < 4)
            return (minSpawnDistance + maxSpawnDistance) / 2f;
        
        float bboxHeight = obj.Height;
        
        if (bboxHeight <= 0.01f)
            return maxSpawnDistance;
        
        // Basit tahmin: büyük bbox = yakın, küçük bbox = uzak
        // bboxHeight 0.5 -> yakın (2m), bboxHeight 0.05 -> uzak (8m)
        float depth = Mathf.Lerp(maxSpawnDistance, minSpawnDistance, bboxHeight * 2f);
        
        return Mathf.Clamp(depth, minSpawnDistance, maxSpawnDistance);
    }

    /// <summary>
    /// Çevredeki noktalara raycast yaparak plane bul
    /// </summary>
    bool TryMultiRaycast(Vector2 centerScreen, out Vector3 foundPos, out Quaternion foundRot)
    {
        var hits = new List<ARRaycastHit>();
        int numPoints = 8;
        
        for (int i = 0; i < numPoints; i++)
        {
            float angle = (360f / numPoints) * i * Mathf.Deg2Rad;
            float offsetX = Mathf.Cos(angle) * multiRaycastRadius;
            float offsetY = Mathf.Sin(angle) * multiRaycastRadius;
            
            Vector2 testPoint = centerScreen + new Vector2(offsetX, offsetY);
            
            if (testPoint.x < 0 || testPoint.x > Screen.width || 
                testPoint.y < 0 || testPoint.y > Screen.height)
                continue;
            
            if (raycastManager.Raycast(testPoint, hits, TrackableType.PlaneWithinPolygon))
            {
                foundPos = hits[0].pose.position;
                foundRot = hits[0].pose.rotation;
                return true;
            }
        }
        
        foundPos = Vector3.zero;
        foundRot = Quaternion.identity;
        return false;
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
    /// Pokemon'a level ata
    /// </summary>
    void SetPokemonLevel(GameObject pokemon)
    {
        WildPokemon wildPokemon = pokemon.GetComponent<WildPokemon>();
        if (wildPokemon == null) return;
        
        // Dinamik zorluk: Oyuncu ilerledikçe daha yüksek level Pokemon çıksın
        int effectiveMaxLevel = maxPokemonLevel;
        
        if (progressiveDifficulty && PokemonBag.Instance != null)
        {
            // Her 2 yakalanan Pokemon için max level +1
            int caughtCount = PokemonBag.Instance.CaughtPokemon.Count;
            int bonusLevel = caughtCount / 2;
            effectiveMaxLevel = Mathf.Min(maxPokemonLevel, minPokemonLevel + 5 + bonusLevel);
        }
        
        // Rastgele level belirle (ağırlıklı - düşük level daha olası)
        int randomLevel = GetWeightedRandomLevel(minPokemonLevel, effectiveMaxLevel);
        
        // WildPokemon'a level ayarlarını ver
        wildPokemon.minLevel = minPokemonLevel;
        wildPokemon.maxLevel = effectiveMaxLevel;
        wildPokemon.level = randomLevel;
        
        Debug.Log($"Pokemon level atandı: {randomLevel} (aralık: {minPokemonLevel}-{effectiveMaxLevel})");
    }
    
    /// <summary>
    /// Ağırlıklı rastgele level - düşük level daha olası
    /// </summary>
    int GetWeightedRandomLevel(int min, int max)
    {
        // %50 düşük level (min - orta), %35 orta level, %15 yüksek level
        float roll = Random.value;
        
        int range = max - min;
        
        if (roll < 0.5f) // %50 - Düşük level
        {
            return Random.Range(min, min + range / 3 + 1);
        }
        else if (roll < 0.85f) // %35 - Orta level
        {
            return Random.Range(min + range / 3, min + (range * 2) / 3 + 1);
        }
        else // %15 - Yüksek level (nadir)
        {
            return Random.Range(min + (range * 2) / 3, max + 1);
        }
    }

    /// <summary>
    /// Objeyi kameraya baktır
    /// </summary>
    void LookAtCamera(GameObject obj)
    {
        Camera cam = Camera.main;
        Vector3 lookDir = cam.transform.position - obj.transform.position;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            obj.transform.rotation = Quaternion.LookRotation(-lookDir);
        }
    }

    GameObject GetPrefabForBiome(string biome)
    {
        biome = biome.ToLower();

        foreach (var entry in biomeEntries)
        {
            if (biome.Contains(entry.biomeName.ToLower()))
                return entry.pokemonPrefab;
        }

        return null;
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
        currentBiome = "";
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
