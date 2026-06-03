using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARMON.Quest;

namespace ARMON.UI.AR.Quest
{
    /// <summary>
    /// In-inventory "Görevler" tab content. NOT spawned standalone; parented into
    /// ARInventoryPanel and shown when the tab is selected.
    /// </summary>
    public class ARQuestListPanel : MonoBehaviour
    {
        Transform listContainer;
        readonly List<GameObject> rowObjects = new List<GameObject>();
        Button createButton;

        public void Init(RectTransform parent)
        {
            var rt = (RectTransform)transform;
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            transform.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            BuildContent();
            if (QuestManager.Instance != null)
                QuestManager.Instance.OnQuestsChanged += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (QuestManager.Instance != null)
                QuestManager.Instance.OnQuestsChanged -= Refresh;
        }

        void BuildContent()
        {
            GameObject scroll = new GameObject("QuestScroll", typeof(RectTransform));
            scroll.transform.SetParent(transform, false);
            var srt = (RectTransform)scroll.transform;
            srt.anchorMin = new Vector2(0.02f, 0.20f);
            srt.anchorMax = new Vector2(0.98f, 0.98f);
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            scroll.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.3f);
            var sr = scroll.AddComponent<ScrollRect>(); sr.horizontal = false; sr.vertical = true;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(scroll.transform, false);
            var vrt = (RectTransform)viewport.transform;
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
            var vimg = viewport.AddComponent<Image>(); vimg.color = new Color(1, 1, 1, 0.01f);
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
            sr.viewport = vrt; sr.content = crt;
            listContainer = content.transform;

            GameObject btn = new GameObject("QuestCreateBtn");
            btn.transform.SetParent(transform, false);
            var bimg = btn.AddComponent<Image>();
            bimg.color = new Color(0.25f, 0.55f, 0.35f, 1f);
            createButton = btn.AddComponent<Button>();
            createButton.targetGraphic = bimg;
            createButton.onClick.AddListener(OnCreateQuest);
            var brt = (RectTransform)btn.transform;
            brt.anchorMin = new Vector2(0.1f, 0.02f);
            brt.anchorMax = new Vector2(0.9f, 0.18f);
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

            GameObject btxt = new GameObject("Text");
            btxt.transform.SetParent(btn.transform, false);
            var bt = btxt.AddComponent<TextMeshProUGUI>();
            bt.text = "QUEST OLUŞTUR"; bt.fontSize = 28; bt.fontStyle = FontStyles.Bold;
            bt.alignment = TextAlignmentOptions.Center; bt.color = Color.white;
            bt.raycastTarget = false;
            var btrt = (RectTransform)btxt.transform;
            btrt.anchorMin = Vector2.zero; btrt.anchorMax = Vector2.one;
            btrt.offsetMin = Vector2.zero; btrt.offsetMax = Vector2.zero;
        }

        public void Refresh()
        {
            foreach (var r in rowObjects) if (r != null) Destroy(r);
            rowObjects.Clear();

            if (QuestManager.Instance == null) return;
            var list = QuestManager.Instance.ActiveQuests;
            for (int i = 0; i < list.Count; i++) CreateRow(list[i]);
            if (list.Count == 0) CreateEmptyRow();

            if (createButton != null)
                createButton.interactable = QuestManager.Instance.CanCreateNewQuest();
        }

