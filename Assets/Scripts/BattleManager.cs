using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ARMON.Data;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }
    
    [Header("Pokemon Prefab")]
    [Tooltip("Oyuncunun Pokemon'u için kullanılacak prefab (boş bırakılırsa küp oluşturulur)")]
    public GameObject playerPokemonPrefab;
    
    [Header("Spawn Ayarları")]
    public float spawnDistance = 2f; // Kameradan ne kadar uzakta spawn olacak
    public float spawnHeight = 0f; // Yerden yükseklik
    
    [Header("Savaş Ayarları")]
    public float attackRange = 1.5f; // Saldırı menzili - yakın mesafe
    public float attackCooldown = 1.5f; // Saldırılar arası bekleme

    [Header("AR References")]
    public ARAnchorManager  anchorManager;
    public ARRaycastManager raycastManager;
    public ARPlaneManager   planeManager;

    [Header("Data")]
    public PokemonSpeciesRegistry registry;

    [Header("Combat")]
    public GameObject beamPrefab; // assign BeamProjectile.prefab in scene/inspector

    public void FireBeam(Vector3 fromWorld, WildPokemon target, ARMON.Data.PokemonSkill skill, int attackerAttack)
    {
        if (beamPrefab == null || target == null || skill == null) return;
        var go = Instantiate(beamPrefab, fromWorld, Quaternion.identity);
        var bp = go.GetComponent<BeamProjectile>();
        if (bp != null) bp.Init(fromWorld, target, skill, attackerAttack);
    }
    
    // Mevcut savaşan Pokemon
    private GameObject currentPlayerPokemon;
    private PlayerPokemonController currentController;
    private int currentPokemonIndex = -1;
    
    public bool HasActivePokemon => currentPlayerPokemon != null;
    public PokemonData ActivePokemonData => currentPokemonIndex >= 0 ? PokemonBag.Instance?.GetPokemonAt(currentPokemonIndex) : null;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // Dokunma/Tıklama kontrolü
        HandleTouchInput();
        
        // Opsiyonel: Test için klavye kontrolleri
        #if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.R))
        {
            RecallPokemon();
        }
        #endif
    }
    
    /// <summary>
    /// Dokunma/Tıklama girişini işle
    /// </summary>
    void HandleTouchInput()
    {
        // Envanter açıksa dokunmayı işleme
        if (PokemonInventoryUI.Instance != null && PokemonInventoryUI.Instance.IsOpen)
            return;
        
        // Pokeball seçiliyse saldırı emri verme (fırlatma modu aktif)
        if (PokeballController.Instance != null && PokeballController.Instance.IsPokeballSelected)
            return;
        
        // Aktif Pokemon yoksa işlem yapma
        if (currentController == null) return;
        
        // Tıklama/Dokunma algılama
        bool inputDetected = false;
        Vector3 inputPosition = Vector3.zero;
        
        #if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
        {
            inputDetected = true;
            inputPosition = Input.mousePosition;
        }
        #else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            inputDetected = true;
            inputPosition = Input.GetTouch(0).position;
        }
        #endif
        
        if (!inputDetected) return;
        
        // Raycast ile neye dokunulduğunu bul
        Ray ray = Camera.main.ScreenPointToRay(inputPosition);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, 100f))
        {
            // Kendi Pokemon'umuza mı dokunduk?
            PlayerPokemonController playerPokemon = hit.collider.GetComponent<PlayerPokemonController>();
            if (playerPokemon == null)
            {
                playerPokemon = hit.collider.GetComponentInParent<PlayerPokemonController>();
            }
            
            if (playerPokemon != null && playerPokemon == currentController)
            {
                // Kendi Pokemon'umuza dokunduk - geri çağır
                Debug.Log("Kendi Pokemon'una dokunuldu - geri çağırılıyor!");
                RecallPokemon();
                return;
            }
            
            // Vahşi Pokemon'a mı dokunduk?
            WildPokemon wildPokemon = hit.collider.GetComponent<WildPokemon>();
            if (wildPokemon == null)
            {
                wildPokemon = hit.collider.GetComponentInParent<WildPokemon>();
            }
            
            if (wildPokemon != null && !wildPokemon.IsFainted)
            {
                // Vahşi Pokemon'a dokunduk - saldırı emri ver
                Debug.Log($"Vahşi {wildPokemon.pokemonName}'a saldırı emri verildi!");
                currentController.SetTarget(wildPokemon);
                return;
            }
        }
    }

    /// <summary>
    /// Çantadan Pokemon çıkar
    /// </summary>
    public void SummonPokemon(int bagIndex)
    {
        if (PokemonBag.Instance == null)
        {
            Debug.LogWarning("PokemonBag bulunamadı!");
            return;
        }
        
        PokemonData pokemonData = PokemonBag.Instance.GetPokemonAt(bagIndex);
        if (pokemonData == null)
        {
            Debug.LogWarning($"Çantada {bagIndex}. sırada Pokemon yok!");
            return;
        }
        
        if (pokemonData.IsFainted)
        {
            Debug.LogWarning($"{pokemonData.pokemonName} bayılmış durumda! Önce iyileştir.");
            return;
        }
        
        // Zaten bir Pokemon varsa geri çağır
        if (currentPlayerPokemon != null)
        {
            RecallPokemon();
        }
        
        // Spawn pozisyonu: ÖNCE oyuncunun nişanladığı plane noktası (ekran ortası AR raycast).
        // Kamera bir plane'e bakıyorsa Pokemon tam oraya doğar; bakmıyorsa kamera-önü fallback.
        Vector3 spawnPos = TryGetAimedSpawnPosition(out Vector3 aimedPos)
            ? aimedPos
            : CalculateSpawnPosition();
        
        // Kayıtlı prefab'ı bul — PokemonSpeciesRegistry üzerinden
        GameObject prefabToUse = null;
        ARMON.Data.PokemonSpecies species = null;

        // 1. speciesId ile registry'den species + prefab çöz
        if (registry != null)
        {
            string lookupId = !string.IsNullOrEmpty(pokemonData.speciesId)
                ? pokemonData.speciesId
                : pokemonData.prefabId; // legacy save fallback
            species = registry.GetById(lookupId);
            if (species != null)
            {
                // Evolved + NewPrefab varsa evolutionPrefab; yoksa basePrefab
                bool useEvolved = pokemonData.hasEvolved
                    && species.evolutionType == ARMON.Data.EvolutionType.NewPrefab
                    && species.evolutionPrefab != null;
                prefabToUse = useEvolved ? species.evolutionPrefab : species.basePrefab;
            }
        }

        // 2. Varsayılan prefab kullan
        if (prefabToUse == null && playerPokemonPrefab != null)
        {
            prefabToUse = playerPokemonPrefab;
            Debug.LogWarning($"SummonPokemon: '{pokemonData.speciesId}' registry'de bulunamadı, varsayılan prefab kullanılıyor.");
        }
        
        // Pokemon oluştur
        if (prefabToUse != null)
        {
            currentPlayerPokemon = Instantiate(prefabToUse, spawnPos, Quaternion.identity);
            
            // WildPokemon component'i varsa kaldır (oyuncu Pokemon'u olacak)
            WildPokemon wildComponent = currentPlayerPokemon.GetComponent<WildPokemon>();
            if (wildComponent != null)
            {
                Destroy(wildComponent);
            }
            
            // PokemonWorldUI varsa kaldır (PlayerPokemonController kendi UI'ını oluşturacak)
            PokemonWorldUI worldUI = currentPlayerPokemon.GetComponent<PokemonWorldUI>();
            if (worldUI != null)
            {
                Destroy(worldUI);
            }
            
            // Canvas varsa da kaldır
            Canvas existingCanvas = currentPlayerPokemon.GetComponentInChildren<Canvas>();
            if (existingCanvas != null)
            {
                Destroy(existingCanvas.gameObject);
            }
            
            Debug.Log($"Pokemon prefab'ı yüklendi: {pokemonData.prefabId}");
        }
        else
        {
            // Prefab yoksa basit bir küp oluştur
            currentPlayerPokemon = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            currentPlayerPokemon.transform.position = spawnPos;
            currentPlayerPokemon.transform.localScale = Vector3.one * 0.5f;
            
            // Renk ver
            Renderer renderer = currentPlayerPokemon.GetComponent<Renderer>();
            renderer.material.color = new Color(0.2f, 0.6f, 1f); // Mavi
            
            Debug.LogWarning($"Pokemon prefabı bulunamadı: {pokemonData.prefabId}, varsayılan model kullanılıyor.");
        }
        
        currentPlayerPokemon.name = $"PlayerPokemon_{pokemonData.pokemonName}";
        
        // Controller ekle
        currentController = currentPlayerPokemon.AddComponent<PlayerPokemonController>();
        currentController.Initialize(pokemonData, bagIndex, this, anchorManager, raycastManager, planeManager);
        
        currentPokemonIndex = bagIndex;
        
        Debug.Log($"=== {pokemonData.pokemonName} savaşa girdi! ===\n{pokemonData.GetStatsSummary()}");
    }

    /// <summary>
    /// Pokemon'u geri çağır
    /// </summary>
    public void RecallPokemon()
    {
        if (currentPlayerPokemon != null)
        {
            PokemonData data = ActivePokemonData;
            string name = data != null ? data.pokemonName : "Pokemon";
            
            Destroy(currentPlayerPokemon);
            currentPlayerPokemon = null;
            currentController = null;
            currentPokemonIndex = -1;
            
            // Çantayı kaydet (can değişmiş olabilir)
            PokemonBag.Instance?.SaveInventory();
            
            Debug.Log($"{name} geri çağrıldı!");
        }
    }

    /// <summary>
    /// En yakın vahşi Pokemon'a saldır
    /// </summary>
    public void AttackNearestWildPokemon()
    {
        if (currentController == null) return;
        
        WildPokemon nearestWild = FindNearestWildPokemon();
        if (nearestWild != null)
        {
            currentController.Attack(nearestWild);
        }
        else
        {
            Debug.Log("Yakında vahşi Pokemon yok!");
        }
    }

    /// <summary>
    /// En yakın vahşi Pokemon'u bul
    /// </summary>
    public WildPokemon FindNearestWildPokemon()
    {
        if (currentPlayerPokemon == null) return null;
        
        WildPokemon[] allWild = FindObjectsByType<WildPokemon>(FindObjectsSortMode.None);
        WildPokemon nearest = null;
        float nearestDist = float.MaxValue;
        
        foreach (var wild in allWild)
        {
            if (wild.IsFainted) continue;
            
            float dist = Vector3.Distance(currentPlayerPokemon.transform.position, wild.transform.position);
            if (dist < nearestDist && dist <= attackRange)
            {
                nearest = wild;
                nearestDist = dist;
            }
        }
        
        return nearest;
    }

    /// <summary>
    /// Oyuncunun nişanladığı plane noktası: ekran ortasından AR raycast.
    /// Yalnızca HorizontalUp plane, 0.5–8m mesafe penceresi. Başarısızsa false.
    /// </summary>
    bool TryGetAimedSpawnPosition(out Vector3 pos)
    {
        pos = default;
        if (raycastManager == null) return false;
        Camera cam = Camera.main;
        if (cam == null) return false;

        var hits = new List<ARRaycastHit>();
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (!raycastManager.Raycast(screenCenter, hits, TrackableType.PlaneWithinPolygon))
            return false;

        foreach (var h in hits)
        {
            ARPlane plane = planeManager != null ? planeManager.GetPlane(h.trackableId) : null;
            if (plane == null) continue;
            if (plane.alignment != PlaneAlignment.HorizontalUp) continue;

            float dist = Vector3.Distance(cam.transform.position, h.pose.position);
            if (dist < 0.5f || dist > 8f) continue;

            pos = h.pose.position + Vector3.up * (spawnHeight + 0.01f);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Fallback spawn pozisyonu (nişanlanan plane yoksa).
    /// Y: mutlak dünya 0'ı DEĞİL — AR'da zemin çoğu zaman y≈-1.5 civarındadır. Öğrenilmiş
    /// zemin (inferred ground) varsa onu, yoksa kameranın 1.5m altını kullan; spawnHeight
    /// zemine göre ek ofsettir.
    /// </summary>
    Vector3 CalculateSpawnPosition()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[BattleManager] Camera.main null — falling back to transform-relative spawn");
            return transform.position + Vector3.forward * spawnDistance;
        }
        Vector3 forward = cam.transform.forward;
        forward.y = 0;
        // Kamera dik aşağı bakarken yatay forward sıfıra çöker — normalize edilirse spawn
        // KAMERANIN İÇİNDE doğar ("place'e bakınca kamerada spawn" bug'ı). O durumda
        // kameranın up vektörünün yatay izdüşümü oyuncunun "ilerisi"ni verir.
        if (forward.sqrMagnitude < 1e-3f)
            forward = Vector3.ProjectOnPlane(cam.transform.up, Vector3.up);
        if (forward.sqrMagnitude < 1e-3f)
            forward = Vector3.forward;
        forward.Normalize();

        Vector3 spawnPos = cam.transform.position + forward * spawnDistance;
        float groundY = ARMON.AR.ARPositionResolver.HasInferredGround
            ? ARMON.AR.ARPositionResolver.inferredGroundY
            : cam.transform.position.y - 1.5f;
        spawnPos.y = groundY + spawnHeight + 0.01f;

        return spawnPos;
    }

    /// <summary>
    /// Vahşi Pokemon öldürüldüğünde çağrılır
    /// </summary>
    public void OnWildPokemonDefeated(WildPokemon defeatedPokemon)
    {
        if (currentController == null || ActivePokemonData == null) return;
        
        // XP hesapla - level farkına göre
        int wildLevel = defeatedPokemon.level;
        int myLevel = ActivePokemonData.level;
        
        // Base XP: düşman level * 10
        int baseXP = wildLevel * 10;
        
        // Level farkı bonusu/cezası
        int levelDiff = wildLevel - myLevel;
        float levelMultiplier = 1f;
        
        if (levelDiff > 0)
        {
            // Düşman daha yüksek level = daha fazla XP (%20 bonus per level)
            levelMultiplier = 1f + (levelDiff * 0.2f);
        }
        else if (levelDiff < 0)
        {
            // Düşman daha düşük level = daha az XP (%15 ceza per level, min %25)
            levelMultiplier = Mathf.Max(0.25f, 1f + (levelDiff * 0.15f));
        }
        
        int xpGain = Mathf.RoundToInt(baseXP * levelMultiplier);
        xpGain = Mathf.Max(5, xpGain); // Minimum 5 XP
        
        // XP ekle
        bool leveledUp = ActivePokemonData.AddXP(xpGain);
        PokemonBag.Instance?.SaveInventory();
        ARMON.Quest.QuestManager.Instance?.OnWildPokemonDefeated(defeatedPokemon);
        
        // UI güncelle
        currentController.UpdateUI();
        
        Debug.Log($"=== {defeatedPokemon.pokemonName} (Lv.{wildLevel}) yenildi! ===");
        Debug.Log($"{ActivePokemonData.pokemonName} (Lv.{myLevel}) +{xpGain} XP kazandı! (x{levelMultiplier:F2})");
        
        if (leveledUp)
        {
            Debug.Log($"*** {ActivePokemonData.pokemonName} LEVEL ATLADI! Yeni Level: {ActivePokemonData.level} ***");
        }
        
        // Pot düşürme şansı
        DropPotions(wildLevel);

        // Vahşi Pokemon + altındaki ARAnchor parent'ı 0.5s sonra temizle
        var anchorTransform = defeatedPokemon.transform.parent;
        GameObject toDestroy = defeatedPokemon.gameObject;
        if (anchorTransform != null &&
            anchorTransform.GetComponent<UnityEngine.XR.ARFoundation.ARAnchor>() != null)
        {
            toDestroy = anchorTransform.gameObject;
        }
        Destroy(toDestroy, 0.5f);
    }
    
    /// <summary>
    /// Vahşi Pokemon öldüğünde pot düşür
    /// </summary>
    void DropPotions(int wildLevel)
    {
        if (PokemonBag.Instance == null) return;
        
        // Level'e göre düşme şansları
        // Küçük pot: %40 şans
        // Süper pot: %15 şans (level 5+)
        // Hyper pot: %5 şans (level 10+)
        // Revive: %10 şans (level 7+)
        
        float rand = Random.value;
        
        // Küçük pot
        if (rand < 0.40f)
        {
            PokemonBag.Instance.AddPotion(PotionType.SmallPotion);
            Debug.Log("[+] Kucuk Pot dustu!");
        }
        
        // Süper pot
        if (wildLevel >= 5 && Random.value < 0.15f)
        {
            PokemonBag.Instance.AddPotion(PotionType.SuperPotion);
            Debug.Log("[+] Super Pot dustu!");
        }
        
        // Hyper pot
        if (wildLevel >= 10 && Random.value < 0.05f)
        {
            PokemonBag.Instance.AddPotion(PotionType.HyperPotion);
            Debug.Log("[+] Hyper Pot dustu!");
        }
        
        // Revive
        if (wildLevel >= 7 && Random.value < 0.10f)
        {
            PokemonBag.Instance.AddPotion(PotionType.Revive);
            Debug.Log("[+] Revive dustu!");
        }
    }
}
