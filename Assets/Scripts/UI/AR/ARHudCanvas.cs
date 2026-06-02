using UnityEngine;
using UnityEngine.UI;

namespace ARMON.UI.AR
{
    /// <summary>
    /// Camera-attached world-space HUD. Singleton.
    /// Exposes 3 slot transforms where other UI scripts mount their content:
    ///   - topLeftSlot     → DetectionInfoUI compact summary
    ///   - topRightSlot    → Bag icon button
    ///   - bottomCenterSlot → Pokeball selection UI
    /// Sits 0.5m in front of the camera, follows automatically (canvas is a child of the camera).
    /// </summary>
    public class ARHudCanvas : MonoBehaviour
    {
        public static ARHudCanvas Instance { get; private set; }

        [Tooltip("World units in meters from camera to HUD plane")]
        public float distanceFromCamera = 0.5f;

        [Tooltip("HUD plane size in meters (X = width, Y = height)")]
        public Vector2 hudSize = new Vector2(0.6f, 1.0f);

        public Transform TopLeftSlot     { get; private set; }
        public Transform TopRightSlot    { get; private set; }
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

        void Start()
        {
            AttachToCamera();
        }

        void AttachToCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[ARHudCanvas] Camera.main null — HUD root not parented yet.");
                return;
            }
            transform.SetParent(cam.transform, false);
            transform.localPosition = new Vector3(0f, 0f, distanceFromCamera);
            transform.localRotation = Quaternion.identity;
        }

        void Update()
        {
            // Re-parent if Camera.main appears later (AR Foundation may swap cameras).
            if (transform.parent == null || transform.parent.GetComponent<Camera>() == null)
                AttachToCamera();
        }

        void BuildCanvas()
        {
            _canvas = gameObject.GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 10;

            if (gameObject.GetComponent<CanvasScaler>() == null)
                gameObject.AddComponent<CanvasScaler>();
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(hudSize.x * 1000f, hudSize.y * 1000f); // 1000 px = 1 m at scale 0.001
            rt.localScale = Vector3.one * 0.001f;

            TopLeftSlot      = CreateSlot("TopLeftSlot",      new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f));
            TopRightSlot     = CreateSlot("TopRightSlot",     new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f));
            BottomCenterSlot = CreateSlot("BottomCenterSlot", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f));
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
            rt.sizeDelta = new Vector2(300f, 200f);
            return go.transform;
        }

        /// <summary>Ensure an EventSystem exists in scene so world-canvas buttons receive input.</summary>
        public static void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                                                   typeof(UnityEngine.EventSystems.StandaloneInputModule));
            DontDestroyOnLoad(go);
        }
    }
}
