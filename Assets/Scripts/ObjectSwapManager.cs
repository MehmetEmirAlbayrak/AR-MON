using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ARMON.AR;

public class ObjectSwapManager : MonoBehaviour
{
    [Header("AR")]
    public ARRaycastManager raycastManager;
    public ARAnchorManager anchorManager;
    public ARPlaneManager planeManager;

    [System.Serializable]
    public class LabelPrefab
    {
        public string label;
        public GameObject prefab;
        [Tooltip("Bu objenin gerçek dünyada ortalama yüksekliği (metre)")]
        public float realWorldHeight = 1.0f;
    }

    public List<LabelPrefab> mappings = new List<LabelPrefab>();

    [Header("Position Resolver")]
    [Tooltip("Server depth=1 (çok yakın) bu mesafeye eşlenir")]
    public float depthNearMeters = 0.5f;
    [Tooltip("Server depth=0 (uzak) bu mesafeye eşlenir")]
    public float depthFarMeters = 8.0f;
    [Tooltip("Hiç bir kaynak yoksa kameranın önüne sabit mesafe")]
    public float fallbackMeters = 2.5f;
    [Tooltip("Tahmini (depth-ray veya fallback) pozisyon Y'sini kameradan bu kadar düşür")]
    public float assumedGroundOffsetY = 1.5f;

    [Header("Dedup / Confidence Gate")]
    [Tooltip("Bir bucket'a bu kadar detection düşmeden spawn etme. ~1 Hz polling'de 2 idealdir.")]
    public int candidateConfirmCount = 2;
    [Tooltip("Confirm penceresi (saniye). 1 Hz polling'de 4s = 3-4 deneme şansı verir.")]
    public float candidateWindowSeconds = 4f;
    [Tooltip("Spatial hash hücresinin minimum kenarı (metre). Kamera titreşimini absorbe eder.")]
    public float minCellSize = 0.6f;

    [Header("Boyut")]
    [Tooltip("Mesafe x bbox formülünden gelen değer için alt sınır (metre). Saçma 0'ları engeller.")]
    public float minObjectHeight = 0.05f;
    [Tooltip("Mesafe x bbox formülünden gelen değer için üst sınır (metre). Saçma değerleri engeller.")]
    public float maxObjectHeight = 10f;
    [Tooltip("Mapping'de realWorldHeight > 0 ise bbox hesabı yerine onu kullan")]
    public bool mappingOverridesBboxSize = false;

    [Header("Despawn")]
    [Tooltip("Kameradan bu mesafeden uzak spawn'lar silinir")]
    public float despawnDistance = 6f;
    [Tooltip("Son görülmeden bu kadar saniye geçince spawn silinir (0 = kapalı)")]
    public float despawnStaleSeconds = 15f;
    public float despawnCheckInterval = 1f;

    [Header("Bilgi")]
    public List<DetectedObject> LastDetectedObjects { get; private set; } = new List<DetectedObject>();

    enum ResolveSource { None, Plane, InferredGround, Depth, Fallback }

    class SpawnedObjectInfo
    {
        public GameObject gameObject;
        public ARAnchor anchor;
        public string label;
        public Vector3 worldPosition;
        public Vector3Int cell;
        public float spawnTime;
        public float lastSeenTime;
    }

    class CandidateBucket
    {
        public Vector3 sumPosition;     // ortalama hesabı için
        public int count;
        public float windowStart;
        public float lastUpdate;
    }

    // (label, cell) -> bucket
    Dictionary<(string, Vector3Int), CandidateBucket> candidates =
        new Dictionary<(string, Vector3Int), CandidateBucket>();

    // Spawn edilen objeler (cell ile indekslenir)
    Dictionary<(string, Vector3Int), SpawnedObjectInfo> spawned =
        new Dictionary<(string, Vector3Int), SpawnedObjectInfo>();

    ARPositionResolver _resolver;

    void Start()
    {
        _resolver = new ARPositionResolver {
            depthNearMeters = depthNearMeters,
            depthFarMeters  = depthFarMeters,
            fallbackMeters  = fallbackMeters,
            groundOffsetY   = assumedGroundOffsetY,
        };
        InvokeRepeating(nameof(DespawnTick), despawnCheckInterval, despawnCheckInterval);
    }

    public void PlaceObjects(List<DetectedObject> objects)
    {
        if (objects == null || objects.Count == 0) return;
        LastDetectedObjects = new List<DetectedObject>(objects);

        foreach (var obj in objects)
            ProcessDetectedObject(obj);

        ExpireOldCandidates();
    }

