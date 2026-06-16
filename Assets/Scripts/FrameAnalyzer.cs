using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;

public class FrameAnalyzer : MonoBehaviour
{
    public static FrameAnalyzer Instance { get; private set; }

    [Header("Bağlantılar")]
    public BiomePokemonSpawner biomeSpawner;
    public ObjectSwapManager objectSwapManager;
    public ServerConfig config;

    public DetectionInfoUI detectionUI;

    [Header("Analiz Ayarları")]
    [Tooltip("İki istek arası minimum süre (saniye). Request flood'u önler.")]
    public float analyzeInterval = 1.5f;
    public int jpgQuality = 75;            // 0-100
    [Tooltip("İstek zaman aşımı (saniye). Süre dolunca istek iptal edilir ve pipeline kilitlenmez.")]
    public int requestTimeoutSeconds = 5;
    [Tooltip("Bu biome güveninin altında spawn tetikleme (0 = kapalı). Stabilite için.")]
    public float minBiomeConfidence = 0.25f;

    [Header("Temiz Kare Yakalama")]
    [Tooltip("Analiz için kullanılan kamera (boşsa Camera.main).")]
    public Camera arCamera;
    [Tooltip("AR kamera arka planı (boşsa kameradan bulunur). Cihazda temiz feed için kullanılır.")]
    public ARCameraBackground arCameraBackground;
    [Tooltip("Gönderilen görüntünün uzun kenarı (px). Daha yüksek = daha çok detay (özellikle küçük taşlar), ama daha çok bant genişliği. Server tarafında YOLO_IMGSZ ile birlikte artırılmalı (yoksa server küçültür).")]
    public int captureLongSide = 1280;

    // Cihaz başına uygulama oturumu kimliği — server'daki temporal smoothing buffer'ı
    // diğer cihazlardan izole tutar. Her app açılışında yenilenir.
    static readonly string SessionId = System.Guid.NewGuid().ToString("N");

    private RenderTexture _captureRT;
    private float timer = 0f;
    private bool isSending = false;
    private float _lastErrorToastTime = -999f;
    const float ErrorToastCooldown = 10f; // saniye — toast spam'ini önler

    /// <summary>Şu anda bir kare server'da işleniyor mu? (UI göstergesi için)</summary>
    public bool IsSending => isSending;

