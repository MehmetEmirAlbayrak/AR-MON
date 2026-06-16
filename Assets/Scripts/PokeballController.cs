// 1/9/2026 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARMON.Data;

public class PokeballController : MonoBehaviour
{
    public static PokeballController Instance { get; private set; }

    public GameObject pokeballPrefab; // Poketopu prefabı

    [Header("Catch")]
    public BallQuality quality = BallQuality.Normal;

    public float QualityMultiplier => quality switch
    {
        BallQuality.Normal => 1.0f,
        BallQuality.Great  => 1.5f,
        BallQuality.Ultra  => 2.0f,
        _                  => 1.0f,
    };
    
    [Header("Kamera Takip Ayarları")]
    public Vector3 pokeballOffset = new Vector3(0f, -0.5f, 1.2f); // Kameraya göre offset (sağ, aşağı, ileri) - daha uzakta
    public float pokeballScale = 0.15f; // Pokeball boyutu (küçültmek için)
    public float followSpeed = 10f; // Takip hızı
    public bool smoothFollow = true; // Yumuşak takip
    
    [Header("Fırlatma Ayarları")]
    public float minThrowForce = 3f; // Minimum fırlatma kuvveti
    public float maxThrowForce = 15f; // Maximum fırlatma kuvveti
    public float swipeMultiplier = 0.01f; // Kaydırma mesafesi çarpanı
    public float forwardForce = 8f; // İleri doğru sabit kuvvet
    
    [Header("Yön Çarpanları")]
    [Range(0.1f, 2f)]
    public float horizontalMultiplier = 1.0f; // Sağ-sol kuvvet çarpanı
    [Range(0.1f, 2f)]
    public float verticalMultiplier = 0.5f; // Yukarı-aşağı kuvvet çarpanı
    
    [Header("Diğer")]
    public float returnDelay = 3f; // Geri dönme süresi

    private GameObject currentPokeball;
    private Vector3 mouseStartPosition;
    private Vector3 mouseEndPosition;
    private bool isDragging = false;
    private bool isThrown = false; // Fırlatıldı mı?
    private Camera mainCamera;
    private float _nextSpawnRetryTime = 0f;
    
    // Pokeball seçim sistemi
    private bool isPokeballSelected = false;
    public bool IsPokeballSelected => isPokeballSelected;
    private BallType currentBallType = BallType.Normal;

    // UI
    private Canvas uiCanvas;
    private GameObject selectButton;
    private Image selectButtonImage;
    private TextMeshProUGUI selectButtonText;
    private Image normalChipImg, greatChipImg, ultraChipImg;
    private TextMeshProUGUI normalChipText, greatChipText, ultraChipText;
    
    // Pokeball renk değişimi için
    private Renderer pokeballRenderer;
    private Color originalPokeballColor;
    private Color selectedPokeballColor = new Color(1f, 0.3f, 0.3f, 1f); // Parlak kırmızı

    void Awake()
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
    
    void Start()
    {
        mainCamera = Camera.main;
        CreateUI();
        SpawnPokeball();
    }
    
    void LateUpdate()
    {
        // Pokeball kamerayı takip etsin (fırlatılmadıysa)
        if (currentPokeball != null && !isThrown && mainCamera != null)
        {
            // Hedef pozisyon: Kameranın önünde ve biraz aşağıda
            Vector3 targetPos = mainCamera.transform.position 
                + mainCamera.transform.forward * pokeballOffset.z
                + mainCamera.transform.right * pokeballOffset.x
                + mainCamera.transform.up * pokeballOffset.y;
            
            if (smoothFollow)
            {
                // Yumuşak takip
                currentPokeball.transform.position = Vector3.Lerp(
                    currentPokeball.transform.position, 
                    targetPos, 
                    Time.deltaTime * followSpeed
                );
            }
            else
            {
                // Anında takip
                currentPokeball.transform.position = targetPos;
            }
            
            // Kameraya doğru baksın
            currentPokeball.transform.LookAt(mainCamera.transform);
        }
    }
    
