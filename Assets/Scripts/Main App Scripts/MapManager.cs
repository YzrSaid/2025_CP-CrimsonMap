using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using System.Linq;

/// <summary>
/// Central manager for handling map switching and coordinating all spawners
/// Manages multiple campuses per map and handles UI updates
/// </summary>
public class MapManager : MonoBehaviour
{
    [Header("UI References")]
    public Button dropdownButton;
    public TextMeshProUGUI dropdownButtonText; // Text component of the dropdown button
    
    [Header("Spawner References")]
    public BarrierSpawner barrierSpawner;
    public InfrastructureSpawner infrastructureSpawner;
    public PathRenderer pathRenderer;
    public MapCoordinateSystem coordinateSystem;
    
    [Header("Map Controls")]
    public MapButtonsAndControlsScript mapControls;
    
    [Header("Current Map Info")]
    public MapData currentMap;
    public List<string> currentCampusIds = new List<string>();
    
    // Data containers
    private List<MapData> availableMaps = new List<MapData>();
    private Dictionary<string, CampusData> allCampuses = new Dictionary<string, CampusData>();
    
    // Events for spawners to listen to
    public System.Action<MapData> OnMapChanged;
    public System.Action OnMapLoadingComplete;
    
    void Start()
    {
        LoadMapAndCampusData();
        
        // Auto-find MapButtonsAndControlsScript if not assigned
        if (mapControls == null)
        {
            mapControls = FindObjectOfType<MapButtonsAndControlsScript>();
        }
        
        // Load first map by default if available
        if (availableMaps.Count > 0)
        {
            LoadMap(availableMaps[0]);
        }
    }
    
    void LoadMapAndCampusData()
    {
        // Load maps.json
        string mapPath = Path.Combine(Application.streamingAssetsPath, "map.json");
        if (File.Exists(mapPath))
        {
            string mapJson = File.ReadAllText(mapPath);
            MapList mapList = JsonUtility.FromJson<MapList>("{\"maps\":" + mapJson + "}");
            availableMaps.AddRange(mapList.maps);
            Debug.Log($"📍 Loaded {availableMaps.Count} maps");
        }
        
        // Load campus.json
        string campusPath = Path.Combine(Application.streamingAssetsPath, "campus.json");
        if (File.Exists(campusPath))
        {
            string campusJson = File.ReadAllText(campusPath);
            CampusList campusList = JsonUtility.FromJson<CampusList>("{\"campuses\":" + campusJson + "}");
            foreach (var campus in campusList.campuses)
            {
                allCampuses[campus.campus_id] = campus;
            }
            Debug.Log($"🏫 Loaded {allCampuses.Count} campus definitions");
        }
    }
    
    /// <summary>
    /// Main method to switch to a different map
    /// This will trigger all spawners to reload their content
    /// </summary>
    public void LoadMap(MapData mapData)
    {
        Debug.Log($"🗺️ Loading map: {mapData.map_name}");
        
        currentMap = mapData;
        currentCampusIds.Clear();
        currentCampusIds.AddRange(mapData.campus_included);
        
        // Update dropdown button text
        UpdateDropdownButtonText(mapData.map_name);
        
        // Clear existing spawned objects
        ClearAllSpawnedObjects();
        
        // Notify coordinate system to recalculate bounds for this map
        StartCoroutine(LoadMapCoroutine());
    }
    
    IEnumerator LoadMapCoroutine()
    {
        // Fire event before loading starts
        OnMapChanged?.Invoke(currentMap);
        
        // Wait for coordinate system to recalculate bounds
        yield return StartCoroutine(coordinateSystem.RecalculateBoundsForCampuses(currentCampusIds));
        
        // Spawn everything in order
        yield return StartCoroutine(barrierSpawner.LoadAndSpawnForCampuses(currentCampusIds));
        yield return StartCoroutine(infrastructureSpawner.LoadAndSpawnForCampuses(currentCampusIds));
        yield return StartCoroutine(pathRenderer.LoadAndRenderForCampuses(currentCampusIds));
        
        // IMPORTANT: Reset map controls after map size changes
        if (mapControls != null)
        {
            mapControls.ResetMapView();
        }
        
        // Fire completion event
        OnMapLoadingComplete?.Invoke();
        
        Debug.Log($"✅ Map '{currentMap.map_name}' loaded successfully with {currentCampusIds.Count} campuses");
    }
    
    void UpdateDropdownButtonText(string mapName)
    {
        if (dropdownButtonText != null)
        {
            dropdownButtonText.text = mapName;
        }
        else
        {
            // Fallback: try to find text component in children
            TextMeshProUGUI text = dropdownButton.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = mapName;
        }
    }
    
    void ClearAllSpawnedObjects()
    {
        // Clear barrier spawner objects
        if (barrierSpawner != null)
        {
            barrierSpawner.ClearSpawnedObjects();
        }
        
        // Clear building spawner objects
        if (infrastructureSpawner != null)
        {
            infrastructureSpawner.ClearSpawnedObjects();
        }
        
        // Clear path renderer objects
        if (pathRenderer != null)
        {
            pathRenderer.ClearSpawnedObjects();
        }
    }
    
    /// <summary>
    /// Get all available maps for dropdown population
    /// </summary>
    public List<MapData> GetAvailableMaps()
    {
        return availableMaps;
    }
    
    /// <summary>
    /// Get campus name by ID
    /// </summary>
    public string GetCampusName(string campusId)
    {
        return allCampuses.ContainsKey(campusId) ? allCampuses[campusId].campus_name : "Unknown Campus";
    }
    
    /// <summary>
    /// Get current map info for debugging
    /// </summary>
    public string GetCurrentMapInfo()
    {
        if (currentMap == null) return "No map loaded";
        
        string campusNames = string.Join(", ", currentCampusIds.Select(id => GetCampusName(id)));
        return $"Map: {currentMap.map_name} | Campuses: {campusNames}";
    }
}

// Campus data class
[System.Serializable]
public class CampusData
{
    public string campus_id;
    public string campus_name;
}

[System.Serializable]
public class CampusList
{
    public List<CampusData> campuses;
}