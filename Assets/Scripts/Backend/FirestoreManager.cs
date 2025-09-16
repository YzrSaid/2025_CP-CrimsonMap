using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;
using System;
using System.Linq;
using Newtonsoft.Json;

public class FirestoreManager : MonoBehaviour
{
    public static FirestoreManager Instance { get; private set; }

    private FirebaseFirestore db;
    private bool isFirebaseReady = false;
    private List<MapInfo> availableMaps = new List<MapInfo>();

    // Collections configuration
    private readonly string MAPS_COLLECTION = "Maps";
    private readonly string MAP_VERSIONS_COLLECTION = "MapVersions";
    private readonly string VERSIONS_SUBCOLLECTION = "versions";
    private readonly string STATIC_DATA_VERSIONS_COLLECTION = "StaticDataVersions";

    // Collections that need to be synced (non-versioned data) - Campus moved here
    private readonly string[] staticCollections = {
        "Infrastructure", // Building info, room details (not affected by map versions)
        "Categories",     // Category definitions
        "Campus"          // Campus data - now static instead of versioned
    };

    // Version-controlled collections (only Nodes and Edges now)
    private readonly string[] versionedCollections = {
        "Nodes",
        "Edges"
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

        // Step 3: Check versions for all maps (only for Nodes and Edges now)
        bool allMapsChecked = false;
        CheckAllMapVersions(() => allMapsChecked = true);
        yield return new WaitUntil(() => allMapsChecked);

        Debug.Log("now lets go");

        // Step 4: Check static collections (Infrastructure, Categories, Campus)
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
                    var mapsArray = JsonConvert.DeserializeObject<List<MapInfo>>(mapsJson);
                    availableMaps.AddRange(mapsArray);

                    Debug.Log($"Loaded {availableMaps.Count} available maps:");
                    foreach (var map in availableMaps)
                    {
                        Debug.Log($"  - {map.map_id}: {map.map_name}");
                        Debug.Log($"    campuses: {string.Join(", ", map.campus_included)}");
                    }
                }
                catch (Exception ex)
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

                    // Debug log to see what fields are actually available
                    Debug.Log($"MapVersions document fields for {mapId}:");
                    foreach (var kvp in data)
                    {
                        Debug.Log($"  {kvp.Key}: {kvp.Value} (Type: {kvp.Value?.GetType()})");
                    }

                    MapVersionInfo serverVersion = new MapVersionInfo
                    {
                        map_id = mapId,
                        // Try different possible field names for current_version
                        current_version = GetStringValue(data, "current_version") ??
                                         GetStringValue(data, "currentVersion") ??
                                         GetStringValue(data, "version") ??
                                         "v1.0.0",
                        // Try different possible field names for map_name
                        map_name = GetStringValue(data, "map_name") ??
                                  GetStringValue(data, "mapName") ??
                                  GetStringValue(data, "name") ??
                                  "Campus Map",
                        // Handle timestamp conversion more safely
                        last_updated = GetTimestampValue(data)
                    };

                    Debug.Log($"Extracted server version info:");
                    Debug.Log($"  map_id: {serverVersion.map_id}");
                    Debug.Log($"  current_version: {serverVersion.current_version}");
                    Debug.Log($"  map_name: {serverVersion.map_name}");
                    Debug.Log($"  last_updated: {serverVersion.last_updated}");

                    // Compare with local cached version for this specific map
                    LocalVersionCache localCache = GetLocalVersionCache(mapId);
                    bool needsUpdate = localCache == null ||
                                      string.IsNullOrEmpty(localCache.cached_version) ||
                                      localCache.cached_version != serverVersion.current_version;

                    Debug.Log($"Map {mapId} version check:");
                    Debug.Log($"  Server version: '{serverVersion.current_version}'");
                    Debug.Log($"  Local cached version: '{localCache?.cached_version ?? "none"}'");
                    Debug.Log($"  Needs update: {needsUpdate}");

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

    // Helper method to safely extract string values
    private string GetStringValue(Dictionary<string, object> data, string key)
    {
        if (data.ContainsKey(key) && data[key] != null)
        {
            return data[key].ToString();
        }
        return null;
    }

