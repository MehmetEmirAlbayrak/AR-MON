using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ARMON.AR;
using ARMON.UI.AR;

namespace ARMON.Quest
{
    /// <summary>
    /// Spawns a QuestGiverNPC on a tracked plane. Plane reqs: HorizontalUp + area > minPlaneArea.
    /// NPC pose: random bounded-rect sample inside the plane's local extents.
    /// </summary>
    public class QuestSpawner : MonoBehaviour
    {
        public static QuestSpawner Instance { get; private set; }

        [Header("AR Refs")]
        public ARPlaneManager planeManager;
        public ARAnchorManager anchorManager;
        [Tooltip("Boşsa runtime'da otomatik bulunur (scene'de yeni alan kablolanmamış olabilir).")]
        public ARRaycastManager raycastManager;

        [Header("NPC Prefab")]
        [Tooltip("If null, a placeholder capsule is generated.")]
        public GameObject npcPrefab;

        [Header("Spawn Filters")]
        public float minPlaneArea = 0.3f;
        [Tooltip("NPC plane kenarından en az bu kadar içeride doğar (metre)")]
        public float edgeMargin = 0.15f;

        // Spawn edilen NPC anchor'ları → plane kaldırılırsa NPC + (Offered ise) quest temizlenir.
        class TrackedNpc
        {
            public ARAnchor anchor;
            public TrackableId planeId;
            public QuestInstance quest;
        }
        readonly System.Collections.Generic.List<TrackedNpc> trackedNpcs =
            new System.Collections.Generic.List<TrackedNpc>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Yeni serialized alan eski scene'de null kalır — otomatik çöz.
            if (raycastManager == null) raycastManager = FindFirstObjectByType<ARRaycastManager>();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnEnable()
        {
            if (planeManager != null)
                planeManager.trackablesChanged.AddListener(OnPlanesChanged);
        }

        void OnDisable()
        {
            if (planeManager != null)
                planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
        }

        void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            if (args.removed == null || args.removed.Count == 0) return;
            foreach (var removed in args.removed)
            {
                TrackableId removedId = removed.Key;
                for (int i = trackedNpcs.Count - 1; i >= 0; i--)
                {
                    var t = trackedNpcs[i];
                    if (t.anchor == null) { trackedNpcs.RemoveAt(i); continue; }
                    if (t.planeId != removedId) continue;

                    Debug.Log($"[QuestSpawner] NPC'nin plane'i kaldırıldı → NPC siliniyor (quest {t.quest?.id})");
                    Destroy(t.anchor.gameObject);
                    // Henüz kabul edilmemiş (Offered) quest'i listeden düş — yoksa NPC'siz
                    // hayalet quest slot işgal eder (MaxActiveQuests=3).
                    if (t.quest != null && t.quest.state == QuestState.Offered)
                        QuestManager.Instance?.Remove(t.quest);
                    trackedNpcs.RemoveAt(i);
                }
            }
        }

        public bool TrySpawnForQuest(QuestInstance quest)
        {
            if (quest == null) return false;
            if (planeManager == null)
            {
                ToastManager.Show("AR plane manager bağlı değil");
                return false;
            }

            // 1) Kullanıcının BAKTIĞI nokta: ekran ortasından AR raycast. NPC, oyuncunun
            //    nişanladığı plane noktasında doğar — beklenen davranış bu.
            // 2) Fallback: en büyük uygun plane'in POLİGON MERKEZİ çevresinden örnekle.
            //    (Eski kod transform origin'inden örnekliyordu; origin çoğu zaman taramanın
            //    başladığı yer = oyuncunun ayağının dibi, hatta poligonun dışı olabiliyordu.)
            ARPlane chosen;
            Pose pose;
            if (!TryGetAimedPose(out pose, out chosen))
            {
                chosen = PickPlane();
                if (chosen == null)
                {
                    ToastManager.Show("Önce zemini tara");
                    return false;
                }
                pose = SamplePoseOnPlane(chosen);
            }

            ResolvedPose rp = new ResolvedPose
            {
                position = pose.position,
                rotation = pose.rotation,
                source = ResolveSource.Plane,
                plane = chosen
            };

            ARAnchor anchor = ARAnchorUtil.CreateAnchor(rp, anchorManager, $"QuestNPC_{quest.id}");
            if (anchor == null)
            {
                ToastManager.Show("Anchor oluşturulamadı");
                return false;
            }

            GameObject npc = InstantiateNPC(anchor.transform);
            var giver = npc.GetComponent<QuestGiverNPC>();
            if (giver == null) giver = npc.AddComponent<QuestGiverNPC>();
            quest.npcAnchorId = anchor.trackableId.ToString();
            giver.Bind(quest);

            trackedNpcs.Add(new TrackedNpc
            {
                anchor = anchor,
                planeId = chosen.trackableId,
                quest = quest,
            });

            return true;
        }

