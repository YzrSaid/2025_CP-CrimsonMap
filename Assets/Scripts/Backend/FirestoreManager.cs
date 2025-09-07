using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;
using System;
using System.Linq;

public class FirestoreManager : MonoBehaviour
{
    public static FirestoreManager Instance { get; private set; }

    private FirebaseFirestore db;
    private bool isFirebaseReady = false;
    private List<MapInfo> availableMaps = new List<MapInfo>();

    // Collections configuration
    private readonly string MAPS_COLLECTION = "Maps";
    private readonly string MAP_VERSIONS_COLLECTION = "mapVersions";
    private readonly string VERSIONS_SUBCOLLECTION = "versions";
    private readonly string STATIC_DATA_VERSIONS_COLLECTION = "StaticDataVersions";

    // Collections that need to be synced (non-versioned data)
    private readonly string[] staticCollections = {
        "Infrastructure", // Building info, room details (not affected by map versions)
        "Categories"      // Category definitions
    };

    // Version-controlled collections (these come from map versions)
    private readonly string[] versionedCollections = {
        "Campus",
        "Maps", 
        "Nodes"
        // "Edges" - if you have edges, add it here
    };

    public bool IsReady => isFirebaseReady;
    public List<MapInfo> AvailableMaps => availableMaps;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void InitializeFirebase(System.Action<bool> onComplete = null)
    {
        StartCoroutine(InitializeFirebaseCoroutine(onComplete));
    }

    private IEnumerator InitializeFirebaseCoroutine(System.Action<bool> onComplete)
    {
        var dependencyTask = FirebaseApp.CheckAndFixDependenciesAsync();
        yield return new WaitUntil(() => dependencyTask.IsCompleted);

        var dependencyStatus = dependencyTask.Result;
        if (dependencyStatus == DependencyStatus.Available)
        {
            FirebaseApp app = FirebaseApp.DefaultInstance;
            db = FirebaseFirestore.DefaultInstance;
            isFirebaseReady = true;
            Debug.Log("Firebase Firestore initialized successfully");
            onComplete?.Invoke(true);
        }
        else
        {
            Debug.LogError($"Could not resolve Firebase dependencies: {dependencyStatus}");
            isFirebaseReady = false;
            onComplete?.Invoke(false);
        }
    }

