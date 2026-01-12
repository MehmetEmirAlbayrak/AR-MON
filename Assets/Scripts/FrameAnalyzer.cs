using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class FrameAnalyzer : MonoBehaviour
{
    [Header("Bağlantılar")]
    public BiomePokemonSpawner biomeSpawner;
    public ObjectSwapManager objectSwapManager;
    public ServerConfig config;

    public DetectionInfoUI detectionUI;

    [Header("Analiz Ayarları")]
    public float analyzeInterval = 1.0f;   // kaç saniyede bir analiz
    public int jpgQuality = 75;            // 0-100

    private float timer = 0f;
    private bool isSending = false;


    string AnalyzeUrl => config.GetAnalyzeUrl();

    void Update()
    {
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

        // Frame tamamen çizilsin diye
        yield return new WaitForEndOfFrame();

        // Ekran görüntüsü al
        Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
        if (tex == null)
        {
            Debug.LogError("Screenshot alınamadı");
            isSending = false;
            yield break;
        }

        // JPG'e çevir
        byte[] jpg = tex.EncodeToJPG(jpgQuality);
        Object.Destroy(tex);

        // Server'a gönder
        yield return StartCoroutine(SendFrame(jpg));

        isSending = false;
    }

    public IEnumerator SendFrame(byte[] jpg)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("image", jpg, "frame.jpg", "image/jpeg");

        using (UnityWebRequest req = UnityWebRequest.Post(AnalyzeUrl, form))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                //Debug.LogError("Analyze failed: " + req.error);
                yield break;
            }

            string json = req.downloadHandler.text;
            //Debug.Log("Server JSON: " + json);

            AnalyzeResult result = JsonUtility.FromJson<AnalyzeResult>(json);
            if (result == null)
            {
                Debug.LogError("AnalyzeResult parse fail");
                yield break;
            }

            // Debug: Sonuçları logla
            Debug.Log($"Analiz sonucu - Biome: {result.biome}, Obje sayısı: {result.objects?.Length ?? 0}");

            // Biome işle
            if (biomeSpawner != null)
                biomeSpawner.PlacePokemonForBiome(result.biome);

            // Object işle
            if (objectSwapManager != null && result.objects != null)
                objectSwapManager.PlaceObjects(result.objects.ToList());

            // UI güncelle (singleton kullan, eğer referans atanmamışsa)
            DetectionInfoUI ui = detectionUI != null ? detectionUI : DetectionInfoUI.Instance;
            if (ui != null)
            {
                ui.UpdateBiome(result.biome);
                ui.UpdateDetectedObjects(result.objects?.ToList());
                Debug.Log($"UI güncellendi - Biome: {result.biome}, Objeler: {result.objects?.Length ?? 0}");
            }
            else
            {
                Debug.LogWarning("DetectionInfoUI bulunamadı! UI güncellenemiyor. Scene'de DetectionInfoUI component'i var mı kontrol edin.");
            }
            
        }
    }
}