using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.ARFoundation;

namespace ARMON.UI.AR
{
    /// <summary>
    /// HUD durum bileşeni (TopRightSlot):
    ///   - Analiz göstergesi: bir kare server'da işlenirken yanıp sönen "● Analiz" satırı
    ///   - Spawn cooldown çubuğu: bir sonraki Pokemon spawn'ına kalan süre
    ///   - Zemin ipucu: tracking var ama 5 sn boyunca hiç plane yoksa
    ///     "Kamerayı düz bir zemine doğrult" uyarısı
    /// FrameAnalyzer.Start() içinden EnsureExists() ile kendi kendini kurar — scene
    /// değişikliği gerektirmez. Singleton.
    /// </summary>
    public class ScanStatusUI : MonoBehaviour
    {
        public static ScanStatusUI Instance { get; private set; }

        const float NoPlaneHintDelay = 5f;     // saniye — plane yokken ipucu gösterme gecikmesi
        const float GroundToastCooldown = 8f;  // saniye — "zemin gerekli" toast throttle

        TextMeshProUGUI statusLabel;
        RectTransform cooldownFillRect;
        TextMeshProUGUI hintLabel;
        CanvasGroup hintGroup;

        BiomePokemonSpawner spawner;
        ARPlaneManager planeManager;

        float _trackingSince = -1f;
        static float _lastGroundToastTime = -999f;

        /// <summary>Sahnede yoksa oluştur (idempotent).</summary>
        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("ScanStatusUI");
            go.AddComponent<ScanStatusUI>();
        }

        /// <summary>
        /// Spawn zemin doğrulamasından döndüğünde çağrılır (throttle'lı kullanıcı uyarısı).
        /// </summary>
        public static void NotifyGroundRequired()
        {
            if (Time.unscaledTime - _lastGroundToastTime < GroundToastCooldown) return;
            _lastGroundToastTime = Time.unscaledTime;
            ToastManager.Show("Spawn için zemin gerekli — kamerayı yere doğrult");
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            // HUD geç kurulabilir — hazır olana kadar her frame dene (ucuz null check).
            if (statusLabel == null && !TryBuild()) return;

            UpdateStatusLine();
            UpdatePlaneHint();
        }

        bool TryBuild()
        {
            if (ARHudCanvas.Instance == null) return false;

            // === Durum paneli (sağ üst) ===
            GameObject panel = new GameObject("ScanStatusPanel", typeof(RectTransform));
            panel.transform.SetParent(ARHudCanvas.Instance.TopRightSlot, false);
            var img = panel.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.45f);
            img.raycastTarget = false;
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = new Vector2(1f, 1f);
            prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot     = new Vector2(1f, 1f);
            prt.anchoredPosition = Vector2.zero;
            prt.sizeDelta = new Vector2(230f, 64f);

            GameObject labelObj = new GameObject("StatusLabel", typeof(RectTransform));
            labelObj.transform.SetParent(panel.transform, false);
            statusLabel = labelObj.AddComponent<TextMeshProUGUI>();
            statusLabel.fontSize = 20;
            statusLabel.fontStyle = FontStyles.Bold;
            statusLabel.alignment = TextAlignmentOptions.Center;
            statusLabel.color = Color.white;
            statusLabel.raycastTarget = false;
            var lrt = (RectTransform)labelObj.transform;
            lrt.anchorMin = new Vector2(0f, 0.35f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(8f, 0f);
            lrt.offsetMax = new Vector2(-8f, -4f);

            // Cooldown bar arka planı
            GameObject barBg = new GameObject("CooldownBarBG", typeof(RectTransform));
            barBg.transform.SetParent(panel.transform, false);
            var bgImg = barBg.AddComponent<Image>();
            bgImg.color = new Color(1f, 1f, 1f, 0.15f);
            bgImg.raycastTarget = false;
            var brt = (RectTransform)barBg.transform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0.3f);
            brt.offsetMin = new Vector2(8f, 6f);
            brt.offsetMax = new Vector2(-8f, 0f);

