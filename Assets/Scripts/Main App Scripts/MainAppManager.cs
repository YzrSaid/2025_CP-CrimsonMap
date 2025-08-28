using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class MainAppManager : MonoBehaviour
{
    public Button homeButton;
    public Button navigateButton;
    public Button settingsButton;

    public GameObject homeUnderline;
    public GameObject navigateUnderline;
    public GameObject settingsUnderline;
    public Color activeColor = new Color32(184, 16, 19, 255);
    public Color inactiveColor = new Color32(30, 30, 30, 255);

    public GameObject homePanel;
    public GameObject explorePanel;
    public GameObject settingsPanel;

    // --- New additions for map & "I'm Here" button ---
    public MapButtonsAndControlsScript mapController;  // Assign your MapContainer script here
    public Button imHereButton;                        // Assign your "I'm Here" button here
    public Vector2 userMapLocalPosition;               // Set/update this to user's position on map

    void Start()
    {
        homeButton.onClick.AddListener(OnHomeButtonClicked);
        navigateButton.onClick.AddListener(OnNavigateButtonClicked);
        settingsButton.onClick.AddListener(OnSettingsButtonClicked);

        // Subscribe to I'm Here button click
        if (imHereButton != null)
        {
            imHereButton.onClick.AddListener(OnImHereClicked);
        }
    }

    void OnHomeButtonClicked()
    {
        homeButton.GetComponent<Image>().color = activeColor;
        navigateButton.GetComponent<Image>().color = inactiveColor;
        settingsButton.GetComponent<Image>().color = inactiveColor;

        homeUnderline.SetActive(true);
        navigateUnderline.SetActive(false);
        settingsUnderline.SetActive(false);

        homePanel.SetActive(true);
        explorePanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    void OnNavigateButtonClicked()
    {
        navigateButton.GetComponent<Image>().color = activeColor;
        homeButton.GetComponent<Image>().color = inactiveColor;
        settingsButton.GetComponent<Image>().color = inactiveColor;

        homeUnderline.SetActive(false);
        navigateUnderline.SetActive(true);
        settingsUnderline.SetActive(false);

        homePanel.SetActive(false);
        explorePanel.SetActive(true);
        settingsPanel.SetActive(false);
    }

    void OnSettingsButtonClicked()
    {
        settingsButton.GetComponent<Image>().color = activeColor;
        homeButton.GetComponent<Image>().color = inactiveColor;
        navigateButton.GetComponent<Image>().color = inactiveColor;

        homeUnderline.SetActive(false);
        navigateUnderline.SetActive(false);
        settingsUnderline.SetActive(true);

        homePanel.SetActive(false);
        explorePanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    // --- New method for I'm Here button ---
    private void OnImHereClicked()
    {
        if (mapController != null)
        {
            // If you want to center on a manually stored position:
            mapController.CenterOnPosition(userMapLocalPosition);

            // Or if you want to center on the actual user pin in the map:
            mapController.CenterOnUserPin();
        }
        else
        {
            Debug.LogWarning("MapController reference not assigned!");
        }
    }


    // Optional: update userMapLocalPosition dynamically somewhere else as user moves
}
