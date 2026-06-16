using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARMON.Quest;

namespace ARMON.UI.AR.Quest
{
    /// <summary>
    /// World-space dialog floating above NPC head. Renders quest body + action buttons
    /// per QuestState (Offered, Active, ObjectiveMet).
    /// </summary>
    public class QuestDialogPanel : ARWorldPanel
    {
        public event Action OnClosed;

        QuestGiverNPC npc;
        QuestInstance quest;
        TextMeshProUGUI bodyText;
        GameObject acceptBtn, rejectBtn, okBtn;

        public static QuestDialogPanel Open(QuestGiverNPC npcArg, Camera cam)
        {
            if (npcArg == null || npcArg.Quest == null) return null;
            ARHudCanvas.EnsureEventSystem();

            var panel = ARWorldPanel.Spawn<QuestDialogPanel>("QuestDialogPanel");
            panel.panelSizeMeters = new Vector2(0.7f, 0.45f);
            panel.npc = npcArg;
            panel.quest = npcArg.Quest;

            Vector3 head = npcArg.transform.position + Vector3.up * 1.6f;
            panel.transform.position = head;
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - head;
                toCam.y = 0;
                if (toCam.sqrMagnitude > 1e-4f)
                    panel.transform.rotation = Quaternion.LookRotation(-toCam);
            }
            panel.Refresh();
            return panel;
        }

        public override void Close()
        {
            OnClosed?.Invoke();
            base.Close();
        }

        protected override void BuildContent()
        {
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.18f, 0.94f);

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(transform, false);
            var title = titleObj.AddComponent<TextMeshProUGUI>();
            title.text = "Yabancı Gezgin";
            title.fontSize = 32;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.color = new Color(1f, 0.92f, 0.6f, 1f);
            title.raycastTarget = false;
            var trt = (RectTransform)titleObj.transform;
            trt.anchorMin = new Vector2(0, 0.82f);
            trt.anchorMax = new Vector2(1, 1f);
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            // Body
            GameObject bodyObj = new GameObject("Body");
            bodyObj.transform.SetParent(transform, false);
            bodyText = bodyObj.AddComponent<TextMeshProUGUI>();
            bodyText.fontSize = 24;
            bodyText.color = Color.white;
            bodyText.alignment = TextAlignmentOptions.Center;
            bodyText.margin = new Vector4(20, 6, 20, 6);
            bodyText.raycastTarget = false;
            var brt = (RectTransform)bodyObj.transform;
            brt.anchorMin = new Vector2(0, 0.30f);
            brt.anchorMax = new Vector2(1, 0.82f);
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