    void CreateUI()
    {
        if (ARMON.UI.AR.ARHudCanvas.Instance == null)
        {
            Debug.LogWarning("[PokeballController] ARHudCanvas.Instance null — pokeball UI deferred.");
            return;
        }
        uiCanvas = ARMON.UI.AR.ARHudCanvas.Instance.GetComponent<Canvas>();

        // Container for pokeball-select widgets, anchored to bottom-left of HUD.
        GameObject container = new GameObject("PokeballSelectGroup", typeof(RectTransform));
        container.transform.SetParent(ARMON.UI.AR.ARHudCanvas.Instance.BottomLeftSlot, false);
        var crt = (RectTransform)container.transform;
        crt.anchorMin = new Vector2(0f, 0f);
        crt.anchorMax = new Vector2(0f, 0f);
        crt.pivot     = new Vector2(0f, 0f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(260f, 220f);

        BuildPokeballWidgets(container.transform);
    }

    void BuildPokeballWidgets(Transform parent)
    {
        // === Ball type chips (top of container) ===
        BuildBallChip(parent, BallType.Normal, new Vector2(0f, 1f),    "Pokéball", new Color(0.85f, 0.25f, 0.25f, 0.95f),
                      out normalChipImg, out normalChipText);
        BuildBallChip(parent, BallType.Great,  new Vector2(0.5f, 1f),  "Great",    new Color(0.20f, 0.45f, 0.80f, 0.95f),
                      out greatChipImg,  out greatChipText);
        BuildBallChip(parent, BallType.Ultra,  new Vector2(1f, 1f),    "Ultra",    new Color(0.55f, 0.20f, 0.65f, 0.95f),
                      out ultraChipImg,  out ultraChipText);

        // Pokeball seçim butonu — chip'lerin altında merkez
        selectButton = new GameObject("PokeballSelectBtn");
        selectButton.transform.SetParent(parent, false);

        selectButtonImage = selectButton.AddComponent<Image>();
        selectButtonImage.color = new Color(0.3f, 0.3f, 0.4f, 0.9f); // Koyu gri (seçili değil)

        RectTransform btnRect = selectButton.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot     = new Vector2(0.5f, 0f);
        btnRect.anchoredPosition = new Vector2(0f, 6f);
        btnRect.sizeDelta = new Vector2(120, 120);

        // Buton text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(selectButton.transform, false);
        selectButtonText = textObj.AddComponent<TextMeshProUGUI>();
        selectButtonText.text = "[ ]\nYAKALA";
        selectButtonText.fontSize = 20;
        selectButtonText.fontStyle = FontStyles.Bold;
        selectButtonText.alignment = TextAlignmentOptions.Center;
        selectButtonText.color = Color.white;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        // Buton
        Button btn = selectButton.AddComponent<Button>();
        btn.targetGraphic = selectButtonImage;
        btn.onClick.AddListener(TogglePokeballSelection);

        RefreshBallChips();
    }

    void BuildBallChip(Transform parent, BallType type, Vector2 anchorRight, string label, Color color,
                       out Image bgOut, out TextMeshProUGUI textOut)
    {
        var go = new GameObject($"Chip_{type}", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        bgOut = go.AddComponent<Image>();
        bgOut.color = color;
        var rt = (RectTransform)go.transform;
        // 3 chip yan yana üst şeritte
        float w = 78f, h = 56f;
        rt.anchorMin = new Vector2(anchorRight.x, 1f);
        rt.anchorMax = new Vector2(anchorRight.x, 1f);
        rt.pivot     = new Vector2(anchorRight.x, 1f);
        rt.anchoredPosition = new Vector2((anchorRight.x - 0.5f) * 0f, -4f);
        rt.sizeDelta = new Vector2(w, h);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bgOut;
        BallType captured = type;
        btn.onClick.AddListener(() => SetBallType(captured));

        var txt = new GameObject("Text", typeof(RectTransform));
        txt.transform.SetParent(go.transform, false);
        textOut = txt.AddComponent<TextMeshProUGUI>();
        textOut.text = $"{label}\n∞";
        textOut.fontSize = 14;
        textOut.fontStyle = FontStyles.Bold;
        textOut.alignment = TextAlignmentOptions.Center;
        textOut.color = Color.white;
        textOut.raycastTarget = false;
        var trt = (RectTransform)txt.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
    }

    void SetBallType(BallType type)
    {
        if (PokemonBag.Instance != null && PokemonBag.Instance.GetBallCount(type) <= 0 && type != BallType.Normal)
        {
            ARMON.UI.AR.ToastManager.Show($"{type} ball yok");
            return;
        }
        currentBallType = type;
        RefreshBallChips();
        // Aktif top değişti → mevcut top varsa yeniden renklendir
        UpdatePokeballColor();
        if (selectButtonText != null)
            selectButtonText.text = isPokeballSelected ? $"[X]\n{BallLabel(type)}!" : $"[ ]\n{BallLabel(type)}";
    }

    void RefreshBallChips()
    {
        var bag = PokemonBag.Instance;
        int g = bag != null ? bag.GreatBallCount : 0;
        int u = bag != null ? bag.UltraBallCount : 0;
        if (normalChipText != null) normalChipText.text = "Pokéball\n∞";
        if (greatChipText  != null) greatChipText.text  = $"Great\n×{g}";
        if (ultraChipText  != null) ultraChipText.text  = $"Ultra\n×{u}";
        // Highlight active
        var dim = new Color(1f, 1f, 1f, 0.45f);
        if (normalChipImg != null) normalChipImg.color = ApplyDim(normalChipImg.color, currentBallType == BallType.Normal, dim);
        if (greatChipImg  != null) greatChipImg.color  = ApplyDim(greatChipImg.color,  currentBallType == BallType.Great,  dim);
        if (ultraChipImg  != null) ultraChipImg.color  = ApplyDim(ultraChipImg.color,  currentBallType == BallType.Ultra,  dim);
    }

    static Color ApplyDim(Color baseColor, bool active, Color dimMul)
    {
        if (active) return new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
        return new Color(baseColor.r * 0.5f, baseColor.g * 0.5f, baseColor.b * 0.5f, baseColor.a * 0.7f);
    }

    static string BallLabel(BallType t)
    {
        switch (t) { case BallType.Great: return "GREAT"; case BallType.Ultra: return "ULTRA"; default: return "YAKALA"; }
    }

    void TogglePokeballSelection()
    {
        isPokeballSelected = !isPokeballSelected;
        UpdateSelectionUI();
        
        if (isPokeballSelected)
        {
            Debug.Log("[+] Pokeball SECILDI - Kaydirarak firlatabilirsin!");
        }
        else
        {
            Debug.Log("[-] Pokeball secimi IPTAL - Pokemon'lara dokunarak saldiri emri verebilirsin!");
        }
    }
    
    void UpdateSelectionUI()
    {
        // Buton görünümünü güncelle
        if (selectButtonImage != null)
        {
            if (isPokeballSelected)
            {
                selectButtonImage.color = new Color(0.9f, 0.2f, 0.2f, 0.95f); // Kırmızı (seçili - fırlatmaya hazır)
                selectButtonText.text = "[X]\nFIRLAT!";
            }
            else
            {
                selectButtonImage.color = new Color(0.3f, 0.3f, 0.4f, 0.9f); // Koyu gri (seçili değil)
                selectButtonText.text = "[ ]\nYAKALA";
            }
        }
        
        // Pokeball'un rengini değiştir
        UpdatePokeballColor();
    }
    
    void UpdatePokeballColor()
    {
        if (currentPokeball == null) return;
        
        // Renderer'ı bul
        if (pokeballRenderer == null)
        {
            pokeballRenderer = currentPokeball.GetComponent<Renderer>();
            if (pokeballRenderer == null)
            {
                pokeballRenderer = currentPokeball.GetComponentInChildren<Renderer>();
            }
            
            if (pokeballRenderer != null)
            {
                originalPokeballColor = pokeballRenderer.material.color;
            }
        }
        
        if (pokeballRenderer != null)
        {
            if (isPokeballSelected)
            {
                // Pokeball'u kırmızı yap ve parlat
                pokeballRenderer.material.color = selectedPokeballColor;
                pokeballRenderer.material.SetColor("_EmissionColor", selectedPokeballColor * 0.5f);
            }
            else
            {
                // Normal renge döndür
                pokeballRenderer.material.color = originalPokeballColor;
                pokeballRenderer.material.SetColor("_EmissionColor", Color.black);
            }
        }
    }
    
    /// <summary>
    /// Pokeball seçimini dışarıdan iptal etmek için
    /// </summary>
    public void DeselectPokeball()
    {
        isPokeballSelected = false;
        UpdateSelectionUI();
    }

    void Update()
    {
        // AR kamera sahneye geç gelmiş olabilir — kaybolduysa/null'sa yeniden bul.
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        // Top yoksa ve bekleyen respawn Invoke'u da yoksa yeniden oluştur
        // (Start'ta kamera hazır değilse top hiç doğmamış olabilir). 0.5s throttle.
        if (currentPokeball == null && !isThrown
            && !IsInvoking(nameof(SpawnPokeball)) && Time.time >= _nextSpawnRetryTime)
        {
            _nextSpawnRetryTime = Time.time + 0.5f;
            SpawnPokeball();
        }

        // Envanter açıksa hiçbir şey yapma
        if (IsInventoryOpen())
        {
            isDragging = false;
            return;
        }
        
        // SADECE pokeball seçiliyse fırlatma işlemi yap
        if (isPokeballSelected)
        {
            // Sol fare tuşuna basıldığında
            if (Input.GetMouseButtonDown(0))
            {
                // UI üzerinde tıklama kontrolü
                if (!IsPointerOverUI())
                {
                    mouseStartPosition = Input.mousePosition;
                    isDragging = true;
                }
            }

            // Sol fare tuşu bırakıldığında
            if (Input.GetMouseButtonUp(0) && isDragging && currentPokeball != null)
            {
                mouseEndPosition = Input.mousePosition;
                
                // Minimum kaydırma mesafesi kontrolü (yanlışlıkla fırlatmayı önle)
                // DPI-bağımsız: ekran boyutunun %2'si kadar kaydırma gerekli
                float swipeDistance = (mouseEndPosition - mouseStartPosition).magnitude;
                float minSwipeDistance = Mathf.Max(30f, Screen.height * 0.02f);
                if (swipeDistance > minSwipeDistance) // Ekran boyutuna göre ayarlı
                {
                    ThrowPokeball();
                    // Fırlattıktan sonra seçimi kaldır
                    DeselectPokeball();
                }
                isDragging = false;
            }
        }
        else
        {
            isDragging = false;
        }

        // Pokeball düştüyse veya çok uzaklaştıysa yenisini oluştur
        if (currentPokeball != null && isThrown)
        {
            float distanceFromCamera = Vector3.Distance(currentPokeball.transform.position, mainCamera.transform.position);
            
            if (currentPokeball.transform.position.y < -10f || distanceFromCamera > 50f)
            {
                Destroy(currentPokeball);
                currentPokeball = null;
                isThrown = false;
                SpawnPokeball();
            }
        }
    }

    bool IsInventoryOpen()
    {
        // PokemonInventoryUI açık mı kontrol et
        return PokemonInventoryUI.Instance != null && PokemonInventoryUI.Instance.IsOpen;
    }

    bool IsPointerOverUI()
    {
        // EventSystem üzerinden UI kontrolü
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        }
        return false;
    }

    void SpawnPokeball()
    {
        if (currentPokeball == null && mainCamera != null)
        {
            if (PokemonBag.Instance != null && PokemonBag.Instance.GetBallCount(currentBallType) <= 0)
            {
                if (selectButtonText != null) selectButtonText.text = $"[ 0 ]\n{BallLabel(currentBallType)}";
                return;
            }
            // Kameranın önünde spawn et
            Vector3 spawnPos = mainCamera.transform.position
                + mainCamera.transform.forward * pokeballOffset.z
                + mainCamera.transform.right * pokeballOffset.x
                + mainCamera.transform.up * pokeballOffset.y;
            
            currentPokeball = Instantiate(pokeballPrefab, spawnPos, Quaternion.identity);
            isThrown = false; // Fırlatılmadı, kamerayı takip edecek
            
            // Boyutu ayarla
            currentPokeball.transform.localScale = Vector3.one * pokeballScale;
            
            // Rigidbody'yi kinematic yap (takip ederken fizik etkilenmesin)
            Rigidbody rb = currentPokeball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }
            
            // Renderer referansını sıfırla (yeni pokeball için)
            pokeballRenderer = null;
            
            // Eğer seçili modundaysak rengi hemen güncelle
            if (isPokeballSelected)
            {
                UpdatePokeballColor();
            }
            
            // Collision script'ini ekle ve initialize et
            PokeballCollision collision = currentPokeball.AddComponent<PokeballCollision>();
            collision.Initialize(this);
            
            Debug.Log("Pokeball spawn edildi - kamerayı takip ediyor");
        }
    }

