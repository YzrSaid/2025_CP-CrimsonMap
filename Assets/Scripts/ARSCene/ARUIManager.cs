using UnityEngine;

/// <summary>
/// AR UI Manager - Handles UI visibility for 4 AR combinations:
/// 1. Direct AR + GPS → Show all UI immediately
/// 2. Direct AR + Offline → Hide all, show QR scan required panel
/// 3. AR Navigation + GPS → Show navigation UI immediately
/// 4. AR Navigation + Offline → Show navigation UI immediately
/// </summary>
public class ARUIManager : MonoBehaviour
{
    [Header("UI Elements - Direct AR Mode")]
    [Tooltip("UI elements shown ONLY in Direct AR mode")]
    public GameObject mapPanel;
    public GameObject stopDirectARButton;

    [Header("UI Elements - Navigation Mode")]
    [Tooltip("UI elements shown ONLY in Navigation mode")]
    public GameObject directionPanel;

    [Header("QR Scan Required Panel (Direct AR + Offline ONLY)")]
    [Tooltip("Panel shown ONLY in Direct AR + Offline before first QR scan")]
    public GameObject scanQRRequiredPanel;
    public GameObject qrScanButton;

    [Header("Shared UI Elements")]
    public GameObject topPanel;

    [Header("Settings")]
    public bool enableDebugLogs = true;

    private enum ARMode { DirectAR, Navigation }
    private enum LocalizationMode { GPS, Offline }
    
    private ARMode currentARMode = ARMode.DirectAR;
    private LocalizationMode currentLocalizationMode = LocalizationMode.GPS;
    private bool hasScannedQRInDirectAROffline = false;

    void Start()
    {
        DetermineModes();
        ConfigureUIForModes();
    }

    private void DetermineModes()
    {
        // Determine AR Mode
        string arModeString = PlayerPrefs.GetString("ARMode");
        currentARMode = arModeString == "Navigation" ? ARMode.Navigation : ARMode.DirectAR;

        // Determine Localization Mode
        string localizationModeString = PlayerPrefs.GetString("LocalizationMode");
        currentLocalizationMode = localizationModeString == "Offline" ? LocalizationMode.Offline : LocalizationMode.GPS;

        if (enableDebugLogs)
            Debug.Log($"[ARUIManager] AR Mode: {currentARMode}, Localization: {currentLocalizationMode}");
    }

    private void ConfigureUIForModes()
    {
        // ════════════════════════════════════════════════════════════
        // MODE 1: Direct AR + GPS
        // ════════════════════════════════════════════════════════════
        if (currentARMode == ARMode.DirectAR && currentLocalizationMode == LocalizationMode.GPS)
        {
            // Show Direct AR UI immediately (no QR scan required for GPS mode)
            SetUIElementActive(mapPanel, true);
            SetUIElementActive(stopDirectARButton, true);
            SetUIElementActive(directionPanel, false);
            SetUIElementActive(scanQRRequiredPanel, false);
            SetUIElementActive(topPanel, true);

            if (enableDebugLogs)
                Debug.Log("✅ Direct AR + GPS: UI shown immediately");
        }
        // ════════════════════════════════════════════════════════════
        // MODE 2: Direct AR + Offline (X,Y)
        // ════════════════════════════════════════════════════════════
        else if (currentARMode == ARMode.DirectAR && currentLocalizationMode == LocalizationMode.Offline)
        {
            if (!hasScannedQRInDirectAROffline)
            {
                // HIDE ALL UI - User must scan QR first in Offline mode
                SetUIElementActive(mapPanel, false);
                SetUIElementActive(stopDirectARButton, false);
                SetUIElementActive(directionPanel, false);
                SetUIElementActive(topPanel, false);

                // SHOW QR scan required panel
                SetUIElementActive(scanQRRequiredPanel, true);
                SetUIElementActive(qrScanButton, true);

                if (enableDebugLogs)
                    Debug.Log("🔒 Direct AR + Offline: Waiting for QR scan to begin...");
            }
            else
            {
                // QR has been scanned - show Direct AR UI
                ShowDirectARUI();
            }
        }
        // ════════════════════════════════════════════════════════════
        // MODE 3: AR Navigation + GPS
        // ════════════════════════════════════════════════════════════
        else if (currentARMode == ARMode.Navigation && currentLocalizationMode == LocalizationMode.GPS)
        {
            // Show Navigation UI immediately
            SetUIElementActive(mapPanel, true);
            SetUIElementActive(directionPanel, true);
            SetUIElementActive(stopDirectARButton, false);
            SetUIElementActive(scanQRRequiredPanel, false);
            SetUIElementActive(topPanel, true);

            if (enableDebugLogs)
                Debug.Log("🧭 AR Navigation + GPS: UI shown immediately");
        }
        // ════════════════════════════════════════════════════════════
        // MODE 4: AR Navigation + Offline (X,Y)
        // ════════════════════════════════════════════════════════════
        else if (currentARMode == ARMode.Navigation && currentLocalizationMode == LocalizationMode.Offline)
        {
            // Show Navigation UI immediately (Navigation mode doesn't require QR scan in Offline)
            SetUIElementActive(mapPanel, true);
            SetUIElementActive(directionPanel, true);
            SetUIElementActive(stopDirectARButton, false);
            SetUIElementActive(scanQRRequiredPanel, false);
            SetUIElementActive(topPanel, true);

            if (enableDebugLogs)
                Debug.Log("🧭 AR Navigation + Offline: UI shown immediately");
        }
    }

