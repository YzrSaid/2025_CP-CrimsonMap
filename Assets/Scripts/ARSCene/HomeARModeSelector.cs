using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Home Page AR Mode Selector
/// Allows user to choose:
/// 1. AR Mode: Direct AR or AR Navigation
/// 2. Localization Mode: GPS (Online) or Offline (X,Y)
/// </summary>
public class HomeARModeSelector : MonoBehaviour
{
    [Header("AR Mode Selection")]
    public Button directARButton;
    public Button navigationARButton;
    public TextMeshProUGUI arModeStatusText;

    [Header("Localization Mode Selection")]
    public GameObject localizationModePanel; // Panel that shows when Direct AR is selected
    public Button gpsLocalizationButton;
    public Button offlineLocalizationButton;
    public TextMeshProUGUI localizationModeStatusText;

    [Header("Confirmation")]
    public Button startARButton;
    public TextMeshProUGUI confirmationText;

    [Header("Colors")]
    public Color selectedColor = new Color(0.74f, 0.06f, 0.18f, 1f); // Crimson
    public Color unselectedColor = Color.white;

    private string selectedARMode = "DirectAR"; // Default
    private string selectedLocalizationMode = "GPS"; // Default

    void Start()
    {
        // Setup button listeners
        if (directARButton != null)
            directARButton.onClick.AddListener(() => SelectARMode("DirectAR"));

        if (navigationARButton != null)
            navigationARButton.onClick.AddListener(() => SelectARMode("Navigation"));

        if (gpsLocalizationButton != null)
            gpsLocalizationButton.onClick.AddListener(() => SelectLocalizationMode("GPS"));

        if (offlineLocalizationButton != null)
            offlineLocalizationButton.onClick.AddListener(() => SelectLocalizationMode("Offline"));

        if (startARButton != null)
            startARButton.onClick.AddListener(ConfirmAndStartAR);

        // Load previously selected modes
        LoadSavedModes();
        UpdateUI();
    }

    private void LoadSavedModes()
    {
        selectedARMode = PlayerPrefs.GetString("ARMode", "DirectAR");
        selectedLocalizationMode = PlayerPrefs.GetString("LocalizationMode", "GPS");

        Debug.Log($"[HomeARModeSelector] Loaded: AR Mode = {selectedARMode}, Localization = {selectedLocalizationMode}");
    }

    public void SelectARMode(string arMode)
    {
        selectedARMode = arMode;
        UpdateUI();

        Debug.Log($"[HomeARModeSelector] Selected AR Mode: {arMode}");
    }

    public void SelectLocalizationMode(string localizationMode)
    {
        selectedLocalizationMode = localizationMode;
        UpdateUI();

        Debug.Log($"[HomeARModeSelector] Selected Localization Mode: {localizationMode}");
    }

    private void UpdateUI()
    {
        // Update AR Mode buttons
        if (directARButton != null)
        {
            ColorBlock colors = directARButton.colors;
            colors.normalColor = selectedARMode == "DirectAR" ? selectedColor : unselectedColor;
            directARButton.colors = colors;
        }

        if (navigationARButton != null)
        {
            ColorBlock colors = navigationARButton.colors;
            colors.normalColor = selectedARMode == "Navigation" ? selectedColor : unselectedColor;
            navigationARButton.colors = colors;
        }

        // Update Localization Mode buttons
        if (gpsLocalizationButton != null)
        {
            ColorBlock colors = gpsLocalizationButton.colors;
            colors.normalColor = selectedLocalizationMode == "GPS" ? selectedColor : unselectedColor;
            gpsLocalizationButton.colors = colors;
        }

        if (offlineLocalizationButton != null)
        {
            ColorBlock colors = offlineLocalizationButton.colors;
            colors.normalColor = selectedLocalizationMode == "Offline" ? selectedColor : unselectedColor;
            offlineLocalizationButton.colors = colors;
        }

        // Show/hide localization panel based on AR mode
        // NOTE: Both Direct AR and Navigation can use either GPS or Offline
        // So we always show the localization panel
        if (localizationModePanel != null)
        {
            localizationModePanel.SetActive(true);
        }

        // Update status text
        if (arModeStatusText != null)
        {
            string arModeDisplay = selectedARMode == "DirectAR" ? "Direct AR (Offline Mode)" : "AR Navigation";
            arModeStatusText.text = $"AR Mode: {arModeDisplay}";
        }

        if (localizationModeStatusText != null)
        {
            string localizationDisplay = selectedLocalizationMode == "GPS" ? "GPS (Online)" : "Offline (X,Y Coordinates)";
            localizationModeStatusText.text = $"Localization: {localizationDisplay}";
        }

        // Update confirmation text
        if (confirmationText != null)
        {
            string arModeDisplay = selectedARMode == "DirectAR" ? "Direct AR" : "AR Navigation";
            string localizationDisplay = selectedLocalizationMode == "GPS" ? "GPS" : "Offline";

            confirmationText.text = $"Ready to start: {arModeDisplay} with {localizationDisplay} localization";

            // Special note for Direct AR + Offline
            if (selectedARMode == "DirectAR" && selectedLocalizationMode == "Offline")
            {
                confirmationText.text += "\n\n⚠️ You will need to scan a QR code first";
            }
        }
    }

    public void ConfirmAndStartAR()
    {
        // Save selected modes to PlayerPrefs
        PlayerPrefs.SetString("ARMode", selectedARMode);
        PlayerPrefs.SetString("LocalizationMode", selectedLocalizationMode);
        PlayerPrefs.Save();

        Debug.Log($"[HomeARModeSelector] ✅ Saved: AR Mode = {selectedARMode}, Localization = {selectedLocalizationMode}");

        // You can add scene loading logic here
        // For example:
        // UnityEngine.SceneManagement.SceneManager.LoadScene("ARScene");
    }

    // Optional: Public methods to get current selections
    public string GetSelectedARMode()
    {
        return selectedARMode;
    }

    public string GetSelectedLocalizationMode()
    {
        return selectedLocalizationMode;
    }
}