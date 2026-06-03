using UnityEngine;

/// <summary>
/// Legacy singleton shim. Eski ScreenSpaceOverlay UI'sı ARInventoryPanel + ARBagButton'a taşındı.
/// Bu sınıf yalnızca eski referans noktalarını canlı tutmak için var (Toggle/Open/Close çağıran kodlar).
/// </summary>
public class PokemonInventoryUI : MonoBehaviour
{
    public static PokemonInventoryUI Instance { get; private set; }

    bool isOpen = false;
    public bool IsOpen => isOpen;

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
        if (isOpen) CloseInventory();
        else OpenInventory();
    }

    public void OpenInventory()
    {
        isOpen = true;
        ARMON.UI.AR.ARInventoryPanel.Open(Camera.main);
    }

    public void CloseInventory()
    {
        isOpen = false;
        if (ARMON.UI.AR.ARInventoryPanel.Current != null)
            ARMON.UI.AR.ARInventoryPanel.Current.Close();
    }
}
