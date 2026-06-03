using UnityEngine;
using ARMON.Data;

public class WildPokemon : MonoBehaviour
{
    [Header("Pokemon Bilgileri")]
    // Stub: populated by spawner; full usage lands in Task 3.2.
    public PokemonSpecies species;
    public int failedCatchAttempts = 0;
    public string pokemonName;
    public int level = 0; // 0 = rastgele level atanacak
    public int minLevel = 1;
    public int maxLevel = 15; // Daha geniş level aralığı
    
    [Header("Statlar")]
    public int maxHealth = 50;
    public int currentHealth;
    public int attack = 10;
    public int defense = 5;
    
    [Header("Yakalama Ayarları")]
    [Range(0f, 1f)]
    public float baseCatchRate = 0.7f; // Temel yakalama oranı (%70)

    [Header("Quest (TrainerBattle)")]
    [Tooltip("True = trainer's pokemon; never catchable; counts to TrainerBattle quest instead of DefeatWild.")]
    public bool isTrainerPokemon = false;
    [Tooltip("If isTrainerPokemon, the QuestInstance.npcAnchorId this trainer belongs to.")]
    public string questOwnerAnchorId = "";
    
    [Header("Savaş Ayarları")]
    public float attackCooldown = 2f;
    public float aggroRange = 2f; // Oyuncu Pokemon'una saldırmaya başlama mesafesi - yakın mesafe
    private float lastAttackTime;
    private PlayerPokemonController targetPlayer;
    private bool isProvoked = false; // Saldırılmadıkça saldırmaz
    
    [Header("UI")]
    public bool autoCreateUI = true;
    private PokemonWorldUI worldUI;
    
    private void Start()
    {
        // İsim belirle
        if (string.IsNullOrEmpty(pokemonName))
        {
            pokemonName = gameObject.name.Replace("(Clone)", "").Replace("_", " ").Trim();
        }
        
        // Rastgele level belirle
        if (level <= 0)
        {
            level = Random.Range(minLevel, maxLevel + 1);
        }
        
        // Level'e göre statları hesapla
        CalculateStats();
        
        // Pokeball collision için collider — vendor prefab'larda yoksa child mesh bounds'una göre kur
        if (GetComponentInChildren<Collider>() == null)
        {
            EnsureBodyCollider();
        }
        
        // UI oluştur
        if (autoCreateUI)
        {
            SetupUI();
        }
        
        Debug.Log($"Vahşi {pokemonName} göründü! Level: {level}, HP: {currentHealth}/{maxHealth}");
    }
    
    private void Update()
    {
        // Bayıldıysa bir şey yapma
        if (IsFainted) return;
        
        // Oyuncu Pokemon'u bul ve saldır
        HandleCombat();
    }
    
    void HandleCombat()
    {
        // Saldırılmadıysa saldırma - pasif kal
        if (!isProvoked) return;
        
        // Hedef yoksa veya öldüyse yeni hedef bul
        if (targetPlayer == null)
        {
            FindPlayerPokemon();
        }
        
        if (targetPlayer == null) return;
        
        // Mesafe kontrolü
        float distance = Vector3.Distance(transform.position, targetPlayer.transform.position);
        
        if (distance <= aggroRange && Time.time - lastAttackTime >= attackCooldown)
        {
            // Karşı saldırı yap
            CounterAttack();
        }
    }
    
    void FindPlayerPokemon()
    {
        targetPlayer = FindFirstObjectByType<PlayerPokemonController>();
    }
    
