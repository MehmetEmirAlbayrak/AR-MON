using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pokemon detay sayfası - envanterde Pokemon'a uzun basınca açılır
/// </summary>
public class PokemonDetailUI : MonoBehaviour
{
    public static PokemonDetailUI Instance { get; private set; }
    
    // UI Elements
    private Canvas detailCanvas;
    private GameObject detailPanel;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI levelText;
    private TextMeshProUGUI hpText;
    private TextMeshProUGUI attackText;
    private TextMeshProUGUI defenseText;
    private TextMeshProUGUI speedText;
    private TextMeshProUGUI catchDateText;
    private TextMeshProUGUI prefabIdText;
    private Image xpBarFill;
    private TextMeshProUGUI xpText;
    private Button closeButton;
    private Button releaseButton;
    private Button summonButton;
    
    private PokemonData currentPokemon;
    private int currentIndex = -1;
    private bool isOpen = false;
    
    public bool IsOpen => isOpen;

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
    }

    void CreateUI()
    {
        // Canvas
        GameObject canvasObj = new GameObject("PokemonDetailCanvas");
        canvasObj.transform.SetParent(transform);
        detailCanvas = canvasObj.AddComponent<Canvas>();
        detailCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        detailCanvas.sortingOrder = 150; // En üstte
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Ana Panel
        detailPanel = new GameObject("DetailPanel");
        detailPanel.transform.SetParent(canvasObj.transform, false);
        
        // Arka plan overlay
        Image overlay = detailPanel.AddComponent<Image>();
        overlay.color = new Color(0, 0, 0, 0.9f);
        RectTransform overlayRect = detailPanel.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;
        
        // İçerik kutusu
        GameObject contentBox = new GameObject("ContentBox");
        contentBox.transform.SetParent(detailPanel.transform, false);
        Image contentBg = contentBox.AddComponent<Image>();
        contentBg.color = new Color(0.12f, 0.14f, 0.18f, 1f);
        RectTransform contentRect = contentBox.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(550, 700);
        
        // ===== BAŞLIK =====
        nameText = CreateText(contentBox.transform, "NameText", "", 36, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRectTransform(nameText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(-40, 50));
        nameText.color = new Color(1f, 0.9f, 0.3f); // Altın sarısı
        
        // Level
        levelText = CreateText(contentBox.transform, "LevelText", "Level 1", 28, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRectTransform(levelText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -75), new Vector2(-40, 40));
        levelText.color = new Color(0.6f, 0.8f, 1f);
        
        // ===== XP BAR =====
        GameObject xpBarBg = new GameObject("XPBarBg");
        xpBarBg.transform.SetParent(contentBox.transform, false);
        Image xpBgImg = xpBarBg.AddComponent<Image>();
        xpBgImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);
        RectTransform xpBgRect = xpBarBg.GetComponent<RectTransform>();
        SetRectTransform(xpBgRect, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -125), new Vector2(-60, 25));
        
        GameObject xpBarFillObj = new GameObject("XPBarFill");
        xpBarFillObj.transform.SetParent(xpBarBg.transform, false);
        xpBarFill = xpBarFillObj.AddComponent<Image>();
        xpBarFill.color = new Color(0.3f, 0.7f, 1f, 1f);
        RectTransform xpFillRect = xpBarFillObj.GetComponent<RectTransform>();
        xpFillRect.anchorMin = Vector2.zero;
        xpFillRect.anchorMax = new Vector2(0.5f, 1f); // Başlangıçta %50
        xpFillRect.offsetMin = new Vector2(2, 2);
        xpFillRect.offsetMax = new Vector2(-2, -2);
        
        xpText = CreateText(contentBox.transform, "XPText", "XP: 0 / 100", 18, FontStyles.Normal, TextAlignmentOptions.Center);
        SetRectTransform(xpText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -155), new Vector2(-40, 25));
        xpText.color = new Color(0.7f, 0.7f, 0.7f);
        
        // ===== STATLAR =====
        // Stat başlığı
        TextMeshProUGUI statsTitle = CreateText(contentBox.transform, "StatsTitle", "-- STATLAR --", 22, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRectTransform(statsTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -200), new Vector2(-40, 35));
        statsTitle.color = new Color(0.9f, 0.6f, 0.3f);
        
        // HP
        hpText = CreateStatRow(contentBox.transform, "HP", 240, new Color(0.3f, 0.9f, 0.4f));
        
        // Attack
        attackText = CreateStatRow(contentBox.transform, "Attack", 290, new Color(0.9f, 0.4f, 0.3f));
        
        // Defense
        defenseText = CreateStatRow(contentBox.transform, "Defense", 340, new Color(0.4f, 0.6f, 0.9f));
        
        // Speed
        speedText = CreateStatRow(contentBox.transform, "Speed", 390, new Color(0.9f, 0.9f, 0.3f));
        
        // ===== EK BİLGİLER =====
        // Yakalanma tarihi
        catchDateText = CreateText(contentBox.transform, "CatchDate", "Yakalandi: --", 16, FontStyles.Italic, TextAlignmentOptions.Center);
        SetRectTransform(catchDateText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -450), new Vector2(-40, 30));
        catchDateText.color = new Color(0.5f, 0.5f, 0.5f);
        
        // Prefab ID
        prefabIdText = CreateText(contentBox.transform, "PrefabId", "Tur: --", 14, FontStyles.Normal, TextAlignmentOptions.Center);
        SetRectTransform(prefabIdText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -480), new Vector2(-40, 25));
        prefabIdText.color = new Color(0.4f, 0.4f, 0.4f);
        
        // ===== BUTONLAR =====
        // Çağır butonu (en üstte, belirgin)
        summonButton = CreateButton(contentBox.transform, "SummonBtn", "SAVAS!", new Color(0.8f, 0.5f, 0.1f), 520);
        summonButton.onClick.AddListener(OnSummonClicked);
        
        // Serbest bırak butonu
        releaseButton = CreateButton(contentBox.transform, "ReleaseBtn", "SERBEST BIRAK", new Color(0.7f, 0.3f, 0.2f), 580);
        releaseButton.onClick.AddListener(OnReleaseClicked);
        
        // Kapat butonu
        closeButton = CreateButton(contentBox.transform, "CloseBtn", "KAPAT", new Color(0.4f, 0.4f, 0.5f), 640);
        closeButton.onClick.AddListener(Close);
        
        // Başlangıçta gizle
        detailPanel.SetActive(false);
        
        Debug.Log("PokemonDetailUI olusturuldu!");
    }
    
    TextMeshProUGUI CreateText(Transform parent, string name, string text, int fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }
    
    TextMeshProUGUI CreateStatRow(Transform parent, string statName, float yPos, Color color)
    {
        // Arka plan
        GameObject rowBg = new GameObject($"{statName}Row");
        rowBg.transform.SetParent(parent, false);
        Image bgImg = rowBg.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.15f, 0.2f, 0.8f);
        RectTransform bgRect = rowBg.GetComponent<RectTransform>();
        SetRectTransform(bgRect, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -yPos), new Vector2(-60, 40));
        
        // Stat ismi
        TextMeshProUGUI label = CreateText(rowBg.transform, "Label", statName, 20, FontStyles.Bold, TextAlignmentOptions.Left);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(0.5f, 1);
        labelRect.offsetMin = new Vector2(15, 5);
        labelRect.offsetMax = new Vector2(0, -5);
        label.color = color;
        
        // Stat değeri
        TextMeshProUGUI value = CreateText(rowBg.transform, "Value", "0", 22, FontStyles.Bold, TextAlignmentOptions.Right);
        RectTransform valueRect = value.rectTransform;
        valueRect.anchorMin = new Vector2(0.5f, 0);
        valueRect.anchorMax = new Vector2(1, 1);
        valueRect.offsetMin = new Vector2(0, 5);
        valueRect.offsetMax = new Vector2(-15, -5);
        value.color = Color.white;
        
        return value;
    }
    
    Button CreateButton(Transform parent, string name, string text, Color bgColor, float yPos)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = bgColor;
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnBg;
        
        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        SetRectTransform(btnRect, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -yPos), new Vector2(-80, 50));
        
        TextMeshProUGUI btnText = CreateText(btnObj.transform, "Text", text, 20, FontStyles.Bold, TextAlignmentOptions.Center);
        RectTransform txtRect = btnText.rectTransform;
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;
        
        return btn;
    }
    
    void SetRectTransform(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
    }
    
    /// <summary>
    /// Detay sayfasını aç
    /// </summary>
    public void Open(int pokemonIndex)
    {
        if (PokemonBag.Instance == null) return;
        
        PokemonData pokemon = PokemonBag.Instance.GetPokemonAt(pokemonIndex);
        if (pokemon == null) return;
        
        currentPokemon = pokemon;
        currentIndex = pokemonIndex;
        
        UpdateUI();
        
        detailPanel.SetActive(true);
        isOpen = true;
        
        Debug.Log($"Pokemon detay sayfasi acildi: {pokemon.pokemonName}");
    }
    
    /// <summary>
    /// UI'ı güncelle
    /// </summary>
    void UpdateUI()
    {
        if (currentPokemon == null) return;
        
        // İsim
        nameText.text = currentPokemon.pokemonName;
        
        // Level
        levelText.text = $"Level {currentPokemon.level}";
        
        // XP Bar
        float xpPercent = (float)currentPokemon.currentXP / currentPokemon.xpToNextLevel;
        xpBarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(xpPercent), 1f);
        xpText.text = $"XP: {currentPokemon.currentXP} / {currentPokemon.xpToNextLevel}";
        
        // Stats
        string hpColor = currentPokemon.IsFainted ? "red" : "white";
        hpText.text = $"<color={hpColor}>{currentPokemon.currentHealth}</color> / {currentPokemon.Health}";
        attackText.text = currentPokemon.Attack.ToString();
        defenseText.text = currentPokemon.Defense.ToString();
        speedText.text = currentPokemon.Speed.ToString();
        
        // Ek bilgiler
        catchDateText.text = $"Yakalandi: {currentPokemon.catchDate}";
        prefabIdText.text = $"Tur: {currentPokemon.prefabId}";
        
        // Savaş butonu - bayılmışsa pasif
        bool canSummon = !currentPokemon.IsFainted;
        summonButton.interactable = canSummon;
        summonButton.GetComponent<Image>().color = canSummon ? new Color(0.8f, 0.5f, 0.1f) : new Color(0.3f, 0.3f, 0.3f);
    }
    
    /// <summary>
    /// Detay sayfasını kapat
    /// </summary>
    public void Close()
    {
        detailPanel.SetActive(false);
        isOpen = false;
        currentPokemon = null;
        currentIndex = -1;
    }
    
    void OnSummonClicked()
    {
        if (currentPokemon == null || currentIndex < 0) return;
        
        if (currentPokemon.IsFainted)
        {
            Debug.Log("Bayilmis Pokemon savasamaz!");
            return;
        }
        
        // Pokemon'u çağır
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.SummonPokemon(currentIndex);
            Debug.Log($"{currentPokemon.pokemonName} savasa cagrildi!");
        }
        
        // Detay sayfasını ve envanteri kapat
        Close();
        if (PokemonInventoryUI.Instance != null)
        {
            PokemonInventoryUI.Instance.CloseInventory();
        }
    }
    
    void OnReleaseClicked()
    {
        if (currentPokemon == null || currentIndex < 0) return;
        
        string pokemonName = currentPokemon.pokemonName;
        PokemonBag.Instance.ReleasePokemon(currentIndex);
        
        Debug.Log($"{pokemonName} serbest birakildi!");
        Close();
        
        // Envanteri güncelle
        if (PokemonInventoryUI.Instance != null)
        {
            PokemonInventoryUI.Instance.RefreshSlots();
        }
    }
}
