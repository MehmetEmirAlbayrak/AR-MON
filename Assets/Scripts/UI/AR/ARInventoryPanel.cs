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
        GameObject itemsTabRoot;
        GameObject questTabRoot;
        ARMON.UI.AR.Quest.ARQuestListPanel questListPanel;
        Button pokeTabBtn, itemsTabBtn, questTabBtn;
        TextMeshProUGUI pokeballCountText, greatBallText, ultraBallText, smallPotText, superPotText, hyperPotText, reviveText;

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

            pokeTabBtn  = CreateTabButton(tabStrip.transform, "POKEMON",  new Vector2(0.02f, 0.05f), new Vector2(0.32f, 0.95f), () => ShowTab(Tab.Pokemon));
            itemsTabBtn = CreateTabButton(tabStrip.transform, "EŞYALAR",  new Vector2(0.34f, 0.05f), new Vector2(0.66f, 0.95f), () => ShowTab(Tab.Items));
            questTabBtn = CreateTabButton(tabStrip.transform, "GÖREVLER", new Vector2(0.68f, 0.05f), new Vector2(0.98f, 0.95f), () => ShowTab(Tab.Quest));

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

            // === Items tab root ===
            itemsTabRoot = BuildItemsTab();

            // === Quest tab root ===
            questTabRoot = new GameObject("QuestTab", typeof(RectTransform));
            questTabRoot.transform.SetParent(transform, false);
            var qrt = (RectTransform)questTabRoot.transform;
            qrt.anchorMin = new Vector2(0f, 0.07f); qrt.anchorMax = new Vector2(1f, 0.90f);
            qrt.offsetMin = Vector2.zero; qrt.offsetMax = Vector2.zero;
            var qListGo = new GameObject("QuestList", typeof(RectTransform));
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

            ShowTab(Tab.Pokemon);
            RefreshSlots();
            RefreshItems();
        }

        enum Tab { Pokemon, Items, Quest }

        GameObject BuildItemsTab()
        {
            var root = new GameObject("ItemsTab", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = new Vector2(0f, 0.07f); rt.anchorMax = new Vector2(1f, 0.90f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var titleObj = new GameObject("Title", typeof(RectTransform));
            titleObj.transform.SetParent(root.transform, false);
            var tt = titleObj.AddComponent<TextMeshProUGUI>();
            tt.text = "Eşyalar"; tt.fontSize = 30; tt.fontStyle = FontStyles.Bold;
            tt.alignment = TextAlignmentOptions.Center; tt.color = Color.white;
            tt.raycastTarget = false;
            var ttrt = (RectTransform)titleObj.transform;
            ttrt.anchorMin = new Vector2(0f, 0.90f); ttrt.anchorMax = new Vector2(1f, 1f);
            ttrt.offsetMin = Vector2.zero; ttrt.offsetMax = Vector2.zero;

            // Scrollable container so future items can't push KAPAT out.
            var scrollGo = new GameObject("ItemScroll", typeof(RectTransform));
            scrollGo.transform.SetParent(root.transform, false);
            var srt = (RectTransform)scrollGo.transform;
            srt.anchorMin = new Vector2(0.02f, 0.08f); srt.anchorMax = new Vector2(0.98f, 0.88f);
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            var scrollBg = scrollGo.AddComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.25f);
            var sr = scrollGo.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true;

            var viewGo = new GameObject("Viewport", typeof(RectTransform));
            viewGo.transform.SetParent(scrollGo.transform, false);
            var vrt = (RectTransform)viewGo.transform;
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
            var vImg = viewGo.AddComponent<Image>(); vImg.color = new Color(1, 1, 1, 0.01f);
            viewGo.AddComponent<Mask>().showMaskGraphic = false;

            var list = new GameObject("Content", typeof(RectTransform));
            list.transform.SetParent(viewGo.transform, false);
            var lrt = (RectTransform)list.transform;
            lrt.anchorMin = new Vector2(0f, 1f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = Vector2.zero; lrt.sizeDelta = Vector2.zero;
            var vlg = list.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 5; vlg.padding = new RectOffset(6, 6, 4, 4);
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false; vlg.childForceExpandHeight = false;
            var fitter = list.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = vrt; sr.content = lrt;

            pokeballCountText = CreateItemRow(list.transform, "Pokéball",          new Color(0.85f, 0.25f, 0.25f, 1f),  () => OnBallRowClicked(BallType.Normal));
            greatBallText     = CreateItemRow(list.transform, "Great Ball",        new Color(0.20f, 0.45f, 0.80f, 1f),  () => OnBallRowClicked(BallType.Great));
            ultraBallText     = CreateItemRow(list.transform, "Ultra Ball",        new Color(0.55f, 0.20f, 0.65f, 1f),  () => OnBallRowClicked(BallType.Ultra));
            smallPotText      = CreateItemRow(list.transform, "Küçük Pot (+20 HP)",   new Color(0.3f, 0.6f, 0.4f, 1f),   () => OpenPokemonPicker(PotionType.SmallPotion));
            superPotText      = CreateItemRow(list.transform, "Süper Pot (+50 HP)",   new Color(0.2f, 0.55f, 0.7f, 1f),  () => OpenPokemonPicker(PotionType.SuperPotion));
            hyperPotText      = CreateItemRow(list.transform, "Hyper Pot (Full HP)",  new Color(0.55f, 0.3f, 0.7f, 1f),  () => OpenPokemonPicker(PotionType.HyperPotion));
            reviveText        = CreateItemRow(list.transform, "Revive",               new Color(0.8f, 0.7f, 0.2f, 1f),   () => OpenPokemonPicker(PotionType.Revive));
            return root;
        }

        TextMeshProUGUI CreateItemRow(Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var row = new GameObject($"Row_{label}", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var img = row.AddComponent<Image>();
            img.color = color;
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 56; le.preferredHeight = 56;

            if (onClick != null)
            {
                var btn = row.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(onClick);
            }

            var txt = new GameObject("Text", typeof(RectTransform));
            txt.transform.SetParent(row.transform, false);
            var t = txt.AddComponent<TextMeshProUGUI>();
            t.text = label;
            t.fontSize = 24; t.color = Color.white; t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.margin = new Vector4(16, 4, 16, 4);
            t.raycastTarget = false;
            var trt = (RectTransform)txt.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            return t;
        }

        GameObject pickerOverlay;

        void OnBallRowClicked(BallType type)
        {
            var bag = PokemonBag.Instance;
            if (bag == null) return;
            string name = type == BallType.Normal ? "Pokéball" : type == BallType.Great ? "Great Ball" : "Ultra Ball";
            if (type == BallType.Normal)
            {
                ToastManager.Show($"{name} — sınırsız (YAKALA ile fırlat)");
                return;
            }
            int cnt = bag.GetBallCount(type);
            ToastManager.Show(cnt > 0
                ? $"{name} ×{cnt} — soldaki chip'ten seç, sonra YAKALA"
                : $"{name} yok — görev tamamla");
        }

        void OpenPokemonPicker(PotionType type)
        {
            if (PokemonBag.Instance == null) return;
            if (PokemonBag.Instance.GetPotionCount(type) <= 0)
            {
                ToastManager.Show($"{PotionLabel(type)} kalmadı");
                return;
            }
            if (pickerOverlay != null) Destroy(pickerOverlay);

            pickerOverlay = new GameObject("PokemonPicker", typeof(RectTransform));
            pickerOverlay.transform.SetParent(transform, false);
            var ort = (RectTransform)pickerOverlay.transform;
            ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one;
            ort.offsetMin = Vector2.zero; ort.offsetMax = Vector2.zero;
            var bg = pickerOverlay.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.06f, 0.10f, 1f);
            bg.raycastTarget = true;

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(pickerOverlay.transform, false);
            var tt = titleGo.AddComponent<TextMeshProUGUI>();
            tt.text = $"{PotionLabel(type)} — Hedef seç";
            tt.fontSize = 28; tt.fontStyle = FontStyles.Bold;
            tt.alignment = TextAlignmentOptions.Center; tt.color = Color.white;
            tt.raycastTarget = false;
            var ttrt = (RectTransform)titleGo.transform;
            ttrt.anchorMin = new Vector2(0f, 0.90f); ttrt.anchorMax = new Vector2(1f, 1f);
            ttrt.offsetMin = Vector2.zero; ttrt.offsetMax = Vector2.zero;

            var listGo = new GameObject("List", typeof(RectTransform));
            listGo.transform.SetParent(pickerOverlay.transform, false);
            var lrt = (RectTransform)listGo.transform;
            lrt.anchorMin = new Vector2(0.04f, 0.10f); lrt.anchorMax = new Vector2(0.96f, 0.88f);
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var vlg = listGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6; vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false; vlg.childForceExpandHeight = false;

            var caught = PokemonBag.Instance.CaughtPokemon;
            bool requireFainted = type == PotionType.Revive;
            int eligibleCount = 0;
            for (int i = 0; i < caught.Count; i++)
            {
                var p = caught[i];
                bool eligible = requireFainted ? p.IsFainted : (!p.IsFainted && p.currentHealth < p.Health);
                if (!eligible) continue;
                eligibleCount++;
                int idx = i;
                CreatePickerRow(listGo.transform, p, () => ApplyPotion(type, idx));
            }
            if (eligibleCount == 0)
            {
                var empty = new GameObject("Empty", typeof(RectTransform));
                empty.transform.SetParent(listGo.transform, false);
                var et = empty.AddComponent<TextMeshProUGUI>();
                et.text = requireFainted ? "Bayılmış Pokémon yok." : "İyileştirilecek Pokémon yok.";
                et.fontSize = 22; et.alignment = TextAlignmentOptions.Center;
                et.color = new Color(0.8f, 0.8f, 0.8f); et.raycastTarget = false;
                empty.AddComponent<LayoutElement>().preferredHeight = 60;
            }

            var cancelGo = new GameObject("Cancel", typeof(RectTransform));
            cancelGo.transform.SetParent(pickerOverlay.transform, false);
            var cimg = cancelGo.AddComponent<Image>();
            cimg.color = new Color(0.55f, 0.2f, 0.2f, 1f);
            var cbtn = cancelGo.AddComponent<Button>();
            cbtn.targetGraphic = cimg;
            cbtn.onClick.AddListener(ClosePokemonPicker);
            var crt = (RectTransform)cancelGo.transform;
            crt.anchorMin = new Vector2(0.30f, 0.01f); crt.anchorMax = new Vector2(0.70f, 0.08f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var ctxt = new GameObject("Text", typeof(RectTransform));
            ctxt.transform.SetParent(cancelGo.transform, false);
            var ct = ctxt.AddComponent<TextMeshProUGUI>();
            ct.text = "İPTAL"; ct.fontSize = 24; ct.fontStyle = FontStyles.Bold;
            ct.alignment = TextAlignmentOptions.Center; ct.color = Color.white;
            ct.raycastTarget = false;
            var ctrt = (RectTransform)ctxt.transform;
            ctrt.anchorMin = Vector2.zero; ctrt.anchorMax = Vector2.one;
            ctrt.offsetMin = Vector2.zero; ctrt.offsetMax = Vector2.zero;
        }

        void CreatePickerRow(Transform parent, PokemonData p, UnityEngine.Events.UnityAction onClick)
        {
            var row = new GameObject($"Pick_{p.pokemonName}", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var img = row.AddComponent<Image>();
            img.color = p.IsFainted ? new Color(0.5f, 0.25f, 0.25f, 1f) : new Color(0.25f, 0.3f, 0.45f, 1f);
            var btn = row.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 64; le.preferredHeight = 64;

            var txt = new GameObject("Text", typeof(RectTransform));
            txt.transform.SetParent(row.transform, false);
            var t = txt.AddComponent<TextMeshProUGUI>();
            string status = p.IsFainted ? " <color=red>(Bayılmış)</color>" : "";
            t.text = $"<b>{p.pokemonName}</b>{status}\n<size=16>Lv.{p.level}  HP:{p.currentHealth}/{p.Health}</size>";
            t.fontSize = 20; t.color = Color.white;
            t.alignment = TextAlignmentOptions.Left;
            t.margin = new Vector4(12, 4, 12, 4);
            t.raycastTarget = false;
            var trt = (RectTransform)txt.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        }

        void ApplyPotion(PotionType type, int pokemonIndex)
        {
            var bag = PokemonBag.Instance;
            if (bag == null) return;
            var target = bag.GetPokemonAt(pokemonIndex);
            string name = target != null ? target.pokemonName : "?";
            bool ok = bag.UsePotion(type, pokemonIndex);
            ToastManager.Show(ok ? $"{name} → {PotionLabel(type)} kullanıldı" : $"{PotionLabel(type)} kullanılamadı");
            ClosePokemonPicker();
            RefreshItems();
            RefreshSlots();
        }

        void ClosePokemonPicker()
        {
            if (pickerOverlay != null) Destroy(pickerOverlay);
            pickerOverlay = null;
        }

        static string PotionLabel(PotionType type)
        {
            switch (type)
            {
                case PotionType.SmallPotion: return "Küçük Pot";
                case PotionType.SuperPotion: return "Süper Pot";
                case PotionType.HyperPotion: return "Hyper Pot";
                case PotionType.Revive:      return "Revive";
            }
            return type.ToString();
        }

        public void RefreshItems()
        {
            var bag = PokemonBag.Instance;
            if (bag == null) return;
            if (pokeballCountText != null) pokeballCountText.text = "<b>Pokéball</b>                                                ∞";
            if (greatBallText     != null) greatBallText.text     = $"<b>Great Ball</b>                                       ×{bag.GreatBallCount}";
            if (ultraBallText     != null) ultraBallText.text     = $"<b>Ultra Ball</b>                                       ×{bag.UltraBallCount}";
            var p = bag.Potions;
            if (p == null) return;
            if (smallPotText != null) smallPotText.text = $"<b>Küçük Pot (+20 HP)</b>                  ×{p.smallPotions}";
            if (superPotText != null) superPotText.text = $"<b>Süper Pot (+50 HP)</b>                  ×{p.superPotions}";
            if (hyperPotText != null) hyperPotText.text = $"<b>Hyper Pot (Full HP)</b>                 ×{p.hyperPotions}";
            if (reviveText   != null) reviveText.text   = $"<b>Revive</b>                                              ×{p.revives}";
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

        void ShowTab(Tab tab)
        {
            if (pokemonTabRoot != null) pokemonTabRoot.SetActive(tab == Tab.Pokemon);
            if (itemsTabRoot   != null) itemsTabRoot.SetActive(tab == Tab.Items);
            if (questTabRoot   != null) questTabRoot.SetActive(tab == Tab.Quest);
            var active = new Color(0.32f, 0.32f, 0.55f, 1f);
            var idle   = new Color(0.18f, 0.18f, 0.28f, 1f);
            if (pokeTabBtn  != null) pokeTabBtn.GetComponent<Image>().color  = tab == Tab.Pokemon ? active : idle;
            if (itemsTabBtn != null) itemsTabBtn.GetComponent<Image>().color = tab == Tab.Items   ? active : idle;
            if (questTabBtn != null) questTabBtn.GetComponent<Image>().color = tab == Tab.Quest   ? active : idle;
            if (tab == Tab.Items && itemsTabRoot != null) RefreshItems();
            if (tab == Tab.Quest && questListPanel != null) questListPanel.Refresh();
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
