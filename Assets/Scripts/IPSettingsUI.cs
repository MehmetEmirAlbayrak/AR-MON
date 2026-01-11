using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class IPSettingsUI : MonoBehaviour
{
    public static IPSettingsUI Instance { get; private set; }
    
    public ServerConfig config;
    
    const string PREF_KEY = "ServerIP";
    
    // UI Elements
    private Canvas mainCanvas;
    private GameObject settingsPanel;
    private TMP_InputField ipInputField;
    private Button settingsButton;
    private Button saveButton;
    private Button closeButton;
    private TextMeshProUGUI statusText;
    
    private bool isOpen = false;

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
        LoadSavedIP();
    }
    
    void CreateUI()
    {
        // Ana Canvas
        GameObject canvasObj = new GameObject("IPSettingsCanvas");
        canvasObj.transform.SetParent(transform);
        mainCanvas = canvasObj.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mainCanvas.sortingOrder = 95;
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Ayarlar butonu (sağ üst köşe)
        GameObject settingsBtnObj = new GameObject("SettingsBtn");
        settingsBtnObj.transform.SetParent(canvasObj.transform, false);
        Image settingsBtnBg = settingsBtnObj.AddComponent<Image>();
        settingsBtnBg.color = new Color(0.3f, 0.3f, 0.4f, 0.9f);
        
        RectTransform settingsBtnRect = settingsBtnObj.GetComponent<RectTransform>();
        // Sağ üst köşe - stretch olmayan sabit boyut
        settingsBtnRect.anchorMin = new Vector2(1, 1);
        settingsBtnRect.anchorMax = new Vector2(1, 1);
        settingsBtnRect.pivot = new Vector2(0.5f, 0.5f);
        settingsBtnRect.sizeDelta = new Vector2(50, 50);
        settingsBtnRect.anchoredPosition = new Vector2(-45, -45); // Sağ üst köşeden 45px içeride
        
        GameObject settingsTxtObj = new GameObject("Text");
        settingsTxtObj.transform.SetParent(settingsBtnObj.transform, false);
        TextMeshProUGUI settingsTxt = settingsTxtObj.AddComponent<TextMeshProUGUI>();
        settingsTxt.text = "IP";
        settingsTxt.fontSize = 20;
        settingsTxt.fontStyle = FontStyles.Bold;
        settingsTxt.alignment = TextAlignmentOptions.Center;
        settingsTxt.color = Color.white;
        RectTransform settingsTxtRect = settingsTxtObj.GetComponent<RectTransform>();
        settingsTxtRect.anchorMin = Vector2.zero;
        settingsTxtRect.anchorMax = Vector2.one;
        settingsTxtRect.sizeDelta = Vector2.zero;
        
        settingsButton = settingsBtnObj.AddComponent<Button>();
        settingsButton.targetGraphic = settingsBtnBg;
        settingsButton.onClick.AddListener(ToggleSettings);
        
        // Ayarlar Paneli
        settingsPanel = new GameObject("SettingsPanel");
        settingsPanel.transform.SetParent(canvasObj.transform, false);
        
        // Arka plan overlay
        Image overlayImg = settingsPanel.AddComponent<Image>();
        overlayImg.color = new Color(0, 0, 0, 0.85f);
        RectTransform overlayRect = settingsPanel.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;
        
        // Dialog kutusu - sabit boyutlu
        GameObject dialog = new GameObject("Dialog");
        dialog.transform.SetParent(settingsPanel.transform, false);
        Image dialogBg = dialog.AddComponent<Image>();
        dialogBg.color = new Color(0.15f, 0.18f, 0.22f, 1f);
        RectTransform dialogRect = dialog.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.sizeDelta = new Vector2(500, 350); // Sabit boyut
        
        // Başlık
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(dialog.transform, false);
        TextMeshProUGUI titleTxt = titleObj.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "SERVER AYARLARI";
        titleTxt.fontSize = 28;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = Color.white;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(0, -15);
        titleRect.sizeDelta = new Vector2(0, 50);
        
        // IP Label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(dialog.transform, false);
        TextMeshProUGUI labelTxt = labelObj.AddComponent<TextMeshProUGUI>();
        labelTxt.text = "Server IP Adresi:";
        labelTxt.fontSize = 20;
        labelTxt.alignment = TextAlignmentOptions.Left;
        labelTxt.color = new Color(0.8f, 0.8f, 0.8f);
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 1);
        labelRect.anchorMax = new Vector2(1, 1);
        labelRect.pivot = new Vector2(0.5f, 1);
        labelRect.anchoredPosition = new Vector2(0, -75);
        labelRect.sizeDelta = new Vector2(-40, 30);
        
        // Input Field
        GameObject inputObj = new GameObject("InputField");
        inputObj.transform.SetParent(dialog.transform, false);
        Image inputBg = inputObj.AddComponent<Image>();
        inputBg.color = new Color(0.25f, 0.28f, 0.32f, 1f);
        RectTransform inputRect = inputObj.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0, 1);
        inputRect.anchorMax = new Vector2(1, 1);
        inputRect.pivot = new Vector2(0.5f, 1);
        inputRect.anchoredPosition = new Vector2(0, -115);
        inputRect.sizeDelta = new Vector2(-40, 50);
        
        // Text Area
        GameObject textArea = new GameObject("TextArea");
        textArea.transform.SetParent(inputObj.transform, false);
        RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(10, 5);
        textAreaRect.offsetMax = new Vector2(-10, -5);
        
        // Input Text
        GameObject inputTxtObj = new GameObject("Text");
        inputTxtObj.transform.SetParent(textArea.transform, false);
        TextMeshProUGUI inputTxt = inputTxtObj.AddComponent<TextMeshProUGUI>();
        inputTxt.fontSize = 22;
        inputTxt.alignment = TextAlignmentOptions.Left;
        inputTxt.verticalAlignment = VerticalAlignmentOptions.Middle;
        inputTxt.color = Color.white;
        RectTransform inputTxtRect = inputTxtObj.GetComponent<RectTransform>();
        inputTxtRect.anchorMin = Vector2.zero;
        inputTxtRect.anchorMax = Vector2.one;
        inputTxtRect.offsetMin = Vector2.zero;
        inputTxtRect.offsetMax = Vector2.zero;
        
        // Placeholder
        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(textArea.transform, false);
        TextMeshProUGUI placeholderTxt = placeholderObj.AddComponent<TextMeshProUGUI>();
        placeholderTxt.text = "http://192.168.1.100:5000";
        placeholderTxt.fontSize = 22;
        placeholderTxt.fontStyle = FontStyles.Italic;
        placeholderTxt.alignment = TextAlignmentOptions.Left;
        placeholderTxt.verticalAlignment = VerticalAlignmentOptions.Middle;
        placeholderTxt.color = new Color(0.5f, 0.5f, 0.5f);
        RectTransform placeholderRect = placeholderObj.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;
        
        // TMP Input Field
        ipInputField = inputObj.AddComponent<TMP_InputField>();
        ipInputField.textComponent = inputTxt;
        ipInputField.textViewport = textAreaRect;
        ipInputField.placeholder = placeholderTxt;
        ipInputField.characterLimit = 100;
        
        // Status text
        GameObject statusObj = new GameObject("Status");
        statusObj.transform.SetParent(dialog.transform, false);
        statusText = statusObj.AddComponent<TextMeshProUGUI>();
        statusText.text = "";
        statusText.fontSize = 18;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.color = new Color(0.5f, 1f, 0.5f);
        RectTransform statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0, 1);
        statusRect.anchorMax = new Vector2(1, 1);
        statusRect.pivot = new Vector2(0.5f, 1);
        statusRect.anchoredPosition = new Vector2(0, -175);
        statusRect.sizeDelta = new Vector2(-40, 30);
        
        // Butonlar
        // Kapat butonu (sol)
        GameObject closeBtnObj = new GameObject("CloseBtn");
        closeBtnObj.transform.SetParent(dialog.transform, false);
        Image closeBtnBg = closeBtnObj.AddComponent<Image>();
        closeBtnBg.color = new Color(0.65f, 0.25f, 0.25f, 1f);
        closeButton = closeBtnObj.AddComponent<Button>();
        closeButton.targetGraphic = closeBtnBg;
        closeButton.onClick.AddListener(CloseSettings);
        RectTransform closeBtnRect = closeBtnObj.GetComponent<RectTransform>();
        closeBtnRect.anchorMin = new Vector2(0, 0);
        closeBtnRect.anchorMax = new Vector2(0.48f, 0);
        closeBtnRect.pivot = new Vector2(0, 0);
        closeBtnRect.anchoredPosition = new Vector2(20, 20);
        closeBtnRect.sizeDelta = new Vector2(0, 60);
        
        GameObject closeTxtObj = new GameObject("Text");
        closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
        TextMeshProUGUI closeTxt = closeTxtObj.AddComponent<TextMeshProUGUI>();
        closeTxt.text = "KAPAT";
        closeTxt.fontSize = 22;
        closeTxt.fontStyle = FontStyles.Bold;
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.color = Color.white;
        RectTransform closeTxtRect = closeTxtObj.GetComponent<RectTransform>();
        closeTxtRect.anchorMin = Vector2.zero;
        closeTxtRect.anchorMax = Vector2.one;
        closeTxtRect.offsetMin = Vector2.zero;
        closeTxtRect.offsetMax = Vector2.zero;
        
        // Kaydet butonu (sağ)
        GameObject saveBtnObj = new GameObject("SaveBtn");
        saveBtnObj.transform.SetParent(dialog.transform, false);
        Image saveBtnBg = saveBtnObj.AddComponent<Image>();
        saveBtnBg.color = new Color(0.2f, 0.65f, 0.35f, 1f);
        saveButton = saveBtnObj.AddComponent<Button>();
        saveButton.targetGraphic = saveBtnBg;
        saveButton.onClick.AddListener(SaveIP);
        RectTransform saveBtnRect = saveBtnObj.GetComponent<RectTransform>();
        saveBtnRect.anchorMin = new Vector2(0.52f, 0);
        saveBtnRect.anchorMax = new Vector2(1, 0);
        saveBtnRect.pivot = new Vector2(1, 0);
        saveBtnRect.anchoredPosition = new Vector2(-20, 20);
        saveBtnRect.sizeDelta = new Vector2(0, 60);
        
        GameObject saveTxtObj = new GameObject("Text");
        saveTxtObj.transform.SetParent(saveBtnObj.transform, false);
        TextMeshProUGUI saveTxt = saveTxtObj.AddComponent<TextMeshProUGUI>();
        saveTxt.text = "KAYDET";
        saveTxt.fontSize = 22;
        saveTxt.fontStyle = FontStyles.Bold;
        saveTxt.alignment = TextAlignmentOptions.Center;
        saveTxt.color = Color.white;
        RectTransform saveTxtRect = saveTxtObj.GetComponent<RectTransform>();
        saveTxtRect.anchorMin = Vector2.zero;
        saveTxtRect.anchorMax = Vector2.one;
        saveTxtRect.offsetMin = Vector2.zero;
        saveTxtRect.offsetMax = Vector2.zero;
        
        // Başlangıçta panel kapalı
        settingsPanel.SetActive(false);
        
        Debug.Log("IP Settings UI oluşturuldu!");
    }
    
    void LoadSavedIP()
    {
        if (config != null)
        {
            string savedIp = PlayerPrefs.GetString(PREF_KEY, config.serverIP);
            config.serverIP = savedIp;
            if (ipInputField != null)
            {
                ipInputField.text = savedIp;
            }
        }
    }
    
    void ToggleSettings()
    {
        if (isOpen)
            CloseSettings();
        else
            OpenSettings();
    }
    
    public void OpenSettings()
    {
        isOpen = true;
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            LoadSavedIP();
            statusText.text = "";
        }
    }
    
    public void CloseSettings()
    {
        isOpen = false;
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    void SaveIP()
    {
        string newIp = ipInputField.text.Trim();

        if (!string.IsNullOrEmpty(newIp))
        {
            if (!newIp.StartsWith("http"))
                newIp = "http://" + newIp;

            if (config != null)
            {
                config.serverIP = newIp;
            }
            PlayerPrefs.SetString(PREF_KEY, newIp);
            PlayerPrefs.Save();

            statusText.text = "IP kaydedildi!";
            statusText.color = new Color(0.5f, 1f, 0.5f);
            Debug.Log("New IP saved: " + newIp);
            
            // 2 saniye sonra kapat
            Invoke(nameof(CloseSettings), 1.5f);
        }
        else
        {
            statusText.text = "IP alani bos olamaz!";
            statusText.color = new Color(1f, 0.5f, 0.5f);
            Debug.LogWarning("IP field empty!");
        }
    }
}