    void CounterAttack()
    {
        if (targetPlayer == null) return;
        
        lastAttackTime = Time.time;
        
        // Hedefe dön
        Vector3 lookDir = targetPlayer.transform.position - transform.position;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDir);
        }
        
        Debug.Log($"Vahşi {pokemonName} karşı saldırı yaptı! {attack} hasar!");
        targetPlayer.TakeDamage(attack);
    }
    
    /// <summary>
    /// Vendor prefab'larda collider yoksa: child mesh bounds'unu hesapla, root'a uygun BoxCollider ekle.
    /// Pokeball'un GÖRSEL body'e değdiğinde OnCollisionEnter ateşlemesini garanti eder.
    /// </summary>
    void EnsureBodyCollider()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            // Fallback: küçük sphere
            var s = gameObject.AddComponent<SphereCollider>();
            s.radius = 0.3f;
            s.isTrigger = false;
            return;
        }

        // Dünya bounds'unu hesapla
        Bounds world = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) world.Encapsulate(renderers[i].bounds);

        // World → local (scale dahil)
        Vector3 localCenter = transform.InverseTransformPoint(world.center);
        Vector3 lossy = transform.lossyScale;
        Vector3 localSize = new Vector3(
            world.size.x / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)),
            world.size.y / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)),
            world.size.z / Mathf.Max(0.0001f, Mathf.Abs(lossy.z)));

        var box = gameObject.AddComponent<BoxCollider>();
        box.center = localCenter;
        // Hafif şişir — küçük modellerde pokeball hit'ini garantile
        box.size = localSize * 1.15f;
        box.isTrigger = false;

        Debug.Log($"[{pokemonName}] BoxCollider eklendi: center={box.center} size={box.size}");
    }

    void CalculateStats()
    {
        // Level 1'de düşük base statlar, level arttıkça güçlensin
        // Level 1: HP ~15-25, ATK ~3-6, DEF ~2-4
        // Level 10: HP ~40-65, ATK ~12-24, DEF ~8-16
        
        int baseHealth = Random.Range(12, 22);  // Düşürüldü
        int baseAttack = Random.Range(3, 6);     // Düşürüldü
        int baseDefense = Random.Range(2, 4);    // Düşürüldü
        
        // Her level %15 artış (daha belirgin level farkı)
        float levelMultiplier = 1 + (level - 1) * 0.15f;
        
        maxHealth = Mathf.RoundToInt(baseHealth * levelMultiplier);
        attack = Mathf.RoundToInt(baseAttack * levelMultiplier);
        defense = Mathf.RoundToInt(baseDefense * levelMultiplier);
        
        currentHealth = maxHealth;
        
        Debug.Log($"{pokemonName} Lv.{level} statları: HP={maxHealth}, ATK={attack}, DEF={defense}");
    }
    
    void SetupUI()
    {
        worldUI = GetComponent<PokemonWorldUI>();
        
        if (worldUI == null)
        {
            worldUI = gameObject.AddComponent<PokemonWorldUI>();
        }
        
        // UI'ı güncelle
        if (worldUI != null)
        {
            worldUI.SetHealth(currentHealth, maxHealth);
            worldUI.UpdateUI();
        }
    }
    
    // Hasar al
    public void TakeDamage(int damage)
    {
        // Saldırıya uğradık - artık karşılık verebiliriz
        if (!isProvoked)
        {
            isProvoked = true;
            Debug.Log($"Vahşi {pokemonName} kışkırtıldı! Artık saldıracak!");
        }
        
        int actualDamage = Mathf.Max(1, damage - defense / 2);
        currentHealth = Mathf.Max(0, currentHealth - actualDamage);
        
        if (worldUI != null)
        {
            worldUI.SetHealth(currentHealth, maxHealth);
        }
        
        Debug.Log($"{pokemonName} {actualDamage} hasar aldı! HP: {currentHealth}/{maxHealth}");
        
        if (currentHealth <= 0)
        {
            OnFainted();
        }
    }
    
    /// <summary>
    /// Pokemon kışkırtılmış mı?
    /// </summary>
    public bool IsProvoked => isProvoked;
    
    // İyileş
    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        
        if (worldUI != null)
        {
            worldUI.SetHealth(currentHealth, maxHealth);
        }
    }
    
    // Bayıldığında
    void OnFainted()
    {
        Debug.Log($"{pokemonName} bayıldı!");

        // 1) BattleManager'a haber ver — XP, pot drop, UI update, destroy işler
        var bm = FindFirstObjectByType<BattleManager>();
        if (bm != null)
        {
            bm.OnWildPokemonDefeated(this);
        }
        else
        {
            // Fallback: BattleManager yoksa da anchor + objeyi temizle
            CleanupAnchorAndSelf();
        }
    }

    /// <summary>
    /// Pokemon'un altında ARAnchor parent'ı varsa onunla birlikte temizler.
    /// BattleManager Destroy(go) yapınca arta kalan boş anchor GameObject'i için de güvenli.
    /// </summary>
    public void CleanupAnchorAndSelf()
    {
        var parent = transform.parent;
        if (parent != null && parent.GetComponent<UnityEngine.XR.ARFoundation.ARAnchor>() != null)
        {
            Destroy(parent.gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public bool IsFainted => currentHealth <= 0;
    
    // Level'e göre yakalama şansını hesapla
    public float GetCatchRate(float ballMultiplier = 1f)
    {
        float speciesBase = species != null ? species.catchRateBase : baseCatchRate;

        float levelPenalty = 1f - (level - 1) * 0.07f;
        levelPenalty = Mathf.Clamp(levelPenalty, 0.15f, 1f);

        float healthPercent = (float)currentHealth / Mathf.Max(1, maxHealth);
        float healthBonus = 1f + (1f - healthPercent) * 0.5f;

        float rate = speciesBase * levelPenalty * healthBonus * Mathf.Max(0.1f, ballMultiplier);
        return Mathf.Clamp01(rate);
    }

    // Yakalama denemesi
    public bool TryCatch(float ballMultiplier)
    {
        if (isTrainerPokemon)
        {
            Debug.Log($"[WildPokemon] {pokemonName} trainer's pokemon — not catchable.");
            return false;
        }
        float catchRate = GetCatchRate(ballMultiplier);
        float roll = Random.value;
        bool ok = roll <= catchRate;
        if (!ok) failedCatchAttempts++;
        Debug.Log($"Catch attempt {pokemonName} Lv.{level} HP%={(currentHealth*100f/maxHealth):F0} rate={(catchRate*100f):F1}% roll={roll:F2} ok={ok} fails={failedCatchAttempts}");
        return ok;
    }

    public bool TryCatch() => TryCatch(1f);

    public bool ShouldEscape()
    {
        if (species == null || species.rarity != Rarity.Legendary) return false;
        if (failedCatchAttempts >= 4) return true;
        if (failedCatchAttempts == 3) return Random.value < 0.5f;
        return false;
    }
}
