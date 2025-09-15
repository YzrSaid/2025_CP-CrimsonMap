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
    
    // Remove the hardcoded userMapLocalPosition - we'll get it dynamically

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

    // --- Updated method for I'm Here button ---
    private void OnImHereClicked()
    {
        if (mapController == null)
        {
            Debug.LogWarning("MapController reference not assigned!");
            return;
        }

        // Get current GPS coordinates and convert to map position
        if (GPSManager.Instance != null)
        {
            Vector2 gpsCoords = GPSManager.Instance.GetSmoothedCoordinates();
            
            // Check if we have valid GPS data
            if (gpsCoords.x == 0f && gpsCoords.y == 0f)
            {
                Debug.LogWarning("I'm Here clicked but GPS coordinates are (0,0) - no valid location");
                return;
            }

            // Convert GPS coordinates to map position
            if (MapCoordinateSystem.Instance != null)
            {
                Vector2 mapPosition = MapCoordinateSystem.Instance.LatLonToMapPosition(gpsCoords.x, gpsCoords.y);
                Debug.Log($"I'm Here clicked - GPS: ({gpsCoords.x:F6}, {gpsCoords.y:F6}) -> Map: ({mapPosition.x:F2}, {mapPosition.y:F2})");
                
                // Center the map on the current user location
                mapController.CenterOnPosition(mapPosition);
            }
            else
            {
                Debug.LogWarning("MapCoordinateSystem.Instance not found!");
            }
        }
        else
        {
            Debug.LogWarning("GPSManager.Instance not found!");
        }

        // Alternative: If you want to use the existing user pin position
        // mapController.CenterOnUserPin();
    }

    // Optional: Get current user position dynamically (for other uses)
    public Vector2 GetCurrentUserMapPosition()
    {
        if (GPSManager.Instance != null && MapCoordinateSystem.Instance != null)
        {
Vector2 gpsCoords = GPSManager.Instance.GetSmoothedCoordinates();
            if (gpsCoords.x != 0f || gpsCoords.y != 0f) // Valid GPS data
            {
                return MapCoordinateSystem.Instance.LatLonToMapPosition(gpsCoords.x, gpsCoords.y);
            }
        }
        return Vector2.zero;
    }
}