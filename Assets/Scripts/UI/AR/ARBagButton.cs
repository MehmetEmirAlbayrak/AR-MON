using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARMON.UI.AR
{
    /// <summary>
    /// Bag icon button mounted into ARHudCanvas.TopRightSlot.
    /// Toggles ARInventoryPanel.
    /// </summary>
    public class ARBagButton : MonoBehaviour
    {
        void Start()
        {
            if (ARHudCanvas.Instance == null)
            {
                Debug.LogWarning("[ARBagButton] ARHudCanvas.Instance null — bag button deferred.");
                return;
            }
            Mount(ARHudCanvas.Instance.TopRightSlot);
        }

        void Mount(Transform slot)
        {
            GameObject btn = new GameObject("BagButton");
            btn.transform.SetParent(slot, false);
            var img = btn.AddComponent<Image>();
            img.color = new Color(0.2f, 0.45f, 0.75f, 0.95f);
            var b = btn.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(OnClicked);

            var rt = (RectTransform)btn.transform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(140f, 70f);

            GameObject txt = new GameObject("Text");
            txt.transform.SetParent(btn.transform, false);
            var t = txt.AddComponent<TextMeshProUGUI>();
            t.text = "ÇANTA"; t.fontSize = 28; t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center; t.color = Color.white;
            var trt = (RectTransform)txt.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        }

        void OnClicked()
        {
            if (ARInventoryPanel.Current != null)
            {
                ARInventoryPanel.Current.Close();
                return;
            }
            ARInventoryPanel.Open(Camera.main);
        }
    }
}