            acceptBtn = CreateButton("AcceptBtn", "KABUL", new Color(0.25f, 0.6f, 0.3f, 1f),
                new Vector2(0.05f, 0.04f), new Vector2(0.47f, 0.26f), OnAccept);
            rejectBtn = CreateButton("RejectBtn", "REDDET", new Color(0.7f, 0.25f, 0.25f, 1f),
                new Vector2(0.53f, 0.04f), new Vector2(0.95f, 0.26f), OnReject);
            okBtn = CreateButton("OkBtn", "TAMAM", new Color(0.3f, 0.4f, 0.7f, 1f),
                new Vector2(0.25f, 0.04f), new Vector2(0.75f, 0.26f), OnOk);
        }

        GameObject CreateButton(string name, string text, Color color, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction callback)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(callback);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            GameObject txt = new GameObject("Text");
            txt.transform.SetParent(go.transform, false);
            var t = txt.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = 24;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.raycastTarget = false;
            var trt = (RectTransform)txt.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            return go;
        }

        public void Refresh()
        {
            if (quest == null) { Close(); return; }
            bodyText.text = BuildBodyText(quest);
            bool offered = quest.state == QuestState.Offered;
            bool active = quest.state == QuestState.Active;
            bool met = quest.state == QuestState.ObjectiveMet;
            acceptBtn.SetActive(offered);
            rejectBtn.SetActive(offered);
            okBtn.SetActive(active || met);
        }

        static string BuildBodyText(QuestInstance q)
        {
            var sb = new StringBuilder();
            switch (q.state)
            {
                case QuestState.Offered:
                    if (q.type == QuestType.TrainerBattle)
                        sb.Append("Bana karşı koy. 3 pokemonum var.\n");
                    else if (q.type == QuestType.CatchTarget)
                        sb.Append($"{q.targetCount} pokemon yakala.\n");
                    else
                        sb.Append($"{q.targetCount} vahşi pokemon yen.\n");
                    sb.Append("Ödül: ").Append(FormatRewards(q));
                    break;

                case QuestState.Active:
                    if (q.type == QuestType.TrainerBattle)
                        sb.Append($"Hala bekliyorum. Yenilen: {q.trainerPokemonDefeated}/3");
                    else
                        sb.Append($"Hala bekliyorum. {q.currentCount}/{q.targetCount}");
                    break;

                case QuestState.ObjectiveMet:
                    sb.Append("Aferin! Ödülünü çantandan teslim et.");
                    break;
            }
            return sb.ToString();
        }

        static string FormatRewards(QuestInstance q)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < q.rewards.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var r = q.rewards[i];
                sb.Append($"{r.count}× {RewardName(r.item)}");
            }
            return sb.ToString();
        }

        static string RewardName(RewardItem i)
        {
            switch (i)
            {
                case RewardItem.Pokeball:    return "Pokéball";
                case RewardItem.GreatBall:   return "Great Ball";
                case RewardItem.UltraBall:   return "Ultra Ball";
                case RewardItem.SmallPotion: return "Küçük Pot";
                case RewardItem.SuperPotion: return "Süper Pot";
                case RewardItem.HyperPotion: return "Hyper Pot";
                case RewardItem.Revive:      return "Revive";
                default: return i.ToString();
            }
        }

        void OnAccept()
        {
            if (quest == null || npc == null) return;
            quest.state = QuestState.Active;

            // Önce persist — sonra NPC destroy (crash window'da phantom quest oluşmasını önle).
            QuestManager.Instance?.OnQuestsChangedForceSave();

            if (quest.type == QuestType.TrainerBattle)
            {
                SpawnTrainerPokemon(0);
                Refresh();
            }
            else
            {
                npc.DestroyNpcAndAnchor();
                Close();
            }
        }

        void OnReject()
        {
            if (quest == null || npc == null) return;
            quest.state = QuestState.Rejected;
            QuestManager.Instance?.Remove(quest);
            npc.DestroyNpcAndAnchor();
            Close();
        }

        void OnOk()
        {
            Close();
        }

        void SpawnTrainerPokemon(int index)
        {
            if (quest == null || npc == null) return;
            if (index < 0 || index >= quest.trainerTeamSpeciesIds.Count) return;
            string sid = quest.trainerTeamSpeciesIds[index];

            var qm = QuestManager.Instance;
            var registry = qm != null ? qm.registry : null;
            if (registry == null)
            {
                Debug.LogWarning("[QuestDialogPanel] No species registry — skipping trainer spawn");
                return;
            }
            var species = registry.GetById(sid);
            if (species == null || species.basePrefab == null)
            {
                Debug.LogWarning($"[QuestDialogPanel] species {sid} missing");
                return;
            }

            // NPC kapsülü anchor'ın 0.9m üstünde durur — pokemon'u NPC'nin değil,
            // ANCHOR'ın (zemin) hizasında spawn et; yoksa havada doğar.
            Transform groundRef = npc.transform.parent != null ? npc.transform.parent : npc.transform;
            Vector3 fwd = npc.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 spawnPos = groundRef.position + fwd * 1.5f;
            spawnPos.y = groundRef.position.y + 0.01f; // z-fighting önleme
            GameObject mon = Instantiate(species.basePrefab, spawnPos, Quaternion.LookRotation(-fwd));
            mon.SetActive(false);
            var wp = mon.GetComponent<WildPokemon>();
            if (wp == null) wp = mon.AddComponent<WildPokemon>();
            wp.species = species;
            wp.pokemonName = species.displayName;
            wp.isTrainerPokemon = true;
            wp.questOwnerAnchorId = quest.npcAnchorId;
            mon.SetActive(true);
        }
    }
}
