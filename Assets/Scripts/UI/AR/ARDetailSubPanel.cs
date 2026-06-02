using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARMON.UI.AR
{
    /// <summary>
    /// Sub-panel mounted to the right of an <see cref="ARInventoryPanel"/>.
    /// Shows one pokemon's stats and a Summon button.
    /// </summary>
    public class ARDetailSubPanel : ARWorldPanel
    {
        int pokemonIndex = -1;
        TextMeshProUGUI body;

        public static ARDetailSubPanel OpenBeside(ARInventoryPanel inv, int pokemonIndex)
        {
            var panel = ARWorldPanel.Spawn<ARDetailSubPanel>("ARDetailSubPanel");
            panel.panelSizeMeters = new Vector2(0.6f, 0.6f);
            panel.pokemonIndex = pokemonIndex;
            panel.PositionBeside(inv);
            panel.Refresh();
            return panel;
        }

        void PositionBeside(ARInventoryPanel inv)
        {
            // Parent so we inherit Y-rotation; offset to the right in inv local space.
            transform.SetParent(inv.transform, false);
            // 0.8m wide inventory → 0.4m half + small gap + 0.3m half detail = 0.75m offset
            transform.localPosition = new Vector3(0.75f * 1000f, 0f, 0f);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one; // inherit canvas scale
        }

        protected override void BuildContent()
        {
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.12f, 0.18f, 0.95f);

            GameObject bodyObj = new GameObject("Body");
            bodyObj.transform.SetParent(transform, false);
            body = bodyObj.AddComponent<TextMeshProUGUI>();
            body.fontSize = 24; body.color = Color.white;
            body.alignment = TextAlignmentOptions.TopLeft;
            body.margin = new Vector4(16, 50, 16, 100);
            var brt = (RectTransform)bodyObj.transform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

            MakeButton("SUMMON", new Vector2(0.05f, 0.18f), new Vector2(0.48f, 0.30f),
                       new Color(0.2f, 0.65f, 0.35f), OnSummonClicked);
            MakeButton("KAPAT",  new Vector2(0.52f, 0.18f), new Vector2(0.95f, 0.30f),
                       new Color(0.75f, 0.25f, 0.25f), OnCloseClicked);
        }

        void MakeButton(string label, Vector2 aMin, Vector2 aMax, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btn = new GameObject(label + "Btn");
            btn.transform.SetParent(transform, false);
            var img = btn.AddComponent<Image>();
            img.color = bgColor;
            var b = btn.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(onClick);
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            GameObject txt = new GameObject("Text");
            txt.transform.SetParent(btn.transform, false);
            var t = txt.AddComponent<TextMeshProUGUI>();
            t.text = label; t.fontSize = 28; t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center; t.color = Color.white;
            var trt = (RectTransform)txt.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        }

        void Refresh()
        {
            if (PokemonBag.Instance == null) { body.text = "Bag yok"; return; }
            var p = PokemonBag.Instance.GetPokemonAt(pokemonIndex);
            if (p == null) { body.text = "Pokemon yok"; return; }
            body.text =
                $"<b>{p.pokemonName}</b>\n" +
                $"Level: {p.level}\n" +
                $"HP: {p.currentHealth}/{p.Health}\n" +
                $"ATK: {p.Attack}\n" +
                $"DEF: {p.Defense}\n" +
                (p.IsFainted ? "<color=red>Bayılmış</color>" : "");
        }

        void OnSummonClicked()
        {
            if (BattleManager.Instance != null) BattleManager.Instance.SummonPokemon(pokemonIndex);
            if (ARInventoryPanel.Current != null) ARInventoryPanel.Current.Close();
        }

        void OnCloseClicked() => Close();
    }
}
