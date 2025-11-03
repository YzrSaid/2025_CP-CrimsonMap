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
    }

    private void ConfigureUIForMode()
    {
        if (currentARMode == ARMode.DirectAR)
        {
            SetUIElementActive(mapPanel, true);
            SetUIElementActive(stopDirectARButton, true);
            SetUIElementActive(directionPanel, false);
            SetUIElementActive(topPanel, true);
        }
        else if (currentARMode == ARMode.Navigation)
        {
            SetUIElementActive(mapPanel, true);
            SetUIElementActive(directionPanel, true);
            SetUIElementActive(stopDirectARButton, false);
            SetUIElementActive(topPanel, true);
        }
    }

    private void SetUIElementActive(GameObject uiElement, bool active)
    {
        if (uiElement != null)
        {
            uiElement.SetActive(active);
        }
    }

    public void SwitchToDirectARMode()
    {
        currentARMode = ARMode.DirectAR;
        PlayerPrefs.SetString("ARMode", "DirectAR");
        PlayerPrefs.Save();
        ConfigureUIForMode();
    }

    public void SwitchToNavigationMode()
    {
        currentARMode = ARMode.Navigation;
        PlayerPrefs.SetString("ARMode", "Navigation");
        PlayerPrefs.Save();
        ConfigureUIForMode();
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