    /// <summary>
    /// PUBLIC METHOD: Called by ARSceneQRRecalibration after user confirms QR scan
    /// Unlocks Direct AR + Offline mode and shows the UI
    /// </summary>
    public void OnQRScannedAndConfirmed()
    {
        if (currentARMode == ARMode.DirectAR && 
            currentLocalizationMode == LocalizationMode.Offline && 
            !hasScannedQRInDirectAROffline)
        {
            hasScannedQRInDirectAROffline = true;

            // Hide QR required panel
            SetUIElementActive(scanQRRequiredPanel, false);

            // Show Direct AR UI
            ShowDirectARUI();

            if (enableDebugLogs)
                Debug.Log("✅ QR Scanned! Direct AR + Offline UI unlocked");
        }
        else if (currentARMode == ARMode.DirectAR && currentLocalizationMode == LocalizationMode.GPS)
        {
            // GPS mode: QR scan is just for recalibration, don't need to unlock UI
            if (enableDebugLogs)
                Debug.Log("✅ QR Scanned in Direct AR + GPS (recalibration)");
        }
        else if (currentARMode == ARMode.Navigation)
        {
            // Navigation mode: QR scan is just for recalibration
            if (enableDebugLogs)
                Debug.Log("✅ QR Scanned in Navigation mode (recalibration)");
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
        currentARMode = ARMode.DirectAR;
        hasScannedQRInDirectAROffline = false; // Reset QR scan requirement
        PlayerPrefs.SetString("ARMode", "DirectAR");
        PlayerPrefs.Save();
        ConfigureUIForModes();

        if (enableDebugLogs)
            Debug.Log("Switched to Direct AR Mode");
    }

    public void SwitchToNavigationMode()
    {
        currentARMode = ARMode.Navigation;
        PlayerPrefs.SetString("ARMode", "Navigation");
        PlayerPrefs.Save();
        ConfigureUIForModes();

        if (enableDebugLogs)
            Debug.Log("Switched to Navigation Mode");
    }

    public void SwitchToGPSMode()
    {
        currentLocalizationMode = LocalizationMode.GPS;
        PlayerPrefs.SetString("LocalizationMode", "GPS");
        PlayerPrefs.Save();
        ConfigureUIForModes();

        if (enableDebugLogs)
            Debug.Log("Switched to GPS Localization");
    }

    public void SwitchToOfflineMode()
    {
        currentLocalizationMode = LocalizationMode.Offline;
        PlayerPrefs.SetString("LocalizationMode", "Offline");
        PlayerPrefs.Save();
        ConfigureUIForModes();

        if (enableDebugLogs)
            Debug.Log("Switched to Offline Localization");
    }

    public bool IsNavigationMode()
    {
        return currentARMode == ARMode.Navigation;
    }

    public bool IsDirectARMode()
    {
        return currentARMode == ARMode.DirectAR;
    }

    public bool IsGPSMode()
    {
        return currentLocalizationMode == LocalizationMode.GPS;
    }

    public bool IsOfflineMode()
    {
        return currentLocalizationMode == LocalizationMode.Offline;
    }

    public bool HasScannedQR()
    {
        return hasScannedQRInDirectAROffline;
    }
}