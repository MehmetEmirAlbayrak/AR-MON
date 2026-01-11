using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ObjectSwapManager : MonoBehaviour
{
    [Header("AR")]
    public ARRaycastManager raycastManager;
    public AROcclusionManager occlusionManager;

    [System.Serializable]
    public class LabelPrefab
    {
        public string label;
        public GameObject prefab;
        [Tooltip("Bu objenin gerçek dünyada ortalama yüksekliği (metre)")]
        public float realWorldHeight = 1.0f;
    }

    public List<LabelPrefab> mappings = new List<LabelPrefab>();

    [Header("Derinlik Tahmini")]
    public bool useDepthEstimation = true;
    public float defaultRealHeight = 1.5f;
    public float minDepth = 0.5f;
    public float maxDepth = 15f;
    public float multiRaycastRadius = 100f;
    public int multiRaycastPoints = 8;

    [Header("Boyut Ayarları")]
    public float referenceDistance = 1.0f;
    public float minObjectSize = 0.1f;
    public float maxObjectSize = 3.0f;

    [Header("Tekrar Spawn Engelleme")]
    [Tooltip("Dünya pozisyonunda aynı label için minimum mesafe (metre) - ANA KONTROL")]
    public float worldDuplicateDistance = 1.0f;
    
    [Tooltip("Aynı frame'de aynı ekran bölgesini tekrar işleme (normalized 0-1)")]
    public float sameFrameScreenThreshold = 0.03f;
    
    [Tooltip("Aynı label için maksimum spawn sayısı (0 = sınırsız)")]
    public int maxSpawnPerLabel = 0;

    // Spawn edilen objeler
    List<SpawnedObjectInfo> spawnedObjects = new List<SpawnedObjectInfo>();
    
    // Bu frame'de işlenen algılamalar (aynı frame tekrarını engellemek için)
    List<FrameDetection> thisFrameDetections = new List<FrameDetection>();
    int lastProcessedFrame = -1;

    public List<DetectedObject> LastDetectedObjects { get; private set; } = new List<DetectedObject>();

    class SpawnedObjectInfo
    {
        public GameObject gameObject;
        public string label;
        public Vector3 worldPosition;
        public float spawnTime;
    }

    class FrameDetection
    {
        public Vector2 screenPos;
        public string label;
    }

    public void PlaceObjects(List<DetectedObject> objects)
    {
        if (objects == null || objects.Count == 0)
            return;

        // Yeni frame başladıysa bu frame listesini temizle
        if (Time.frameCount != lastProcessedFrame)
        {
            thisFrameDetections.Clear();
            lastProcessedFrame = Time.frameCount;
        }

        LastDetectedObjects = new List<DetectedObject>(objects);
        CleanupDestroyedObjects();

        foreach (var obj in objects)
        {
            ProcessDetectedObject(obj);
        }
    }

    void ProcessDetectedObject(DetectedObject obj)
    {
        GameObject prefab = GetPrefabForLabel(obj.label);
        if (prefab == null)
            return;

        if (obj.center == null || obj.center.Length < 2)
            return;

        Vector2 normalizedCenter = new Vector2(obj.center[0], obj.center[1]);
        Vector2 screenPos = new Vector2(normalizedCenter.x * Screen.width, (1f - normalizedCenter.y) * Screen.height);

        // 1. Bu frame'de aynı ekran bölgesi zaten işlendi mi? (aynı frame tekrarı engelle)
        if (IsProcessedThisFrame(normalizedCenter, obj.label))
        {
            return;
        }

        // 2. Derinlik tahmini yap ve dünya pozisyonunu hesapla
        float estimatedDepth = EstimateDepthFromBbox(obj);
        
        Vector3 worldPosition;
        Quaternion worldRotation;
        float actualDepth;
        
        if (!TryGetWorldPosition(screenPos, estimatedDepth, out worldPosition, out worldRotation, out actualDepth))
        {
            return;
        }

        // 3. ANA KONTROL: Bu dünya pozisyonunda aynı tipte obje zaten var mı?
        if (IsWorldPositionOccupied(worldPosition, obj.label))
        {
            // Zaten var, spawn etme - ama frame'e kaydet ki bu frame tekrar bakmasın
            RegisterThisFrameDetection(normalizedCenter, obj.label);
            return;
        }

        // 4. Label limiti kontrolü
        if (maxSpawnPerLabel > 0 && GetSpawnCountForLabel(obj.label) >= maxSpawnPerLabel)
        {
            return;
        }

        // 5. Yeni obje spawn et
        var go = Instantiate(prefab, worldPosition, worldRotation);
        ScaleObjectToRealSize(go, obj, actualDepth);
        LookAtCamera(go);

        RegisterSpawn(obj.label, go, worldPosition);
        RegisterThisFrameDetection(normalizedCenter, obj.label);
        
        Debug.Log($"Obje spawn edildi: {obj.label} - Dünya: {worldPosition}, Derinlik: {actualDepth:F2}m");
    }

    /// <summary>
    /// Bu frame'de aynı ekran bölgesi işlendi mi? (aynı frame içi tekrar engelle)
    /// </summary>
    bool IsProcessedThisFrame(Vector2 normalizedPos, string label)
    {
        foreach (var detection in thisFrameDetections)
        {
            if (detection.label.ToLower() == label.ToLower())
            {
                float dist = Vector2.Distance(normalizedPos, detection.screenPos);
                if (dist < sameFrameScreenThreshold)
                {
                    return true;
                }
            }
        }
        return false;
    }

    void RegisterThisFrameDetection(Vector2 normalizedPos, string label)
    {
        thisFrameDetections.Add(new FrameDetection
        {
            screenPos = normalizedPos,
            label = label
        });
    }

    /// <summary>
    /// DÜNYA POZİSYONUNDA aynı tipte obje var mı? (ANA KONTROL - kamera açısından bağımsız)
    /// </summary>
    bool IsWorldPositionOccupied(Vector3 position, string label)
    {
        string lowerLabel = label.ToLower();
        
        foreach (var spawnInfo in spawnedObjects)
        {
            if (spawnInfo.gameObject == null) continue;
            
            // Aynı label kontrolü
            if (spawnInfo.label.ToLower() != lowerLabel) continue;
            
            // Dünya mesafesi kontrolü
            float worldDist = Vector3.Distance(position, spawnInfo.worldPosition);
            if (worldDist < worldDuplicateDistance)
            {
                Debug.Log($"'{label}' zaten mevcut - mesafe: {worldDist:F2}m < {worldDuplicateDistance}m");
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Bounding box boyutundan derinlik tahmini
    /// </summary>
    float EstimateDepthFromBbox(DetectedObject obj)
    {
        if (!useDepthEstimation || obj.bbox == null || obj.bbox.Length < 4)
            return referenceDistance;

        float realHeight = GetRealHeightForLabel(obj.label);
        float bboxHeight = obj.Height;
        
        if (bboxHeight <= 0.01f)
            return maxDepth;

        Camera cam = Camera.main;
        float verticalFOV = cam.fieldOfView * Mathf.Deg2Rad;
        
        float estimatedDepth = realHeight / (2f * Mathf.Tan(verticalFOV / 2f) * bboxHeight);
        return Mathf.Clamp(estimatedDepth, minDepth, maxDepth);
    }

    float GetRealHeightForLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return defaultRealHeight;
        
        label = label.ToLower();
        
        foreach (var map in mappings)
        {
            if (label.Contains(map.label.ToLower()))
                return map.realWorldHeight;
        }
        
        // Yaygın objeler
        if (label.Contains("tree") || label.Contains("agac")) return 4f;
        if (label.Contains("person") || label.Contains("insan")) return 1.7f;
        if (label.Contains("car") || label.Contains("araba")) return 1.5f;
        if (label.Contains("dog") || label.Contains("kopek")) return 0.5f;
        if (label.Contains("cat") || label.Contains("kedi")) return 0.3f;
        if (label.Contains("chair") || label.Contains("sandalye")) return 0.9f;
        if (label.Contains("table") || label.Contains("masa")) return 0.75f;
        if (label.Contains("bottle") || label.Contains("sise")) return 0.25f;
        if (label.Contains("phone") || label.Contains("telefon")) return 0.15f;
        if (label.Contains("laptop") || label.Contains("bilgisayar")) return 0.3f;
        if (label.Contains("rock") || label.Contains("kaya")) return 0.5f;
        if (label.Contains("flower") || label.Contains("cicek")) return 0.3f;
        if (label.Contains("bush") || label.Contains("cali")) return 1f;
        if (label.Contains("bench") || label.Contains("bank")) return 0.8f;
        if (label.Contains("lamp") || label.Contains("lamba")) return 2f;
        if (label.Contains("sign") || label.Contains("tabela")) return 1.5f;
        if (label.Contains("bird") || label.Contains("kus")) return 0.2f;
        
        return defaultRealHeight;
    }

    bool TryGetWorldPosition(Vector2 screenPos, float estimatedDepth, out Vector3 worldPos, out Quaternion worldRot, out float actualDepth)
    {
        Camera cam = Camera.main;
        var hits = new List<ARRaycastHit>();
        
        // 1. AR Plane raycast
        if (raycastManager.Raycast(screenPos, hits, TrackableType.PlaneWithinPolygon))
        {
            worldPos = hits[0].pose.position;
            worldRot = hits[0].pose.rotation;
            actualDepth = Vector3.Distance(cam.transform.position, worldPos);
            
            // Tahminle çok farklıysa tahmini kullan
            if (Mathf.Abs(actualDepth - estimatedDepth) > estimatedDepth * 0.5f)
            {
                worldPos = GetPositionAtDepth(screenPos, estimatedDepth, worldPos.y);
                actualDepth = estimatedDepth;
            }
            
            return true;
        }
        
        // 2. Multi raycast
        if (TryMultiRaycast(screenPos, out Vector3 nearbyPos, out Quaternion nearbyRot))
        {
            worldPos = GetPositionAtDepth(screenPos, estimatedDepth, nearbyPos.y);
            worldRot = nearbyRot;
            actualDepth = estimatedDepth;
            return true;
        }
        
        // 3. Depth API (varsa)
        if (occlusionManager != null && TryGetDepthFromAPI(screenPos, out float apiDepth))
        {
            worldPos = GetPositionAtDepth(screenPos, apiDepth, 0);
            worldRot = Quaternion.identity;
            actualDepth = apiDepth;
            return true;
        }
        
        // 4. Sadece tahmin
        if (useDepthEstimation && estimatedDepth > 0)
        {
            float assumedGroundY = cam.transform.position.y - 1.5f;
            worldPos = GetPositionAtDepth(screenPos, estimatedDepth, assumedGroundY);
            worldRot = Quaternion.identity;
            actualDepth = estimatedDepth;
            return true;
        }
        
        worldPos = Vector3.zero;
        worldRot = Quaternion.identity;
        actualDepth = 0;
        return false;
    }

    Vector3 GetPositionAtDepth(Vector2 screenPos, float depth, float overrideY)
    {
        Camera cam = Camera.main;
        Ray ray = cam.ScreenPointToRay(screenPos);
        Vector3 position = ray.origin + ray.direction * depth;
        
        if (overrideY != 0)
            position.y = overrideY;
        
        return position;
    }

    bool TryMultiRaycast(Vector2 centerScreen, out Vector3 foundPos, out Quaternion foundRot)
    {
        var hits = new List<ARRaycastHit>();
        List<ARRaycastHit> allHits = new List<ARRaycastHit>();
        
        for (int i = 0; i < multiRaycastPoints; i++)
        {
            float angle = (360f / multiRaycastPoints) * i * Mathf.Deg2Rad;
            Vector2 testPoint = centerScreen + new Vector2(
                Mathf.Cos(angle) * multiRaycastRadius,
                Mathf.Sin(angle) * multiRaycastRadius
            );
            
            if (testPoint.x < 0 || testPoint.x > Screen.width || 
                testPoint.y < 0 || testPoint.y > Screen.height)
                continue;
            
            if (raycastManager.Raycast(testPoint, hits, TrackableType.PlaneWithinPolygon))
                allHits.Add(hits[0]);
        }
        
        if (allHits.Count > 0)
        {
            ARRaycastHit closest = allHits[0];
            float closestDist = float.MaxValue;
            
            foreach (var hit in allHits)
            {
                float dist = Vector3.Distance(Camera.main.transform.position, hit.pose.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = hit;
                }
            }
            
            foundPos = closest.pose.position;
            foundRot = closest.pose.rotation;
            return true;
        }
        
        foundPos = Vector3.zero;
        foundRot = Quaternion.identity;
        return false;
    }

    bool TryGetDepthFromAPI(Vector2 screenPos, out float depth)
    {
        depth = 0;
        return false;
    }

    void LookAtCamera(GameObject go)
    {
        Camera cam = Camera.main;
        Vector3 lookDir = cam.transform.position - go.transform.position;
        lookDir.y = 0;
        
        if (lookDir != Vector3.zero)
            go.transform.rotation = Quaternion.LookRotation(-lookDir);
    }

    void CleanupDestroyedObjects()
    {
        spawnedObjects.RemoveAll(info => info.gameObject == null);
    }

    void ScaleObjectToRealSize(GameObject go, DetectedObject detectedObj, float distance)
    {
        if (detectedObj.bbox == null || detectedObj.bbox.Length < 4)
            return;

        Camera cam = Camera.main;
        float verticalFOV = cam.fieldOfView * Mathf.Deg2Rad;
        float horizontalFOV = 2f * Mathf.Atan(Mathf.Tan(verticalFOV / 2f) * cam.aspect);

        float realWidth = 2f * distance * Mathf.Tan(horizontalFOV / 2f) * detectedObj.Width;
        float realHeight = 2f * distance * Mathf.Tan(verticalFOV / 2f) * detectedObj.Height;

        realWidth = Mathf.Clamp(realWidth, minObjectSize, maxObjectSize);
        realHeight = Mathf.Clamp(realHeight, minObjectSize, maxObjectSize);

        Renderer renderer = go.GetComponentInChildren<Renderer>();
        if (renderer == null) return;

        Vector3 originalSize = renderer.bounds.size;
        float scaleFactorX = originalSize.x > 0 ? realWidth / originalSize.x : 1f;
        float scaleFactorY = originalSize.y > 0 ? realHeight / originalSize.y : 1f;
        float uniformScale = (scaleFactorX + scaleFactorY) / 2f;
        
        go.transform.localScale = Vector3.one * uniformScale;
    }

    GameObject GetPrefabForLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return null;
        label = label.ToLower();

        foreach (var map in mappings)
        {
            if (label.Contains(map.label.ToLower()))
                return map.prefab;
        }
        return null;
    }

    void RegisterSpawn(string label, GameObject go, Vector3 worldPos)
    {
        spawnedObjects.Add(new SpawnedObjectInfo
        {
            gameObject = go,
            label = label,
            worldPosition = worldPos,
            spawnTime = Time.time
        });
    }

    public void ClearAllSpawns()
    {
        foreach (var info in spawnedObjects)
        {
            if (info.gameObject != null)
                Destroy(info.gameObject);
        }
        spawnedObjects.Clear();
        thisFrameDetections.Clear();
        LastDetectedObjects.Clear();
        Debug.Log("Tüm objeler temizlendi.");
    }
    
    public int GetTotalSpawnCount()
    {
        CleanupDestroyedObjects();
        return spawnedObjects.Count;
    }
    
    public int GetSpawnCountForLabel(string label)
    {
        CleanupDestroyedObjects();
        string lowerLabel = label.ToLower();
        int count = 0;
        
        foreach (var info in spawnedObjects)
        {
            if (info.label.ToLower() == lowerLabel)
                count++;
        }
        return count;
    }

    public void ClearSpawnsForLabel(string label)
    {
        string lowerLabel = label.ToLower();
        
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedObjects[i].label.ToLower() == lowerLabel)
            {
                if (spawnedObjects[i].gameObject != null)
                    Destroy(spawnedObjects[i].gameObject);
                spawnedObjects.RemoveAt(i);
            }
        }
    }
}