    string AnalyzeUrl => config.GetAnalyzeUrl();

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        // Tarama göstergesi + zemin ipucu + spawn cooldown HUD'u (kendi kendini kurar).
        ARMON.UI.AR.ScanStatusUI.EnsureExists();
    }

    void Update()
    {
        // AR oturumu izlemeye başlamadan analiz etme — tracking yoksa spawn'lar havada kalır.
        if (ARSession.state != ARSessionState.SessionTracking) return;

        // Otomatik mod: belli aralıklarla kare yakala
        timer += Time.deltaTime;

        if (timer >= analyzeInterval && !isSending)
        {
            timer = 0f;
            StartCoroutine(CaptureAndAnalyze());
        }
    }

    /// <summary>
    /// İstersen UI butonundan da çağırabilirsin.
    /// </summary>
    public void TriggerOneShot()
    {
        if (!isSending)
            StartCoroutine(CaptureAndAnalyze());
    }

    private IEnumerator CaptureAndAnalyze()
    {
        isSending = true;
        // try/finally: SendFrame içinde ne olursa olsun isSending kilidi MUTLAKA açılır.
        // Aksi halde tek bir exception tüm analiz pipeline'ını kalıcı dondurur.
        try
        {
            // Frame tamamen çizilsin diye
            yield return new WaitForEndOfFrame();

            // Temiz kamera karesi (spawn edilen monster/obje ve UI hariç)
            byte[] jpg = CaptureCleanJpg();
            if (jpg == null)
            {
                Debug.LogError("Temiz kare alınamadı");
                yield break;
            }

            // Server'a gönder
            yield return StartCoroutine(SendFrame(jpg));
        }
        finally
        {
            isSending = false;
        }
    }

    /// <summary>
    /// Sınıflandırıcıya gönderilecek TEMİZ kareyi üretir.
    /// Cihazda yalnızca AR kamera arka planını (gerçek kamera feed'i) bir RenderTexture'a
    /// blit eder — sahnedeki hiçbir geometri/UI çizilmediği için spawn edilen monster,
    /// obje, isim etiketi veya UI asla görüntüye girmez. Arka plan materyali ekran
    /// dönüşümünü uyguladığından sonuç ekranla hizalıdır ve server koordinatları eşleşir.
    /// Editörde (XR-Sim feed'i offscreen vermediği için) ekran görüntüsüne düşülür.
    /// </summary>
    private byte[] CaptureCleanJpg()
    {
        Camera cam = arCamera != null ? arCamera : Camera.main;
        if (cam == null) return null;

        // Editör (XR-Sim) kamera feed'ini offscreen capture'a vermediği için ekran
        // görüntüsüne düşeriz (içerikli, yalnızca editör testi). Cihazda temiz feed çalışır.
        if (Application.isEditor)
            return CaptureViaScreen();

        var bg = arCameraBackground != null
            ? arCameraBackground
            : cam.GetComponent<ARCameraBackground>();
        if (bg == null || bg.material == null)
        {
            Debug.LogWarning("ARCameraBackground/material yok — ekran görüntüsüne düşülüyor.");
            return CaptureViaScreen();
        }

        int sw = Screen.width, sh = Screen.height;
        if (sw <= 0 || sh <= 0) return null;
        int longSide = Mathf.Max(sw, sh);
        float scale = captureLongSide > 0 ? Mathf.Min(1f, (float)captureLongSide / longSide) : 1f;
        int rw = Mathf.Max(16, Mathf.RoundToInt(sw * scale));
        int rh = Mathf.Max(16, Mathf.RoundToInt(sh * scale));

        if (_captureRT == null || _captureRT.width != rw || _captureRT.height != rh)
        {
            if (_captureRT != null) _captureRT.Release();
            _captureRT = new RenderTexture(rw, rh, 0, RenderTextureFormat.Default);
            _captureRT.Create();
        }

        RenderTexture prevActive = RenderTexture.active;
        Graphics.Blit(null, _captureRT, bg.material); // yalnızca kamera feed'i
        RenderTexture.active = _captureRT;
        Texture2D tex = new Texture2D(rw, rh, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rw, rh), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;

        byte[] jpg = tex.EncodeToJPG(jpgQuality);
        Destroy(tex);
        return jpg;
    }

    /// <summary>
    /// Editör/yedek: tüm ekranı yakalar (içerik dahil).
    /// captureLongSide'a göre küçültür — aksi halde editörde tam çözünürlük (ör. 1920x1080)
    /// JPEG'ler gönderilir ve bant genişliği/encode süresi cihaz yolundan kat kat büyük olur.
    /// </summary>
    private byte[] CaptureViaScreen()
    {
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        if (shot == null) return null;

        int longSide = Mathf.Max(shot.width, shot.height);
        if (captureLongSide > 0 && longSide > captureLongSide)
        {
            float scale = (float)captureLongSide / longSide;
            int rw = Mathf.Max(16, Mathf.RoundToInt(shot.width * scale));
            int rh = Mathf.Max(16, Mathf.RoundToInt(shot.height * scale));

            RenderTexture rt = RenderTexture.GetTemporary(rw, rh, 0);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(shot, rt);
            RenderTexture.active = rt;
            Texture2D small = new Texture2D(rw, rh, TextureFormat.RGB24, false);
            small.ReadPixels(new Rect(0, 0, rw, rh), 0, 0);
            small.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(shot);

            byte[] scaledJpg = small.EncodeToJPG(jpgQuality);
            Destroy(small);
            return scaledJpg;
        }

        byte[] jpg = shot.EncodeToJPG(jpgQuality);
        Destroy(shot);
        return jpg;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_captureRT != null) { _captureRT.Release(); _captureRT = null; }
    }

    public IEnumerator SendFrame(byte[] jpg)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("image", jpg, "frame.jpg", "image/jpeg");
        form.AddField("session_id", SessionId);

        using (UnityWebRequest req = UnityWebRequest.Post(AnalyzeUrl, form))
        {
            req.timeout = Mathf.Max(1, requestTimeoutSeconds);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[FrameAnalyzer] Analyze isteği başarısız: {req.result} — {req.error}");
                // Kullanıcıya throttle'lı geri bildirim (her hatada değil, 10 sn'de bir)
                if (Time.unscaledTime - _lastErrorToastTime > ErrorToastCooldown)
                {
                    _lastErrorToastTime = Time.unscaledTime;
                    ARMON.UI.AR.ToastManager.Show("Sunucuya ulaşılamıyor — IP ayarlarını kontrol et");
                }
                yield break;
            }

            string json = req.downloadHandler.text;

            AnalyzeResult result = null;
            try
            {
                result = JsonUtility.FromJson<AnalyzeResult>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FrameAnalyzer] AnalyzeResult parse hatası: {e.Message}\nJSON: {json}");
            }
            if (result == null) yield break;

            Debug.Log($"Analiz sonucu - Biome: {result.biome} ({result.biome_confidence:P0}), Obje sayısı: {result.objects?.Length ?? 0}");

            // Biome işle — düşük güvenli sonuçlarda spawn etme (stabilite).
            if (biomeSpawner != null && result.biome_confidence >= minBiomeConfidence)
                biomeSpawner.PlacePokemonForBiome(result.biome);

            // Object işle
            if (objectSwapManager != null && result.objects != null)
                objectSwapManager.PlaceObjects(result.objects.ToList());

            // UI güncelle (singleton kullan, eğer referans atanmamışsa)
            DetectionInfoUI ui = detectionUI != null ? detectionUI : DetectionInfoUI.Instance;
            if (ui != null)
            {
                ui.UpdateBiome(result.biome, result.biome_confidence);
                ui.UpdateDetectedObjects(result.objects?.ToList());
            }
            else
            {
                Debug.LogWarning("DetectionInfoUI bulunamadı! UI güncellenemiyor. Scene'de DetectionInfoUI component'i var mı kontrol edin.");
            }
        }
    }
}