using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.ARFoundation;
using ARMON.AR;

public class PlayerPokemonController : MonoBehaviour
{
    [Header("Takip Ayarları")]
    public float followDistance = 2f; // Kameradan ne kadar uzakta duracak
    public float followSpeed = 5f; // Takip hızı
    public float rotationSpeed = 10f;
    public float heightOffset = 0f; // Yerden yükseklik
    
    [Header("Savaş Ayarları")]
    public float attackCooldown = 1.5f;
    public float attackRange = 1.5f; // Saldırı menzili - yakın mesafe
    public float chaseRange = 5f; // Düşmanı kovalama menzili - sadece yakındaysa kovala
    public float chaseSpeed = 4f; // Kovalama hızı
    
    private PokemonData pokemonData;
    private int bagIndex;
    private BattleManager battleManager;
    private float lastAttackTime;
    private Transform cameraTransform;
    
    // UI
    private Canvas worldCanvas;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI levelText;
    private Image healthFill;
    
    // Hedef ve durum
    private WildPokemon currentTarget;
    private bool isChasing = false;
    private bool hasCommand = false; // Saldırı emri verildi mi?
    
    // Collider for click detection
    private Collider pokemonCollider;

    private ARAnchorManager  anchorManager;
    private ARRaycastManager raycastManager;
    private ARPlaneManager   planeManager;
    private ARAnchor         currentAnchor;
    private ARPositionResolver resolver;

    public void Initialize(PokemonData data, int index, BattleManager manager,
        ARAnchorManager am, ARRaycastManager rm, ARPlaneManager pm)
    {
        pokemonData = data;
        bagIndex = index;
        battleManager = manager;
        lastAttackTime = -attackCooldown;
        cameraTransform = Camera.main.transform;
        hasCommand = false;
        currentTarget = null;

        anchorManager = am;
        raycastManager = rm;
        planeManager = pm;
        resolver = new ARPositionResolver();

        if (battleManager != null) attackRange = battleManager.attackRange;

        pokemonCollider = GetComponent<Collider>();
        if (pokemonCollider == null)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.5f;
            sphere.isTrigger = true;
            pokemonCollider = sphere;
        }

        AnchorToWorld();
        CreateUI();
        UpdateUI();