    void ProcessDetectedObject(DetectedObject obj)
    {
        GameObject prefab = GetPrefabForLabel(obj.label);
        if (prefab == null) return;
        if (obj.center == null || obj.center.Length < 2) return;

        Vector2 normalizedCenter = new Vector2(obj.center[0], obj.center[1]);
        Vector2 screenPos = new Vector2(
            normalizedCenter.x * Screen.width,
            (1f - normalizedCenter.y) * Screen.height);

        if (!TryResolveWorldPosition(screenPos, obj.depth,
                out Vector3 worldPos, out Quaternion worldRot,
                out ResolveSource src, out ARPlane hitPlane))
            return;

        float realHeight = GetRealHeightForLabel(obj.label);
        float cellSize = Mathf.Max(minCellSize, realHeight * 0.5f);
        Vector3Int cell = WorldToCell(worldPos, cellSize);
        string label = obj.label.ToLower();
        var key = (label, cell);

        // Bu bucket'ta zaten bir spawn varsa: sadece "hâlâ görüldü"
        if (spawned.TryGetValue(key, out var existing))
        {
            existing.lastSeenTime = Time.time;
            return;
        }

        // Candidate buffer
        if (!candidates.TryGetValue(key, out var bucket))
        {
            bucket = new CandidateBucket
            {
                sumPosition = worldPos,
                count = 1,
                windowStart = Time.time,
                lastUpdate = Time.time,
            };
            candidates[key] = bucket;
            return;
        }

        bucket.sumPosition += worldPos;
        bucket.count++;
        bucket.lastUpdate = Time.time;

        if (Time.time - bucket.windowStart > candidateWindowSeconds)
        {
            // Pencere kaydı — yeni pencere başlat
            bucket.windowStart = Time.time;
            bucket.sumPosition = worldPos;
            bucket.count = 1;
            return;
        }

        if (bucket.count < candidateConfirmCount) return;

        // Promote: spawn et
        Vector3 finalPos = bucket.sumPosition / bucket.count;
        SpawnObject(prefab, label, cell, finalPos, worldRot, realHeight, obj, src, hitPlane);
        candidates.Remove(key);
    }

    // ============ POSITION RESOLVER ============

    bool TryResolveWorldPosition(Vector2 screenPos, float depth01,
        out Vector3 pos, out Quaternion rot, out ResolveSource source, out ARPlane plane)
    {
        if (!_resolver.TryResolve(screenPos, depth01, raycastManager, planeManager, out var r))
        {
            pos = default; rot = Quaternion.identity; source = ResolveSource.None; plane = null;
            return false;
        }
        pos = r.position;
        rot = r.rotation;
        plane = r.plane;
        source = r.source switch {
            ARMON.AR.ResolveSource.Plane          => ResolveSource.Plane,
            ARMON.AR.ResolveSource.InferredGround => ResolveSource.InferredGround,
            ARMON.AR.ResolveSource.Depth          => ResolveSource.Depth,
            ARMON.AR.ResolveSource.Fallback       => ResolveSource.Fallback,
            _                                      => ResolveSource.None,
        };
        return true;
    }

    // ============ SPAWN ============

    void SpawnObject(GameObject prefab, string label, Vector3Int cell,
        Vector3 pos, Quaternion rot, float mappingHeight,
        DetectedObject detection,
        ResolveSource source, ARPlane plane)
    {
        var rp = new ARMON.AR.ResolvedPose {
            position = pos,
            rotation = rot,
            plane    = plane,
            source   = source == ResolveSource.Plane
                ? ARMON.AR.ResolveSource.Plane
                : ARMON.AR.ResolveSource.Fallback,
        };
        ARAnchor anchor = ARAnchorUtil.CreateAnchor(rp, anchorManager, $"Anchor_{label}");
        GameObject go = Instantiate(prefab, anchor.transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        float targetHeight = ResolveTargetHeight(detection, pos, mappingHeight);
        ScaleToRealHeight(go, targetHeight);

        spawned[(label, cell)] = new SpawnedObjectInfo
        {
            gameObject = go,
            anchor = anchor,
            label = label,
            worldPosition = pos,
            cell = cell,
            spawnTime = Time.time,
            lastSeenTime = Time.time,
        };

        Debug.Log($"Spawn: {label} @ {pos} (src={source}, anchor={(anchor != null)})");
    }

    /// <summary>
    /// Hedef yüksekliği belirler: mapping override istenmediyse bbox x mesafe formülünden hesaplar,
    /// aksi halde label fallback'i kullanır. Sonucu min/max ile sınırlar.
    /// </summary>
    float ResolveTargetHeight(DetectedObject obj, Vector3 worldPos, float mappingHeight)
    {
        if (mappingOverridesBboxSize && mappingHeight > 0f)
            return Mathf.Clamp(mappingHeight, minObjectHeight, maxObjectHeight);

        Camera cam = Camera.main;
        if (cam != null && obj != null && obj.bbox != null && obj.bbox.Length == 4)
        {
            float bboxH = Mathf.Abs(obj.bbox[3] - obj.bbox[1]); // normalize 0..1
            if (bboxH > 0.001f)
            {
                float dist = Vector3.Distance(cam.transform.position, worldPos);
                float vFov = cam.fieldOfView * Mathf.Deg2Rad;
                float estimated = 2f * dist * Mathf.Tan(vFov * 0.5f) * bboxH;
                return Mathf.Clamp(estimated, minObjectHeight, maxObjectHeight);
            }
        }

        // bbox yoksa mapping'e düş
        return Mathf.Clamp(mappingHeight, minObjectHeight, maxObjectHeight);
    }

    void ScaleToRealHeight(GameObject go, float targetHeight)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        float currentHeight = combined.size.y;
        if (currentHeight <= 0.001f) return;

        float scale = targetHeight / currentHeight;
        go.transform.localScale = Vector3.one * scale;
    }