            // Cooldown bar dolgusu (anchorMax.x ile ölçeklenir — sprite gerektirmez)
            GameObject fill = new GameObject("CooldownFill", typeof(RectTransform));
            fill.transform.SetParent(barBg.transform, false);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.35f, 0.85f, 0.45f, 0.9f);
            fillImg.raycastTarget = false;
            cooldownFillRect = (RectTransform)fill.transform;
            cooldownFillRect.anchorMin = Vector2.zero;
            cooldownFillRect.anchorMax = new Vector2(1f, 1f);
            cooldownFillRect.offsetMin = Vector2.zero;
            cooldownFillRect.offsetMax = Vector2.zero;

            // === Zemin ipucu (ekran ortası üstü) ===
            GameObject hintObj = new GameObject("PlaneHint", typeof(RectTransform));
            hintObj.transform.SetParent(ARHudCanvas.Instance.transform, false);
            hintGroup = hintObj.AddComponent<CanvasGroup>();
            hintGroup.alpha = 0f;
            hintGroup.interactable = false;
            hintGroup.blocksRaycasts = false;
            hintLabel = hintObj.AddComponent<TextMeshProUGUI>();
            hintLabel.text = "Kamerayı yavaşça düz bir zemine doğrult";
            hintLabel.fontSize = 26;
            hintLabel.fontStyle = FontStyles.Bold;
            hintLabel.alignment = TextAlignmentOptions.Center;
            hintLabel.color = new Color(1f, 0.95f, 0.7f, 1f);
            hintLabel.outlineWidth = 0.25f;
            hintLabel.outlineColor = Color.black;
            hintLabel.raycastTarget = false;
            var hrt = (RectTransform)hintObj.transform;
            hrt.anchorMin = new Vector2(0.1f, 0.62f);
            hrt.anchorMax = new Vector2(0.9f, 0.72f);
            hrt.offsetMin = Vector2.zero;
            hrt.offsetMax = Vector2.zero;

            return true;
        }

        void UpdateStatusLine()
        {
            if (spawner == null) spawner = FindFirstObjectByType<BiomePokemonSpawner>();

            bool sending = FrameAnalyzer.Instance != null && FrameAnalyzer.Instance.IsSending;
            float remaining = spawner != null ? spawner.RemainingCooldown : 0f;
            float total = spawner != null ? Mathf.Max(0.01f, spawner.spawnCooldown) : 1f;

            if (sending)
            {
                float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 5f));
                statusLabel.text = "<color=#7FD4FF>● Analiz ediliyor...</color>";
                statusLabel.alpha = pulse;
            }
            else if (ARSession.state != ARSessionState.SessionTracking)
            {
                statusLabel.text = "<color=#FFB37F>AR takip bekleniyor</color>";
                statusLabel.alpha = 1f;
            }
            else if (remaining > 0.05f)
            {
                statusLabel.text = $"Sonraki spawn: {Mathf.CeilToInt(remaining)}s";
                statusLabel.alpha = 1f;
            }
            else
            {
                statusLabel.text = "<color=#9FE89F>Spawn hazır</color>";
                statusLabel.alpha = 1f;
            }

            float progress = 1f - Mathf.Clamp01(remaining / total);
            cooldownFillRect.anchorMax = new Vector2(Mathf.Max(0.001f, progress), 1f);
        }

        void UpdatePlaneHint()
        {
            if (planeManager == null) planeManager = FindFirstObjectByType<ARPlaneManager>();

            bool tracking = ARSession.state == ARSessionState.SessionTracking;
            if (!tracking)
            {
                _trackingSince = -1f;
            }
            else if (_trackingSince < 0f)
            {
                _trackingSince = Time.time;
            }

            bool noPlanes = planeManager == null || planeManager.trackables.count == 0;
            bool show = tracking && noPlanes
                        && _trackingSince >= 0f
                        && (Time.time - _trackingSince) > NoPlaneHintDelay;

            hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, show ? 1f : 0f, Time.deltaTime * 3f);
        }
    }
}
