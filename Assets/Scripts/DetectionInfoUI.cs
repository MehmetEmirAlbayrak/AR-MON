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
    
    [Tooltip("Güven eşiği (bunun altındakiler gösterilmez). Server'ın kalibre edilmiş " +
             "sınıf eşikleri 0.20'ye kadar iner — bundan yüksek tutmak geçerli tespitleri gizler.")]
    [Range(0f, 1f)]
    public float confidenceThreshold = 0.25f;

    [Header("Renk Ayarları")]
    public Color biomeColor = new Color(0.2f, 0.8f, 0.4f);  // Yeşilimsi
    public Color objectColor = new Color(0.4f, 0.6f, 1f);    // Mavimsi
    public Color highConfidenceColor = new Color(0.2f, 1f, 0.2f);
    public Color lowConfidenceColor = new Color(1f, 0.8f, 0.2f);

    // Son güncelleme zamanı (çok sık güncellemeyi engellemek için)
    float lastUpdateTime;
    float updateInterval = 0.1f;

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
        if (ARMON.UI.AR.ARHudCanvas.Instance == null)
        {
            Debug.LogWarning("[DetectionInfoUI] ARHudCanvas.Instance null — UI mount deferred.");
            return;
        }
        MountIntoHud(ARMON.UI.AR.ARHudCanvas.Instance.TopLeftSlot);
        ClearDisplay();
    }

    void MountIntoHud(Transform slot)
    {
        // One combined panel: biome line + objects line, compact.
        GameObject panel = new GameObject("DetectionInfoPanel");
        panel.transform.SetParent(slot, false);
        var img = panel.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0f, 0f, 0f, 0.5f);
        img.raycastTarget = false;

        var rt = (RectTransform)panel.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(260f, 70f);

        // Biome line (top)
        GameObject bObj = new GameObject("BiomeText");
        bObj.transform.SetParent(panel.transform, false);
        biomeText = bObj.AddComponent<TMPro.TextMeshProUGUI>();
        biomeText.fontSize = 22;
        biomeText.fontStyle = TMPro.FontStyles.Bold;
        biomeText.alignment = TMPro.TextAlignmentOptions.Left;
        biomeText.color = biomeColor;
        biomeText.raycastTarget = false;
        var brt = (RectTransform)bObj.transform;
        brt.anchorMin = new Vector2(0f, 0.5f);
        brt.anchorMax = new Vector2(1f, 1f);
        brt.offsetMin = new Vector2(10f, 0f);
        brt.offsetMax = new Vector2(-10f, -5f);

        // Object summary line (bottom)
        GameObject oObj = new GameObject("ObjectsText");
        oObj.transform.SetParent(panel.transform, false);
        objectsText = oObj.AddComponent<TMPro.TextMeshProUGUI>();
        objectsText.fontSize = 16;
        objectsText.alignment = TMPro.TextAlignmentOptions.Left;
        objectsText.color = objectColor;
        objectsText.raycastTarget = false;
        var ort = (RectTransform)oObj.transform;
        ort.anchorMin = new Vector2(0f, 0f);
        ort.anchorMax = new Vector2(1f, 0.5f);
        ort.offsetMin = new Vector2(10f, 5f);
        ort.offsetMax = new Vector2(-10f, 0f);
    }

    /// <summary>
    /// Biome bilgisini güncelle
    /// </summary>
    public void UpdateBiome(string biome)
    {
        UpdateBiome(biome, -1f);
    }

    /// <summary>
    /// Biome bilgisini güven yüzdesiyle birlikte güncelle (confidence &lt; 0 → gizle)
    /// </summary>
    public void UpdateBiome(string biome, float confidence)
    {
        string confSuffix = confidence >= 0f ? $" <size=70%>({confidence:P0})</size>" : "";
        string displayText = string.IsNullOrEmpty(biome)
            ? $"{biomePrefix}Algılanıyor..."
            : $"{biomePrefix}<color=#{ColorUtility.ToHtmlStringRGB(biomeColor)}>{FormatBiomeName(biome)}</color>{confSuffix}";

        if (biomeText != null)
        {
            biomeText.text = displayText;
        }

        if (legacyBiomeText != null)
        {
            // Legacy UI HTML tag desteklemez, düz metin kullan
            string plainConf = confidence >= 0f ? $" ({confidence:P0})" : "";
            legacyBiomeText.text = string.IsNullOrEmpty(biome)
                ? $"{biomePrefix}Algılanıyor..."
                : $"{biomePrefix}{FormatBiomeName(biome)}{plainConf}";
            legacyBiomeText.color = biomeColor;
        }
    }

    /// <summary>
    /// Algılanan objeleri güncelle
    /// </summary>
    public void UpdateDetectedObjects(List<DetectedObject> objects)
    {
        if (Time.time - lastUpdateTime < updateInterval) return;
        lastUpdateTime = Time.time;

        if (objects == null || objects.Count == 0)
        {
            SetObjectsText($"{objectsPrefix}0");
            return;
        }
        int aboveThreshold = 0;
        string topLabel = null;
        float topConf = 0f;
        foreach (var obj in objects)
        {
            if (obj.confidence < confidenceThreshold) continue;
            aboveThreshold++;
            if (obj.confidence > topConf) { topConf = obj.confidence; topLabel = obj.label; }
        }
        if (aboveThreshold == 0) { SetObjectsText($"{objectsPrefix}0"); return; }
        SetObjectsText($"{objectsPrefix}{aboveThreshold} ({FormatLabel(topLabel)} {topConf:P0})");
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
