using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace ARMON.AR
{
    public static class ARAnchorUtil
    {
        /// <summary>Attach an anchor to the resolved pose. Plane-anchor when available.</summary>
        public static ARAnchor CreateAnchor(ResolvedPose r, ARAnchorManager mgr, string debugName)
        {
            if (r.source == ResolveSource.Plane && r.plane != null && mgr != null)
            {
                var a = mgr.AttachAnchor(r.plane, new Pose(r.position, r.rotation));
                if (a != null) return a;
            }
            var go = new GameObject(debugName ?? "Anchor");
            go.transform.SetPositionAndRotation(r.position, r.rotation);
            return go.AddComponent<ARAnchor>();
        }

        public static void DestroyAnchor(ARAnchor a)
        {
            if (a == null) return;
            Object.Destroy(a.gameObject);
        }
    }
}
