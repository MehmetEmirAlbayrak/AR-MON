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

        [Header("NPC Prefab")]
        [Tooltip("If null, a placeholder capsule is generated.")]
        public GameObject npcPrefab;

        [Header("Spawn Filters")]
        public float minPlaneArea = 0.3f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public bool TrySpawnForQuest(QuestInstance quest)
        {
            if (quest == null) return false;
            if (planeManager == null)
            {
                ToastManager.Show("AR plane manager bağlı değil");
                return false;
            }

            ARPlane chosen = PickPlane();
            if (chosen == null)
            {
                ToastManager.Show("Önce zemini tara");
                return false;
            }

            Pose pose = SamplePoseOnPlane(chosen);
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

        Pose SamplePoseOnPlane(ARPlane plane)
        {
            Vector2 ext = plane.size * 0.5f;
            float dx = Random.Range(-ext.x, ext.x);
            float dz = Random.Range(-ext.y, ext.y);
            Vector3 worldPos = plane.transform.TransformPoint(new Vector3(dx, 0f, dz));

            Vector3 toCenter = plane.center - worldPos;
            toCenter.y = 0;
            Quaternion rot = toCenter.sqrMagnitude > 1e-4f
                ? Quaternion.LookRotation(-toCenter)
                : Quaternion.identity;

            return new Pose(worldPos, rot);
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
