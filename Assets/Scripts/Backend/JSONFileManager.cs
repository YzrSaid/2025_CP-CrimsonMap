using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;
using System.Linq;

public class JSONFileManager : MonoBehaviour
{
    public static JSONFileManager Instance { get; private set; }

    private string dataPath;
    private string streamingAssetsPath;
    private bool useStreamingAssets;
    
    // Base required JSON files (static collections and system files)
    private readonly string[] baseRequiredFiles = {
        "categories.json",      // Static collection
        "infrastructure.json",  // Static collection
        "campus.json",          // Static collection (moved from versioned)
        "maps.json",            // Maps collection
        "recent_destinations.json",
        "rooms.json",
        "saved_destinations.json",
        "static_data_cache.json" // For Infrastructure/Categories/Campus sync tracking
    };

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Determine if we're in Unity Editor or built app
            useStreamingAssets = Application.isEditor;
            
            if (useStreamingAssets)
            {
                // In Unity Editor - use StreamingAssets folder
                streamingAssetsPath = Path.Combine(Application.streamingAssetsPath);
                dataPath = streamingAssetsPath;
                
                // Create StreamingAssets folder if it doesn't exist
                if (!Directory.Exists(streamingAssetsPath))
                {
                    Directory.CreateDirectory(streamingAssetsPath);
                    Debug.Log($"Created StreamingAssets folder at: {streamingAssetsPath}");
                }
            }
            else
            {
                // In built app - use persistent data path
                dataPath = Application.persistentDataPath;
            }
            
            Debug.Log($"JSON Data Path ({(useStreamingAssets ? "StreamingAssets" : "PersistentData")}): {dataPath}");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void InitializeJSONFiles(System.Action onComplete = null)
    {
        StartCoroutine(CheckAndCreateJSONFiles(onComplete));
    }

    private IEnumerator CheckAndCreateJSONFiles(System.Action onComplete)
    {
        Debug.Log("Checking for required JSON files...");
        
        // First, create base required files
        foreach (string fileName in baseRequiredFiles)
        {
            string filePath = Path.Combine(dataPath, fileName);
            
            if (!File.Exists(filePath))
            {
                Debug.Log($"Creating {fileName}...");
                CreateDefaultJSONFile(fileName, filePath);
            }
            else
            {
                Debug.Log($"{fileName} already exists");
            }
            
            yield return null; // Spread work across frames
        }
        
        Debug.Log("Base JSON files checked/created successfully");
        onComplete?.Invoke();
    }

    // Method to initialize map-specific files after maps.json is available
    // Only creates files for versioned collections (Nodes and Edges)
    public void InitializeMapSpecificFiles(List<string> mapIds, System.Action onComplete = null)
    {
        StartCoroutine(InitializeMapSpecificFilesCoroutine(mapIds, onComplete));
    }

    private IEnumerator InitializeMapSpecificFilesCoroutine(List<string> mapIds, System.Action onComplete)
    {
        Debug.Log($"Initializing map-specific files for {mapIds.Count} maps...");

        foreach (string mapId in mapIds)
        {
            // Create version cache file for each map
            string versionCacheFile = $"version_cache_{mapId}.json";
            string filePath = Path.Combine(dataPath, versionCacheFile);
            
            if (!File.Exists(filePath))
            {
                Debug.Log($"Creating version cache for map {mapId}...");
                CreateDefaultVersionCache(mapId, filePath);
            }

            // Create map-specific collection files only for versioned collections (Nodes and Edges)
            string[] versionedCollections = { "nodes", "edges" }; // Only Nodes and Edges are versioned now
            
            foreach (string collection in versionedCollections)
            {
                string mapSpecificFile = $"{collection}_{mapId}.json";
                string mapSpecificPath = Path.Combine(dataPath, mapSpecificFile);
                
                if (!File.Exists(mapSpecificPath))
                {
                    Debug.Log($"Creating {mapSpecificFile}...");
                    CreateDefaultJSONFile(mapSpecificFile, mapSpecificPath);
                }
            }
            
            yield return null; // Spread work across frames
        }

        Debug.Log("Map-specific files initialized successfully");
        onComplete?.Invoke();
    }

