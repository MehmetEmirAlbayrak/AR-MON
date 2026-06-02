using UnityEngine;
using UnityEngine.UI;

namespace ARMON.UI.AR
{
    /// <summary>
    /// Base for world-anchored panels (Inventory, Detail).
    /// Lifecycle:
    ///   var panel = ARWorldPanel.Spawn&lt;ARInventoryPanel&gt;("InventoryPanel");
    ///   panel.OpenAtUserFront(Camera.main, distance: 1.5f, heightOffset: -0.2f);
    ///   ...
    ///   panel.Close();
    ///
    /// Visual: world-space Canvas, billboard-style yaw rotation toward camera.
    /// Subclasses build UI in BuildContent() called from Awake.
    /// </summary>
    public abstract class ARWorldPanel : MonoBehaviour
    {
        [Tooltip("Panel size in meters (width × height)")]
        public Vector2 panelSizeMeters = new Vector2(0.8f, 0.6f);

        [Tooltip("Pixels per meter for internal layout (RectTransform unit scale)")]
        public float pixelsPerMeter = 1000f;

        protected Canvas canvas;
        protected RectTransform root;

        protected virtual void Awake()
        {
            BuildCanvas();
            BuildContent();
        }

        void BuildCanvas()
        {
            canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 5;

            if (gameObject.GetComponent<CanvasScaler>() == null)
                gameObject.AddComponent<CanvasScaler>();
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            root = (RectTransform)transform;
            root.sizeDelta = panelSizeMeters * pixelsPerMeter;
            root.localScale = Vector3.one * (1f / pixelsPerMeter);
        }

        /// <summary>Subclass adds its UI children to <see cref="root"/>.</summary>
        protected abstract void BuildContent();

        public void OpenAtUserFront(Camera cam, float distance = 1.5f, float heightOffset = -0.2f)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[ARWorldPanel] Camera.main null — cannot position panel.");
                return;
            }
            Vector3 fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();

            Vector3 pos = cam.transform.position + fwd * distance;
            pos.y = cam.transform.position.y + heightOffset;
            transform.position = pos;

            // Face camera (Y-only billboard).
            Vector3 toCam = cam.transform.position - pos;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.LookRotation(-toCam);
        }

        public virtual void Close()
        {
            Destroy(gameObject);
        }

        /// <summary>Convenience factory: instantiate a panel subclass on a fresh GameObject.</summary>
        public static T Spawn<T>(string objectName) where T : ARWorldPanel
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            return go.AddComponent<T>();
        }
    }
}
