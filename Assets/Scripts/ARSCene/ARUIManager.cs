using UnityEngine;

public class ARUIManager : MonoBehaviour
{
    [Header("UI Elements - Direct AR Mode")]
    [Tooltip("UI elements shown ONLY in Direct AR mode")]
    public GameObject mapPanel;
    public GameObject stopDirectARButton;

    [Header("UI Elements - Navigation Mode")]
    [Tooltip("UI elements shown ONLY in Navigation mode")]
    public GameObject directionPanel;

    [Header("QR Scan Required Panel (Direct AR)")]
    [Tooltip("Panel shown in Direct AR before QR scan")]
    public GameObject scanQRRequiredPanel;
    public GameObject qrScanButton; // The scan QR button in ARSceneQRRecalibration

    [Header("TopPanel")]
    public GameObject topPanel; 

    [Header("Settings")]
    public bool enableDebugLogs = true;

    private enum ARMode { DirectAR, Navigation }
    private ARMode currentMode = ARMode.DirectAR;
    private bool hasScannedQRInDirectAR = false;

    void Start()
    {
        DetermineARMode();
        ConfigureUIForMode();
    }

    private void DetermineARMode()
    {
        string arModeString = PlayerPrefs.GetString("ARMode", "DirectAR");

        if (arModeString == "Navigation")
        {
            currentMode = ARMode.Navigation;
            if (enableDebugLogs)
                Debug.Log("[ARUIManager] Mode: AR Navigation");
        }
        else
        {
            currentMode = ARMode.DirectAR;
            if (enableDebugLogs)
                Debug.Log("[ARUIManager] Mode: Direct AR (Offline)");
        }
    }

    private void ConfigureUIForMode()
    {
        if (currentMode == ARMode.DirectAR)
        {
            // Direct AR Mode: Hide everything, show "Scan QR Required" panel
            if (!hasScannedQRInDirectAR)
            {
                // HIDE ALL UI - User must scan QR first
                SetUIElementActive(mapPanel, false);
                SetUIElementActive(stopDirectARButton, false);
                SetUIElementActive(directionPanel, false);
                SetUIElementActive(topPanel, false);

                // SHOW QR scan required panel
                SetUIElementActive(scanQRRequiredPanel, true);
                SetUIElementActive(qrScanButton, true);

                if (enableDebugLogs)
                    Debug.Log("🔒 Direct AR: Waiting for QR scan to begin...");
            }
            else
            {
                // QR has been scanned - show Direct AR UI
                ShowDirectARUI();
            }
        }
        else
        {
            // Navigation Mode: Show map, show directions, hide stop button
            SetUIElementActive(mapPanel, true);
            SetUIElementActive(directionPanel, true);
            SetUIElementActive(stopDirectARButton, false);
            SetUIElementActive(scanQRRequiredPanel, false);

            if (enableDebugLogs)
                Debug.Log("🧭 Navigation Mode UI configured");
        }
    }

    /// <summary>
    /// PUBLIC METHOD: Called by ARSceneQRRecalibration after user confirms QR scan
    /// Unlocks Direct AR mode and shows the UI
    /// </summary>
    public void OnQRScannedAndConfirmed()
    {
        if (currentMode == ARMode.DirectAR && !hasScannedQRInDirectAR)
        {
            hasScannedQRInDirectAR = true;

            // Hide QR required panel
            SetUIElementActive(scanQRRequiredPanel, false);

            // Show Direct AR UI
            ShowDirectARUI();

            if (enableDebugLogs)
                Debug.Log("✅ QR Scanned! Direct AR UI unlocked");
        }
    }

    /// <summary>
    /// Show Direct AR mode UI elements
    /// </summary>
    private void ShowDirectARUI()
    {
        SetUIElementActive(mapPanel, true);
        SetUIElementActive(stopDirectARButton, true);
        SetUIElementActive(directionPanel, false);
        SetUIElementActive(scanQRRequiredPanel, false);
        SetUIElementActive(topPanel, true);

        if (enableDebugLogs)
            Debug.Log("🗺️ Direct AR Mode UI shown");
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
        currentMode = ARMode.DirectAR;
        hasScannedQRInDirectAR = false; // Reset QR scan requirement
        PlayerPrefs.SetString("ARMode", "DirectAR");
        PlayerPrefs.Save();
        ConfigureUIForMode();

        if (enableDebugLogs)
            Debug.Log("Switched to Direct AR Mode");
    }

    public void SwitchToNavigationMode()
    {
        currentMode = ARMode.Navigation;
        PlayerPrefs.SetString("ARMode", "Navigation");
        PlayerPrefs.Save();
        ConfigureUIForMode();

        if (enableDebugLogs)
            Debug.Log("Switched to Navigation Mode");
    }

    public bool IsNavigationMode()
    {
        return currentMode == ARMode.Navigation;
    }

    public bool IsDirectARMode()
    {
        return currentMode == ARMode.DirectAR;
    }

    public bool HasScannedQR()
    {
        return hasScannedQRInDirectAR;
    }
}