        // Apply evolved visual (no-op if not evolved or NewPrefab mode)
        if (battleManager != null && battleManager.registry != null && pokemonData != null)
        {
            var sp = battleManager.registry.GetById(pokemonData.speciesId);
            ARMON.Data.EvolutionVisual.Apply(gameObject, pokemonData, sp);
        }
    }

    void AnchorToWorld()
    {
        if (currentAnchor != null)
        {
            ARAnchorUtil.DestroyAnchor(currentAnchor);
            currentAnchor = null;
        }
        if (resolver == null) return;
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (!resolver.TryResolve(screenCenter, 0f, raycastManager, planeManager, out var r))
            return;
        currentAnchor = ARAnchorUtil.CreateAnchor(r, anchorManager,
            $"OwnedAnchor_{(pokemonData != null ? pokemonData.pokemonName : "?")}");
        transform.SetParent(currentAnchor.transform, worldPositionStays: false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    void DetachAnchorForChase()
    {
        if (currentAnchor == null) return;
        transform.SetParent(null, worldPositionStays: true);
    }

    void ReanchorAfterChase()
    {
        AnchorToWorld();
    }

    void Update()
    {
        if (cameraTransform == null) 
        {
            cameraTransform = Camera.main?.transform;
            if (cameraTransform == null) return;
        }
        
        if (hasCommand && currentTarget != null)
        {
            if (currentTarget.IsFainted)
            {
                ClearTarget();
                ReanchorAfterChase();
            }
            else
            {
                DetachAnchorForChase();
                ChaseAndAttack();
            }
        }

        UpdateUIPosition();
    }

    /// <summary>
    /// Hedefe saldırma emri ver
    /// </summary>
    public void SetTarget(WildPokemon target)
    {
        if (target == null || target.IsFainted) return;
        
        currentTarget = target;
        hasCommand = true;
        isChasing = true;
        
        Debug.Log($"{pokemonData.pokemonName} hedef aldı: {target.pokemonName}!");
    }

    /// <summary>
    /// Hedefi temizle ve takip moduna dön
    /// </summary>
    public void ClearTarget()
    {
        currentTarget = null;
        hasCommand = false;
        isChasing = false;
        
        Debug.Log($"{pokemonData.pokemonName} hedefi bıraktı, sahibini takip ediyor.");
    }

    /// <summary>
    /// Aktif hedef var mı?
    /// </summary>
    public bool HasTarget => hasCommand && currentTarget != null;

    void ChaseAndAttack()
    {
        if (currentTarget == null) return;
        
        float distToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
        
        // Hedefe dön
        Vector3 lookDir = currentTarget.transform.position - transform.position;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
        
        // Menzil dışındaysa yaklaş
        if (distToTarget > attackRange * 0.8f)
        {
            isChasing = true;
            Vector3 moveDir = (currentTarget.transform.position - transform.position).normalized;
            moveDir.y = 0;
            
            Vector3 newPos = transform.position + moveDir * chaseSpeed * Time.deltaTime;
            newPos.y = heightOffset;
            transform.position = newPos;
        }
        else
        {
            isChasing = false;
            // Menzildeyse saldır
            if (Time.time - lastAttackTime >= attackCooldown)
            {
                Attack(currentTarget);
            }
        }
    }

    public void Attack(WildPokemon target)
    {
        if (pokemonData == null || target == null) return;
        if (Time.time - lastAttackTime < attackCooldown) return;
        
        lastAttackTime = Time.time;
        
        // Hasar hesapla
        int damage = pokemonData.Attack;
        
        Debug.Log($"{pokemonData.pokemonName} saldırdı! {damage} hasar!");

        // Route through colored beam if species + registry available
        var sp = battleManager != null && battleManager.registry != null
            ? battleManager.registry.GetById(pokemonData.speciesId)
            : null;

        if (sp != null && sp.skill != null && battleManager.beamPrefab != null)
        {
            battleManager.FireBeam(transform.position, target, sp.skill, sp.baseAttack);
            // Apply damage immediately to keep IsFainted check below valid (beam visual is async)
            int beamDamage = sp.skill.DamageAgainst(sp.baseAttack);
            target.TakeDamage(beamDamage);
            damage = beamDamage;
        }
        else
        {
            target.TakeDamage(damage);
        }
        
        // Öldü mü kontrol et
        if (target.IsFainted)
        {
            if (battleManager != null)
            {
                battleManager.OnWildPokemonDefeated(target);
            }
            currentTarget = null;
            isChasing = false;
        }
    }

    public void TakeDamage(int damage)
    {
        if (pokemonData == null) return;
        
        pokemonData.TakeDamage(damage);
        UpdateUI();
        
        Debug.Log($"{pokemonData.pokemonName} {damage} hasar aldı! HP: {pokemonData.currentHealth}/{pokemonData.Health}");
        
        // Bayıldı mı?
        if (pokemonData.IsFainted)
        {
            OnFainted();
        }
    }

    void OnFainted()
    {
        Debug.Log($"{pokemonData.pokemonName} bayıldı!");
        PokemonBag.Instance?.SaveInventory();
        battleManager?.RecallPokemon();
    }

    #region UI
    
    void CreateUI()
    {
        // Canvas oluştur
        GameObject canvasObj = new GameObject("PlayerPokemonUI");
        canvasObj.transform.SetParent(transform);
        canvasObj.transform.localPosition = new Vector3(0, 2f, 0);
        
        worldCanvas = canvasObj.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10;
        
        RectTransform canvasRect = worldCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(200, 80);
        canvasRect.localScale = Vector3.one * 0.01f;
        
        // Arka plan (mavi tonlu - oyuncu Pokemon'u)
        GameObject bgPanel = new GameObject("Background");
        bgPanel.transform.SetParent(canvasObj.transform, false);
        Image bgImage = bgPanel.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.2f, 0.4f, 0.8f);
        RectTransform bgRect = bgPanel.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(200, 80);
        
        // İsim
        GameObject nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(bgPanel.transform, false);
        nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 24;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = new Color(0.5f, 0.8f, 1f);
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(190, 30);
        nameRect.anchoredPosition = new Vector2(0, 20);
        
        // Level
        GameObject levelObj = new GameObject("LevelText");
        levelObj.transform.SetParent(bgPanel.transform, false);
        levelText = levelObj.AddComponent<TextMeshProUGUI>();
        levelText.fontSize = 18;
        levelText.alignment = TextAlignmentOptions.Center;
        levelText.color = new Color(1f, 0.9f, 0.5f);
        RectTransform levelRect = levelObj.GetComponent<RectTransform>();
        levelRect.sizeDelta = new Vector2(190, 25);
        levelRect.anchoredPosition = new Vector2(0, -5);
        
        // Health bar background
        GameObject healthBgObj = new GameObject("HealthBarBG");
        healthBgObj.transform.SetParent(bgPanel.transform, false);
        Image healthBgImage = healthBgObj.AddComponent<Image>();
        healthBgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        RectTransform healthBgRect = healthBgObj.GetComponent<RectTransform>();
        healthBgRect.sizeDelta = new Vector2(180, 15);
        healthBgRect.anchoredPosition = new Vector2(0, -28);
        
        // Health bar fill
        GameObject healthFillObj = new GameObject("HealthFill");
        healthFillObj.transform.SetParent(healthBgObj.transform, false);
        healthFill = healthFillObj.AddComponent<Image>();
        healthFill.color = new Color(0.2f, 0.8f, 0.2f);
        RectTransform fillRect = healthFillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(1, 1);
        fillRect.sizeDelta = new Vector2(-4, -4);
        fillRect.anchoredPosition = Vector2.zero;
    }

    public void UpdateUI()
    {
        if (pokemonData == null) return;
        
        if (nameText != null)
            nameText.text = pokemonData.pokemonName;
        
        if (levelText != null)
            levelText.text = $"Lv.{pokemonData.level} | XP: {pokemonData.currentXP}/{pokemonData.xpToNextLevel}";
        
        if (healthFill != null)
        {
            float healthPercent = (float)pokemonData.currentHealth / pokemonData.Health;
            healthFill.rectTransform.anchorMax = new Vector2(healthPercent, 1);
            
            if (healthPercent > 0.5f)
                healthFill.color = new Color(0.2f, 0.8f, 0.2f);
            else if (healthPercent > 0.25f)
                healthFill.color = new Color(1f, 0.8f, 0.2f);
            else
                healthFill.color = new Color(0.9f, 0.2f, 0.2f);
        }
    }

    void UpdateUIPosition()
    {
        if (worldCanvas == null) return;
        
        Camera cam = Camera.main;
        if (cam != null)
        {
            worldCanvas.transform.LookAt(cam.transform);
            worldCanvas.transform.Rotate(0, 180, 0);
        }
    }
    
    #endregion
}
