using UnityEngine;

/// <summary>
/// Notch/çentik ve safe area desteği için yardımcı script.
/// Bu scripti safe area'ya uyması gereken panellere ekleyin.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaHandler : MonoBehaviour
{
    RectTransform rectTransform;
    Rect lastSafeArea = Rect.zero;
    
    [Tooltip("Sol kenar için safe area uygula")]
    public bool applyLeft = true;
    
    [Tooltip("Sağ kenar için safe area uygula")]
    public bool applyRight = true;
    
    [Tooltip("Üst kenar için safe area uygula (notch için)")]
    public bool applyTop = true;
    
    [Tooltip("Alt kenar için safe area uygula (home bar için)")]
    public bool applyBottom = true;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    void Update()
    {
        // Ekran döndüğünde veya safe area değiştiğinde güncelle
        if (lastSafeArea != Screen.safeArea)
        {
            ApplySafeArea();
        }
    }

    void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        lastSafeArea = safeArea;

        // Eğer safe area tüm ekranı kaplıyorsa bir şey yapma
        if (safeArea == new Rect(0, 0, Screen.width, Screen.height))
        {
            return;
        }

        // Anchor'ları hesapla
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        // Seçili kenarlara uygula
        if (!applyLeft) anchorMin.x = rectTransform.anchorMin.x;
        if (!applyRight) anchorMax.x = rectTransform.anchorMax.x;
        if (!applyBottom) anchorMin.y = rectTransform.anchorMin.y;
        if (!applyTop) anchorMax.y = rectTransform.anchorMax.y;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;

        Debug.Log($"Safe Area uygulandı: {safeArea}");
    }

    /// <summary>
    /// Statik yardımcı: Verilen pozisyonu safe area içine al
    /// </summary>
    public static Vector2 GetSafePosition(Vector2 originalPos, RectTransform.Edge edge, float padding = 0)
    {
        Rect safeArea = Screen.safeArea;
        Vector2 result = originalPos;

        switch (edge)
        {
            case RectTransform.Edge.Top:
                float topInset = Screen.height - (safeArea.y + safeArea.height);
                result.y -= (topInset + padding);
                break;
            case RectTransform.Edge.Bottom:
                result.y += (safeArea.y + padding);
                break;
            case RectTransform.Edge.Left:
                result.x += (safeArea.x + padding);
                break;
            case RectTransform.Edge.Right:
                float rightInset = Screen.width - (safeArea.x + safeArea.width);
                result.x -= (rightInset + padding);
                break;
        }

        return result;
    }

    /// <summary>
    /// Safe area padding değerlerini al
    /// </summary>
    public static RectOffset GetSafeAreaPadding()
    {
        Rect safeArea = Screen.safeArea;
        
        int left = Mathf.RoundToInt(safeArea.x);
        int right = Mathf.RoundToInt(Screen.width - safeArea.x - safeArea.width);
        int bottom = Mathf.RoundToInt(safeArea.y);
        int top = Mathf.RoundToInt(Screen.height - safeArea.y - safeArea.height);
        
        return new RectOffset(left, right, top, bottom);
    }
}
