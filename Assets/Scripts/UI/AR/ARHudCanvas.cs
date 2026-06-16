using UnityEngine;
using UnityEngine.UI;

namespace ARMON.UI.AR
{
    /// <summary>
    /// Screen-space overlay HUD. Singleton.
    /// Exposes 5 slot transforms where other UI scripts mount their content:
    ///   - TopLeftSlot      → DetectionInfoUI compact summary
    ///   - TopRightSlot     → (reserved)
    ///   - BottomLeftSlot   → Pokeball selection UI
    ///   - BottomRightSlot  → Bag icon button
    ///   - BottomCenterSlot → (reserved)
    /// Renders directly to screen, independent of camera. Stable under XR Simulator,
    /// AR Foundation camera swaps, and editor Game view.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ARHudCanvas : MonoBehaviour
    {
        public static ARHudCanvas Instance { get; private set; }

        [Tooltip("Reference resolution for CanvasScaler — designed for portrait phones.")]
        public Vector2 referenceResolution = new Vector2(1080f, 1920f);

        public Transform TopLeftSlot      { get; private set; }
        public Transform TopRightSlot     { get; private set; }
        public Transform BottomLeftSlot   { get; private set; }
        public Transform BottomRightSlot  { get; private set; }
        public Transform BottomCenterSlot { get; private set; }

        Canvas _canvas;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildCanvas();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void BuildCanvas()
        {
            // Detach from any parent (e.g. previously camera-parented setups).
            if (transform.parent != null) transform.SetParent(null, false);

            _canvas = gameObject.GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            // Insets from screen edges (in reference-resolution pixels).
            const float edgePad = 40f;
            const float bottomPad = 80f; // larger to clear iOS home indicator / Android nav bar
            TopLeftSlot      = CreateSlot("TopLeftSlot",      new Vector2(0f, 1f),   new Vector2(0f, 1f),   new Vector2( edgePad, -edgePad));
            TopRightSlot     = CreateSlot("TopRightSlot",     new Vector2(1f, 1f),   new Vector2(1f, 1f),   new Vector2(-edgePad, -edgePad));
            BottomLeftSlot   = CreateSlot("BottomLeftSlot",   new Vector2(0f, 0f),   new Vector2(0f, 0f),   new Vector2( edgePad,  bottomPad));
            BottomRightSlot  = CreateSlot("BottomRightSlot",  new Vector2(1f, 0f),   new Vector2(1f, 0f),   new Vector2(-edgePad,  bottomPad));
            BottomCenterSlot = CreateSlot("BottomCenterSlot", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f,        bottomPad));
        }

        Transform CreateSlot(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot     = anchorMin;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(400f, 250f);
            return go.transform;
        }

        /// <summary>Ensure an EventSystem exists in scene so canvas buttons receive input.</summary>
        public static void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                                                   typeof(UnityEngine.EventSystems.StandaloneInputModule));
            DontDestroyOnLoad(go);
        }
    }
}