    public void CheckAndSyncData(System.Action onComplete = null)
    {
        if (!isFirebaseReady)
        {
            Debug.LogWarning("Firebase not ready, using cached data");
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(CheckAndSyncDataCoroutine(onComplete));
    }

    private IEnumerator CheckAndSyncDataCoroutine(System.Action onComplete)
    {
        Debug.Log("Starting comprehensive data sync check...");

        // Step 1: Sync Maps collection first to get available maps
        bool mapsSyncComplete = false;
        SyncCollectionToLocal(MAPS_COLLECTION, () => mapsSyncComplete = true);
        yield return new WaitUntil(() => mapsSyncComplete);

        // Step 2: Load available maps from local JSON
        LoadAvailableMaps();

        if (availableMaps.Count == 0)
        {
            Debug.LogWarning("No maps found in Maps collection");
            onComplete?.Invoke();
            yield break;
        }

        Debug.Log($"Found {availableMaps.Count} maps to check for updates");

        // Step 3: Check versions for all maps
        bool allMapsChecked = false;
        CheckAllMapVersions(() => allMapsChecked = true);
        yield return new WaitUntil(() => allMapsChecked);

        // Step 4: Always check static collections (infrastructure, categories)
        bool staticSyncComplete = false;
        SyncStaticCollections(() => staticSyncComplete = true);
        yield return new WaitUntil(() => staticSyncComplete);

        Debug.Log("Comprehensive data sync check completed");
        onComplete?.Invoke();
    }

    private void LoadAvailableMaps()
    {
        availableMaps.Clear();
        
        if (JSONFileManager.Instance != null)
        {
            string mapsJson = JSONFileManager.Instance.ReadJSONFile("maps.json");
            if (!string.IsNullOrEmpty(mapsJson))
            {
                try
                {
                    // Parse JSON array using fixed JsonHelper
                    var mapsArray = JsonHelper.FromJson<MapInfo>(mapsJson);
                    availableMaps.AddRange(mapsArray);
                    
                    Debug.Log($"Loaded {availableMaps.Count} available maps:");
                    foreach (var map in availableMaps)
                    {
                        Debug.Log($"  - {map.map_id}: {map.map_name}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to parse maps.json: {ex.Message}");
                }
            }
        }
    }

    private void CheckAllMapVersions(System.Action onComplete)
    {
        if (availableMaps.Count == 0)
        {
            Debug.Log("No maps to check versions for");
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(CheckAllMapVersionsCoroutine(onComplete));
    }

    private IEnumerator CheckAllMapVersionsCoroutine(System.Action onComplete)
    {
        int completedChecks = 0;
        int totalMaps = availableMaps.Count;
        List<MapVersionInfo> mapsNeedingUpdate = new List<MapVersionInfo>();

        // Check each map's version
        foreach (var mapInfo in availableMaps)
        {
            Debug.Log($"Checking version for map: {mapInfo.map_id}");
            
            CheckSingleMapVersion(mapInfo.map_id, (needsUpdate, serverVersion) =>
            {
                if (needsUpdate && serverVersion != null)
                {
                    Debug.Log($"Map {mapInfo.map_id} needs update to version {serverVersion.current_version}");
                    mapsNeedingUpdate.Add(serverVersion);
                }
                else
                {
                    Debug.Log($"Map {mapInfo.map_id} is up to date");
                }
                completedChecks++;
            });
        }

        // Wait for all version checks to complete
        yield return new WaitUntil(() => completedChecks >= totalMaps);

        // Sync all maps that need updates
        if (mapsNeedingUpdate.Count > 0)
        {
            Debug.Log($"Syncing {mapsNeedingUpdate.Count} maps with updates...");
            
            int completedSyncs = 0;
            foreach (var mapVersion in mapsNeedingUpdate)
            {
                SyncSingleMapVersion(mapVersion, () => completedSyncs++);
            }
            
            yield return new WaitUntil(() => completedSyncs >= mapsNeedingUpdate.Count);
            Debug.Log("All map updates completed");
        }
        else
        {
            Debug.Log("All maps are up to date");
        }

        onComplete?.Invoke();
    }

    private void CheckSingleMapVersion(string mapId, System.Action<bool, MapVersionInfo> onComplete)
    {
        // Get server version for specific map
        DocumentReference mapRef = db.Collection(MAP_VERSIONS_COLLECTION).Document(mapId);
        mapRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                DocumentSnapshot snapshot = task.Result;
                if (snapshot.Exists)
                {
                    var data = snapshot.ToDictionary();
                    MapVersionInfo serverVersion = new MapVersionInfo
                    {
                        map_id = mapId,
                        current_version = data.ContainsKey("current_version") ? data["current_version"].ToString() : "v1.0.0",
                        map_name = data.ContainsKey("map_name") ? data["map_name"].ToString() : "Campus Map",
                        last_updated = data.ContainsKey("last_updated") ? Convert.ToInt64(data["last_updated"]) : 0
                    };

                    // Compare with local cached version for this specific map
                    LocalVersionCache localCache = GetLocalVersionCache(mapId);
                    bool needsUpdate = localCache == null || localCache.cached_version != serverVersion.current_version;

                    Debug.Log($"Map {mapId} - Server: {serverVersion.current_version}, Local: {localCache?.cached_version ?? "none"}");
                    onComplete?.Invoke(needsUpdate, serverVersion);
                }
                else
                {
                    Debug.LogWarning($"Map document {mapId} not found in mapVersions collection");
                    onComplete?.Invoke(false, null);
                }
            }
            else
            {
                Debug.LogError($"Failed to check version for map {mapId}: {task.Exception}");
                onComplete?.Invoke(false, null);
            }
        });
    }

    private void SyncSingleMapVersion(MapVersionInfo mapVersion, System.Action onComplete)
    {
        StartCoroutine(SyncSingleMapVersionCoroutine(mapVersion, onComplete));
    }

