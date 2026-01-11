using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PokemonWorldUI : MonoBehaviour
{
    [Header("UI Referansları")]
    public Canvas worldCanvas;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI levelText;
    public Slider healthBar;
    public Image healthFill;
    
    [Header("Ayarlar")]
    public Vector3 offset = new Vector3(0, 1.5f, 0); // Pokemon'un üstünde ne kadar yüksekte
    public bool lookAtCamera = true;
    public float uiScale = 0.01f; // World space UI ölçeği
    
    [Header("Renk Ayarları")]
    public Color healthyColor = new Color(0.2f, 0.8f, 0.2f); // Yeşil
    public Color warnColor = new Color(1f, 0.8f, 0.2f); // Sarı
    public Color dangerColor = new Color(0.9f, 0.2f, 0.2f); // Kırmızı
    
    private WildPokemon wildPokemon;
    private Transform cameraTransform;
    private int maxHealth = 100;
    private int currentHealth = 100;

    void Start()
    {
        cameraTransform = Camera.main.transform;
        wildPokemon = GetComponent<WildPokemon>();
        
        // UI yoksa oluştur
        if (worldCanvas == null)
        {
            CreateUI();
        }
        
        // WildPokemon varsa bilgileri al
        if (wildPokemon != null)
        {
            UpdateUI();
        }
    }

    void LateUpdate()
    {
        if (worldCanvas == null) return;
        
        // UI'ı Pokemon'un üstünde tut
        worldCanvas.transform.position = transform.position + offset;
        
        // Kameraya baksın
        if (lookAtCamera && cameraTransform != null)
        {
            worldCanvas.transform.LookAt(cameraTransform);
            worldCanvas.transform.Rotate(0, 180, 0); // Ters dönmemesi için
        }
    }

    void CreateUI()
    {
        // Canvas oluştur
        GameObject canvasObj = new GameObject("PokemonUI_Canvas");
        canvasObj.transform.SetParent(transform);
        canvasObj.transform.localPosition = offset;
        
        worldCanvas = canvasObj.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10;
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        RectTransform canvasRect = worldCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(200, 80);
        canvasRect.localScale = Vector3.one * uiScale;
        
        // Arka plan panel
        GameObject bgPanel = CreatePanel(canvasObj.transform, "Background", new Vector2(200, 80));
        Image bgImage = bgPanel.GetComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.6f);
        
        // İsim text
        GameObject nameObj = CreateTextObject(bgPanel.transform, "NameText", new Vector2(0, 20), new Vector2(190, 30));
        nameText = nameObj.GetComponent<TextMeshProUGUI>();
        nameText.text = GetPokemonName();
        nameText.fontSize = 24;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = Color.white;
        
        // Level text
        GameObject levelObj = CreateTextObject(bgPanel.transform, "LevelText", new Vector2(0, -5), new Vector2(190, 25));
        levelText = levelObj.GetComponent<TextMeshProUGUI>();
        levelText.text = "Lv. 1";
        levelText.fontSize = 18;
        levelText.alignment = TextAlignmentOptions.Center;
        levelText.color = new Color(1f, 0.9f, 0.5f);
        
        // Health bar arka plan
        GameObject healthBgObj = CreatePanel(bgPanel.transform, "HealthBarBG", new Vector2(180, 15));
        RectTransform healthBgRect = healthBgObj.GetComponent<RectTransform>();
        healthBgRect.anchoredPosition = new Vector2(0, -28);
        Image healthBgImage = healthBgObj.GetComponent<Image>();
        healthBgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        
        // Health bar slider
        GameObject sliderObj = new GameObject("HealthBar");
        sliderObj.transform.SetParent(healthBgObj.transform, false);
        
        healthBar = sliderObj.AddComponent<Slider>();
        RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.anchorMin = Vector2.zero;
        sliderRect.anchorMax = Vector2.one;
        sliderRect.sizeDelta = Vector2.zero;
        sliderRect.anchoredPosition = Vector2.zero;
        
        // Fill area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = new Vector2(-10, -6);
        fillAreaRect.anchoredPosition = Vector2.zero;
        
        // Fill
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        healthFill = fill.AddComponent<Image>();
        healthFill.color = healthyColor;
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        fillRect.anchoredPosition = Vector2.zero;
        
        healthBar.fillRect = fillRect;
        healthBar.targetGraphic = healthFill;
        healthBar.direction = Slider.Direction.LeftToRight;
        healthBar.minValue = 0;
        healthBar.maxValue = 1;
        healthBar.value = 1;
        healthBar.interactable = false;
        
        // Background ve handle'ı kaldır (sadece fill kullan)
        healthBar.transition = Selectable.Transition.None;
    }

    GameObject CreatePanel(Transform parent, string name, Vector2 size)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        
        Image image = panel.AddComponent<Image>();
        image.color = Color.white;
        
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        
        return panel;
    }

    GameObject CreateTextObject(Transform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        
        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        
        return textObj;
    }

    string GetPokemonName()
    {
        string name = gameObject.name;
        // (Clone) ve benzeri ekleri temizle
        name = name.Replace("(Clone)", "").Trim();
        name = name.Replace("_", " ");
        return name;
    }

    public void UpdateUI()
    {
        if (wildPokemon != null)
        {
            // İsim
            if (nameText != null)
                nameText.text = GetPokemonName();
            
            // Level
            if (levelText != null)
                levelText.text = $"Lv. {wildPokemon.level}";
            
            // Can (WildPokemon'da can sistemi yoksa varsayılan kullan)
            UpdateHealthBar(currentHealth, maxHealth);
        }
    }

    public void SetHealth(int current, int max)
    {
        currentHealth = current;
        maxHealth = max;
        UpdateHealthBar(current, max);
    }

    void UpdateHealthBar(int current, int max)
    {
        if (healthBar == null || healthFill == null) return;
        
        float healthPercent = (float)current / max;
        healthBar.value = healthPercent;
        
        // Renge göre değiştir
        if (healthPercent > 0.5f)
            healthFill.color = healthyColor;
        else if (healthPercent > 0.25f)
            healthFill.color = warnColor;
        else
            healthFill.color = dangerColor;
    }

    public void TakeDamage(int damage)
    {
        currentHealth = Mathf.Max(0, currentHealth - damage);
        UpdateHealthBar(currentHealth, maxHealth);
    }
}
