using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using System.Collections;

public class MapDropdown : MonoBehaviour
{
    [Header("UI References")]
    public Button dropdownButton;
    public GameObject panel; 
    public GameObject panelForBG;
    public GameObject mapButtonPrefab;
    public Transform buttonContainer;
    
    private List<MapInfo> availableMaps = new List<MapInfo>();
    private bool isDataLoaded = false;

    void Start()
    {
        dropdownButton.onClick.AddListener(TogglePanel);
        StartCoroutine(WaitForMapManagerData());
        panel.SetActive(false);
        panelForBG.SetActive(false);
    }

    IEnumerator WaitForMapManagerData()
    {
        Debug.Log("MapDropdown: Waiting for MapManager to be ready...");
        
        // Wait until MapManager is ready and has data
        while (MapManager.Instance == null || !MapManager.Instance.IsReady())
        {
            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log("MapDropdown: MapManager is ready, getting available maps...");

        // Get maps from MapManager
        availableMaps = MapManager.Instance.GetAvailableMaps();
        isDataLoaded = true;
        PopulatePanel();

        Debug.Log($"MapDropdown: Loaded {availableMaps.Count} maps for dropdown");
        
        // Debug: Show what maps we got
        foreach (var map in availableMaps)
        {
            Debug.Log($"  - {map.map_id}: {map.map_name} (Campuses: {string.Join(", ", map.campus_included)})");
        }
    }

    void TogglePanel()
    {
        if (!isDataLoaded)
        {
            Debug.LogWarning("MapDropdown: Data not loaded yet, cannot show dropdown");
            return;
        }

        bool isActive = !panel.activeSelf;
        panel.SetActive(isActive);
        panelForBG.SetActive(isActive);
    }

    void PopulatePanel()
    {
        // Clear existing buttons
        foreach (Transform child in buttonContainer)
        {
            Destroy(child.gameObject);
        }

        Debug.Log($"MapDropdown: Creating buttons for {availableMaps.Count} maps");

        // Create button for each available map
        foreach (var map in availableMaps)
        {
            GameObject btnObj = Instantiate(mapButtonPrefab, buttonContainer);

            // Set button text to map name
            TMPro.TextMeshProUGUI tmpText = btnObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmpText != null)
            {
                tmpText.text = map.map_name;
            }
            else
            {
                Text regularText = btnObj.GetComponentInChildren<Text>();
                if (regularText != null)
                    regularText.text = map.map_name;
                else
                    Debug.LogWarning($"MapDropdown: No text component found in button prefab for map {map.map_name}");
            }

            // Add click listener
            Button button = btnObj.GetComponent<Button>();
            if (button != null)
            {
                // Capture the map in a local variable for the closure
                MapInfo selectedMap = map;
                button.onClick.AddListener(() => SelectMap(selectedMap));
                
                Debug.Log($"MapDropdown: Created button for {map.map_name} (ID: {map.map_id})");
            }
            else
            {
                Debug.LogError($"MapDropdown: No Button component found in prefab for map {map.map_name}");
            }
        }
    }

    void SelectMap(MapInfo map)
    {
        Debug.Log($"MapDropdown: Selected Map - ID: {map.map_id}, Name: {map.map_name}");
        Debug.Log($"MapDropdown: Map includes campuses: {string.Join(", ", map.campus_included)}");

        // Close dropdown
        panel.SetActive(false);
        panelForBG.SetActive(false);

        // Load the selected map through MapManager
        if (MapManager.Instance != null && MapManager.Instance.IsReady())
        {
            Debug.Log($"MapDropdown: Loading map through MapManager: {map.map_name}");
            MapManager.Instance.LoadMap(map);
        }
        else
        {
            Debug.LogError("MapDropdown: MapManager not ready or not found!");
        }
    }

    // Public method to get the currently selected map (from MapManager)
    public MapInfo GetCurrentlySelectedMap()
    {
        if (MapManager.Instance != null)
        {
            return MapManager.Instance.GetCurrentMap();
        }
        return null;
    }

    // Method to refresh the dropdown data (useful for testing or manual refresh)
    public void RefreshMapList()
    {
        if (MapManager.Instance != null && MapManager.Instance.IsReady())
        {
            availableMaps = MapManager.Instance.GetAvailableMaps();
            PopulatePanel();
            Debug.Log($"MapDropdown: Refreshed map list - {availableMaps.Count} maps available");
        }
        else
        {
            Debug.LogWarning("MapDropdown: Cannot refresh - MapManager not ready");
        }
    }

    // Method to programmatically select a map by ID (useful for default selection)
    public void SelectMapById(string mapId)
    {
        MapInfo targetMap = availableMaps.Find(m => m.map_id == mapId);
        if (targetMap != null)
        {
            SelectMap(targetMap);
            Debug.Log($"MapDropdown: Programmatically selected map {mapId}");
        }
        else
        {
            Debug.LogWarning($"MapDropdown: Map with ID {mapId} not found in available maps");
        }
    }

    // Optional: Method to set default map (first map, or specific map)
    public void SelectDefaultMap()
    {
        if (availableMaps.Count > 0)
        {
            SelectMap(availableMaps[0]);
            Debug.Log($"MapDropdown: Selected default map: {availableMaps[0].map_name}");
        }
        else
        {
            Debug.LogWarning("MapDropdown: No maps available to select as default");
        }
    }

    void OnDestroy()
    {
        // Clean up listeners
        if (dropdownButton != null)
            dropdownButton.onClick.RemoveAllListeners();
    }
}