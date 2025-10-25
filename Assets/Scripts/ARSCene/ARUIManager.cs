using UnityEngine;

public class ARUIManager : MonoBehaviour
{
    [Header( "UI Elements - Direct AR Mode" )]
    [Tooltip( "UI elements shown ONLY in Direct AR mode" )]
    public GameObject mapPanel;
    public GameObject stopDirectARButton;

    [Header( "UI Elements - Navigation Mode" )]
    [Tooltip( "UI elements shown ONLY in Navigation mode" )]
    public GameObject directionPanel;

    [Header( "Settings" )]
    public bool enableDebugLogs = true;

    private enum ARMode { DirectAR, Navigation }
    private ARMode currentMode = ARMode.DirectAR;

    void Start()
    {
        stopDirectARButton.SetActive(false);
        DetermineARMode();

        ConfigureUIForMode();
    }

    private void DetermineARMode()
    {
        string arModeString = PlayerPrefs.GetString( "ARMode");

        if ( arModeString == "Navigation" ) {
            currentMode = ARMode.Navigation;
        } else {
            currentMode = ARMode.DirectAR;
        }
    }

    private void ConfigureUIForMode()
    {
        if (currentMode == ARMode.DirectAR)
        {
            // Direct AR Mode: Show map, show stop button, hide directions
            SetUIElementActive(mapPanel, true);
            SetUIElementActive(stopDirectARButton, true);
            SetUIElementActive(directionPanel, false);

        }
        else
        {
            // Navigation Mode: Show map, show directions, hide stop button
            SetUIElementActive(mapPanel, true);
            SetUIElementActive(directionPanel, true);
            SetUIElementActive(stopDirectARButton, false);
        }
    }

    private void SetUIElementActive( GameObject uiElement, bool active )
    {
        if ( uiElement != null ) {
            uiElement.SetActive( active );
        }
    }

    public void SwitchToDirectARMode()
    {
        currentMode = ARMode.DirectAR;
        PlayerPrefs.SetString( "ARMode", "DirectAR" );
        PlayerPrefs.Save();
        ConfigureUIForMode();
    }

    public void SwitchToNavigationMode()
    {
        currentMode = ARMode.Navigation;
        PlayerPrefs.SetString( "ARMode", "Navigation" );
        PlayerPrefs.Save();
        ConfigureUIForMode();
    }

    public bool IsNavigationMode()
    {
        return currentMode == ARMode.Navigation;
    }

    public bool IsDirectARMode()
    {
        return currentMode == ARMode.DirectAR;
    }
}