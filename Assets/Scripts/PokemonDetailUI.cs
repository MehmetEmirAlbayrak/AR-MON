using UnityEngine;

/// <summary>
/// Legacy singleton shim. Eski ScreenSpaceOverlay detay UI'sı ARDetailSubPanel'e taşındı.
/// Bu sınıf yalnızca eski PokemonDetailUI.Instance.Open(index) çağrılarını canlı tutmak için var.
/// </summary>
public class PokemonDetailUI : MonoBehaviour
{
    public static PokemonDetailUI Instance { get; private set; }

    public bool IsOpen { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Eski API: index ile detay paneli açar. ARDetailSubPanel'e delege eder.</summary>
    public void Open(int index)
    {
        IsOpen = true;
        if (ARMON.UI.AR.ARInventoryPanel.Current != null)
            ARMON.UI.AR.ARDetailSubPanel.OpenBeside(ARMON.UI.AR.ARInventoryPanel.Current, index);
    }

    public void Close()
    {
        IsOpen = false;
    }
}