        void CreateRow(QuestInstance q)
        {
            GameObject row = new GameObject($"Quest_{q.id}");
            row.transform.SetParent(listContainer, false);
            var img = row.AddComponent<Image>();
            img.color = q.state == QuestState.ObjectiveMet
                ? new Color(0.3f, 0.5f, 0.25f, 1f)
                : new Color(0.25f, 0.25f, 0.35f, 1f);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 90; le.preferredHeight = 90;

            GameObject lbl = new GameObject("Label");
            lbl.transform.SetParent(row.transform, false);
            var lt = lbl.AddComponent<TextMeshProUGUI>();
            lt.fontSize = 20; lt.color = Color.white;
            lt.alignment = TextAlignmentOptions.Left;
            lt.margin = new Vector4(10, 4, 10, 4);
            lt.raycastTarget = false;
            lt.text = FormatQuestLabel(q);
            var lrt = (RectTransform)lbl.transform;
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(0.65f, 1);
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

            GameObject act = new GameObject("Action");
            act.transform.SetParent(row.transform, false);
            var aimg = act.AddComponent<Image>();
            var abtn = act.AddComponent<Button>();
            abtn.targetGraphic = aimg;
            var art = (RectTransform)act.transform;
            art.anchorMin = new Vector2(0.66f, 0.15f); art.anchorMax = new Vector2(0.98f, 0.85f);
            art.offsetMin = Vector2.zero; art.offsetMax = Vector2.zero;

            GameObject atxt = new GameObject("Text");
            atxt.transform.SetParent(act.transform, false);
            var atext = atxt.AddComponent<TextMeshProUGUI>();
            atext.fontSize = 18; atext.fontStyle = FontStyles.Bold;
            atext.alignment = TextAlignmentOptions.Center; atext.color = Color.white;
            atext.raycastTarget = false;
            var atrt = (RectTransform)atxt.transform;
            atrt.anchorMin = Vector2.zero; atrt.anchorMax = Vector2.one;
            atrt.offsetMin = Vector2.zero; atrt.offsetMax = Vector2.zero;

            QuestInstance captured = q;
            if (q.state == QuestState.ObjectiveMet)
            {
                aimg.color = new Color(0.85f, 0.65f, 0.2f, 1f);
                atext.text = "TESLİM ET";
                abtn.onClick.AddListener(() => OnClaim(captured));
            }
            else
            {
                aimg.color = new Color(0.7f, 0.3f, 0.3f, 1f);
                atext.text = "İPTAL";
                abtn.onClick.AddListener(() => OnCancel(captured));
            }

            rowObjects.Add(row);
        }

        void CreateEmptyRow()
        {
            GameObject row = new GameObject("Empty");
            row.transform.SetParent(listContainer, false);
            var t = row.AddComponent<TextMeshProUGUI>();
            t.text = "Aktif görev yok — yeni bir tane oluştur.";
            t.fontSize = 20; t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            t.raycastTarget = false;
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 60;
            rowObjects.Add(row);
        }

        static string FormatQuestLabel(QuestInstance q)
        {
            string verb;
            int cur, tgt;
            switch (q.type)
            {
                case QuestType.CatchTarget:   verb = "Yakala";        cur = q.currentCount;            tgt = q.targetCount; break;
                case QuestType.DefeatWild:    verb = "Yen (vahşi)";   cur = q.currentCount;            tgt = q.targetCount; break;
                case QuestType.TrainerBattle: verb = "Trainer'ı Yen"; cur = q.trainerPokemonDefeated;  tgt = 3;             break;
                default:                      verb = q.type.ToString(); cur = 0; tgt = 0;                                  break;
            }
            string status = q.state == QuestState.ObjectiveMet ? " <color=yellow>(TAMAMLANDI)</color>" : "";
            return $"<b>{verb}</b>  {cur}/{tgt}{status}";
        }

        void OnCancel(QuestInstance q) { QuestManager.Instance?.Remove(q); }
        void OnClaim(QuestInstance q)  { QuestManager.Instance?.TryClaim(q); }

        void OnCreateQuest()
        {
            var qm = QuestManager.Instance;
            if (qm == null) return;
            if (!qm.CanCreateNewQuest())
            {
                ToastManager.Show("Önce mevcut bir görevi tamamla/iptal et");
                return;
            }
            if (QuestSpawner.Instance == null)
            {
                ToastManager.Show("QuestSpawner sahnede yok");
                return;
            }
            var quest = QuestRoller.Roll(qm.CurrentBiome, qm.registry);
            if (!qm.Add(quest))
            {
                ToastManager.Show("Quest eklenemedi");
                return;
            }
            if (!QuestSpawner.Instance.TrySpawnForQuest(quest))
            {
                qm.Remove(quest);
                return;
            }
            ToastManager.Show("NPC haritada belirdi!");
        }
    }
}
