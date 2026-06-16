using UnityEngine;

/// <summary>
/// Legacy singleton shim. Eski ScreenSpaceOverlay UI'sı ARInventoryPanel + ARBagButton'a taşındı.
/// Bu sınıf yalnızca eski referans noktalarını canlı tutmak için var (Toggle/Open/Close çağıran kodlar).
/// </summary>
public class PokemonInventoryUI : MonoBehaviour
{
    public static PokemonInventoryUI Instance { get; private set; }

    // Gerçek panel durumundan türetilir — panel kendi KAPAT butonuyla kapatıldığında
    // burada tutulan ayrı bir bool stale kalıyordu (throw/saldırı girişi kalıcı kilitleniyordu).
    public bool IsOpen => ARMON.UI.AR.ARInventoryPanel.Current != null;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ToggleInventory()
    {
        if (IsOpen) CloseInventory();
        else OpenInventory();
    }

    public void OpenInventory()
    {
        ARMON.UI.AR.ARInventoryPanel.Open(Camera.main);
    }

    public void CloseInventory()
    {
        if (ARMON.UI.AR.ARInventoryPanel.Current != null)
            ARMON.UI.AR.ARInventoryPanel.Current.Close();
    }
}
