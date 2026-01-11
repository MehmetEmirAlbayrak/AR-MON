using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Ekranda algılanan biome ve objeleri gösteren UI bileşeni
/// </summary>
public class DetectionInfoUI : MonoBehaviour
{
    public static DetectionInfoUI Instance { get; private set; }
    
    [Header("UI Referansları")]
    [Tooltip("Biome bilgisini gösterecek Text (TMP veya Legacy)")]
    public TextMeshProUGUI biomeText;
    
    [Tooltip("Algılanan objeleri gösterecek Text (TMP veya Legacy)")]
    public TextMeshProUGUI objectsText;
    
    [Tooltip("Legacy UI Text kullanıyorsanız bunları doldurun")]
    public Text legacyBiomeText;
    public Text legacyObjectsText;

    [Header("Görünüm Ayarları")]
    [Tooltip("Biome etiketi öneki")]
    public string biomePrefix = "Biome: ";
    
    [Tooltip("Objeler etiketi öneki")]
    public string objectsPrefix = "Algilanan: ";
    
    [Tooltip("Maksimum gösterilecek obje sayısı")]
    public int maxDisplayedObjects = 5;
    
    [Tooltip("Güven eşiği (bunun altındakiler gösterilmez)")]
    [Range(0f, 1f)]
    public float confidenceThreshold = 0.5f;

    [Header("Renk Ayarları")]
    public Color biomeColor = new Color(0.2f, 0.8f, 0.4f);  // Yeşilimsi
    public Color objectColor = new Color(0.4f, 0.6f, 1f);    // Mavimsi
    public Color highConfidenceColor = new Color(0.2f, 1f, 0.2f);
    public Color lowConfidenceColor = new Color(1f, 0.8f, 0.2f);

    [Header("Dinamik UI")]
    public bool createDynamicUI = true;
    
    // Son güncelleme zamanı (çok sık güncellemeyi engellemek için)
    float lastUpdateTime;
    float updateInterval = 0.1f;
    
    // Dinamik UI elementleri
    private Canvas uiCanvas;
    private GameObject biomePanel;
    private GameObject objectsPanel;

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
        // Eğer referanslar atanmamışsa ve dinamik UI açıksa, oluştur
        if (createDynamicUI && (biomeText == null || objectsText == null))
        {
            CreateDynamicUI();
        }
        
