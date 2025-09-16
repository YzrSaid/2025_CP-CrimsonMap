using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

/// <summary>
/// Central manager for handling map switching and coordinating with MapDropdown
/// Updated to work with FirestoreManager and MapInfo instead of local JSON files
/// Manages multiple campuses per map and handles UI updates
/// </summary>
public class MapManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI dropdownButtonText; // Text component of the dropdown button to show current map
    
    [Header("Spawner References")]
    public BarrierSpawner barrierSpawner;
    public InfrastructureSpawner infrastructureSpawner;
    // public PathRenderer pathRenderer;
    
    [Header("Current Map Info")]
    public MapInfo currentMap;
    public List<string> currentCampusIds = new List<string>();
    
    [Header("Debug")]
    public bool enableDebugLogs = true;
    
    // Data containers
    private List<MapInfo> availableMaps = new List<MapInfo>();
    private Dictionary<string, CampusData> allCampuses = new Dictionary<string, CampusData>();
    private bool isInitialized = false;
    
    // Events for spawners to listen to
    public System.Action<MapInfo> OnMapChanged;
    public System.Action OnMapLoadingComplete;
    public System.Action OnMapLoadingStarted;
    
    // Singleton pattern for easy access from MapDropdown
    public static MapManager Instance { get; private set; }
    
    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("Multiple MapManager instances found, destroying duplicate");
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        DebugLog("🗺️ MapManager starting...");
        StartCoroutine(InitializeMapManager());
    }
    
    IEnumerator InitializeMapManager()
    {
        DebugLog("⏳ Waiting for FirestoreManager to be ready...");
        
        // Wait for FirestoreManager to be ready
        while (FirestoreManager.Instance == null || !FirestoreManager.Instance.IsReady)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        DebugLog("✅ FirestoreManager ready, loading map data...");
        
        // Wait for maps to be loaded
        while (FirestoreManager.Instance.AvailableMaps.Count == 0)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        // Get available maps from FirestoreManager
        availableMaps = FirestoreManager.Instance.AvailableMaps;
        DebugLog($"📍 Loaded {availableMaps.Count} maps from Firestore:");
        
        // Debug: Show available maps
        foreach (var map in availableMaps)
        {
            DebugLog($"  - {map.map_id}: {map.map_name} (Campuses: {string.Join(", ", map.campus_included)})");
        }
        
        // Load campus data (if you still have local campus.json)
        yield return StartCoroutine(LoadCampusData());

        isInitialized = true;
        DebugLog("🚀 MapManager initialization complete");
        
        // Load first map by default if available
        if (availableMaps.Count > 0)
        {
            DebugLog($"🎯 Loading default map: {availableMaps[0].map_name}");
            LoadMap(availableMaps[0]);
        }
        else
        {
            Debug.LogWarning("⚠️ No maps available to load");
        }
    }
    
    IEnumerator LoadCampusData()
    {
        // Load campus data if available (optional)
        bool loadCompleted = false;
        
        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile("campus.json",
            // onSuccess
            (jsonContent) => {
                try
                {
                    CampusList campusList = JsonUtility.FromJson<CampusList>("{\"campuses\":" + jsonContent + "}");
                    foreach (var campus in campusList.campuses)
                    {
                        allCampuses[campus.campus_id] = campus;
                    }
                    DebugLog($"🏫 Loaded {allCampuses.Count} campus definitions");
                    loadCompleted = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ Error parsing campus JSON: {e.Message}");
                    loadCompleted = true;
                }
            },
            // onError
            (error) => {
                DebugLog($"ℹ️ No campus.json file found (optional): {error}");
                // Continue without campus data if file doesn't exist
                loadCompleted = true;
            }
        ));
        
        yield return new WaitUntil(() => loadCompleted);
    }
    
    /// <summary>
    /// Main method to switch to a different map
    /// Called by MapDropdown when user selects a map
    /// </summary>
    public void LoadMap(MapInfo mapInfo)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("⚠️ MapManager not initialized yet, cannot load map");
            return;
        }
        
        if (mapInfo == null)
        {
            Debug.LogError("❌ Cannot load null map");
            return;
        }
        
        DebugLog($"🗺️ Loading map: {mapInfo.map_name} (ID: {mapInfo.map_id})");
        
        currentMap = mapInfo;
        currentCampusIds.Clear();
        currentCampusIds.AddRange(mapInfo.campus_included);
        
        DebugLog($"🏫 Map includes campuses: {string.Join(", ", currentCampusIds)}");
        
        // Update dropdown button text to show current map
        UpdateDropdownButtonText(mapInfo.map_name);
        
        // Start loading process
        StartCoroutine(LoadMapCoroutine());
    }
    
    IEnumerator LoadMapCoroutine()
    {
        DebugLog("🔄 Starting map loading process...");
        
        // Fire event before loading starts
        OnMapLoadingStarted?.Invoke();
        OnMapChanged?.Invoke(currentMap);
        
        // Clear existing spawned objects first
        yield return StartCoroutine(ClearAllSpawnedObjects());
        
        // Update spawners with the new campus IDs
        UpdateSpawnersForCurrentMap();
        
        // For now, just load paths since that's what you have working
        // if (pathRenderer != null)
        // {
        //     DebugLog("🛤️ Loading pathways for current map...");
        //     yield return StartCoroutine(pathRenderer.LoadAndRenderPaths());
        // }
        
        // TODO: Load barriers and infrastructure when ready
        // if (barrierSpawner != null)
        // {
        //     DebugLog("🚧 Loading barriers...");
        //     yield return StartCoroutine(barrierSpawner.LoadAndSpawnForCampuses(currentCampusIds));
        // }
        
        // if (infrastructureSpawner != null)
        // {
        //     DebugLog("🏢 Loading infrastructure...");
        //     yield return StartCoroutine(infrastructureSpawner.LoadAndSpawnForCampuses(currentCampusIds));
        // }

        // Fire completion event
        OnMapLoadingComplete?.Invoke();
        
        DebugLog($"✅ Map '{currentMap.map_name}' loaded successfully with {currentCampusIds.Count} campuses");
    }
    
    void UpdateSpawnersForCurrentMap()
    {
        // Update PathRenderer with current campus IDs
        // if (pathRenderer != null)
        // {
        //     DebugLog($"🛤️ Updating PathRenderer for campuses: {string.Join(", ", currentCampusIds)}");
        //     pathRenderer.targetCampusIds.Clear();
        //     pathRenderer.targetCampusIds.AddRange(currentCampusIds);
        // }
        
        // TODO: Update other spawners when ready
        // if (barrierSpawner != null)
        // {
        //     // You might need to add a similar targetCampusIds property to BarrierSpawner
        //     DebugLog("🚧 Updating BarrierSpawner for current map");
        // }
        
        // if (infrastructureSpawner != null)
        // {
        //     // You might need to add a similar targetCampusIds property to InfrastructureSpawner
        //     DebugLog("🏢 Updating InfrastructureSpawner for current map");
        // }
    }
    
    void UpdateDropdownButtonText(string mapName)
    {
        if (dropdownButtonText != null)
        {
            dropdownButtonText.text = mapName;
            DebugLog($"📝 Updated dropdown button text to: {mapName}");
        }
        else
        {
            DebugLog("⚠️ Dropdown button text component not assigned");
        }
    }
    
    IEnumerator ClearAllSpawnedObjects()
    {
        DebugLog("🧹 Clearing all spawned objects...");
        
        // Clear path renderer objects
        // if (pathRenderer != null)
        // {
        //     pathRenderer.ClearSpawnedPaths();
        //     yield return null; // Wait a frame
        // }
        
        // TODO: Clear other spawners when ready
        // if (barrierSpawner != null)
        // {
        //     barrierSpawner.ClearSpawnedObjects();
        //     yield return null; // Wait a frame
        // }
        
        // if (infrastructureSpawner != null)
        // {
        //     infrastructureSpawner.ClearSpawnedObjects();
        //     yield return null; // Wait a frame
        // }
        
        DebugLog("✅ All spawned objects cleared");
        yield break;
    }
    
    /// <summary>
    /// Get all available maps for dropdown population
    /// </summary>
    public List<MapInfo> GetAvailableMaps()
    {
        return availableMaps;
    }
    
    /// <summary>
    /// Get current map info
    /// </summary>
    public MapInfo GetCurrentMap()
    {
        return currentMap;
    }
    
    /// <summary>
    /// Get current campus IDs for the active map
    /// </summary>
    public List<string> GetCurrentCampusIds()
    {
        return new List<string>(currentCampusIds);
    }
    
    /// <summary>
    /// Get campus name by ID (if campus data is available)
    /// </summary>
    public string GetCampusName(string campusId)
    {
        return allCampuses.ContainsKey(campusId) ? allCampuses[campusId].campus_name : campusId;
    }
    
    /// <summary>
    /// Get current map info for debugging
    /// </summary>
    public string GetCurrentMapInfo()
    {
        if (currentMap == null) return "No map loaded";
        
        string campusNames = string.Join(", ", currentCampusIds.Select(id => GetCampusName(id)));
        return $"Map: {currentMap.map_name} | Campuses: {campusNames} | Campus IDs: {string.Join(", ", currentCampusIds)}";
    }
    
    /// <summary>
    /// Method to load a map by ID (useful for external calls)
    /// </summary>
    public void LoadMapById(string mapId)
    {
        MapInfo targetMap = availableMaps.Find(m => m.map_id == mapId);
        if (targetMap != null)
        {
            LoadMap(targetMap);
        }
        else
        {
            Debug.LogWarning($"⚠️ Map with ID {mapId} not found in available maps");
        }
    }
    
    /// <summary>
    /// Check if manager is ready for map operations
    /// </summary>
    public bool IsReady()
    {
        return isInitialized && availableMaps.Count > 0;
    }
    
    /// <summary>
    /// Force refresh all spawners for current map (useful for debugging)
    /// </summary>
    public void RefreshCurrentMap()
    {
        if (currentMap != null && isInitialized)
        {
            DebugLog($"🔄 Force refreshing current map: {currentMap.map_name}");
            LoadMap(currentMap);
        }
        else
        {
            Debug.LogWarning("⚠️ Cannot refresh - no current map or not initialized");
        }
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[MapManager] {message}");
        }
    }
    
    // Debug methods for editor
    void Update()
    {
        if (Application.isEditor && enableDebugLogs)
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                Debug.Log($"=== MAP MANAGER STATUS ===");
                Debug.Log($"Initialized: {isInitialized}");
                Debug.Log($"Available maps: {availableMaps.Count}");
                Debug.Log($"Current map: {currentMap?.map_name ?? "None"}");
                Debug.Log($"Current campus IDs: {string.Join(", ", currentCampusIds)}");
                Debug.Log($"Current map info: {GetCurrentMapInfo()}");
            }
            
            if (Input.GetKeyDown(KeyCode.R))
            {
                RefreshCurrentMap();
            }
        }
    }
    
    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}