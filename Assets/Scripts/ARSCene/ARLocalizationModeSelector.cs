using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ARLocalizationModeSelector : MonoBehaviour
{
    [Header("GPS Button Outlines")]
    public Outline gpsMaroonOutline;
    public Outline gpsDarkGrayOutline;

    [Header("Offline Button Outlines")]
    public Outline offlineMaroonOutline;
    public Outline offlineDarkGrayOutline;

    [Header("Buttons")]
    public Button gpsButton;
    public Button offlineButton;
    public Button confirmButton;

    private string selectedLocalizationMode = "GPS";

    void Start()
    {
        // Setup button listeners
        if (gpsButton != null)
            gpsButton.onClick.AddListener(() => SelectLocalizationMode("GPS"));

        if (offlineButton != null)
            offlineButton.onClick.AddListener(() => SelectLocalizationMode("Offline"));

        if (confirmButton != null)
            confirmButton.onClick.AddListener(ConfirmSelection);
            
        UpdateOutlines();
    }

    void OnEnable()
    {
        // Auto-select GPS when panel opens
        selectedLocalizationMode = "GPS";
        UpdateOutlines();
        Debug.Log("[ARLocalizationModeSelector] Panel opened - GPS auto-selected");
    }

    public void SelectLocalizationMode(string mode)
    {
        selectedLocalizationMode = mode;
        UpdateOutlines();
        Debug.Log($"[ARLocalizationModeSelector] Selected: {mode}");
    }

    private void UpdateOutlines()
    {
        if (selectedLocalizationMode == "GPS")
        {
            // GPS SELECTED - Show MAROON, hide dark gray
            if (gpsMaroonOutline != null) gpsMaroonOutline.enabled = true;
            if (gpsDarkGrayOutline != null) gpsDarkGrayOutline.enabled = false;

            // OFFLINE NOT SELECTED - Show DARK GRAY, hide maroon
            if (offlineDarkGrayOutline != null) offlineDarkGrayOutline.enabled = true;
            if (offlineMaroonOutline != null) offlineMaroonOutline.enabled = false;
        }
        else // Offline selected
        {
            // GPS NOT SELECTED - Show DARK GRAY, hide maroon
            if (gpsDarkGrayOutline != null) gpsDarkGrayOutline.enabled = true;
            if (gpsMaroonOutline != null) gpsMaroonOutline.enabled = false;

            // OFFLINE SELECTED - Show MAROON, hide dark gray
            if (offlineMaroonOutline != null) offlineMaroonOutline.enabled = true;
            if (offlineDarkGrayOutline != null) offlineDarkGrayOutline.enabled = false;
        }
    }

    public void ConfirmSelection()
    {
        // Save localization mode to PlayerPrefs
        PlayerPrefs.SetString("LocalizationMode", selectedLocalizationMode);
        PlayerPrefs.Save();
    }

    public string GetSelectedLocalizationMode()
    {
        return selectedLocalizationMode;
    }
}