    // ============ DESPAWN ============

    void DespawnTick()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        var toRemove = new List<(string, Vector3Int)>();
        foreach (var kv in spawned)
        {
            var info = kv.Value;
            if (info.gameObject == null) { toRemove.Add(kv.Key); continue; }

            float dist = Vector3.Distance(cam.transform.position, info.worldPosition);
            bool tooFar = dist > despawnDistance;
            bool stale = despawnStaleSeconds > 0f
                         && (Time.time - info.lastSeenTime) > despawnStaleSeconds;

            if (tooFar || stale)
            {
                Destroy(info.gameObject);
                if (info.anchor != null) Destroy(info.anchor.gameObject);
                toRemove.Add(kv.Key);
            }
        }
        foreach (var key in toRemove) spawned.Remove(key);
    }

    void ExpireOldCandidates()
    {
        if (candidates.Count == 0) return;
        var stale = new List<(string, Vector3Int)>();
        foreach (var kv in candidates)
        {
            if (Time.time - kv.Value.lastUpdate > candidateWindowSeconds * 2f)
                stale.Add(kv.Key);
        }
        foreach (var k in stale) candidates.Remove(k);
    }

    // ============ HELPERS ============

    Vector3Int WorldToCell(Vector3 pos, float cellSize)
    {
        return new Vector3Int(
            Mathf.FloorToInt(pos.x / cellSize),
            Mathf.FloorToInt(pos.y / cellSize),
            Mathf.FloorToInt(pos.z / cellSize));
    }

    GameObject GetPrefabForLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return null;
        label = label.ToLower();
        foreach (var m in mappings)
            if (label.Contains(m.label.ToLower())) return m.prefab;
        return null;
    }

    float GetRealHeightForLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return 1.0f;
        label = label.ToLower();

        foreach (var m in mappings)
            if (label.Contains(m.label.ToLower())) return m.realWorldHeight;

        // Yaygın objeler — sadece bbox bilgisi yoksa veya mappingOverridesBboxSize=true ise kullanılır
        if (label.Contains("tree") || label.Contains("agac")) return 4f;
        if (label.Contains("person") || label.Contains("insan")) return 1.7f;
        if (label.Contains("car") || label.Contains("araba")) return 1.5f;
        if (label.Contains("dog") || label.Contains("kopek")) return 0.5f;
        if (label.Contains("cat") || label.Contains("kedi")) return 0.3f;
        if (label.Contains("chair") || label.Contains("sandalye")) return 0.9f;
        if (label.Contains("table") || label.Contains("masa")) return 0.75f;
        if (label.Contains("bottle") || label.Contains("sise")) return 0.25f;
        if (label.Contains("phone") || label.Contains("telefon")) return 0.15f;
        if (label.Contains("laptop")) return 0.3f;
        if (label.Contains("rock") || label.Contains("kaya")) return 0.5f;
        if (label.Contains("flower") || label.Contains("cicek")) return 0.3f;
        if (label.Contains("leaf") || label.Contains("yaprak")) return 0.2f;
        if (label.Contains("bush") || label.Contains("cali")) return 1f;
        if (label.Contains("bench") || label.Contains("bank")) return 0.8f;
        if (label.Contains("lamp") || label.Contains("lamba")) return 2f;
        if (label.Contains("sign") || label.Contains("tabela")) return 1.5f;
        if (label.Contains("bird") || label.Contains("kus")) return 0.2f;
        return 1.0f;
    }

    // ============ PUBLIC API (compat) ============

    public void ClearAllSpawns()
    {
        foreach (var info in spawned.Values)
        {
            if (info.gameObject != null) Destroy(info.gameObject);
            if (info.anchor != null) Destroy(info.anchor.gameObject);
        }
        spawned.Clear();
        candidates.Clear();
        LastDetectedObjects.Clear();
        Debug.Log("Tüm spawn'lar temizlendi.");
    }

    public int GetTotalSpawnCount()
    {
        // Destroy edilenleri temizle
        var dead = new List<(string, Vector3Int)>();
        foreach (var kv in spawned)
            if (kv.Value.gameObject == null) dead.Add(kv.Key);
        foreach (var k in dead) spawned.Remove(k);
        return spawned.Count;
    }

    public int GetSpawnCountForLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return 0;
        string lower = label.ToLower();
        int count = 0;
        foreach (var info in spawned.Values)
            if (info.gameObject != null && info.label == lower) count++;
        return count;
    }

    public void ClearSpawnsForLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return;
        string lower = label.ToLower();
        var toRemove = new List<(string, Vector3Int)>();
        foreach (var kv in spawned)
            if (kv.Value.label == lower) toRemove.Add(kv.Key);
        foreach (var k in toRemove)
        {
            var info = spawned[k];
            if (info.gameObject != null) Destroy(info.gameObject);
            if (info.anchor != null) Destroy(info.anchor.gameObject);
            spawned.Remove(k);
        }
    }
}
