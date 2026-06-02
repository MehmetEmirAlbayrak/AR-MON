using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ARMON.AR
{
    public enum ResolveSource { None, Plane, Depth, Fallback }

    public struct ResolvedPose
    {
        public Vector3 position;
        public Quaternion rotation;
        public ResolveSource source;
        public ARPlane plane; // null unless Plane
    }

    public class ARPositionResolver
    {
        public float depthNearMeters    = 0.5f;
        public float depthFarMeters     = 8f;
        public float fallbackMeters     = 2.5f;
        public float groundOffsetY      = 1.5f;

        public bool TryResolve(
            Vector2 screenPos, float depth01,
            ARRaycastManager raycastManager, ARPlaneManager planeManager,
            out ResolvedPose result)
        {
            result = default;
            Camera cam = Camera.main;
            if (cam == null) return false;

            // 1) plane raycast
            if (raycastManager != null)
            {
                var hits = new List<ARRaycastHit>();
                if (raycastManager.Raycast(screenPos, hits, TrackableType.PlaneWithinPolygon)
                    && hits.Count > 0)
                {
                    result.position = hits[0].pose.position;
                    result.rotation = hits[0].pose.rotation;
                    result.source   = ResolveSource.Plane;
                    if (planeManager != null)
                        result.plane = planeManager.GetPlane(hits[0].trackableId);
                    return true;
                }
            }

            // 2) depth
            if (depth01 > 0.001f)
            {
                float meters = Mathf.Lerp(depthFarMeters, depthNearMeters, Mathf.Clamp01(depth01));
                Ray ray = cam.ScreenPointToRay(screenPos);
                Vector3 pos = ray.GetPoint(meters);
                pos.y = Mathf.Min(pos.y, cam.transform.position.y - 0.05f);
                result.position = pos;
                result.rotation = FaceCamera(pos, cam);
                result.source   = ResolveSource.Depth;
                return true;
            }

            // 3) fallback
            {
                Ray ray = cam.ScreenPointToRay(screenPos);
                Vector3 pos = ray.GetPoint(fallbackMeters);
                pos.y = cam.transform.position.y - groundOffsetY;
                result.position = pos;
                result.rotation = FaceCamera(pos, cam);
                result.source   = ResolveSource.Fallback;
                return true;
            }
        }

        static Quaternion FaceCamera(Vector3 pos, Camera cam)
        {
            Vector3 dir = cam.transform.position - pos;
            dir.y = 0;
            return dir.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(-dir)
                : Quaternion.identity;
        }
    }
}
