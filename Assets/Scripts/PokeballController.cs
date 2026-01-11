// 1/9/2026 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PokeballController : MonoBehaviour
{
    public static PokeballController Instance { get; private set; }
    
    public GameObject pokeballPrefab; // Poketopu prefabı
    public Transform spawnPoint; // Poketopu fırlatma noktası
    
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
    public float returnDelay = 5f; // Geri dönme süresi

    private GameObject currentPokeball;
    private Vector3 mouseStartPosition;
    private Vector3 mouseEndPosition;
    private bool isDragging = false;
    
    // Pokeball seçim sistemi
    private bool isPokeballSelected = false;
    public bool IsPokeballSelected => isPokeballSelected;
    
    // UI
    private Canvas uiCanvas;
    private GameObject selectButton;
    private Image selectButtonImage;
    private TextMeshProUGUI selectButtonText;
    
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
        CreateUI();
        SpawnPokeball();
    }
    
    void CreateUI()
    {
        // Canvas oluştur
        GameObject canvasObj = new GameObject("PokeballSelectCanvas");
        uiCanvas = canvasObj.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 90; // Envanter UI'dan düşük
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Pokeball seçim butonu - SOL alt köşe (çanta sağda olduğu için)
        // Mobil için home bar'dan uzak ve daha büyük
        selectButton = new GameObject("PokeballSelectBtn");
        selectButton.transform.SetParent(canvasObj.transform, false);
        
        selectButtonImage = selectButton.AddComponent<Image>();
        selectButtonImage.color = new Color(0.3f, 0.3f, 0.4f, 0.9f); // Koyu gri (seçili değil)
        
        RectTransform btnRect = selectButton.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0, 0); // Sol alt
        btnRect.anchorMax = new Vector2(0, 0);
        btnRect.pivot = new Vector2(0, 0);
        btnRect.anchoredPosition = new Vector2(20, 50); // Home bar için daha yukarıda
        btnRect.sizeDelta = new Vector2(120, 120); // Daha büyük dokunma alanı
        
        // Buton text - Mobil için daha büyük font
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(selectButton.transform, false);
        selectButtonText = textObj.AddComponent<TextMeshProUGUI>();
        selectButtonText.text = "[ ]\nYAKALA";
        selectButtonText.fontSize = 20; // 16'dan 20'ye
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
        
        Debug.Log("Pokeball seçim UI oluşturuldu!");
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

        // Pokeball düştüyse yenisini oluştur
        if (currentPokeball != null && currentPokeball.transform.position.y < -10f)
        {
            Destroy(currentPokeball);
            currentPokeball = null;
            SpawnPokeball();
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
        if (currentPokeball == null)
        {
            currentPokeball = Instantiate(pokeballPrefab, spawnPoint.position, spawnPoint.rotation);
            
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
        }
    }

    void ThrowPokeball()
    {
        if (currentPokeball == null) return;
        
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
            
            // X: Sağ-sol yön (kaydırma yönüne göre)
            // Y: Yukarı kuvvet (kaydırma mesafesine göre)
            // Z: İleri kuvvet (sabit + kaydırma mesafesine göre)
            Vector3 force = new Vector3(
                throwDirection.x * actualForce * horizontalMultiplier,  // Sağ-sol
                throwDirection.y * actualForce * verticalMultiplier + actualForce * 0.3f,  // Yukarı
                forwardForce + actualForce * 0.5f  // İleri
            );
            
            rb.AddForce(force, ForceMode.Impulse);
            
            Debug.Log($"Pokeball fırlatıldı! Mesafe: {swipeDistance:F0}px, Güç: {actualForce:F1}");
        }
    }

    public void OnPokemonCaught()
    {
        // Pokeball'u yok et ve yenisini oluştur
        Destroy(currentPokeball);
        currentPokeball = null;
        Invoke(nameof(SpawnPokeball), returnDelay);
    }

    public void OnPokemonEscaped()
    {
        // Pokeball'u yok et ve yenisini oluştur
        Destroy(currentPokeball);
        currentPokeball = null;
        Invoke(nameof(SpawnPokeball), returnDelay);
    }
}
