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

    private ARModeHelper.ARMode currentARMode;


    void Start()
    {
        DetermineARMode();
        ConfigureUIForMode();
    }
    private void DetermineARMode()
    {
        currentARMode = ARModeHelper.GetCurrentARMode();
    }

    private void ConfigureUIForMode()
    {
        if (currentARMode == ARModeHelper.ARMode.DirectAR)
        {
            SetUIElementActive(mapPanel, true);
            SetUIElementActive(stopDirectARButton, true);
            SetUIElementActive(directionPanel, false);
            SetUIElementActive(topPanel, false);
        }
        else if (currentARMode == ARModeHelper.ARMode.Navigation)
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
}