using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARMON.UI.AR
{
    /// <summary>
    /// World-anchored Pokemon inventory. Spawned via <see cref="Open(Camera)"/>;
    /// destroys itself on <see cref="Close"/>.
    /// Data source is the global <see cref="PokemonBag"/>; this panel only renders.
    /// </summary>
    public class ARInventoryPanel : ARWorldPanel
    {
        public static ARInventoryPanel Current { get; private set; }

        Transform slotContainer;
        TextMeshProUGUI titleText;
        readonly List<GameObject> slotObjects = new List<GameObject>();
        ARDetailSubPanel currentDetail;

        GameObject pokemonTabRoot;
        GameObject questTabRoot;
        ARMON.UI.AR.Quest.ARQuestListPanel questListPanel;
        Button pokeTabBtn, questTabBtn;

        public static ARInventoryPanel Open(Camera cam)
        {
            if (Current != null) return Current;
            ARHudCanvas.EnsureEventSystem();
            var panel = ARWorldPanel.Spawn<ARInventoryPanel>("ARInventoryPanel");
            panel.OpenAtUserFront(cam, distance: 1.5f, heightOffset: -0.1f);
            Current = panel;
            return panel;
        }

        public override void Close()
        {
            if (currentDetail != null) currentDetail.Close();
            if (Current == this) Current = null;
            base.Close();
        }

        protected override void BuildContent()
        {
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.14f, 0.94f);

            // === Tab strip (top 10%) ===
            GameObject tabStrip = new GameObject("TabStrip", typeof(RectTransform));
            tabStrip.transform.SetParent(transform, false);
            var tsrt = (RectTransform)tabStrip.transform;
            tsrt.anchorMin = new Vector2(0f, 0.90f);
            tsrt.anchorMax = new Vector2(1f, 1f);
            tsrt.offsetMin = Vector2.zero; tsrt.offsetMax = Vector2.zero;

            pokeTabBtn  = CreateTabButton(tabStrip.transform, "POKEMON",  new Vector2(0.02f, 0.05f), new Vector2(0.49f, 0.95f), () => ShowTab(true));
            questTabBtn = CreateTabButton(tabStrip.transform, "GÖREVLER", new Vector2(0.51f, 0.05f), new Vector2(0.98f, 0.95f), () => ShowTab(false));

            // === Pokemon tab root ===
            pokemonTabRoot = new GameObject("PokemonTab", typeof(RectTransform));
            pokemonTabRoot.transform.SetParent(transform, false);
            var prt = (RectTransform)pokemonTabRoot.transform;
            prt.anchorMin = new Vector2(0f, 0.07f); prt.anchorMax = new Vector2(1f, 0.90f);
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;

            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(pokemonTabRoot.transform, false);
            titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "Pokemonlarım";
            titleText.fontSize = 30; titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center; titleText.color = Color.white;
            titleText.raycastTarget = false;
            var trt = (RectTransform)titleObj.transform;
            trt.anchorMin = new Vector2(0f, 0.90f); trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            GameObject scroll = new GameObject("Scroll", typeof(RectTransform));
            scroll.transform.SetParent(pokemonTabRoot.transform, false);
            var scrt = (RectTransform)scroll.transform;
            scrt.anchorMin = new Vector2(0.02f, 0.02f);
            scrt.anchorMax = new Vector2(0.98f, 0.90f);
            scrt.offsetMin = Vector2.zero; scrt.offsetMax = Vector2.zero;
            var scrollImg = scroll.AddComponent<Image>();
            scrollImg.color = new Color(0f, 0f, 0f, 0.3f);
            var sr = scroll.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(scroll.transform, false);
            var vp = (RectTransform)viewport.transform;
            vp.anchorMin = Vector2.zero; vp.anchorMax = Vector2.one;
            vp.offsetMin = Vector2.zero; vp.offsetMax = Vector2.zero;
            var vpImg = viewport.AddComponent<Image>(); vpImg.color = new Color(1, 1, 1, 0.01f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var crt = (RectTransform)content.transform;
            crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = Vector2.zero; crt.sizeDelta = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6; vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false; vlg.childForceExpandHeight = false;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = vp; sr.content = crt;
            slotContainer = content.transform;

            // === Quest tab root ===
            questTabRoot = new GameObject("QuestTab", typeof(RectTransform));
            questTabRoot.transform.SetParent(transform, false);
            var qrt = (RectTransform)questTabRoot.transform;
            qrt.anchorMin = new Vector2(0f, 0.07f); qrt.anchorMax = new Vector2(1f, 0.90f);
            qrt.offsetMin = Vector2.zero; qrt.offsetMax = Vector2.zero;
            var qListGo = new GameObject("QuestList");
            qListGo.transform.SetParent(questTabRoot.transform, false);
            questListPanel = qListGo.AddComponent<ARMON.UI.AR.Quest.ARQuestListPanel>();
            questListPanel.Init((RectTransform)questTabRoot.transform);

            // === Close button (root, bottom strip) ===
            GameObject closeBtn = new GameObject("CloseBtn");
            closeBtn.transform.SetParent(transform, false);
            var cbImg = closeBtn.AddComponent<Image>();
            cbImg.color = new Color(0.75f, 0.25f, 0.25f, 1f);
            var cbBtn = closeBtn.AddComponent<Button>();
            cbBtn.targetGraphic = cbImg;
            cbBtn.onClick.AddListener(Close);
            var cbrt = (RectTransform)closeBtn.transform;
            cbrt.anchorMin = new Vector2(0.35f, 0.005f);
            cbrt.anchorMax = new Vector2(0.65f, 0.06f);
            cbrt.offsetMin = Vector2.zero; cbrt.offsetMax = Vector2.zero;

            GameObject cbTxt = new GameObject("Text");
            cbTxt.transform.SetParent(closeBtn.transform, false);
            var ct = cbTxt.AddComponent<TextMeshProUGUI>();
            ct.text = "KAPAT"; ct.fontSize = 26; ct.fontStyle = FontStyles.Bold;
            ct.alignment = TextAlignmentOptions.Center; ct.color = Color.white;
            ct.raycastTarget = false;
            var ctrt = (RectTransform)cbTxt.transform;
            ctrt.anchorMin = Vector2.zero; ctrt.anchorMax = Vector2.one;
            ctrt.offsetMin = Vector2.zero; ctrt.offsetMax = Vector2.zero;

            ShowTab(true);
            RefreshSlots();
        }

        Button CreateTabButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject($"Tab_{label}");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.18f, 0.28f, 1f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            GameObject txt = new GameObject("Text");
            txt.transform.SetParent(go.transform, false);
            var t = txt.AddComponent<TextMeshProUGUI>();
            t.text = label; t.fontSize = 26; t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center; t.color = Color.white;
            t.raycastTarget = false;
            var trt = (RectTransform)txt.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            return btn;
        }

        void ShowTab(bool pokemon)
        {
            if (pokemonTabRoot != null) pokemonTabRoot.SetActive(pokemon);
            if (questTabRoot != null)   questTabRoot.SetActive(!pokemon);
            if (pokeTabBtn != null)
                pokeTabBtn.GetComponent<Image>().color = pokemon
                    ? new Color(0.32f, 0.32f, 0.55f, 1f)
                    : new Color(0.18f, 0.18f, 0.28f, 1f);
            if (questTabBtn != null)
                questTabBtn.GetComponent<Image>().color = !pokemon
                    ? new Color(0.32f, 0.32f, 0.55f, 1f)
                    : new Color(0.18f, 0.18f, 0.28f, 1f);
            if (!pokemon && questListPanel != null) questListPanel.Refresh();
        }

        public void RefreshSlots()
        {
            foreach (var s in slotObjects) if (s != null) Destroy(s);
            slotObjects.Clear();

            if (PokemonBag.Instance == null) return;
            var list = PokemonBag.Instance.CaughtPokemon;
            if (titleText != null) titleText.text = $"Pokemonlarım ({list.Count})";

            for (int i = 0; i < list.Count; i++) CreateSlot(list[i], i);
            if (list.Count == 0) CreateEmptyMessage();
        }

        void CreateSlot(PokemonData pokemon, int index)
        {
            GameObject slot = new GameObject($"Slot_{index}");
            slot.transform.SetParent(slotContainer, false);
            var img = slot.AddComponent<Image>();
            img.color = pokemon.IsFainted
                ? new Color(0.5f, 0.25f, 0.25f, 1f)
                : new Color(0.25f, 0.25f, 0.35f, 1f);
            var le = slot.AddComponent<LayoutElement>();
            le.minHeight = 80; le.preferredHeight = 80;

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(slot.transform, false);
            var t = txtObj.AddComponent<TextMeshProUGUI>();
            string status = pokemon.IsFainted ? " <color=red>(Bayılmış)</color>" : "";
            t.text = $"<b>{pokemon.pokemonName}</b>{status}\n<size=18>Lv.{pokemon.level}  HP:{pokemon.currentHealth}/{pokemon.Health}</size>";
            t.fontSize = 22; t.color = Color.white;
            t.alignment = TextAlignmentOptions.Left;
            t.margin = new Vector4(12, 6, 12, 6);
            t.raycastTarget = false;
            var trt = (RectTransform)txtObj.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            var btn = slot.AddComponent<Button>();
            btn.targetGraphic = img;
            int idx = index;
            btn.onClick.AddListener(() => OnSlotClicked(idx));

            slotObjects.Add(slot);
        }

        void CreateEmptyMessage()
        {
            GameObject msg = new GameObject("Empty");
            msg.transform.SetParent(slotContainer, false);
            var t = msg.AddComponent<TextMeshProUGUI>();
            t.text = "Henüz Pokemon yakalamamışsın!";
            t.fontSize = 22; t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.7f, 0.7f, 0.7f);
            var le = msg.AddComponent<LayoutElement>();
            le.preferredHeight = 80;
            slotObjects.Add(msg);
        }

        void OnSlotClicked(int index)
        {
            if (currentDetail != null) currentDetail.Close();
            currentDetail = ARDetailSubPanel.OpenBeside(this, index);
        }
    }
}
