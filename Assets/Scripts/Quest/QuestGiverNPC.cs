using UnityEngine;
using UnityEngine.XR.ARFoundation;
using ARMON.UI.AR.Quest;

namespace ARMON.Quest
{
    /// <summary>
    /// Tap-detectable NPC. Owns a QuestInstance and opens QuestDialogPanel on tap.
    /// Sits as a child of an ARAnchor; ARAnchorUtil pose follows tracking.
    /// </summary>
    public class QuestGiverNPC : MonoBehaviour
    {
        public QuestInstance Quest { get; protected set; }

        Camera cam;
        QuestDialogPanel openDialog;

        void Awake()
        {
            EnsureCollider();
        }

        void EnsureCollider()
        {
            if (GetComponent<Collider>() != null) return;
            if (GetComponentInChildren<Collider>() != null) return;
            var c = gameObject.AddComponent<SphereCollider>();
            c.radius = 0.5f;
            c.center = new Vector3(0, 0.5f, 0);
        }

        public virtual void Bind(QuestInstance q)
        {
            Quest = q;
        }

        void Update()
        {
            if (Quest == null) return;
            if (openDialog != null) return;

            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Vector2? screenPoint = null;
            int touchFingerId = -1;
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began) { screenPoint = t.position; touchFingerId = t.fingerId; }
            }
            else if (Input.GetMouseButtonDown(0))
            {
                screenPoint = Input.mousePosition;
            }
            if (!screenPoint.HasValue) return;

            // UI üzerinde tıklama varsa (inventory butonları vs) NPC dialog'unu açma.
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null)
            {
                if (touchFingerId >= 0)
                {
                    if (es.IsPointerOverGameObject(touchFingerId)) return;
                }
                else if (es.IsPointerOverGameObject())
                {
                    return;
                }
            }

            Ray r = cam.ScreenPointToRay(screenPoint.Value);
            if (Physics.Raycast(r, out RaycastHit hit, 50f))
            {
                if (hit.collider != null &&
                    (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)))
                {
                    OpenDialog();
                }
            }
        }

        void OpenDialog()
        {
            if (Quest == null) return;
            openDialog = QuestDialogPanel.Open(this, Camera.main);
            if (openDialog != null) openDialog.OnClosed += OnDialogClosed;
        }

        void OnDialogClosed()
        {
            if (openDialog != null) openDialog.OnClosed -= OnDialogClosed;
            openDialog = null;
        }

        public void DestroyNpcAndAnchor()
        {
            var anchor = GetComponentInParent<ARAnchor>();
            if (anchor != null) Destroy(anchor.gameObject);
            else Destroy(gameObject);
        }
    }
}
