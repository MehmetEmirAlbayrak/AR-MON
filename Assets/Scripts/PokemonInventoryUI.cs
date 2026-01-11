using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class PokemonInventoryUI : MonoBehaviour
{
    public static PokemonInventoryUI Instance { get; private set; }
    
    [Header("UI Referansları")]
    public Canvas mainCanvas; // Her zaman görünür canvas
    public GameObject inventoryPanel; // Açılıp kapanan panel
    public GameObject pokemonSlotPrefab;
    public Transform slotContainer;
    public Button closeButton;
    public Button openButton;
    public TextMeshProUGUI titleText;
    
    [Header("Ayarlar")]
    public Color normalSlotColor = new Color(0.25f, 0.25f, 0.35f, 1f);
    public Color selectedSlotColor = new Color(0.3f, 0.5f, 0.8f, 1f);
    public Color faintedSlotColor = new Color(0.5f, 0.25f, 0.25f, 1f);
    
    private List<GameObject> slots = new List<GameObject>();
    private bool isOpen = false;
    
    // Pot UI
    private GameObject potionPanel;
    private TextMeshProUGUI smallPotionText;
    private TextMeshProUGUI superPotionText;
    private TextMeshProUGUI hyperPotionText;
    private TextMeshProUGUI reviveText;
    private int selectedPokemonForPotion = -1;
    private PotionType? selectedPotionType = null;
    
    // Dışarıdan erişim için
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
            return;
        }
    }

    void Start()
    {
        // Eğer UI henüz oluşturulmadıysa oluştur
        if (mainCanvas == null)
        {
            CreateUI();
        }
        CloseInventory();
    }
    
    void EnsureUIExists()
    {
        if (slotContainer == null)
        {
            Debug.LogWarning("slotContainer kaybolmuş, UI yeniden oluşturuluyor...");
            CreateUI();
        }
    }
    
    void Update()
    {
        // Sadece envanter açıkken ve pot seçiliyken debug
        if (!isOpen) return;
        
        // Tıklama algılama
        if (Input.GetMouseButtonDown(0))
        {
            // Raycast ile neye tıklandığını bul
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            pointer.position = Input.mousePosition;
            
            var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, results);
            
            if (results.Count > 0)
            {
                string hitList = "";
                foreach (var r in results)
                {
                    hitList += r.gameObject.name + ", ";
                }
                Debug.Log($"[UI Click] Tıklanan: {hitList}");
            }
            else
            {
                Debug.Log("[UI Click] Hiçbir UI elementine tıklanmadı!");
            }
        }
    }

    public void ToggleInventory()
    {
        if (isOpen)
            CloseInventory();
        else
            OpenInventory();
    }

    public void OpenInventory()
    {
        // UI'ın var olduğundan emin ol
        EnsureUIExists();
        
        isOpen = true;
        selectedPotionType = null; // Pot seçimini sıfırla
        
        // Rename paneli varsa kapat
        if (renamePanel != null)
        {
            renamePanel.SetActive(false);
        }
        
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(true);
        }
        if (openButton != null)
        {
            openButton.gameObject.SetActive(false);
        }
        RefreshSlots();
        UpdatePotionCounts();
        UpdatePotionButtonColors();
        
        Debug.Log($"Envanter açıldı! selectedPotionType: {selectedPotionType}");
    }

    public void CloseInventory()
    {
        isOpen = false;
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }
        if (openButton != null)
        {
            openButton.gameObject.SetActive(true);
        }
    }

    public void RefreshSlots()
    {
        // slotContainer kontrolü
        if (slotContainer == null)
        {
            Debug.LogError("RefreshSlots: slotContainer hâlâ null! UI yeniden oluşturuluyor...");
            CreateUI();
            if (slotContainer == null)
            {
                Debug.LogError("slotContainer oluşturulamadı!");
                return;
            }
        }
        
        // Eski slotları temizle
        foreach (var slot in slots)
        {
            if (slot != null) Destroy(slot);
        }
        slots.Clear();
        
        if (PokemonBag.Instance == null)
        {
            Debug.LogWarning("PokemonBag.Instance bulunamadı!");
            return;
        }
        
        var pokemonList = PokemonBag.Instance.CaughtPokemon;
        
        if (titleText != null)
        {
            titleText.text = $"Pokemonlarım ({pokemonList.Count})";
        }
        
        Debug.Log($"RefreshSlots: {pokemonList.Count} Pokemon, slotContainer: {slotContainer.name}");
        
        for (int i = 0; i < pokemonList.Count; i++)
        {
            CreateSlot(pokemonList[i], i);
        }
        
        // Pokemon yoksa bilgi göster
        if (pokemonList.Count == 0)
        {
            CreateEmptyMessage();
        }
        
        Debug.Log($"RefreshSlots tamamlandı. {slots.Count} slot oluşturuldu.");
        
        // Layout'u bir sonraki frame'de yeniden hesapla
        StartCoroutine(RebuildLayoutNextFrame());
    }
    
    IEnumerator RebuildLayoutNextFrame()
    {
        yield return null; // Bir frame bekle
        
        if (slotContainer != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotContainer.GetComponent<RectTransform>());
            
            // Parent'ları da rebuild et
            Transform parent = slotContainer.parent;
            while (parent != null)
            {
                RectTransform parentRt = parent.GetComponent<RectTransform>();
                if (parentRt != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
                }
                parent = parent.parent;
            }
        }
        
        Debug.Log("Layout rebuild tamamlandı.");
    }

    void CreateSlot(PokemonData pokemon, int index)
    {
        if (slotContainer == null)
        {
            Debug.LogError("slotContainer null!");
            return;
        }
        
        // Basit slot objesi
        GameObject slot = new GameObject($"Slot_{index}");
        slot.transform.SetParent(slotContainer, false);
        
        // Image ÖNCE eklenmeli
        Image bgImage = slot.AddComponent<Image>();
        bgImage.color = pokemon.IsFainted ? faintedSlotColor : normalSlotColor;
        bgImage.raycastTarget = true;
        
        // RectTransform - açıkça boyut ver
        RectTransform rt = slot.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        
        // LayoutElement - ZORUNLU
        LayoutElement le = slot.AddComponent<LayoutElement>();
        le.minHeight = 80;
        le.preferredHeight = 80;
        
        // Tek bir text ile tüm bilgiyi göster
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(slot.transform, false);
        
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        string status = pokemon.IsFainted ? " <color=red>(Bayılmış)</color>" : "";
        text.text = $"<b>{pokemon.pokemonName}</b>{status}\n<size=16>Lv.{pokemon.level}  HP:{pokemon.currentHealth}/{pokemon.Health}  ATK:{pokemon.Attack}</size>";
        text.fontSize = 20;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Left;
        text.raycastTarget = false;
        text.margin = new Vector4(15, 10, 15, 10);
        
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        
        // Seç butonu
        Button btn = slot.AddComponent<Button>();
        btn.targetGraphic = bgImage;
        
        int idx = index;
        btn.onClick.AddListener(() => OnSlotClicked(idx));
        
        // İsim Değiştir butonu
        GameObject renameBtn = new GameObject("RenameBtn");
        renameBtn.transform.SetParent(slot.transform, false);
        Image renameBg = renameBtn.AddComponent<Image>();
        renameBg.color = new Color(0.4f, 0.4f, 0.5f, 1f);
        renameBg.raycastTarget = true;
        
        RectTransform renameRect = renameBtn.GetComponent<RectTransform>();
        renameRect.anchorMin = new Vector2(1, 0.5f);
        renameRect.anchorMax = new Vector2(1, 0.5f);
        renameRect.pivot = new Vector2(1, 0.5f);
        renameRect.anchoredPosition = new Vector2(-10, 0);
        renameRect.sizeDelta = new Vector2(60, 35);
        
        GameObject renameTxtObj = new GameObject("Text");
        renameTxtObj.transform.SetParent(renameBtn.transform, false);
        TextMeshProUGUI renameTxt = renameTxtObj.AddComponent<TextMeshProUGUI>();
        renameTxt.text = "AD";
        renameTxt.fontSize = 20;
        renameTxt.alignment = TextAlignmentOptions.Center;
        renameTxt.raycastTarget = false;
        RectTransform renameTxtRect = renameTxtObj.GetComponent<RectTransform>();
        renameTxtRect.anchorMin = Vector2.zero;
        renameTxtRect.anchorMax = Vector2.one;
        renameTxtRect.offsetMin = Vector2.zero;
        renameTxtRect.offsetMax = Vector2.zero;
        
        Button renameButton = renameBtn.AddComponent<Button>();
        renameButton.targetGraphic = renameBg;
        renameButton.onClick.AddListener(() => StartRename(idx));
        
        slots.Add(slot);
        Debug.Log($"Slot #{index}: {pokemon.pokemonName}");
    }
    
    // İsim değiştirme paneli referansları
    private GameObject renamePanel;
    private TMP_InputField renameInputField;
    private int currentRenameIndex = -1;
    
    // İsim değiştirme başlat
    void StartRename(int index)
    {
        if (PokemonBag.Instance == null) return;
        PokemonData pokemon = PokemonBag.Instance.GetPokemonAt(index);
        if (pokemon == null) return;
        
        currentRenameIndex = index;
        ShowRenameDialog(pokemon.pokemonName);
    }
    
    void ShowRenameDialog(string currentName)
    {
        // Eğer panel zaten varsa göster
        if (renamePanel != null)
        {
            renamePanel.SetActive(true);
            renameInputField.text = currentName;
            renameInputField.Select();
            renameInputField.ActivateInputField();
            return;
        }
        
        // Yeni panel oluştur
        renamePanel = new GameObject("RenamePanel");
        renamePanel.transform.SetParent(mainCanvas.transform, false);
        
        // Arka plan overlay
        Image overlay = renamePanel.AddComponent<Image>();
        overlay.color = new Color(0, 0, 0, 0.8f);
        RectTransform overlayRect = renamePanel.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;
        
        // Dialog kutusu
        GameObject dialog = new GameObject("Dialog");
        dialog.transform.SetParent(renamePanel.transform, false);
        Image dialogBg = dialog.AddComponent<Image>();
        dialogBg.color = new Color(0.15f, 0.15f, 0.2f, 1f);
        RectTransform dialogRect = dialog.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.1f, 0.35f);
        dialogRect.anchorMax = new Vector2(0.9f, 0.65f);
        dialogRect.sizeDelta = Vector2.zero;
        
        // Başlık
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(dialog.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "İsim Değiştir";
        titleText.fontSize = 28;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.7f);
        titleRect.anchorMax = new Vector2(1, 0.95f);
        titleRect.sizeDelta = Vector2.zero;
        
        // Input Field
        GameObject inputObj = new GameObject("InputField");
        inputObj.transform.SetParent(dialog.transform, false);
        Image inputBg = inputObj.AddComponent<Image>();
        inputBg.color = new Color(0.25f, 0.25f, 0.3f, 1f);
        RectTransform inputRect = inputObj.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.05f, 0.4f);
        inputRect.anchorMax = new Vector2(0.95f, 0.65f);
        inputRect.sizeDelta = Vector2.zero;
        
        // Text Area
        GameObject textArea = new GameObject("TextArea");
        textArea.transform.SetParent(inputObj.transform, false);
        RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
        textAreaRect.anchorMin = new Vector2(0.02f, 0);
        textAreaRect.anchorMax = new Vector2(0.98f, 1);
        textAreaRect.sizeDelta = Vector2.zero;
        
        // Input Text
        GameObject inputTextObj = new GameObject("Text");
        inputTextObj.transform.SetParent(textArea.transform, false);
        TextMeshProUGUI inputText = inputTextObj.AddComponent<TextMeshProUGUI>();
        inputText.fontSize = 24;
        inputText.alignment = TextAlignmentOptions.Left;
        inputText.color = Color.white;
        RectTransform inputTextRect = inputTextObj.GetComponent<RectTransform>();
        inputTextRect.anchorMin = Vector2.zero;
        inputTextRect.anchorMax = Vector2.one;
        inputTextRect.sizeDelta = Vector2.zero;
        
        // TMP Input Field
        renameInputField = inputObj.AddComponent<TMP_InputField>();
        renameInputField.textComponent = inputText;
        renameInputField.textViewport = textAreaRect;
        renameInputField.text = currentName;
        renameInputField.characterLimit = 20;
        
        // Kaydet butonu
        GameObject saveBtn = new GameObject("SaveBtn");
        saveBtn.transform.SetParent(dialog.transform, false);
        Image saveBg = saveBtn.AddComponent<Image>();
        saveBg.color = new Color(0.2f, 0.65f, 0.35f, 1f);
        Button saveButton = saveBtn.AddComponent<Button>();
        saveButton.targetGraphic = saveBg;
        saveButton.onClick.AddListener(ConfirmRename);
        RectTransform saveRect = saveBtn.GetComponent<RectTransform>();
        saveRect.anchorMin = new Vector2(0.55f, 0.08f);
        saveRect.anchorMax = new Vector2(0.95f, 0.3f);
        saveRect.sizeDelta = Vector2.zero;
        
        GameObject saveTxtObj = new GameObject("Text");
        saveTxtObj.transform.SetParent(saveBtn.transform, false);
        TextMeshProUGUI saveTxt = saveTxtObj.AddComponent<TextMeshProUGUI>();
        saveTxt.text = "KAYDET";
        saveTxt.fontSize = 22;
        saveTxt.fontStyle = FontStyles.Bold;
        saveTxt.alignment = TextAlignmentOptions.Center;
        saveTxt.color = Color.white;
        RectTransform saveTxtRect = saveTxtObj.GetComponent<RectTransform>();
        saveTxtRect.anchorMin = Vector2.zero;
        saveTxtRect.anchorMax = Vector2.one;
        saveTxtRect.sizeDelta = Vector2.zero;
        
        // İptal butonu
        GameObject cancelBtn = new GameObject("CancelBtn");
        cancelBtn.transform.SetParent(dialog.transform, false);
        Image cancelBg = cancelBtn.AddComponent<Image>();
        cancelBg.color = new Color(0.65f, 0.25f, 0.25f, 1f);
        Button cancelButton = cancelBtn.AddComponent<Button>();
        cancelButton.targetGraphic = cancelBg;
        cancelButton.onClick.AddListener(CancelRename);
        RectTransform cancelRect = cancelBtn.GetComponent<RectTransform>();
        cancelRect.anchorMin = new Vector2(0.05f, 0.08f);
        cancelRect.anchorMax = new Vector2(0.45f, 0.3f);
        cancelRect.sizeDelta = Vector2.zero;
        
        GameObject cancelTxtObj = new GameObject("Text");
        cancelTxtObj.transform.SetParent(cancelBtn.transform, false);
        TextMeshProUGUI cancelTxt = cancelTxtObj.AddComponent<TextMeshProUGUI>();
        cancelTxt.text = "İPTAL";
        cancelTxt.fontSize = 22;
        cancelTxt.fontStyle = FontStyles.Bold;
        cancelTxt.alignment = TextAlignmentOptions.Center;
        cancelTxt.color = Color.white;
        RectTransform cancelTxtRect = cancelTxtObj.GetComponent<RectTransform>();
        cancelTxtRect.anchorMin = Vector2.zero;
        cancelTxtRect.anchorMax = Vector2.one;
        cancelTxtRect.sizeDelta = Vector2.zero;
        
        // Input field'ı aktif et
        renameInputField.Select();
        renameInputField.ActivateInputField();
    }
    
    void ConfirmRename()
    {
        if (currentRenameIndex >= 0 && renameInputField != null && !string.IsNullOrEmpty(renameInputField.text))
        {
            RenamePokemon(currentRenameIndex, renameInputField.text);
        }
        HideRenameDialog();
    }
    
    void CancelRename()
    {
        HideRenameDialog();
    }
    
    void HideRenameDialog()
    {
        if (renamePanel != null)
        {
            renamePanel.SetActive(false);
        }
        currentRenameIndex = -1;
    }
    
    void RenamePokemon(int index, string newName)
    {
        if (PokemonBag.Instance == null) return;
        PokemonData pokemon = PokemonBag.Instance.GetPokemonAt(index);
        if (pokemon == null) return;
        
        string oldName = pokemon.pokemonName;
        pokemon.pokemonName = newName;
        PokemonBag.Instance.SaveInventory();
        
        Debug.Log($"Pokemon ismi değiştirildi: {oldName} -> {newName}");
        RefreshSlots();
    }

    void OnSlotClicked(int index)
    {
        Debug.Log($"OnSlotClicked: index={index}, selectedPotionType={selectedPotionType}");
        
        if (PokemonBag.Instance == null) return;
        
        PokemonData pokemon = PokemonBag.Instance.GetPokemonAt(index);
        if (pokemon == null) return;
        
        // Eğer pot seçiliyse, pot kullan
        if (selectedPotionType != null)
        {
            Debug.Log($"Pot seçili, kullanılıyor...");
            UsePotionOnPokemon(index);
            return;
        }
        
        // Detay sayfasını aç
        if (PokemonDetailUI.Instance != null)
        {
            PokemonDetailUI.Instance.Open(index);
        }
        else
        {
            Debug.LogWarning("PokemonDetailUI.Instance bulunamadı!");
            // Fallback: Eski davranış
            if (pokemon.IsFainted)
            {
                Debug.Log($"{pokemon.pokemonName} bayılmış durumda! Revive kullan!");
                return;
            }
            
            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.SummonPokemon(index);
            }
            CloseInventory();
        }
    }

    void CreateEmptyMessage()
    {
        if (slotContainer == null) return;
        
        GameObject msgObj = new GameObject("EmptyMessage");
        msgObj.transform.SetParent(slotContainer, false);
        
        TextMeshProUGUI msgText = msgObj.AddComponent<TextMeshProUGUI>();
        msgText.text = "Henüz Pokemon yakalamamışsın!\nVahşi Pokemon'lara pokeball at!";
        msgText.fontSize = 22;
        msgText.color = new Color(0.7f, 0.7f, 0.7f);
        msgText.alignment = TextAlignmentOptions.Center;
        
        LayoutElement layout = msgObj.AddComponent<LayoutElement>();
        layout.preferredHeight = 100;
        layout.flexibleWidth = 1;
        
        slots.Add(msgObj);
    }

    void CreateUI()
    {
        // Eğer zaten oluşturulmuşsa, eski UI'ı temizle
        if (mainCanvas != null)
        {
            Destroy(mainCanvas.gameObject);
        }
        
        // Ana Canvas (her zaman görünür)
        GameObject canvasObj = new GameObject("PokemonInventoryCanvas");
        canvasObj.transform.SetParent(transform);
        
        mainCanvas = canvasObj.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mainCanvas.sortingOrder = 100;
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // ============ ENVANTER PANELİ ============
        inventoryPanel = new GameObject("InventoryPanel");
        inventoryPanel.transform.SetParent(canvasObj.transform, false);
        
        // Arka plan overlay
        Image overlayImage = inventoryPanel.AddComponent<Image>();
        overlayImage.color = new Color(0, 0, 0, 0.85f);
        RectTransform overlayRect = inventoryPanel.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;
        
        // İçerik paneli
        GameObject contentPanel = new GameObject("ContentPanel");
        contentPanel.transform.SetParent(inventoryPanel.transform, false);
        Image contentBg = contentPanel.AddComponent<Image>();
        contentBg.color = new Color(0.12f, 0.12f, 0.18f, 0.98f);
        RectTransform contentRect = contentPanel.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.05f, 0.08f);
        contentRect.anchorMax = new Vector2(0.95f, 0.92f);
        contentRect.sizeDelta = Vector2.zero;
        
        // Başlık
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(contentPanel.transform, false);
        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Pokemonlarım";
        titleText.fontSize = 32;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(0, -15);
        titleRect.sizeDelta = new Vector2(0, 50);
        
        // SCROLL VIEW - Düzgün scroll için
        GameObject scrollView = new GameObject("ScrollView");
        scrollView.transform.SetParent(contentPanel.transform, false);
        RectTransform scrollViewRect = scrollView.AddComponent<RectTransform>();
        scrollViewRect.anchorMin = new Vector2(0.02f, 0.12f);
        scrollViewRect.anchorMax = new Vector2(0.98f, 0.88f);
        scrollViewRect.offsetMin = Vector2.zero;
        scrollViewRect.offsetMax = new Vector2(0, -55);
        
        Image scrollBg = scrollView.AddComponent<Image>();
        scrollBg.color = new Color(0.06f, 0.06f, 0.1f, 0.7f);
        
        ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.1f;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.135f;
        scrollRect.scrollSensitivity = 20f;
        
        // Viewport
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollView.transform, false);
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewportRect.pivot = new Vector2(0, 1);
        
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1, 1, 1, 0.01f);
        Mask viewportMask = viewport.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;
        
        // Content (Slot Container)
        GameObject content = new GameObject("SlotContainer");
        content.transform.SetParent(viewport.transform, false);
        
        RectTransform contentRectT = content.AddComponent<RectTransform>();
        contentRectT.anchorMin = new Vector2(0, 1);
        contentRectT.anchorMax = new Vector2(1, 1);
        contentRectT.pivot = new Vector2(0.5f, 1);
        contentRectT.anchoredPosition = Vector2.zero;
        contentRectT.sizeDelta = new Vector2(0, 0);
        
        slotContainer = content.transform;
        Debug.Log($"slotContainer atandı: {slotContainer.name}");
        
        // Vertical Layout Group
        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 8;
        contentLayout.padding = new RectOffset(10, 10, 10, 10);
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        
        // Content Size Fitter
        ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        // ScrollRect referanslarını ayarla
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRectT;
        
        // ============ POT PANELİ ============
        CreatePotionPanel(contentPanel);
        
        // Kapat butonu
        GameObject closeObj = new GameObject("CloseButton");
        closeObj.transform.SetParent(contentPanel.transform, false);
        Image closeBg = closeObj.AddComponent<Image>();
        closeBg.color = new Color(0.75f, 0.25f, 0.25f, 1f);
        closeButton = closeObj.AddComponent<Button>();
        closeButton.targetGraphic = closeBg;
        closeButton.onClick.AddListener(CloseInventory);
        
        RectTransform closeRect = closeObj.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.1f, 0);
        closeRect.anchorMax = new Vector2(0.9f, 0.1f);
        closeRect.offsetMin = new Vector2(0, 15);
        closeRect.offsetMax = new Vector2(0, -5);
        
        GameObject closeTextObj = new GameObject("Text");
        closeTextObj.transform.SetParent(closeObj.transform, false);
        TextMeshProUGUI closeText = closeTextObj.AddComponent<TextMeshProUGUI>();
        closeText.text = "KAPAT";
        closeText.fontSize = 26;
        closeText.fontStyle = FontStyles.Bold;
        closeText.alignment = TextAlignmentOptions.Center;
        closeText.color = Color.white;
        RectTransform closeTextRect = closeTextObj.GetComponent<RectTransform>();
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.sizeDelta = Vector2.zero;
        
        // ============ ÇANTA BUTONU (her zaman görünür) ============
        GameObject openObj = new GameObject("OpenButton");
        openObj.transform.SetParent(canvasObj.transform, false);
        Image openBg = openObj.AddComponent<Image>();
        openBg.color = new Color(0.2f, 0.45f, 0.75f, 0.95f);
        openButton = openObj.AddComponent<Button>();
        openButton.targetGraphic = openBg;
        openButton.onClick.AddListener(OpenInventory);
        
        RectTransform openRect = openObj.GetComponent<RectTransform>();
        openRect.anchorMin = new Vector2(1, 0);
        openRect.anchorMax = new Vector2(1, 0);
        openRect.pivot = new Vector2(1, 0);
        openRect.anchoredPosition = new Vector2(-20, 20);
        openRect.sizeDelta = new Vector2(140, 55);
        
        GameObject openTextObj = new GameObject("Text");
        openTextObj.transform.SetParent(openObj.transform, false);
        TextMeshProUGUI openText = openTextObj.AddComponent<TextMeshProUGUI>();
        openText.text = "ÇANTA";
        openText.fontSize = 22;
        openText.fontStyle = FontStyles.Bold;
        openText.alignment = TextAlignmentOptions.Center;
        openText.color = Color.white;
        RectTransform openTextRect = openTextObj.GetComponent<RectTransform>();
        openTextRect.anchorMin = Vector2.zero;
        openTextRect.anchorMax = Vector2.one;
        openTextRect.sizeDelta = Vector2.zero;
        
        Debug.Log("PokemonInventoryUI oluşturuldu");
    }
    
    void CreatePotionPanel(GameObject parent)
    {
        // Pot paneli - scroll area ile kapat butonu arasında
        potionPanel = new GameObject("PotionPanel");
        potionPanel.transform.SetParent(parent.transform, false);
        Image potionBg = potionPanel.AddComponent<Image>();
        potionBg.color = new Color(0.18f, 0.22f, 0.28f, 0.95f);
        
        RectTransform potionRect = potionPanel.GetComponent<RectTransform>();
        potionRect.anchorMin = new Vector2(0.02f, 0.11f);
        potionRect.anchorMax = new Vector2(0.98f, 0.22f);
        potionRect.offsetMin = Vector2.zero;
        potionRect.offsetMax = Vector2.zero;
        
        // Başlık
        GameObject potTitleObj = new GameObject("PotTitle");
        potTitleObj.transform.SetParent(potionPanel.transform, false);
        TextMeshProUGUI potTitle = potTitleObj.AddComponent<TextMeshProUGUI>();
        potTitle.text = "[+] IYILESTIRME";
        potTitle.fontSize = 16;
        potTitle.fontStyle = FontStyles.Bold;
        potTitle.alignment = TextAlignmentOptions.Center;
        potTitle.color = new Color(0.7f, 0.9f, 0.7f);
        RectTransform potTitleRect = potTitleObj.GetComponent<RectTransform>();
        potTitleRect.anchorMin = new Vector2(0, 0.7f);
        potTitleRect.anchorMax = new Vector2(1, 1f);
        potTitleRect.sizeDelta = Vector2.zero;
        
        // Pot butonları container
        GameObject potButtons = new GameObject("PotButtons");
        potButtons.transform.SetParent(potionPanel.transform, false);
        RectTransform potBtnRect = potButtons.GetComponent<RectTransform>();
        if (potBtnRect == null) potBtnRect = potButtons.AddComponent<RectTransform>();
        potBtnRect.anchorMin = new Vector2(0.02f, 0.05f);
        potBtnRect.anchorMax = new Vector2(0.98f, 0.7f);
        potBtnRect.sizeDelta = Vector2.zero;
        
        HorizontalLayoutGroup potLayout = potButtons.AddComponent<HorizontalLayoutGroup>();
        potLayout.spacing = 8;
        potLayout.padding = new RectOffset(5, 5, 0, 0);
        potLayout.childAlignment = TextAnchor.MiddleCenter;
        potLayout.childControlWidth = true;
        potLayout.childControlHeight = true;
        potLayout.childForceExpandWidth = true;
        potLayout.childForceExpandHeight = true;
        
        // Küçük Pot butonu
        smallPotionText = CreatePotionButton(potButtons.transform, "Kucuk\n+20 HP", 
            new Color(0.3f, 0.7f, 0.4f), () => SelectPotion(PotionType.SmallPotion));
        
        // Süper Pot butonu
        superPotionText = CreatePotionButton(potButtons.transform, "Super\n+50 HP", 
            new Color(0.3f, 0.5f, 0.8f), () => SelectPotion(PotionType.SuperPotion));
        
        // Hyper Pot butonu
        hyperPotionText = CreatePotionButton(potButtons.transform, "Hyper\nFull HP", 
            new Color(0.7f, 0.4f, 0.8f), () => SelectPotion(PotionType.HyperPotion));
        
        // Revive butonu
        reviveText = CreatePotionButton(potButtons.transform, "Revive\nDirilt", 
            new Color(0.8f, 0.6f, 0.2f), () => SelectPotion(PotionType.Revive));
    }
    
    TextMeshProUGUI CreatePotionButton(Transform parent, string text, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject("PotionBtn");
        btnObj.transform.SetParent(parent, false);
        
        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = bgColor;
        
        LayoutElement layout = btnObj.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1;
        layout.minHeight = 50;
        
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnBg;
        btn.onClick.AddListener(onClick);
        
        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        txt.text = text;
        txt.fontSize = 12;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;
        
        return txt;
    }
    
    void SelectPotion(PotionType type)
    {
        if (PokemonBag.Instance == null) return;
        
        // Aynı pot tekrar seçilirse iptal et
        if (selectedPotionType == type)
        {
            selectedPotionType = null;
            Debug.Log($"{type} seçimi iptal edildi.");
            UpdatePotionButtonColors();
            return;
        }
        
        int count = PokemonBag.Instance.GetPotionCount(type);
        if (count <= 0)
        {
            Debug.Log($"{type} kalmadı!");
            return;
        }
        
        selectedPotionType = type;
        Debug.Log($"{type} seçildi (x{count}). Şimdi bir Pokemon'a tıkla!");
        
        // Buton renklerini güncelle
        UpdatePotionButtonColors();
    }
    
    void UpdatePotionButtonColors()
    {
        // Seçili potu vurgula
        if (smallPotionText != null)
        {
            Image bg = smallPotionText.transform.parent.GetComponent<Image>();
            if (bg != null) bg.color = selectedPotionType == PotionType.SmallPotion 
                ? new Color(0.5f, 1f, 0.6f) : new Color(0.3f, 0.7f, 0.4f);
        }
        if (superPotionText != null)
        {
            Image bg = superPotionText.transform.parent.GetComponent<Image>();
            if (bg != null) bg.color = selectedPotionType == PotionType.SuperPotion 
                ? new Color(0.5f, 0.7f, 1f) : new Color(0.3f, 0.5f, 0.8f);
        }
        if (hyperPotionText != null)
        {
            Image bg = hyperPotionText.transform.parent.GetComponent<Image>();
            if (bg != null) bg.color = selectedPotionType == PotionType.HyperPotion 
                ? new Color(0.9f, 0.6f, 1f) : new Color(0.7f, 0.4f, 0.8f);
        }
        if (reviveText != null)
        {
            Image bg = reviveText.transform.parent.GetComponent<Image>();
            if (bg != null) bg.color = selectedPotionType == PotionType.Revive 
                ? new Color(1f, 0.8f, 0.4f) : new Color(0.8f, 0.6f, 0.2f);
        }
    }
    
    void UsePotionOnPokemon(int pokemonIndex)
    {
        Debug.Log($"UsePotionOnPokemon çağrıldı: index={pokemonIndex}, potionType={selectedPotionType}");
        
        if (selectedPotionType == null || PokemonBag.Instance == null)
        {
            Debug.Log("selectedPotionType veya PokemonBag null!");
            return;
        }
        
        PokemonData pokemon = PokemonBag.Instance.GetPokemonAt(pokemonIndex);
        Debug.Log($"Pokemon: {pokemon?.pokemonName}, IsFainted: {pokemon?.IsFainted}, HP: {pokemon?.currentHealth}/{pokemon?.Health}");
        
        bool success = PokemonBag.Instance.UsePotion(selectedPotionType.Value, pokemonIndex);
        Debug.Log($"UsePotion sonucu: {success}");
        
        if (success)
        {
            UpdatePotionCounts();
            RefreshSlots();
        }
        
        // Seçimi temizle
        selectedPotionType = null;
    }
    
    void UpdatePotionCounts()
    {
        if (PokemonBag.Instance == null) return;
        
        if (smallPotionText != null)
            smallPotionText.text = $"Kucuk\n+20 HP\nx{PokemonBag.Instance.GetPotionCount(PotionType.SmallPotion)}";
        
        if (superPotionText != null)
            superPotionText.text = $"Super\n+50 HP\nx{PokemonBag.Instance.GetPotionCount(PotionType.SuperPotion)}";
        
        if (hyperPotionText != null)
            hyperPotionText.text = $"Hyper\nFull HP\nx{PokemonBag.Instance.GetPotionCount(PotionType.HyperPotion)}";
        
        if (reviveText != null)
            reviveText.text = $"Revive\nDirilt\nx{PokemonBag.Instance.GetPotionCount(PotionType.Revive)}";
    }
}
