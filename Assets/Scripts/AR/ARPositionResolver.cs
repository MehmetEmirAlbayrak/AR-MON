using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ARMON.AR
{
    public enum ResolveSource { None, Plane, InferredGround, Depth, Fallback }

    public struct ResolvedPose
    {
        public Vector3 position;
        public Quaternion rotation;
        public ResolveSource source;
        public ARPlane plane; // null unless Plane
    }

    /// <summary>
    /// Multi-tier AR position resolver.
    ///
    /// Lookup order:
    /// 1. <b>Plane raycast</b> — direct hit on a tracked AR plane (near, scanned surfaces)
    /// 2. <b>Inferred ground</b> — once any horizontal plane near the user has been seen, its Y is
    ///    cached as a "world floor" and any screen ray is projected onto that Y plane. Lets us place
    ///    far objects (10–20m) correctly on the ground without needing a plane to physically exist
    ///    at that distance.
    /// 3. <b>Depth</b> — server-provided depth hint (0..1) → meters along screen ray
    /// 4. <b>Fallback</b> — fixed distance in front of camera, drop to assumed ground offset
    /// </summary>
    public class ARPositionResolver
    {
        public float depthNearMeters    = 0.5f;
        public float depthFarMeters     = 8f;
        public float fallbackMeters     = 2.5f;
        public float groundOffsetY      = 1.5f;

        /// <summary>
        /// Inferred ground Y — STATIC ve paylaşımlı.
        /// İlk plane'i kim görürse görsün (ObjectSwap yüksek frekansta, BiomeSpawner düşük),
        /// her iki resolver da aynı groundY'yi kullanır. NaN = henüz öğrenilmedi.
        /// </summary>
        public static float inferredGroundY = float.NaN;

        /// <summary>Test/reset için kullanılabilir.</summary>
        public static void ResetInferredGround() { inferredGroundY = float.NaN; }

        /// <summary>How close (XZ meters) a horizontal plane must be to the camera to be considered the ground.</summary>
        public float groundLearnMaxXZDistance = 4f;

        /// <summary>Smoothing factor for groundY updates (EMA). 1.0 = snap to new reading, 0.0 = freeze.</summary>
        [Range(0f, 1f)] public float groundLearnAlpha = 0.25f;

        public bool TryResolve(
            Vector2 screenPos, float depth01,
            ARRaycastManager raycastManager, ARPlaneManager planeManager,
            out ResolvedPose result)
        {
            result = default;
            Camera cam = Camera.main;
            if (cam == null) return false;

            // Refresh inferred ground from any nearby horizontal plane every call.
            // Cheap — trackables.count is usually 0-5.
            UpdateInferredGround(cam, planeManager);

            // 1) plane raycast — most accurate when available
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

            // 2) inferred ground — project ray onto cached groundY
            if (!float.IsNaN(inferredGroundY))
            {
                Ray ray = cam.ScreenPointToRay(screenPos);
                // Solve: ray.origin.y + t * ray.direction.y = inferredGroundY
                float dy = ray.direction.y;
                if (Mathf.Abs(dy) > 1e-4f)
                {
                    float t = (inferredGroundY - ray.origin.y) / dy;
                    // Only accept hits in front of the camera (t > 0). Sky-pointing rays (t < 0) skip.
                    if (t > 0.05f && t < 50f)
                    {
                        Vector3 pos = ray.GetPoint(t);
                        result.position = pos;
                        result.rotation = FaceCamera(pos, cam);
                        result.source   = ResolveSource.InferredGround;
                        return true;
                    }
                }
            }

            // 3) depth
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

            // 4) fallback
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

        /// <summary>
        /// Look at every tracked horizontal plane; if any is near the camera in XZ,
        /// blend its Y into <see cref="inferredGroundY"/> with EMA.
        /// Cheap: usually 0–5 trackables in mobile AR.
        /// </summary>
        void UpdateInferredGround(Camera cam, ARPlaneManager planeManager)
        {
            if (planeManager == null) return;
            float camY = cam.transform.position.y;
            Vector3 camXZ = new Vector3(cam.transform.position.x, 0f, cam.transform.position.z);

            float bestPlaneY = float.NaN;
            float bestXZDist = float.MaxValue;

            foreach (var plane in planeManager.trackables)
            {
                if (plane == null) continue;
                // Horizontal only (up or down facing). PlaneAlignment.HorizontalUp is most common.
                if (plane.alignment != PlaneAlignment.HorizontalUp &&
                    plane.alignment != PlaneAlignment.HorizontalDown) continue;

                Vector3 c = plane.center;
                float xzDist = Vector3.Distance(camXZ, new Vector3(c.x, 0f, c.z));
                if (xzDist > groundLearnMaxXZDistance) continue;

                // Prefer planes BELOW the camera (likely the floor user is standing on).
                if (c.y > camY) continue;

                if (xzDist < bestXZDist)
                {
                    bestXZDist = xzDist;
                    bestPlaneY = c.y;
                }
            }

            if (float.IsNaN(bestPlaneY)) return;

            if (float.IsNaN(inferredGroundY))
            {
                inferredGroundY = bestPlaneY;        // first learn — snap
                Debug.Log($"[ARPositionResolver] Inferred groundY öğrenildi: {bestPlaneY:F3} (xzDist={bestXZDist:F2}m)");
            }
            else
            {
                inferredGroundY = Mathf.Lerp(inferredGroundY, bestPlaneY, groundLearnAlpha);
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