    // Helper method to safely extract timestamp values
    private long GetTimestampValue(Dictionary<string, object> data)
    {
        // Try different possible timestamp field names
        string[] timestampFields = { "last_updated", "lastUpdated", "createdAt", "created_at", "updatedAt", "updated_at" };

        foreach (string field in timestampFields)
        {
            if (data.ContainsKey(field) && data[field] != null)
            {
                var value = data[field];

                // Handle Firebase Timestamp
                if (value is Firebase.Firestore.Timestamp timestamp)
                {
                    return (long)timestamp.ToDateTime().ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                }
                // Handle long/int values
                else if (value is long longVal)
                {
                    return longVal;
                }
                else if (value is int intVal)
                {
                    return intVal;
                }
                // Try parsing string values
                else if (long.TryParse(value.ToString(), out long parsedVal))
                {
                    return parsedVal;
                }
            }
        }

        // Return current timestamp if no valid timestamp found
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    // Also update your SyncSingleMapVersionCoroutine method to add debug logging
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

        Debug.Log($"Looking for version document at: {MAP_VERSIONS_COLLECTION}/{mapVersion.map_id}/{VERSIONS_SUBCOLLECTION}/{mapVersion.current_version}");

        versionRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                DocumentSnapshot snapshot = task.Result;
                if (snapshot.Exists)
                {
                    var versionData = snapshot.ToDictionary();

                    // Debug log to see what's in the version document
                    Debug.Log($"Version document fields for {mapVersion.map_id} v{mapVersion.current_version}:");
                    foreach (var kvp in versionData)
                    {
                        if (kvp.Value is IEnumerable<object> list && !(kvp.Value is string))
                        {
                            Debug.Log($"  {kvp.Key}: Array with {((IEnumerable<object>)kvp.Value).Count()} items");
                        }
                        else
                        {
                            Debug.Log($"  {kvp.Key}: {kvp.Value} (Type: {kvp.Value?.GetType()})");
                        }
                    }

                    // Extract each collection data from the version document (only Nodes and Edges)
                    foreach (string collectionName in versionedCollections)
                    {
                        string collectionKey = collectionName.ToLower();
                        Debug.Log($"Looking for collection key: '{collectionKey}' in version data");

                        if (versionData.ContainsKey(collectionKey))
                        {
                            // The data should be an array of documents
                            var collectionData = versionData[collectionKey];
                            Debug.Log($"Found {collectionName} data for {mapVersion.map_id}");
                            ProcessVersionedCollection(mapVersion.map_id, collectionName, collectionData, () => completedSyncs++);
                        }
                        else
                        {
                            Debug.LogWarning($"Collection key '{collectionKey}' not found in version {mapVersion.current_version} for map {mapVersion.map_id}");
                            Debug.Log($"Available keys: {string.Join(", ", versionData.Keys)}");
                            completedSyncs++;
                        }
                    }
                }
                else
                {
                    Debug.LogError($"Version document {mapVersion.current_version} not found for map {mapVersion.map_id}");
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

    private void SyncSingleMapVersion(MapVersionInfo mapVersion, System.Action onComplete)
    {
        StartCoroutine(SyncSingleMapVersionCoroutine(mapVersion, onComplete));
    }


    private void ProcessVersionedCollection(string mapId, string collectionName, object collectionData, System.Action onComplete)
    {
        try
        {
            // Create map-specific filename only for versioned collections (Nodes and Edges)
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
        Debug.Log("weeew");
        DocumentReference staticRef = db.Collection(STATIC_DATA_VERSIONS_COLLECTION).Document("GlobalInfo");
        staticRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                DocumentSnapshot snapshot = task.Result;
                if (snapshot.Exists)
                {

                    var data = snapshot.ToDictionary();

                    Debug.Log("lol");
                    StaticDataVersionInfo serverInfo = new StaticDataVersionInfo
                    {
                        infrastructure_updated = data.ContainsKey("infrastructure_updated") ? (bool)data["infrastructure_updated"] : false,
                        categories_updated = data.ContainsKey("categories_updated") ? (bool)data["categories_updated"] : false,
                        campus_updated = data.ContainsKey("campus_updated") ? (bool)data["campus_updated"] : false,

                    };

                    Debug.Log("wuw");

                    Debug.Log($"Server flags - Infrastructure: {serverInfo.infrastructure_updated}, Categories: {serverInfo.categories_updated}, Campus: {serverInfo.campus_updated}");

                    // Get local cache
                    LocalStaticDataCache localCache = GetLocalStaticDataCache();

                    // Log local cache state
                    if (localCache != null)
                    {
                        Debug.Log($"Local cache - Infrastructure: {localCache.infrastructure_synced}, Categories: {localCache.categories_synced}, Campus: {localCache.campus_synced}");
                    }
                    else
                    {
                        Debug.Log("No local cache found - will sync all collections");
                    }

                    bool bootstrapNeeded = localCache != null &&
                         !localCache.infrastructure_synced &&
                         !localCache.categories_synced &&
                         !localCache.campus_synced &&
                         !serverInfo.infrastructure_updated &&
                         !serverInfo.categories_updated &&
                         !serverInfo.campus_updated;

                    bool needsUpdate = localCache == null || bootstrapNeeded ||
                                       serverInfo.infrastructure_updated ||
                                       serverInfo.categories_updated ||
                                       serverInfo.campus_updated;


                    Debug.Log($"Needs update: {needsUpdate}");

                    onComplete?.Invoke(needsUpdate, serverInfo);
                }
                else
                {
                    Debug.LogWarning("StaticDataVersions document not found, forcing initial sync");
                    StaticDataVersionInfo defaultInfo = new StaticDataVersionInfo
                    {
                        infrastructure_updated = true,
                        categories_updated = true,
                        campus_updated = true,
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

    private IEnumerator SyncStaticDataSelectivelyCoroutine(StaticDataVersionInfo versionInfo, System.Action onComplete)
    {
        Debug.Log("=== STARTING SELECTIVE STATIC DATA SYNC DEBUG ===");
        Debug.Log($"Server flags - Infrastructure: {versionInfo.infrastructure_updated}, Categories: {versionInfo.categories_updated}, Campus: {versionInfo.campus_updated}");

        List<string> collectionsToSync = new List<string>();
        List<string> flagsToReset = new List<string>();

        // Get current local cache state
        LocalStaticDataCache localCache = GetLocalStaticDataCache();
        bool hasLocalCache = localCache != null;

        Debug.Log($"Local cache exists: {hasLocalCache}");
        if (hasLocalCache)
        {
            Debug.Log($"Local cache values - Infrastructure: {localCache.infrastructure_synced}, Categories: {localCache.categories_synced}, Campus: {localCache.campus_synced}");
        }
        else
        {
            Debug.Log("No local cache found");
        }

        // Infrastructure check with detailed logging
        bool infraCondition1 = versionInfo.infrastructure_updated;
        bool infraCondition2 = !hasLocalCache;
        bool infraCondition3 = hasLocalCache && !localCache.infrastructure_synced;
        bool shouldSyncInfra = infraCondition1 || infraCondition2 || infraCondition3;

        Debug.Log($"Infrastructure sync check:");
        Debug.Log($"  - Server updated: {infraCondition1}");
        Debug.Log($"  - No local cache: {infraCondition2}");
        Debug.Log($"  - Local not synced: {infraCondition3}");
        Debug.Log($"  - Should sync: {shouldSyncInfra}");

        if (shouldSyncInfra)
        {
            collectionsToSync.Add("Infrastructure");
            flagsToReset.Add("infrastructure_updated");
            Debug.Log("✅ Infrastructure will be synced");
        }
        else
        {
            Debug.Log("❌ Infrastructure will NOT be synced");
        }

        // Categories check with detailed logging
        bool catsCondition1 = versionInfo.categories_updated;
        bool catsCondition2 = !hasLocalCache;
        bool catsCondition3 = hasLocalCache && !localCache.categories_synced;
        bool shouldSyncCats = catsCondition1 || catsCondition2 || catsCondition3;

        Debug.Log($"Categories sync check:");
        Debug.Log($"  - Server updated: {catsCondition1}");
        Debug.Log($"  - No local cache: {catsCondition2}");
        Debug.Log($"  - Local not synced: {catsCondition3}");
        Debug.Log($"  - Should sync: {shouldSyncCats}");

        if (shouldSyncCats)
        {
            collectionsToSync.Add("Categories");
            flagsToReset.Add("categories_updated");
            Debug.Log("✅ Categories will be synced");
        }
        else
        {
            Debug.Log("❌ Categories will NOT be synced");
        }

        // Campus check with detailed logging
        bool campusCondition1 = versionInfo.campus_updated;
        bool campusCondition2 = !hasLocalCache;
        bool campusCondition3 = hasLocalCache && !localCache.campus_synced;
        bool shouldSyncCampus = campusCondition1 || campusCondition2 || campusCondition3;

        Debug.Log($"Campus sync check:");
        Debug.Log($"  - Server updated: {campusCondition1}");
        Debug.Log($"  - No local cache: {campusCondition2}");
        Debug.Log($"  - Local not synced: {campusCondition3}");
        Debug.Log($"  - Should sync: {shouldSyncCampus}");

        if (shouldSyncCampus)
        {
            collectionsToSync.Add("Campus");
            flagsToReset.Add("campus_updated");
            Debug.Log("✅ Campus will be synced");
        }
        else
        {
            Debug.Log("❌ Campus will NOT be synced");
        }

        Debug.Log($"=== SYNC DECISION: Will sync {collectionsToSync.Count} collections ===");
        foreach (string collection in collectionsToSync)
        {
            Debug.Log($"  - {collection}");
        }

        if (collectionsToSync.Count == 0)
        {
            Debug.Log("❌ No collections to sync - EXITING");
            onComplete?.Invoke();
            yield break;
        }

        // Track successful syncs
        Dictionary<string, bool> syncResults = new Dictionary<string, bool>();
        int completedSyncs = 0;

        // Sync each collection
        foreach (string collectionName in collectionsToSync)
        {
            syncResults[collectionName] = false; // Initialize as failed

            Debug.Log($"🔄 Starting sync for {collectionName}...");
            SyncCollectionToLocalWithCallback(collectionName, (success) =>
            {
                syncResults[collectionName] = success;
                completedSyncs++;
                Debug.Log($"✅ Sync result for {collectionName}: {(success ? "SUCCESS" : "❌ FAILED")}");
            });
        }

        // Wait for all syncs to complete
        Debug.Log("⏳ Waiting for all syncs to complete...");
        yield return new WaitUntil(() => completedSyncs >= collectionsToSync.Count);

        Debug.Log("=== ALL SYNCS COMPLETED - RESULTS ===");
        foreach (var result in syncResults)
        {
            Debug.Log($"  {result.Key}: {(result.Value ? "✅ SUCCESS" : "❌ FAILED")}");
        }

        // Update local cache only for successfully synced collections
        Debug.Log("📝 Updating local cache...");
        UpdateLocalStaticDataCacheSelectively(syncResults);

        // Reset server flags only for successfully synced collections
        Debug.Log("🔄 Resetting server flags...");
        ResetStaticDataFlagsSelectively(syncResults, flagsToReset);

        Debug.Log("=== SELECTIVE STATIC DATA SYNC COMPLETED ===");
        onComplete?.Invoke();
    }

    // NEW METHOD: Sync with success callback
    private void SyncCollectionToLocalWithCallback(string collectionName, System.Action<bool> onComplete)
    {
        if (!isFirebaseReady)
        {
            Debug.LogWarning($"Firebase not ready, cannot sync {collectionName}");
            onComplete?.Invoke(false);
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
                        var rawDict = document.ToDictionary();
                        Dictionary<string, object> docData = new Dictionary<string, object>();

                        foreach (var kv in rawDict)
                        {
                            if (kv.Value is IEnumerable<object> listValue && !(kv.Value is string))
                            {
                                docData[kv.Key] = listValue.Select(v => v?.ToString()).ToList();
                            }
                            else
                            {
                                docData[kv.Key] = kv.Value;
                            }
                        }

                        docData["id"] = document.Id;
                        documents.Add(docData);
                    }
                }

                string jsonArray = JsonConvert.SerializeObject(documents, Formatting.Indented);

                if (JSONFileManager.Instance != null)
                {
                    try
                    {
                        JSONFileManager.Instance.WriteJSONFile(fileName, jsonArray);
                        Debug.Log($"Successfully synced {collectionName}: {documents.Count} documents written to {fileName}");

                        // Verify the file was actually written
                        string verifyContent = JSONFileManager.Instance.ReadJSONFile(fileName);
                        if (!string.IsNullOrEmpty(verifyContent))
                        {
                            Debug.Log($"Verified: {fileName} contains data");
                            onComplete?.Invoke(true);
                        }
                        else
                        {
                            Debug.LogError($"Verification failed: {fileName} is empty after write");
                            onComplete?.Invoke(false);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Failed to write {fileName}: {ex.Message}");
                        onComplete?.Invoke(false);
                    }
                }
                else
                {
                    Debug.LogError("JSONFileManager.Instance is null");
                    onComplete?.Invoke(false);
                }
            }
            else
            {
                Debug.LogError($"Failed to sync {collectionName}: {task.Exception}");
                onComplete?.Invoke(false);
            }
        });
    }

    // Fixed method: Update cache selectively based on sync results
    private void UpdateLocalStaticDataCacheSelectively(Dictionary<string, bool> syncResults)
    {
        // Get existing cache or create new one with proper defaults
        LocalStaticDataCache cache = GetLocalStaticDataCache();

        if (cache == null)
        {
            Debug.Log("No existing local cache found, creating new one");
            cache = new LocalStaticDataCache
            {
                infrastructure_synced = false,
                categories_synced = false,
                campus_synced = false,
            };
        }
        else
        {
            Debug.Log($"Existing cache found - Infrastructure: {cache.infrastructure_synced}, Categories: {cache.categories_synced}, Campus: {cache.campus_synced}");
        }

        // Update only the successfully synced collections
        if (syncResults.ContainsKey("Infrastructure") && syncResults["Infrastructure"])
        {
            cache.infrastructure_synced = true;
            Debug.Log("Local cache: Infrastructure marked as synced");
        }

        if (syncResults.ContainsKey("Categories") && syncResults["Categories"])
        {
            cache.categories_synced = true;
            Debug.Log("Local cache: Categories marked as synced");
        }

        if (syncResults.ContainsKey("Campus") && syncResults["Campus"])
        {
            cache.campus_synced = true;
            Debug.Log("Local cache: Campus marked as synced");
        }


        // Save updated cache
        string jsonContent = JsonUtility.ToJson(cache, true);
        if (JSONFileManager.Instance != null)
        {
            JSONFileManager.Instance.WriteJSONFile("static_data_cache.json", jsonContent);
            Debug.Log($"Updated local static data cache: Infrastructure={cache.infrastructure_synced}, Categories={cache.categories_synced}, Campus={cache.campus_synced}");
        }
        else
        {
            Debug.LogError("JSONFileManager.Instance is null, cannot save cache");
        }
    }

    // NEW METHOD: Reset server flags selectively
    private void ResetStaticDataFlagsSelectively(Dictionary<string, bool> syncResults, List<string> flagsToReset)
    {
        DocumentReference staticRef = db.Collection(STATIC_DATA_VERSIONS_COLLECTION).Document("GlobalInfo");

        // Prepare data to update - only reset flags for successful syncs
        var resetData = new Dictionary<string, object>();
        bool hasUpdates = false;

        foreach (string flag in flagsToReset)
        {
            string collectionName = flag.Replace("_updated", "").Replace("_", "");
            collectionName = char.ToUpper(collectionName[0]) + collectionName.Substring(1); // Capitalize

            if (syncResults.ContainsKey(collectionName) && syncResults[collectionName])
            {
                resetData[flag] = false;
                hasUpdates = true;
                Debug.Log($"Will reset server flag: {flag}");
            }
            else
            {
                Debug.Log($"Will NOT reset server flag {flag} - sync failed or not attempted");
            }
        }

        // Always update last_check
        resetData["last_check"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        hasUpdates = true;

        if (hasUpdates)
        {
            staticRef.SetAsync(resetData, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    Debug.Log("Successfully updated server flags for successful syncs");
                }
                else
                {
                    Debug.LogWarning($"Failed to update server flags: {task.Exception}");
                }
            });
        }
    }

    private void SyncStaticDataSelectively(StaticDataVersionInfo versionInfo, System.Action onComplete)
    {
        StartCoroutine(SyncStaticDataSelectivelyCoroutine(versionInfo, onComplete));
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
            campus_synced = true, // Added campus sync flag
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
            { "campus_updated", false }, // Reset campus flag too
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
                        var rawDict = document.ToDictionary();
                        Dictionary<string, object> docData = new Dictionary<string, object>();

                        foreach (var kv in rawDict)
                        {
                            if (kv.Value is IEnumerable<object> listValue && !(kv.Value is string))
                            {
                                // ✅ Convert Firestore array → JSON array of strings
                                docData[kv.Key] = listValue.Select(v => v?.ToString()).ToList();
                            }
                            else
                            {
                                docData[kv.Key] = kv.Value;
                            }
                        }

                        docData["id"] = document.Id;
                        documents.Add(docData);
                    }
                }

                string jsonArray = JsonConvert.SerializeObject(documents, Formatting.Indented);

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