        ARPlane PickPlane()
        {
            ARPlane best = null;
            float bestArea = minPlaneArea;
            foreach (var p in planeManager.trackables)
            {
                if (p == null) continue;
                if (p.alignment != PlaneAlignment.HorizontalUp) continue;
                Vector2 ext = p.size;
                float area = Mathf.Abs(ext.x * ext.y);
                if (area >= bestArea)
                {
                    bestArea = area;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>
        /// Ekran ortasından AR raycast: oyuncunun nişanladığı HorizontalUp plane noktası.
        /// Alan + kenar payı doğrulanır. Başarısızsa false (fallback örnekleme devreye girer).
        /// </summary>
        bool TryGetAimedPose(out Pose pose, out ARPlane plane)
        {
            pose = default;
            plane = null;
            if (raycastManager == null) return false;
            Camera cam = Camera.main;
            if (cam == null) return false;

            var hits = new System.Collections.Generic.List<ARRaycastHit>();
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (!raycastManager.Raycast(screenCenter, hits, TrackableType.PlaneWithinPolygon))
                return false;

            foreach (var h in hits)
            {
                ARPlane p = planeManager.GetPlane(h.trackableId);
                if (p == null) continue;
                if (p.alignment != PlaneAlignment.HorizontalUp) continue;
                if (p.size.x * p.size.y < minPlaneArea) continue;
                if (!ARPositionResolver.IsAwayFromPlaneEdge(p, h.pose.position, edgeMargin)) continue;

                pose = new Pose(h.pose.position, FacePlayerYaw(h.pose.position, cam));
                plane = p;
                return true;
            }
            return false;
        }

        Pose SamplePoseOnPlane(ARPlane plane)
        {
            // POLİGON merkezini local'e çevir — ARPlane'in transform origin'i poligon
            // merkezinden uzak olabilir (plane büyüdükçe kayar); origin etrafından
            // örneklemek NPC'yi poligon dışına / oyuncunun dibine düşürüyordu.
            Vector3 localCenter = plane.transform.InverseTransformPoint(plane.center);

            // Kenar payı: NPC plane sınırının tam üstüne doğup yarısı boşlukta kalmasın.
            Vector2 ext = plane.extents;
            float mx = Mathf.Min(edgeMargin, ext.x * 0.5f);
            float mz = Mathf.Min(edgeMargin, ext.y * 0.5f);
            float dx = localCenter.x + Random.Range(-(ext.x - mx), ext.x - mx);
            float dz = localCenter.z + Random.Range(-(ext.y - mz), ext.y - mz);
            Vector3 worldPos = plane.transform.TransformPoint(new Vector3(dx, 0f, dz));

            Camera cam = Camera.main;
            Quaternion rot = cam != null ? FacePlayerYaw(worldPos, cam) : Quaternion.identity;
            return new Pose(worldPos, rot);
        }

        /// <summary>NPC oyuncuya dönük doğsun — yalnızca Y ekseni (dik duruş).</summary>
        static Quaternion FacePlayerYaw(Vector3 npcPos, Camera cam)
        {
            Vector3 toPlayer = cam.transform.position - npcPos;
            toPlayer.y = 0;
            return toPlayer.sqrMagnitude > 1e-4f
                ? Quaternion.LookRotation(toPlayer)
                : Quaternion.identity;
        }

        GameObject InstantiateNPC(Transform parent)
        {
            GameObject npc;
            if (npcPrefab != null)
            {
                npc = Instantiate(npcPrefab, parent);
                npc.transform.localPosition = Vector3.zero;
                npc.transform.localRotation = Quaternion.identity;
            }
            else
            {
                npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                npc.name = "QuestGiverNPC_Placeholder";
                npc.transform.SetParent(parent, false);
                npc.transform.localPosition = new Vector3(0, 0.9f, 0);
                npc.transform.localScale = new Vector3(0.4f, 0.9f, 0.4f);
                var rend = npc.GetComponent<Renderer>();
                if (rend != null) rend.material.color = new Color(0.6f, 0.4f, 0.85f, 1f);
            }
            return npc;
        }
    }
}