    private IEnumerator SyncSingleMapVersionCoroutine(MapVersionInfo mapVersion, System.Action onComplete)
    {
        Debug.Log($"Syncing map {mapVersion.map_id} to version: {mapVersion.current_version}");

        int completedSyncs = 0;
        int totalSyncs = versionedCollections.Length;

        // Get the specific version data from subcollection
        DocumentReference versionRef = db.Collection(MAP_VERSIONS_COLLECTION)
            .Document(mapVersion.map_id)
            .Collection(VERSIONS_SUBCOLLECTION)
            .Document(mapVersion.current_version);

        versionRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                DocumentSnapshot snapshot = task.Result;
                if (snapshot.Exists)
                {
                    var versionData = snapshot.ToDictionary();

                    // Extract each collection data from the version document
                    foreach (string collectionName in versionedCollections)
                    {
                        string collectionKey = collectionName.ToLower();
                        if (versionData.ContainsKey(collectionKey))
                        {
                            // The data should be an array of documents
                            var collectionData = versionData[collectionKey];
                            ProcessVersionedCollection(mapVersion.map_id, collectionName, collectionData, () => completedSyncs++);
                        }
                        else
                        {
                            Debug.LogWarning($"Collection {collectionName} not found in version {mapVersion.current_version} for map {mapVersion.map_id}");
                            completedSyncs++;
                        }
                    }
                }
                else
                {
                    Debug.LogError($"Version {mapVersion.current_version} not found for map {mapVersion.map_id}");
                    completedSyncs = totalSyncs; // Skip all syncs
                }
            }
            else
            {
                Debug.LogError($"Failed to fetch version {mapVersion.current_version} for map {mapVersion.map_id}: {task.Exception}");
                completedSyncs = totalSyncs; // Skip all syncs
            }
        });

        yield return new WaitUntil(() => completedSyncs >= totalSyncs);

        // Update local version cache for this specific map
        UpdateLocalVersionCache(mapVersion);
        
        Debug.Log($"Map {mapVersion.map_id} sync completed for version: {mapVersion.current_version}");
        onComplete?.Invoke();
    }

    private void ProcessVersionedCollection(string mapId, string collectionName, object collectionData, System.Action onComplete)
    {
        try
        {
            // Create map-specific filename to avoid conflicts
            string fileName = $"{collectionName.ToLower()}_{mapId}.json";
            string jsonContent;

            if (collectionData is List<object> dataList)
            {
                // Convert the list to JSON array format
                List<Dictionary<string, object>> documents = new List<Dictionary<string, object>>();
                foreach (var item in dataList)
                {
                    if (item is Dictionary<string, object> doc)
                    {
                        documents.Add(doc);
                    }
                }
                jsonContent = ConvertToJsonArray(documents);
            }
            else
            {
                // Handle single object or other formats
                jsonContent = JsonUtility.ToJson(collectionData, true);
            }

            if (JSONFileManager.Instance != null)
            {
                JSONFileManager.Instance.WriteJSONFile(fileName, jsonContent);
                Debug.Log($"Successfully synced {collectionName} for map {mapId}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to process collection {collectionName} for map {mapId}: {ex.Message}");
        }

        onComplete?.Invoke();
    }

    private void SyncStaticCollections(System.Action onComplete)
    {
        // Check static data versions first
        CheckStaticDataVersions((needsUpdate, versionInfo) =>
        {
            if (needsUpdate && versionInfo != null)
            {
                Debug.Log("Static data needs updating");
                SyncStaticDataSelectively(versionInfo, onComplete);
            }
            else
            {
                Debug.Log("Static data is up to date");
                onComplete?.Invoke();
            }
        });
    }

    private void CheckStaticDataVersions(System.Action<bool, StaticDataVersionInfo> onComplete)
    {
        // Get static data version flags from Firestore
        DocumentReference staticRef = db.Collection(STATIC_DATA_VERSIONS_COLLECTION).Document("GlobalInfo");
        staticRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                DocumentSnapshot snapshot = task.Result;
                if (snapshot.Exists)
                {
                    var data = snapshot.ToDictionary();
                    StaticDataVersionInfo serverInfo = new StaticDataVersionInfo
                    {
                        infrastructure_updated = data.ContainsKey("infrastructure_updated") ? (bool)data["infrastructure_updated"] : false,
                        categories_updated = data.ContainsKey("categories_updated") ? (bool)data["categories_updated"] : false,
                        last_check = data.ContainsKey("last_check") ? Convert.ToInt64(data["last_check"]) : 0
                    };

                    // Compare with local cache
                    LocalStaticDataCache localCache = GetLocalStaticDataCache();
                    bool needsUpdate = localCache == null || 
                                     (serverInfo.infrastructure_updated && !localCache.infrastructure_synced) ||
                                     (serverInfo.categories_updated && !localCache.categories_synced);

                    Debug.Log($"Static data check - Infrastructure: {(serverInfo.infrastructure_updated ? "UPDATE NEEDED" : "OK")}, " +
                             $"Categories: {(serverInfo.categories_updated ? "UPDATE NEEDED" : "OK")}");
                    
                    onComplete?.Invoke(needsUpdate, serverInfo);
                }
                else
                {
                    Debug.LogWarning("StaticDataVersions document not found, forcing initial sync");
                    StaticDataVersionInfo defaultInfo = new StaticDataVersionInfo
                    {
                        infrastructure_updated = true, // Force initial sync
                        categories_updated = true,
                        last_check = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };
                    onComplete?.Invoke(true, defaultInfo);
                }
            }
            else
            {
                Debug.LogError($"Failed to check static data versions: {task.Exception}");
                onComplete?.Invoke(false, null);
            }
        });
    }

    private void SyncStaticDataSelectively(StaticDataVersionInfo versionInfo, System.Action onComplete)
    {
        StartCoroutine(SyncStaticDataSelectivelyCoroutine(versionInfo, onComplete));
    }

    private IEnumerator SyncStaticDataSelectivelyCoroutine(StaticDataVersionInfo versionInfo, System.Action onComplete)
    {
        Debug.Log("Starting selective static data sync...");

        int completedSyncs = 0;
        int totalSyncs = 0;

        // Count what needs to be synced
        if (versionInfo.infrastructure_updated) totalSyncs++;
        if (versionInfo.categories_updated) totalSyncs++;

        if (totalSyncs == 0)
        {
            Debug.Log("No static data needs syncing");
            onComplete?.Invoke();
            yield break;
        }

        // Sync Infrastructure if needed
        if (versionInfo.infrastructure_updated)
        {
            Debug.Log("Syncing Infrastructure collection...");
            SyncCollectionToLocal("Infrastructure", () => completedSyncs++);
        }

        // Sync Categories if needed  
        if (versionInfo.categories_updated)
        {
            Debug.Log("Syncing Categories collection...");
            SyncCollectionToLocal("Categories", () => completedSyncs++);
        }

        yield return new WaitUntil(() => completedSyncs >= totalSyncs);

        // Update local cache to reflect successful sync
        UpdateLocalStaticDataCache(versionInfo);
        
        // Reset server flags (tell admin data has been synced)
        ResetStaticDataFlags();

        Debug.Log("Selective static data sync completed");
        onComplete?.Invoke();
    }

    private LocalVersionCache GetLocalVersionCache(string mapId)
    {
        if (JSONFileManager.Instance != null)
        {
            string fileName = $"version_cache_{mapId}.json";
            string cacheContent = JSONFileManager.Instance.ReadJSONFile(fileName);
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
        }
        return null;
    }

    private void UpdateLocalVersionCache(MapVersionInfo serverVersion)
    {
        LocalVersionCache cache = new LocalVersionCache
        {
            map_id = serverVersion.map_id,
            cached_version = serverVersion.current_version,
            map_name = serverVersion.map_name,
            cache_timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        string fileName = $"version_cache_{serverVersion.map_id}.json";
        string jsonContent = JsonUtility.ToJson(cache, true);
        if (JSONFileManager.Instance != null)
        {
            JSONFileManager.Instance.WriteJSONFile(fileName, jsonContent);
            Debug.Log($"Updated local version cache for {serverVersion.map_id} to: {cache.cached_version}");
        }
    }

    private LocalStaticDataCache GetLocalStaticDataCache()
    {
        if (JSONFileManager.Instance != null)
        {
            string cacheContent = JSONFileManager.Instance.ReadJSONFile("static_data_cache.json");
            if (!string.IsNullOrEmpty(cacheContent))
            {
                try
                {
                    return JsonUtility.FromJson<LocalStaticDataCache>(cacheContent);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Failed to parse static data cache: {ex.Message}");
                }
            }
        }
        return null;
    }

    private void UpdateLocalStaticDataCache(StaticDataVersionInfo serverInfo)
    {
        LocalStaticDataCache cache = new LocalStaticDataCache
        {
            infrastructure_synced = true, // Mark as synced after successful download
            categories_synced = true,
            cache_timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        string jsonContent = JsonUtility.ToJson(cache, true);
        if (JSONFileManager.Instance != null)
        {
            JSONFileManager.Instance.WriteJSONFile("static_data_cache.json", jsonContent);
            Debug.Log("Updated local static data cache");
        }
    }

    private void ResetStaticDataFlags()
    {
        // Reset the server flags to false after successful sync
        DocumentReference staticRef = db.Collection(STATIC_DATA_VERSIONS_COLLECTION).Document("GlobalInfo");
        
        var resetData = new Dictionary<string, object>
        {
            { "infrastructure_updated", false },
            { "categories_updated", false },
            { "last_check", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
        };

        staticRef.SetAsync(resetData, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                Debug.Log("Successfully reset static data flags on server");
            }
            else
            {
                Debug.LogWarning($"Failed to reset static data flags: {task.Exception}");
            }
        });
    }

    // Keep existing methods for backward compatibility and manual operations
    public void SyncCollectionToLocal(string collectionName, System.Action onComplete = null)
    {
        if (!isFirebaseReady)
        {
            Debug.LogWarning($"Firebase not ready, cannot sync {collectionName}");
            onComplete?.Invoke();
            return;
        }

        string fileName = $"{collectionName.ToLower()}.json";
        Debug.Log($"Syncing {collectionName} to {fileName}...");

        db.Collection(collectionName).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                QuerySnapshot snapshot = task.Result;
                List<Dictionary<string, object>> documents = new List<Dictionary<string, object>>();

                foreach (DocumentSnapshot document in snapshot.Documents)
                {
                    if (document.Exists)
                    {
                        var docData = document.ToDictionary();
                        docData["id"] = document.Id;
                        documents.Add(docData);
                    }
                }

                string jsonArray = ConvertToJsonArray(documents);
                if (JSONFileManager.Instance != null)
                {
                    JSONFileManager.Instance.WriteJSONFile(fileName, jsonArray);
                    Debug.Log($"Successfully synced {collectionName}: {documents.Count} documents");
                }

                onComplete?.Invoke();
            }
            else
            {
                Debug.LogError($"Failed to sync {collectionName}: {task.Exception}");
                onComplete?.Invoke();
            }
        });
    }

    // Method to get available versions for a specific map
    public void GetAvailableMapVersions(string mapId, System.Action<List<string>> onComplete)
    {
        if (!isFirebaseReady)
        {
            Debug.LogWarning("Firebase not ready");
            onComplete?.Invoke(new List<string>());
            return;
        }

        CollectionReference versionsRef = db.Collection(MAP_VERSIONS_COLLECTION)
            .Document(mapId)
            .Collection(VERSIONS_SUBCOLLECTION);

        versionsRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                List<string> versions = new List<string>();
                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    versions.Add(doc.Id);
                }
                
                // Sort versions (assuming they follow semantic versioning)
                versions.Sort((a, b) => CompareVersions(a, b));
                onComplete?.Invoke(versions);
            }
            else
            {
                Debug.LogError($"Failed to get available versions for map {mapId}: {task.Exception}");
                onComplete?.Invoke(new List<string>());
            }
        });
    }

    // Method to switch to a specific version of a specific map
    public void SwitchToMapVersion(string mapId, string version, System.Action onComplete = null)
    {
        Debug.Log($"Switching map {mapId} to version: {version}");
        
        // Update local cache to reflect the switch
        MapVersionInfo versionInfo = new MapVersionInfo
        {
            map_id = mapId,
            current_version = version,
            map_name = "Campus Map",
            last_updated = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // Sync the specific version data
        SyncSingleMapVersion(versionInfo, () =>
        {
            Debug.Log($"Successfully switched map {mapId} to version: {version}");
            onComplete?.Invoke();
        });
    }

    // Helper method to get current version for a specific map
    public string GetCurrentMapVersion(string mapId)
    {
        LocalVersionCache cache = GetLocalVersionCache(mapId);
        return cache?.cached_version ?? "unknown";
    }

    // Helper method to get all current map versions
    public Dictionary<string, string> GetAllCurrentMapVersions()
    {
        Dictionary<string, string> versions = new Dictionary<string, string>();
        foreach (var map in availableMaps)
        {
            versions[map.map_id] = GetCurrentMapVersion(map.map_id);
        }
        return versions;
    }

    private int CompareVersions(string v1, string v2)
    {
        // Simple version comparison (you might want to implement semantic versioning)
        return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase);
    }

    private string ConvertToJsonArray(List<Dictionary<string, object>> documents)
    {
        List<string> formattedDocs = new List<string>();
        foreach (var doc in documents)
        {
            formattedDocs.Add(ConvertDictionaryToJsonPretty(doc, 1));
        }

        return "[\n" + string.Join(",\n", formattedDocs) + "\n]";
    }

    private string ConvertDictionaryToJsonPretty(Dictionary<string, object> dict, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 4);
        List<string> keyValuePairs = new List<string>();

        foreach (var kvp in dict)
        {
            string value;

            if (kvp.Value == null)
            {
                value = "null";
            }
            else if (kvp.Value is string)
            {
                value = $"\"{kvp.Value}\"";
            }
            else if (kvp.Value is bool)
            {
                value = kvp.Value.ToString().ToLower();
            }
            else if (kvp.Value is Firebase.Firestore.Timestamp timestamp)
            {
                value = $"\"{timestamp.ToDateTime():yyyy-MM-ddTHH:mm:ss.fffZ}\"";
            }
            else if (kvp.Value is Dictionary<string, object> nestedDict)
            {
                value = ConvertDictionaryToJsonPretty(nestedDict, indentLevel + 1);
            }
            else
            {
                value = kvp.Value.ToString();
            }

            keyValuePairs.Add($"{indent}    \"{kvp.Key}\": {value}");
        }

        return "{\n" + string.Join(",\n", keyValuePairs) + "\n" + indent + "}";
    }

    // Keep existing methods
    public void FetchDocument(string collectionName, string documentId, System.Action<Dictionary<string, object>> onComplete)
    {
        if (!isFirebaseReady)
        {
            Debug.LogWarning("Firebase not ready");
            onComplete?.Invoke(null);
            return;
        }

        DocumentReference docRef = db.Collection(collectionName).Document(documentId);
        docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                DocumentSnapshot snapshot = task.Result;
                if (snapshot.Exists)
                {
                    Dictionary<string, object> data = snapshot.ToDictionary();
                    data["id"] = snapshot.Id;
                    onComplete?.Invoke(data);
                }
                else
                {
                    Debug.LogWarning($"Document {documentId} not found in {collectionName}");
                    onComplete?.Invoke(null);
                }
            }
            else
            {
                Debug.LogError($"Failed to fetch document: {task.Exception}");
                onComplete?.Invoke(null);
            }
        });
    }

    public void ListenToCollection(string collectionName, System.Action<List<Dictionary<string, object>>> onUpdate)
    {
        if (!isFirebaseReady)
        {
            Debug.LogWarning("Firebase not ready for real-time listening");
            return;
        }

        db.Collection(collectionName).Listen(snapshot =>
        {
            List<Dictionary<string, object>> documents = new List<Dictionary<string, object>>();

            foreach (DocumentSnapshot document in snapshot.Documents)
            {
                if (document.Exists)
                {
                    var docData = document.ToDictionary();
                    docData["id"] = document.Id;
                    documents.Add(docData);
                }
            }

            Debug.Log($"Real-time update received for {collectionName}: {documents.Count} documents");
            onUpdate?.Invoke(documents);
        });
    }
}