using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

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
        ARAnchor planeAnchor;

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

        /// <summary>
        /// Panel yerleşim zinciri (öncelik sırasıyla):
        ///   1. Ekran ortası AR raycast → bakılan YATAY (HorizontalUp) plane'in üstünde durur (anchor'lı).
        ///   2. Plane poligonu oraya uzanmıyorsa → bakış ışınının öğrenilmiş zeminle (inferred ground)
        ///      kesiştiği noktada durur — yine "baktığın yerde", plane'siz.
        ///   3. Hiç zemin öğrenilmediyse → kameranın önünde serbest yüzer (son çare).
        /// "Bakılan plane yerine kameranın dibine açılma" bug'ının düzeltmesi: eski kod 1'de duvar
        /// hit'lerini kabul ediyor, 1 başarısızsa zemini tamamen yok sayıp paneli göz hizasına koyuyordu.
        /// </summary>
        public void OpenAtUserFront(Camera cam, float distance = 1.5f, float heightOffset = -0.2f)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[ARWorldPanel] Camera.main null — cannot position panel.");
                return;
            }

            if (TryAnchorToPlane(cam))
                return;

            if (TryStandOnInferredGround(cam, distance))
                return;

            // Son çare: kameranın önünde serbest yüzer (anchor yok, dünya pozisyonunda kalır).
            Vector3 fwd = HorizontalForward(cam);
            Vector3 pos = cam.transform.position + fwd * distance;
            pos.y = cam.transform.position.y + heightOffset;
            transform.position = pos;
            transform.rotation = FaceCamera(cam, pos);
        }

        bool TryAnchorToPlane(Camera cam)
        {
            var raycaster = Object.FindFirstObjectByType<ARRaycastManager>();
            var anchorMgr = Object.FindFirstObjectByType<ARAnchorManager>();
            if (raycaster == null) return false;

            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var hits = new List<ARRaycastHit>();
            if (!raycaster.Raycast(screenCenter, hits, TrackableType.PlaneWithinPolygon) || hits.Count == 0)
                return false;

            // İlk YATAY plane hit'ini al — duvar (Vertical) veya tavan (HorizontalDown) hit'i
            // panelin duvara gömülmesine/yamuk durmasına yol açar, onları atla.
            for (int i = 0; i < hits.Count; i++)
            {
                var plane = hits[i].trackable as ARPlane;
                if (plane == null || plane.alignment != PlaneAlignment.HorizontalUp) continue;

                // Panel yüzeyin üstüne oturur ve kameraya TAM döner (pitch dahil).
                PlaceStandingAt(hits[i].pose.position, cam);

                if (anchorMgr != null)
                {
                    planeAnchor = anchorMgr.AttachAnchor(plane, new Pose(transform.position, transform.rotation));
                    if (planeAnchor != null) transform.SetParent(planeAnchor.transform, true);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Bakış ışınını öğrenilmiş zemine (ARPositionResolver.inferredGroundY) projeler ve paneli
        /// o noktanın üstüne diker. Plane poligonu bakılan yere uzanmasa bile panel "baktığın yerde" açılır.
        /// </summary>
        bool TryStandOnInferredGround(Camera cam, float panelDistance)
        {
            if (!ARMON.AR.ARPositionResolver.HasInferredGround) return false;
            float groundY = ARMON.AR.ARPositionResolver.inferredGroundY;

            Vector3 spot;
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            float dy = ray.direction.y;
            float t = Mathf.Abs(dy) > 1e-4f ? (groundY - ray.origin.y) / dy : -1f;
            if (t > 0.05f && t < 6f)
            {
                // Kullanıcının baktığı zemin noktası (6m içinde) — paneli oraya dik.
                spot = ray.GetPoint(t);
            }
            else
            {
                // Ufka/yukarı bakıyor — önüne, zeminin üstüne koy.
                spot = cam.transform.position + HorizontalForward(cam) * panelDistance;
            }
            spot.y = groundY;

            PlaceStandingAt(spot, cam);
            return true;
        }

        /// <summary>
        /// Yüzeyde dururken panel merkezinin yüzeyden yüksekliği: alt kenar yüzeye değer,
        /// minik bir boşlukla z-fighting önlenir.
        /// </summary>
        float StandCenterOffset => panelSizeMeters.y * 0.5f + 0.02f;

        /// <summary>
        /// Kameranın yatay ileri yönü. Dik aşağı bakışta forward'ın yatay bileşeni sıfıra çöker —
        /// o durumda kameranın up vektörünün yatay izdüşümü "ilerisi"ni verir (panelin kameranın
        /// İÇİNDE açılması bug'ının kökü buydu).
        /// </summary>
        static Vector3 HorizontalForward(Camera cam)
        {
            Vector3 fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-3f) fwd = Vector3.ProjectOnPlane(cam.transform.up, Vector3.up);
            if (fwd.sqrMagnitude < 1e-3f) fwd = Vector3.forward;
            return fwd.normalized;
        }

        /// <summary>
        /// Paneli yüzey noktasının üstüne oturtur ve kameraya TAM döndürür (yaw + pitch).
        /// Panel geriye yattıkça dikey kapladığı alan azalır; merkez yüksekliği eğime göre
        /// hesaplanır ki alt kenar yüzeye değmeye devam etsin (yatıkken havada asılı kalmasın).
        /// </summary>
        void PlaceStandingAt(Vector3 surfacePoint, Camera cam)
        {
            Quaternion rot = FaceCamera(cam, surfacePoint + Vector3.up * StandCenterOffset);
            // 1 = tam dik, 0 = tam yatık. Panelin local-up'ının dünya dikeyine izdüşümü.
            float uprightness = Mathf.Abs(Vector3.Dot(rot * Vector3.up, Vector3.up));
            float centerY = panelSizeMeters.y * 0.5f * uprightness + 0.02f;

            Vector3 standPos = surfacePoint + Vector3.up * centerY;
            transform.position = standPos;
            transform.rotation = FaceCamera(cam, standPos);
        }

        /// <summary>
        /// Kameraya tam dönük rotasyon (yaw + pitch) — aşağı bakan oyuncuya panel
        /// tablet gibi geriye yatarak döner. Roll, dünya up'ıyla sabitlenir.
        /// </summary>
        static Quaternion FaceCamera(Camera cam, Vector3 selfPos)
        {
            Vector3 toCam = cam.transform.position - selfPos;
            return toCam.sqrMagnitude > 1e-4f
                ? Quaternion.LookRotation(-toCam, Vector3.up)
                : Quaternion.identity;
        }

        public virtual void Close()
        {
            if (planeAnchor != null) Destroy(planeAnchor.gameObject);
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
