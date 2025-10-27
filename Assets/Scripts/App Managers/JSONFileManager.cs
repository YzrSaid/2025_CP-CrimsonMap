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
    
    private readonly string[] baseRequiredFiles = {
        "categories.json",
        "infrastructure.json",
        "campus.json",
        "maps.json",
        "recent_destinations.json",
        "saved_destinations.json",
        "static_data_cache.json",
        "indoor.json"
    };

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            useStreamingAssets = Application.isEditor;
            
            if (useStreamingAssets)
            {
                streamingAssetsPath = Path.Combine(Application.streamingAssetsPath);
                dataPath = streamingAssetsPath;
                
                if (!Directory.Exists(streamingAssetsPath))
                {
                    Directory.CreateDirectory(streamingAssetsPath);
                }
            }
            else
            {
                dataPath = Application.persistentDataPath;
            }
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
        foreach (string fileName in baseRequiredFiles)
        {
            string filePath = Path.Combine(dataPath, fileName);
            
            if (!File.Exists(filePath))
            {
                CreateDefaultJSONFile(fileName, filePath);
            }
            
            yield return null;
        }
        
        onComplete?.Invoke();
    }

    public void InitializeMapSpecificFiles(List<string> mapIds, System.Action onComplete = null)
    {
        StartCoroutine(InitializeMapSpecificFilesCoroutine(mapIds, onComplete));
    }

    private IEnumerator InitializeMapSpecificFilesCoroutine(List<string> mapIds, System.Action onComplete)
    {
        foreach (string mapId in mapIds)
        {
            string versionCacheFile = $"version_cache_{mapId}.json";
            string filePath = Path.Combine(dataPath, versionCacheFile);
            
            if (!File.Exists(filePath))
            {
                CreateDefaultVersionCache(mapId, filePath);
            }

            string[] versionedCollections = { "nodes", "edges" };
            
            foreach (string collection in versionedCollections)
            {
                string mapSpecificFile = $"{collection}_{mapId}.json";
                string mapSpecificPath = Path.Combine(dataPath, mapSpecificFile);
                
                if (!File.Exists(mapSpecificPath))
                {
                    CreateDefaultJSONFile(mapSpecificFile, mapSpecificPath);
                }
            }
            
            yield return null;
        }

        onComplete?.Invoke();
    }

    private void CreateDefaultJSONFile(string fileName, string filePath)
    {
        string defaultContent = GetDefaultJSONContent(fileName);
        
        try
        {
            File.WriteAllText(filePath, defaultContent);
            
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
        if (fileName.Contains("_M-") || fileName.Contains("_Map") || 
            fileName.StartsWith("nodes_") || fileName.StartsWith("edges_"))
        {
            return "[]";
        }

        switch (fileName)
        {
            case "categories.json":
                return "[]";
                
            case "infrastructure.json":
                return "[]";
                
            case "campus.json":
                return "[]";

            case "indoor.json":
                return "[]";
                
            case "maps.json":
                return "[]";
                
            case "recent_destinations.json":
                return CreateDefaultRecentDestinations();
                
            case "saved_destinations.json":
                return CreateDefaultSavedDestinations();

            case "static_data_cache.json":
                return CreateDefaultStaticDataCache();
                
            default:
                return "[]";
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
            campus_synced = false,
            indoor_synced = false,
        };
        return JsonUtility.ToJson(defaultData, true);
    }

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
        return File.Exists(filePath);
    }

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

    public string GetMapSpecificFileName(string baseFileName, string mapId)
    {
        string nameWithoutExtension = Path.GetFileNameWithoutExtension(baseFileName).ToLower();
        if (nameWithoutExtension == "nodes" || nameWithoutExtension == "edges")
        {
            string extension = Path.GetExtension(baseFileName);
            return $"{nameWithoutExtension}_{mapId}{extension}";
        }
        
        return baseFileName;
    }

    public string ReadMapSpecificData(string collectionName, string mapId)
    {
        string collectionLower = collectionName.ToLower();
        
        if (collectionLower == "nodes" || collectionLower == "edges")
        {
            string fileName = $"{collectionLower}_{mapId}.json";
            return ReadJSONFile(fileName);
        }
        
        string staticFileName = $"{collectionLower}.json";
        return ReadJSONFile(staticFileName);
    }

    public void WriteMapSpecificData(string collectionName, string mapId, string jsonContent)
    {
        string collectionLower = collectionName.ToLower();
        
        if (collectionLower == "nodes" || collectionLower == "edges")
        {
            string fileName = $"{collectionLower}_{mapId}.json";
            WriteJSONFile(fileName, jsonContent);
        }
        else
        {
            string staticFileName = $"{collectionLower}.json";
            WriteJSONFile(staticFileName, jsonContent);
        }
    }

    public void AddRecentDestination(Dictionary<string, object> destination)
    {
        try
        {
            string jsonContent = ReadJSONFile("recent_destinations.json");
            if (!string.IsNullOrEmpty(jsonContent))
            {
                var data = JsonUtility.FromJson<RecentDestinationsData>(jsonContent);
                var recentList = new List<Dictionary<string, object>>(data.recent_destinations ?? new Dictionary<string, object>[0]);
                
                recentList.RemoveAll(d => d.ContainsKey("id") && destination.ContainsKey("id") && 
                                          d["id"].ToString() == destination["id"].ToString());
                
                recentList.Insert(0, destination);
                
                if (recentList.Count > 10)
                {
                    recentList = recentList.GetRange(0, 10);
                }
                
                data.recent_destinations = recentList.ToArray();
                string updatedJson = JsonUtility.ToJson(data, true);
                WriteJSONFile("recent_destinations.json", updatedJson);
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
                
                bool alreadyExists = savedList.Any(d => d.ContainsKey("id") && destination.ContainsKey("id") && 
                                                       d["id"].ToString() == destination["id"].ToString());
                
                if (!alreadyExists)
                {
                    savedList.Add(destination);
                    data.saved_destinations = savedList.ToArray();
                    string updatedJson = JsonUtility.ToJson(data, true);
                    WriteJSONFile("saved_destinations.json", updatedJson);
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
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to remove saved destination: {ex.Message}");
        }
    }

    public void ClearAllCaches()
    {
        WriteJSONFile("static_data_cache.json", CreateDefaultStaticDataCache());
        
        List<string> mapIds = GetAvailableMapIds();
        foreach (string mapId in mapIds)
        {
            ClearMapVersionCache(mapId);
        }
    }

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
    }

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

    public void CleanupUnusedMapFiles()
    {
        List<string> currentMapIds = GetAvailableMapIds();
        string[] allFiles = Directory.GetFiles(dataPath, "*.json");
        
        foreach (string filePath in allFiles)
        {
            string fileName = Path.GetFileName(filePath);
            
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
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"Failed to delete unused file {fileName}: {ex.Message}");
                    }
                }
            }
        }
    }

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
        
        status += "Base Files Status:\n";
        
        foreach (string file in baseRequiredFiles)
        {
            bool exists = DoesFileExist(file);
            status += $"  - {file}: {(exists ? "OK" : "MISSING")}\n";
        }
        
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