using UnityEngine;

public class WildPokemon : MonoBehaviour
{
    [Header("Pokemon Bilgileri")]
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
        
        // Click detection için collider ekle (yoksa)
        if (GetComponent<Collider>() == null)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.5f;
            sphere.isTrigger = false; // Pokeball ile çarpışabilmesi için
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
        // İstersen burada death animasyonu vs. ekleyebilirsin
    }
    
    public bool IsFainted => currentHealth <= 0;
    
    // Level'e göre yakalama şansını hesapla
    public float GetCatchRate()
    {
        // Level arttıkça yakalama zorlaşır
        // Can azaldıkça yakalama kolaylaşır
        float levelPenalty = 1f - (level - 1) * 0.07f;
        levelPenalty = Mathf.Clamp(levelPenalty, 0.15f, 1f);
        
        // Can bonusu: düşük can = daha kolay yakalama
        float healthPercent = (float)currentHealth / maxHealth;
        float healthBonus = 1f + (1f - healthPercent) * 0.5f; // Can düştükçe %50'ye kadar bonus
        
        float finalCatchRate = baseCatchRate * levelPenalty * healthBonus;
        finalCatchRate = Mathf.Clamp01(finalCatchRate);
        
        return finalCatchRate;
    }
    
    // Yakalama denemesi
    public bool TryCatch()
    {
        float catchRate = GetCatchRate();
        float roll = Random.value;
        
        float healthPercent = (float)currentHealth / maxHealth * 100f;
        Debug.Log($"Yakalama denemesi - {pokemonName} Lv.{level}, HP: %{healthPercent:F0}, Şans: %{(catchRate * 100):F1}");
        
        return roll <= catchRate;
    }
}