    private void CreateDefaultJSONFile(string fileName, string filePath)
    {
        string defaultContent = GetDefaultJSONContent(fileName);
        
        try
        {
            File.WriteAllText(filePath, defaultContent);
            Debug.Log($"Created {fileName} at {filePath}");
            
            // In Unity Editor, refresh the asset database to show the new file
            if (useStreamingAssets)
            {
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to create {fileName}: {ex.Message}");
        }
    }

    private void CreateDefaultVersionCache(string mapId, string filePath)
    {
        var defaultData = new LocalVersionCache
        {
            map_id = mapId,
            cached_version = "",
            map_name = "",
            cache_timestamp = 0
        };

        string jsonContent = JsonUtility.ToJson(defaultData, true);
        
        try
        {
            File.WriteAllText(filePath, jsonContent);
            Debug.Log($"Created version cache for map {mapId} at {filePath}");
            
            if (useStreamingAssets)
            {
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to create version cache for map {mapId}: {ex.Message}");
        }
    }

    private string GetDefaultJSONContent(string fileName)
    {
        // Handle map-specific files (those with mapId suffix) - only Nodes and Edges
        if (fileName.Contains("_M-") || fileName.Contains("_Map") || 
            fileName.StartsWith("nodes_") || fileName.StartsWith("edges_"))
        {
            // This is a map-specific versioned file
            return "[]"; // Default empty array for collections
        }

        switch (fileName)
        {
            case "categories.json":
                return "[]"; // Will be populated from Firestore static data
                
            case "infrastructure.json":
                return "[]"; // Will be populated from Firestore static data
                
            case "campus.json":
                return "[]"; // Will be populated from Firestore static data (moved from versioned)
                
            case "maps.json":
                return "[]"; // Will be populated from Firestore Maps collection
                
            case "recent_destinations.json":
                return CreateDefaultRecentDestinations();
                
            case "rooms.json":
                return "[]"; // Empty array initially
                
            case "saved_destinations.json":
                return CreateDefaultSavedDestinations();

            case "static_data_cache.json":
                return CreateDefaultStaticDataCache();
                
            default:
                return "[]"; // Default empty array
        }
    }

    private string CreateDefaultRecentDestinations()
    {
        var defaultData = new {
            recent_destinations = new object[] { }
        };
        return JsonUtility.ToJson(defaultData, true);
    }

    private string CreateDefaultSavedDestinations()
    {
        var defaultData = new {
            saved_destinations = new object[] { }
        };
        return JsonUtility.ToJson(defaultData, true);
    }

    private string CreateDefaultStaticDataCache()
    {
        var defaultData = new LocalStaticDataCache
        {
            infrastructure_synced = false,
            categories_synced = false,
            campus_synced = false, // Added campus sync flag
            cache_timestamp = 0
        };
        return JsonUtility.ToJson(defaultData, true);
    }

    // Helper methods for reading/writing JSON files
    public string ReadJSONFile(string fileName)
    {
        string filePath = Path.Combine(dataPath, fileName);
        
        if (File.Exists(filePath))
        {
            try
            {
                return File.ReadAllText(filePath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to read {fileName}: {ex.Message}");
                return null;
            }
        }
        else
        {
            Debug.LogWarning($"{fileName} does not exist at {filePath}");
            return null;
        }
    }

    public void WriteJSONFile(string fileName, string jsonContent)
    {
        string filePath = Path.Combine(dataPath, fileName);
        
        try
        {
            File.WriteAllText(filePath, jsonContent);
            Debug.Log($"Successfully wrote {fileName} to {filePath}");
            
            // In Unity Editor, refresh the asset database to show the new file
            if (useStreamingAssets)
            {
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to write {fileName}: {ex.Message}");
        }
    }

    public bool DoesFileExist(string fileName)
    {
        string filePath = Path.Combine(dataPath, fileName);
        bool exists = File.Exists(filePath);
        Debug.Log($"Checking {fileName}: {(exists ? "EXISTS" : "NOT FOUND")} at {filePath}");
        return exists;
    }

    // Method to check if data is fresh for a specific map
    public bool IsMapDataFresh(string mapId, int maxAgeHours = 24)
    {
        string fileName = $"version_cache_{mapId}.json";
        string cacheContent = ReadJSONFile(fileName);
        if (!string.IsNullOrEmpty(cacheContent))
        {
            try
            {
                var cache = JsonUtility.FromJson<LocalVersionCache>(cacheContent);
                long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long ageHours = (currentTime - cache.cache_timestamp) / 3600;
                
                return ageHours < maxAgeHours;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to check data freshness for map {mapId}: {ex.Message}");
            }
        }
        return false;
    }

    // Method to check if static data is fresh - Updated to include campus
    public bool IsStaticDataFresh(int maxAgeHours = 24)
    {
        string cacheContent = ReadJSONFile("static_data_cache.json");
        if (!string.IsNullOrEmpty(cacheContent))
        {
            try
            {
                var cache = JsonUtility.FromJson<LocalStaticDataCache>(cacheContent);
                long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long ageHours = (currentTime - cache.cache_timestamp) / 3600;
                
                return ageHours < maxAgeHours && 
                       cache.infrastructure_synced && 
                       cache.categories_synced && 
                       cache.campus_synced; // Added campus check
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to check static data freshness: {ex.Message}");
            }
        }
        return false;
    }

    // Get all available map IDs from maps.json
    public List<string> GetAvailableMapIds()
    {
        List<string> mapIds = new List<string>();
        string mapsJson = ReadJSONFile("maps.json");
        
        if (!string.IsNullOrEmpty(mapsJson))
        {
            try
            {
                var mapsArray = JsonHelper.FromJson<MapInfo>(mapsJson);
                mapIds.AddRange(mapsArray.Select(m => m.map_id));
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to parse maps.json to get map IDs: {ex.Message}");
            }
        }
        
        return mapIds;
    }

    // Get map-specific file name - Only for versioned collections (Nodes and Edges)
    public string GetMapSpecificFileName(string baseFileName, string mapId)
    {
        // Only create map-specific files for versioned collections
        string nameWithoutExtension = Path.GetFileNameWithoutExtension(baseFileName).ToLower();
        if (nameWithoutExtension == "nodes" || nameWithoutExtension == "edges")
        {
            string extension = Path.GetExtension(baseFileName);
            return $"{nameWithoutExtension}_{mapId}{extension}";
        }
        
        // For static collections (Infrastructure, Categories, Campus), return original name
        return baseFileName;
    }

    // Read map-specific data - Only works for versioned collections
    public string ReadMapSpecificData(string collectionName, string mapId)
    {
        string collectionLower = collectionName.ToLower();
        
        // Only Nodes and Edges have map-specific files
        if (collectionLower == "nodes" || collectionLower == "edges")
        {
            string fileName = $"{collectionLower}_{mapId}.json";
            return ReadJSONFile(fileName);
        }
        
        // For static collections, read the regular file
        string staticFileName = $"{collectionLower}.json";
        return ReadJSONFile(staticFileName);
    }

    // Write map-specific data - Only for versioned collections
    public void WriteMapSpecificData(string collectionName, string mapId, string jsonContent)
    {
        string collectionLower = collectionName.ToLower();
        
        // Only Nodes and Edges have map-specific files
        if (collectionLower == "nodes" || collectionLower == "edges")
        {
            string fileName = $"{collectionLower}_{mapId}.json";
            WriteJSONFile(fileName, jsonContent);
        }
        else
        {
            // For static collections, write to regular file
            string staticFileName = $"{collectionLower}.json";
            WriteJSONFile(staticFileName, jsonContent);
            Debug.LogWarning($"Collection {collectionName} is static and doesn't use map-specific files. Written to {staticFileName}");
        }
    }

    // Enhanced destination management methods
    public void AddRecentDestination(Dictionary<string, object> destination)
    {
        try
        {
            string jsonContent = ReadJSONFile("recent_destinations.json");
            if (!string.IsNullOrEmpty(jsonContent))
            {
                var data = JsonUtility.FromJson<RecentDestinationsData>(jsonContent);
                var recentList = new List<Dictionary<string, object>>(data.recent_destinations ?? new Dictionary<string, object>[0]);
                
                // Remove if already exists (to move to top)
                recentList.RemoveAll(d => d.ContainsKey("id") && destination.ContainsKey("id") && 
                                          d["id"].ToString() == destination["id"].ToString());
                
                // Add to beginning
                recentList.Insert(0, destination);
                
                // Keep only last 10
                if (recentList.Count > 10)
                {
                    recentList = recentList.GetRange(0, 10);
                }
                
                data.recent_destinations = recentList.ToArray();
                string updatedJson = JsonUtility.ToJson(data, true);
                WriteJSONFile("recent_destinations.json", updatedJson);
                
                Debug.Log("Added recent destination successfully");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to add recent destination: {ex.Message}");
        }
    }

    public void AddSavedDestination(Dictionary<string, object> destination)
    {
        try
        {
            string jsonContent = ReadJSONFile("saved_destinations.json");
            if (!string.IsNullOrEmpty(jsonContent))
            {
                var data = JsonUtility.FromJson<SavedDestinationsData>(jsonContent);
                var savedList = new List<Dictionary<string, object>>(data.saved_destinations ?? new Dictionary<string, object>[0]);
                
                // Check if already saved
                bool alreadyExists = savedList.Any(d => d.ContainsKey("id") && destination.ContainsKey("id") && 
                                                       d["id"].ToString() == destination["id"].ToString());
                
                if (!alreadyExists)
                {
                    savedList.Add(destination);
                    data.saved_destinations = savedList.ToArray();
                    string updatedJson = JsonUtility.ToJson(data, true);
                    WriteJSONFile("saved_destinations.json", updatedJson);
                    
                    Debug.Log("Added saved destination successfully");
                }
                else
                {
                    Debug.Log("Destination already saved");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to add saved destination: {ex.Message}");
        }
    }

    public void RemoveSavedDestination(string destinationId)
    {
        try
        {
            string jsonContent = ReadJSONFile("saved_destinations.json");
            if (!string.IsNullOrEmpty(jsonContent))
            {
                var data = JsonUtility.FromJson<SavedDestinationsData>(jsonContent);
                var savedList = new List<Dictionary<string, object>>(data.saved_destinations ?? new Dictionary<string, object>[0]);
                
                savedList.RemoveAll(d => d.ContainsKey("id") && d["id"].ToString() == destinationId);
                
                data.saved_destinations = savedList.ToArray();
                string updatedJson = JsonUtility.ToJson(data, true);
                WriteJSONFile("saved_destinations.json", updatedJson);
                
                Debug.Log("Removed saved destination successfully");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to remove saved destination: {ex.Message}");
        }
    }

    // Method to clear all caches and force re-download
    public void ClearAllCaches()
    {
        // Clear static data cache
        WriteJSONFile("static_data_cache.json", CreateDefaultStaticDataCache());
        
        // Clear all map version caches
        List<string> mapIds = GetAvailableMapIds();
        foreach (string mapId in mapIds)
        {
            ClearMapVersionCache(mapId);
        }
        
        Debug.Log("All caches cleared - next sync will download fresh data");
    }

    // Method to clear specific map version cache
    public void ClearMapVersionCache(string mapId)
    {
        var defaultCache = new LocalVersionCache
        {
            map_id = mapId,
            cached_version = "",
            map_name = "",
            cache_timestamp = 0
        };
        
        string fileName = $"version_cache_{mapId}.json";
        string jsonContent = JsonUtility.ToJson(defaultCache, true);
        WriteJSONFile(fileName, jsonContent);
        Debug.Log($"Version cache cleared for map {mapId}");
    }

    // Method to get current cached version for a map
    public LocalVersionCache GetMapVersionCache(string mapId)
    {
        string fileName = $"version_cache_{mapId}.json";
        string cacheContent = ReadJSONFile(fileName);
        
        if (!string.IsNullOrEmpty(cacheContent))
        {
            try
            {
                return JsonUtility.FromJson<LocalVersionCache>(cacheContent);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to parse version cache for map {mapId}: {ex.Message}");
            }
        }
        
        return null;
    }

    // Method to get all cached map versions
    public Dictionary<string, string> GetAllCachedMapVersions()
    {
        Dictionary<string, string> versions = new Dictionary<string, string>();
        List<string> mapIds = GetAvailableMapIds();
        
        foreach (string mapId in mapIds)
        {
            LocalVersionCache cache = GetMapVersionCache(mapId);
            versions[mapId] = cache?.cached_version ?? "none";
        }
        
        return versions;
    }

    // Method to clean up unused map files (when maps are removed)
    // Updated to only clean versioned collection files
    public void CleanupUnusedMapFiles()
    {
        List<string> currentMapIds = GetAvailableMapIds();
        string[] allFiles = Directory.GetFiles(dataPath, "*.json");
        
        foreach (string filePath in allFiles)
        {
            string fileName = Path.GetFileName(filePath);
            
            // Check if it's a map-specific file (version caches or versioned collections)
            if (fileName.StartsWith("version_cache_") || 
                fileName.StartsWith("nodes_") || 
                fileName.StartsWith("edges_"))
            {
                bool isUsed = false;
                foreach (string mapId in currentMapIds)
                {
                    if (fileName.Contains(mapId))
                    {
                        isUsed = true;
                        break;
                    }
                }
                
                if (!isUsed)
                {
                    try
                    {
                        File.Delete(filePath);
                        Debug.Log($"Cleaned up unused file: {fileName}");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"Failed to delete unused file {fileName}: {ex.Message}");
                    }
                }
            }
        }
    }

    // Get system status for debugging
    public string GetFileSystemStatus()
    {
        List<string> mapIds = GetAvailableMapIds();
        Dictionary<string, string> versions = GetAllCachedMapVersions();
        
        string status = "=== JSON FILE MANAGER STATUS ===\n";
        status += $"Data Path: {dataPath}\n";
        status += $"Available Maps: {mapIds.Count}\n";
        
        foreach (string mapId in mapIds)
        {
            status += $"  - {mapId}: {versions.GetValueOrDefault(mapId, "unknown")}\n";
        }
        
        status += $"Static Data Fresh: {IsStaticDataFresh()}\n";
        status += "Base Files Status:\n";
        
        foreach (string file in baseRequiredFiles)
        {
            bool exists = DoesFileExist(file);
            status += $"  - {file}: {(exists ? "OK" : "MISSING")}\n";
        }
        
        // Check versioned files for each map
        status += "Versioned Files Status:\n";
        foreach (string mapId in mapIds)
        {
            status += $"  Map {mapId}:\n";
            status += $"    - nodes_{mapId}.json: {(DoesFileExist($"nodes_{mapId}.json") ? "OK" : "MISSING")}\n";
            status += $"    - edges_{mapId}.json: {(DoesFileExist($"edges_{mapId}.json") ? "OK" : "MISSING")}\n";
        }
        
        return status;
    }
}