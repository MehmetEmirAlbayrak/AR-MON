using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARMON.UI.AR
{
    /// <summary>
    /// Minimal AR toast — pops up a ~2 second fade-out message on the ARHudCanvas
    /// (top area). Singleton. Falls back to Debug.Log if HUD is not ready.
    /// </summary>
    public class ToastManager : MonoBehaviour
    {
        public static ToastManager Instance { get; private set; }

        const float VisibleSeconds = 1.4f;
        const float FadeSeconds    = 0.6f;

        TextMeshProUGUI label;
        CanvasGroup group;
        Coroutine current;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public static void Show(string message)
        {
            Debug.Log($"[Toast] {message}");
            if (Instance != null) Instance.ShowInternal(message);
        }

        void ShowInternal(string message)
        {
            EnsureLabel();
            if (label == null) return;
            label.text = message;
            if (current != null) StopCoroutine(current);
            current = StartCoroutine(FadeRoutine());
        }

        void EnsureLabel()
        {
            if (label != null) return;
            if (ARHudCanvas.Instance == null) return;

            var hudT = ARHudCanvas.Instance.transform;
            GameObject go = new GameObject("ToastLabel", typeof(RectTransform));
            go.transform.SetParent(hudT, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.1f, 0.85f);
            rt.anchorMax = new Vector2(0.9f, 0.95f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            label = go.AddComponent<TextMeshProUGUI>();
            label.fontSize = 28;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.95f, 0.7f, 1f);
            label.outlineWidth = 0.25f;
            label.outlineColor = Color.black;
            label.raycastTarget = false;
        }

        IEnumerator FadeRoutine()
        {
            if (group == null) yield break;
            group.alpha = 1f;
            yield return new WaitForSeconds(VisibleSeconds);
            float t = 0f;
            while (t < FadeSeconds)
            {
                t += Time.deltaTime;
                group.alpha = 1f - (t / FadeSeconds);
                yield return null;
            }
            group.alpha = 0f;
            current = null;
        }
    }
}
