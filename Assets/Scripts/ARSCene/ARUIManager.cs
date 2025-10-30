using UnityEngine;

public class ARUIManager : MonoBehaviour
{
    [Header("UI Elements - Direct AR Mode")]
    public GameObject mapPanel;
    public GameObject stopDirectARButton;

    [Header("UI Elements - Navigation Mode")]
    public GameObject directionPanel;

    [Header("Shared UI Elements")]
    public GameObject topPanel;

    [Header("Settings")]
    public bool enableDebugLogs = true;

    public enum ARMode { DirectAR, Navigation }

    private ARMode currentARMode = ARMode.DirectAR;


    void Start()
    {
        DetermineARMode();
        ConfigureUIForMode();
    }

    private void DetermineARMode()
    {
        string arModeString = PlayerPrefs.GetString("ARMode", "DirectAR");
        currentARMode = arModeString == "Navigation" ? ARMode.Navigation : ARMode.DirectAR;

        if (enableDebugLogs)
        {
            Debug.Log($"ARUIManager: Current AR Mode = {currentARMode}");
        }
    }

    private void ConfigureUIForMode()
    {
        if (currentARMode == ARMode.DirectAR)
        {
            // Direct AR Mode - Show map and stop button, hide direction panel
            SetUIElementActive(mapPanel, true);
            SetUIElementActive(stopDirectARButton, true);
            SetUIElementActive(directionPanel, false);
            SetUIElementActive(topPanel, true);

            if (enableDebugLogs)
            {
                Debug.Log("ARUIManager: Configured for Direct AR Mode");
            }
        }
        else if (currentARMode == ARMode.Navigation)
        {
            // Navigation Mode - Show map and direction panel, hide stop button
            SetUIElementActive(mapPanel, true);
            SetUIElementActive(directionPanel, true);
            SetUIElementActive(stopDirectARButton, false);
            SetUIElementActive(topPanel, true);

            if (enableDebugLogs)
            {
                Debug.Log("ARUIManager: Configured for Navigation Mode");
            }
        }
    }

    private void SetUIElementActive(GameObject uiElement, bool active)
    {
        if (uiElement != null)
        {
            uiElement.SetActive(active);
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning($"ARUIManager: UI Element is null, cannot set active to {active}");
        }
    }

    public void SwitchToDirectARMode()
    {
        currentARMode = ARMode.DirectAR;
        PlayerPrefs.SetString("ARMode", "DirectAR");
        PlayerPrefs.Save();
        ConfigureUIForMode();

        if (enableDebugLogs)
        {
            Debug.Log("ARUIManager: Switched to Direct AR Mode");
        }
    }

    public void SwitchToNavigationMode()
    {
        currentARMode = ARMode.Navigation;
        PlayerPrefs.SetString("ARMode", "Navigation");
        PlayerPrefs.Save();
        ConfigureUIForMode();

        if (enableDebugLogs)
        {
            Debug.Log("ARUIManager: Switched to Navigation Mode");
        }
    }

    public bool IsNavigationMode()
    {
        return currentARMode == ARMode.Navigation;
    }

    public bool IsDirectARMode()
    {
        return currentARMode == ARMode.DirectAR;
    }

    public ARMode GetCurrentARMode()
    {
        return currentARMode;
    }
}