        // Başlangıçta boş göster
        ClearDisplay();
    }
    
    void CreateDynamicUI()
    {
        // Canvas
        GameObject canvasObj = new GameObject("DetectionInfoCanvas");
        canvasObj.transform.SetParent(transform);
        uiCanvas = canvasObj.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 50; // Diğer UI'lardan düşük
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // ============ SOL ÜST - Biome Panel ============
        // Notch için daha aşağıda
        biomePanel = new GameObject("BiomePanel");
        biomePanel.transform.SetParent(canvasObj.transform, false);
        Image biomeBg = biomePanel.AddComponent<Image>();
        biomeBg.color = new Color(0, 0, 0, 0.7f);
        biomeBg.raycastTarget = false;
        
        RectTransform biomeRect = biomePanel.GetComponent<RectTransform>();
        biomeRect.anchorMin = new Vector2(0, 1);
        biomeRect.anchorMax = new Vector2(0, 1);
        biomeRect.pivot = new Vector2(0, 1);
        biomeRect.anchoredPosition = new Vector2(15, -70); // Notch için -70
        biomeRect.sizeDelta = new Vector2(320, 50);
        
        // Biome Text
        GameObject biomeTxtObj = new GameObject("BiomeText");
        biomeTxtObj.transform.SetParent(biomePanel.transform, false);
        biomeText = biomeTxtObj.AddComponent<TextMeshProUGUI>();
        biomeText.text = "Biome: Algilaniyor...";
        biomeText.fontSize = 22; // Mobilde daha okunabilir
        biomeText.fontStyle = FontStyles.Bold;
        biomeText.alignment = TextAlignmentOptions.Left;
        biomeText.color = biomeColor;
        biomeText.raycastTarget = false;
        
        RectTransform biomeTxtRect = biomeTxtObj.GetComponent<RectTransform>();
        biomeTxtRect.anchorMin = Vector2.zero;
        biomeTxtRect.anchorMax = Vector2.one;
        biomeTxtRect.offsetMin = new Vector2(12, 8);
        biomeTxtRect.offsetMax = new Vector2(-12, -8);
        
        // ============ SOL ALT - Objects Panel ============
        // Home bar için daha yukarıda
        objectsPanel = new GameObject("ObjectsPanel");
        objectsPanel.transform.SetParent(canvasObj.transform, false);
        Image objectsBg = objectsPanel.AddComponent<Image>();
        objectsBg.color = new Color(0, 0, 0, 0.7f);
        objectsBg.raycastTarget = false;
        
        RectTransform objectsRect = objectsPanel.GetComponent<RectTransform>();
        objectsRect.anchorMin = new Vector2(0, 0);
        objectsRect.anchorMax = new Vector2(0, 0);
        objectsRect.pivot = new Vector2(0, 0);
        objectsRect.anchoredPosition = new Vector2(15, 100); // Home bar + ÇANTA butonu için
        objectsRect.sizeDelta = new Vector2(350, 180);
        
        // Objects Text
        GameObject objectsTxtObj = new GameObject("ObjectsText");
        objectsTxtObj.transform.SetParent(objectsPanel.transform, false);
        objectsText = objectsTxtObj.AddComponent<TextMeshProUGUI>();
        objectsText.text = "Algilanan: Bekleniyor...";
        objectsText.fontSize = 18;
        objectsText.alignment = TextAlignmentOptions.Left;
        objectsText.color = objectColor;
        objectsText.raycastTarget = false;
        
        RectTransform objectsTxtRect = objectsTxtObj.GetComponent<RectTransform>();
        objectsTxtRect.anchorMin = Vector2.zero;
        objectsTxtRect.anchorMax = Vector2.one;
        objectsTxtRect.offsetMin = new Vector2(12, 8);
        objectsTxtRect.offsetMax = new Vector2(-12, -8);
        
        Debug.Log("DetectionInfoUI dinamik olarak oluşturuldu (mobil uyumlu)!");
    }

    /// <summary>
    /// Biome bilgisini güncelle
    /// </summary>
    public void UpdateBiome(string biome)
    {
        string displayText = string.IsNullOrEmpty(biome) 
            ? $"{biomePrefix}Algılanıyor..." 
            : $"{biomePrefix}<color=#{ColorUtility.ToHtmlStringRGB(biomeColor)}>{FormatBiomeName(biome)}</color>";

        if (biomeText != null)
        {
            biomeText.text = displayText;
        }
        
        if (legacyBiomeText != null)
        {
            // Legacy UI HTML tag desteklemez, düz metin kullan
            legacyBiomeText.text = string.IsNullOrEmpty(biome) 
                ? $"{biomePrefix}Algılanıyor..." 
                : $"{biomePrefix}{FormatBiomeName(biome)}";
            legacyBiomeText.color = biomeColor;
        }
    }

    /// <summary>
    /// Algılanan objeleri güncelle
    /// </summary>
    public void UpdateDetectedObjects(List<DetectedObject> objects)
    {
        if (Time.time - lastUpdateTime < updateInterval)
            return;
        
        lastUpdateTime = Time.time;

        if (objects == null || objects.Count == 0)
        {
            SetObjectsText($"{objectsPrefix}Obje bulunamadı");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.Append(objectsPrefix);

        int displayCount = 0;
        foreach (var obj in objects)
        {
            if (displayCount >= maxDisplayedObjects)
            {
                sb.Append($"\n  ... +{objects.Count - maxDisplayedObjects} daha");
                break;
            }

            if (obj.confidence < confidenceThreshold)
                continue;

            if (displayCount > 0)
                sb.Append("\n");

            // Güven seviyesine göre renk
            Color confColor = Color.Lerp(lowConfidenceColor, highConfidenceColor, obj.confidence);
            string confColorHex = ColorUtility.ToHtmlStringRGB(confColor);

            // Obje boyutu bilgisi
            string sizeInfo = "";
            if (obj.bbox != null && obj.bbox.Length >= 4)
            {
                float widthPercent = obj.Width * 100f;
                float heightPercent = obj.Height * 100f;
                sizeInfo = $" ({widthPercent:F0}%x{heightPercent:F0}%)";
            }

            sb.Append($"  • {FormatLabel(obj.label)} <color=#{confColorHex}>[{obj.confidence:P0}]</color>{sizeInfo}");
            displayCount++;
        }

        if (displayCount == 0)
        {
            SetObjectsText($"{objectsPrefix}Düşük güvenilirlik");
            return;
        }

        SetObjectsText(sb.ToString());
    }

    /// <summary>
    /// Biome ve objeleri tek seferde güncelle
    /// </summary>
    public void UpdateAll(string biome, List<DetectedObject> objects)
    {
        UpdateBiome(biome);
        UpdateDetectedObjects(objects);
    }

    /// <summary>
    /// Ekranı temizle
    /// </summary>
    public void ClearDisplay()
    {
        UpdateBiome("");
        SetObjectsText($"{objectsPrefix}Bekleniyor...");
    }

    void SetObjectsText(string text)
    {
        if (objectsText != null)
        {
            objectsText.text = text;
        }
        
        if (legacyObjectsText != null)
        {
            // HTML taglarını temizle
            legacyObjectsText.text = System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", "");
            legacyObjectsText.color = objectColor;
        }
    }

    /// <summary>
    /// Biome ismini formatla (ilk harf büyük)
    /// </summary>
    string FormatBiomeName(string biome)
    {
        if (string.IsNullOrEmpty(biome)) return biome;
        
        // Türkçe biome isimleri için çeviri
        string formatted = biome.ToLower() switch
        {
            "forest" => "Orman",
            "water" => "Su",
            "urban" => "Sehir",
            "mountain" => "Dag",
            "desert" => "Col",
            "snow" => "Kar",
            "grass" => "Cayir",
            "beach" => "Plaj",
            "cave" => "Magara",
            _ => char.ToUpper(biome[0]) + biome.Substring(1).ToLower()
        };
        
        return formatted;
    }

    /// <summary>
    /// Label'ı formatla
    /// </summary>
    string FormatLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return label;
        
        // Yaygın obje çevirileri
        string formatted = label.ToLower() switch
        {
            "tree" => "Agac",
            "rock" => "Kaya",
            "grass" => "Cimen",
            "flower" => "Cicek",
            "water" => "Su",
            "bench" => "Bank",
            "car" => "Araba",
            "person" => "Insan",
            "dog" => "Kopek",
            "cat" => "Kedi",
            "bird" => "Kus",
            "building" => "Bina",
            "sky" => "Gokyuzu",
            "road" => "Yol",
            "plant" => "Bitki",
            _ => char.ToUpper(label[0]) + label.Substring(1).ToLower()
        };
        
        return formatted;
    }
}