    void ThrowPokeball()
    {
        if (currentPokeball == null || mainCamera == null) return;

        // Stoğu burada düş (Normal sınırsız → her zaman true). Great/Ultra biterse fırlatmayı iptal et.
        if (PokemonBag.Instance != null && !PokemonBag.Instance.TryConsumeBall(currentBallType))
        {
            if (selectButtonText != null) selectButtonText.text = $"[ 0 ]\n{BallLabel(currentBallType)}";
            return;
        }
        RefreshBallChips();

        // Artık kamerayı takip etmesin
        isThrown = true;
        
        // Kaydırma vektörünü hesapla
        Vector3 swipeVector = mouseEndPosition - mouseStartPosition;
        
        // Kaydırma mesafesini hesapla (piksel cinsinden)
        float swipeDistance = swipeVector.magnitude;
        
        // Kaydırma mesafesine göre kuvvet hesapla
        float throwPower = Mathf.Clamp(swipeDistance * swipeMultiplier, 0f, 1f);
        float actualForce = Mathf.Lerp(minThrowForce, maxThrowForce, throwPower);
        
        // Yön hesapla (normalize edilmiş)
        Vector3 throwDirection = swipeVector.normalized;
        
        Rigidbody rb = currentPokeball.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            
            // Kamera yönüne göre fırlatma kuvveti hesapla
            Vector3 cameraForward = mainCamera.transform.forward;
            Vector3 cameraRight = mainCamera.transform.right;
            Vector3 cameraUp = mainCamera.transform.up;
            
            // X: Sağ-sol yön (kaydırma yönüne göre, kamera koordinatlarında)
            // Y: Yukarı kuvvet (kaydırma mesafesine göre)
            // Z: İleri kuvvet (kamera yönünde)
            Vector3 force = cameraForward * (forwardForce + actualForce * 0.5f)
                + cameraRight * (throwDirection.x * actualForce * horizontalMultiplier)
                + cameraUp * (throwDirection.y * actualForce * verticalMultiplier + actualForce * 0.3f);
            
            rb.AddForce(force, ForceMode.Impulse);
            
            Debug.Log($"Pokeball fırlatıldı! Mesafe: {swipeDistance:F0}px, Güç: {actualForce:F1}");
        }
    }

    public void OnPokemonCaught()
    {
        // Pokeball'u yok et ve yenisini oluştur
        Destroy(currentPokeball);
        currentPokeball = null;
        isThrown = false;
        Invoke(nameof(SpawnPokeball), returnDelay);
    }

    public void OnPokemonEscaped()
    {
        // Pokeball'u yok et ve yenisini oluştur
        Destroy(currentPokeball);
        currentPokeball = null;
        isThrown = false;
        Invoke(nameof(SpawnPokeball), returnDelay);
    }
    
    /// <summary>
    /// Pokeball offset'ini runtime'da değiştirmek için
    /// </summary>
    public void SetPokeballOffset(Vector3 newOffset)
    {
        pokeballOffset = newOffset;
    }
}
