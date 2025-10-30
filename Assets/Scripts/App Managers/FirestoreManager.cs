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

    private readonly string MAPS_COLLECTION = "Maps";
    private readonly string MAP_VERSIONS_COLLECTION = "MapVersions";
    private readonly string VERSIONS_SUBCOLLECTION = "versions";
    private readonly string STATIC_DATA_VERSIONS_COLLECTION = "StaticDataVersions";

    private readonly string[] staticCollections = {
        "Infrastructure",
        "Categories",
        "Campus",
        "IndoorInfrastructure"
    };

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
            onComplete?.Invoke(true);
        }
        else
        {
            isFirebaseReady = false;
            onComplete?.Invoke(false);
        }
    }

    public void CheckAndSyncData(System.Action onComplete = null)
    {
        if (!isFirebaseReady)
        {
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(CheckAndSyncDataCoroutine(onComplete));
    }

    private IEnumerator CheckAndSyncDataCoroutine(System.Action onComplete)
    {
        bool mapsSyncComplete = false;
        SyncCollectionToLocal(MAPS_COLLECTION, () => mapsSyncComplete = true);
        yield return new WaitUntil(() => mapsSyncComplete);

        LoadAvailableMaps();
        
        if (JSONFileManager.Instance != null)
        {
            List<string> currentMapIds = availableMaps.Select(m => m.map_id).ToList();
            JSONFileManager.Instance.CleanupUnusedMapFiles(currentMapIds);
        }

        if (availableMaps.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        bool allMapsChecked = false;
        CheckAllMapVersions(() => allMapsChecked = true);
        yield return new WaitUntil(() => allMapsChecked);

        bool staticSyncComplete = false;
        SyncStaticCollections(() => staticSyncComplete = true);
        yield return new WaitUntil(() => staticSyncComplete);

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
                }
                catch (Exception ex)
                {
                }
            }
        }
    }

    private void CheckAllMapVersions(System.Action onComplete)
    {
        if (availableMaps.Count == 0)
        {
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

        foreach (var mapInfo in availableMaps)
        {
            CheckSingleMapVersion(mapInfo.map_id, (needsUpdate, serverVersion) =>
            {
                if (needsUpdate && serverVersion != null)
                {
                    mapsNeedingUpdate.Add(serverVersion);
                }
                completedChecks++;
            });
        }

        yield return new WaitUntil(() => completedChecks >= totalMaps);

        if (mapsNeedingUpdate.Count > 0)
        {
            int completedSyncs = 0;
            foreach (var mapVersion in mapsNeedingUpdate)
            {
                SyncSingleMapVersion(mapVersion, () => completedSyncs++);
            }

            yield return new WaitUntil(() => completedSyncs >= mapsNeedingUpdate.Count);
        }

        onComplete?.Invoke();
    }

    private void CheckSingleMapVersion(string mapId, System.Action<bool, MapVersionInfo> onComplete)
    {
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
                        current_version = GetStringValue(data, "current_version") ??
                                         GetStringValue(data, "currentVersion") ??
                                         GetStringValue(data, "version") ??
                                         "v1.0.0",
                        map_name = GetStringValue(data, "map_name") ??
                                  GetStringValue(data, "mapName") ??
                                  GetStringValue(data, "name") ??
                                  "Campus Map",
                        last_updated = GetTimestampValue(data)
                    };

                    LocalVersionCache localCache = GetLocalVersionCache(mapId);
                    bool needsUpdate = localCache == null ||
                                      string.IsNullOrEmpty(localCache.cached_version) ||
                                      localCache.cached_version != serverVersion.current_version;

                    onComplete?.Invoke(needsUpdate, serverVersion);
                }
                else
                {
                    onComplete?.Invoke(false, null);
                }
            }
            else
            {
                onComplete?.Invoke(false, null);
            }
        });
    }

    private string GetStringValue(Dictionary<string, object> data, string key)
    {
        if (data.ContainsKey(key) && data[key] != null)
        {
            return data[key].ToString();
        }
        return null;
    }

    private long GetTimestampValue(Dictionary<string, object> data)
    {
        string[] timestampFields = { "last_updated", "lastUpdated", "createdAt", "created_at", "updatedAt", "updated_at" };

        foreach (string field in timestampFields)
        {
            if (data.ContainsKey(field) && data[field] != null)
            {
                var value = data[field];

                if (value is Firebase.Firestore.Timestamp timestamp)
                {
                    return (long)timestamp.ToDateTime().ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                }
                else if (value is long longVal)
                {
                    return longVal;
                }
                else if (value is int intVal)
                {
                    return intVal;
                }
                else if (long.TryParse(value.ToString(), out long parsedVal))
                {
                    return parsedVal;
                }
            }
        }

        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private IEnumerator SyncSingleMapVersionCoroutine(MapVersionInfo mapVersion, System.Action onComplete)
    {
        int completedSyncs = 0;
        int totalSyncs = versionedCollections.Length;

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

                    foreach (string collectionName in versionedCollections)
                    {
                        string collectionKey = collectionName.ToLower();

                        if (versionData.ContainsKey(collectionKey))
                        {
                            var collectionData = versionData[collectionKey];
                            ProcessVersionedCollection(mapVersion.map_id, collectionName, collectionData, () => completedSyncs++);
                        }
                        else
                        {
                            completedSyncs++;
                        }
                    }
                }
                else
                {
                    completedSyncs = totalSyncs;
                }
            }
            else
            {
                completedSyncs = totalSyncs;
            }
        });

        yield return new WaitUntil(() => completedSyncs >= totalSyncs);

        UpdateLocalVersionCache(mapVersion);

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
            string fileName = $"{collectionName.ToLower()}_{mapId}.json";
            string jsonContent;

            if (collectionData is List<object> dataList)
            {
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
                jsonContent = JsonUtility.ToJson(collectionData, true);
            }

            if (JSONFileManager.Instance != null)
            {
                JSONFileManager.Instance.WriteJSONFile(fileName, jsonContent);
            }
        }
        catch (System.Exception ex)
        {
        }

        onComplete?.Invoke();
    }

    private void SyncStaticCollections(System.Action onComplete)
    {
        CheckStaticDataVersions((needsUpdate, versionInfo) =>
        {
            if (needsUpdate && versionInfo != null)
            {
                SyncStaticDataSelectively(versionInfo, onComplete);
            }
            else
            {
                onComplete?.Invoke();
            }
        });
    }

    private void CheckStaticDataVersions(System.Action<bool, StaticDataVersionInfo> onComplete)
    {
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
                        campus_updated = data.ContainsKey("campus_updated") ? (bool)data["campus_updated"] : false,
                        indoor_infrastructure_updated = data.ContainsKey("indoor_infrastructure_updated") ? (bool)data["indoor_infrastructure_updated"] : false
                    };

                    LocalStaticDataCache localCache = GetLocalStaticDataCache();

                    bool bootstrapNeeded = localCache != null &&
                         !localCache.infrastructure_synced &&
                         !localCache.categories_synced &&
                         !localCache.campus_synced &&
                         !localCache.indoor_synced &&
                         !serverInfo.infrastructure_updated &&
                         !serverInfo.categories_updated &&
                         !serverInfo.campus_updated &&
                         !serverInfo.indoor_infrastructure_updated;

                    bool needsUpdate = localCache == null || bootstrapNeeded ||
                                       serverInfo.infrastructure_updated ||
                                       serverInfo.categories_updated ||
                                       serverInfo.campus_updated ||
                                       serverInfo.indoor_infrastructure_updated;

                    onComplete?.Invoke(needsUpdate, serverInfo);
                }
                else
                {
                    StaticDataVersionInfo defaultInfo = new StaticDataVersionInfo
                    {
                        infrastructure_updated = true,
                        categories_updated = true,
                        campus_updated = true,
                        indoor_infrastructure_updated = true
                    };
                    onComplete?.Invoke(true, defaultInfo);
                }
            }
            else
            {
                onComplete?.Invoke(false, null);
            }
        });
    }

    private IEnumerator SyncStaticDataSelectivelyCoroutine(StaticDataVersionInfo versionInfo, System.Action onComplete)
    {
        List<string> collectionsToSync = new List<string>();
        List<string> flagsToReset = new List<string>();

        LocalStaticDataCache localCache = GetLocalStaticDataCache();
        bool hasLocalCache = localCache != null;

        bool shouldSyncInfra = versionInfo.infrastructure_updated || !hasLocalCache || (hasLocalCache && !localCache.infrastructure_synced);
        if (shouldSyncInfra)
        {
            collectionsToSync.Add("Infrastructure");
            flagsToReset.Add("infrastructure_updated");
        }

        bool shouldSyncCats = versionInfo.categories_updated || !hasLocalCache || (hasLocalCache && !localCache.categories_synced);
        if (shouldSyncCats)
        {
            collectionsToSync.Add("Categories");
            flagsToReset.Add("categories_updated");
        }

        bool shouldSyncCampus = versionInfo.campus_updated || !hasLocalCache || (hasLocalCache && !localCache.campus_synced);
        if (shouldSyncCampus)
        {
            collectionsToSync.Add("Campus");
            flagsToReset.Add("campus_updated");
        }

        bool shouldSyncIndoor = versionInfo.indoor_infrastructure_updated || !hasLocalCache || (hasLocalCache && !localCache.indoor_synced);
        if (shouldSyncIndoor)
        {
            collectionsToSync.Add("IndoorInfrastructure");
            flagsToReset.Add("indoor_infrastructure_updated");
        }

        if (collectionsToSync.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        Dictionary<string, bool> syncResults = new Dictionary<string, bool>();
        int completedSyncs = 0;

        foreach (string collectionName in collectionsToSync)
        {
            syncResults[collectionName] = false;

            SyncCollectionToLocalWithCallback(collectionName, (success) =>
            {
                syncResults[collectionName] = success;
                completedSyncs++;
            });
        }

        yield return new WaitUntil(() => completedSyncs >= collectionsToSync.Count);

        UpdateLocalStaticDataCacheSelectively(syncResults);

        ResetStaticDataFlagsSelectively(syncResults, flagsToReset);

        onComplete?.Invoke();
    }

    private void SyncCollectionToLocalWithCallback(string collectionName, System.Action<bool> onComplete)
    {
        if (!isFirebaseReady)
        {
            onComplete?.Invoke(false);
            return;
        }

        string fileName;
        if (collectionName == "IndoorInfrastructure")
        {
            fileName = "indoor.json";
        }
        else
        {
            fileName = $"{collectionName.ToLower()}.json";
        }

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

                        string verifyContent = JSONFileManager.Instance.ReadJSONFile(fileName);
                        if (!string.IsNullOrEmpty(verifyContent))
                        {
                            onComplete?.Invoke(true);
                        }
                        else
                        {
                            onComplete?.Invoke(false);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        onComplete?.Invoke(false);
                    }
                }
                else
                {
                    onComplete?.Invoke(false);
                }
            }
            else
            {
                onComplete?.Invoke(false);
            }
        });
    }

    private void UpdateLocalStaticDataCacheSelectively(Dictionary<string, bool> syncResults)
    {
        LocalStaticDataCache cache = GetLocalStaticDataCache();

        if (cache == null)
        {
            cache = new LocalStaticDataCache
            {
                infrastructure_synced = false,
                categories_synced = false,
                campus_synced = false,
                indoor_synced = false
            };
        }

        if (syncResults.ContainsKey("Infrastructure") && syncResults["Infrastructure"])
        {
            cache.infrastructure_synced = true;
        }

        if (syncResults.ContainsKey("Categories") && syncResults["Categories"])
        {
            cache.categories_synced = true;
        }

        if (syncResults.ContainsKey("Campus") && syncResults["Campus"])
        {
            cache.campus_synced = true;
        }

        if (syncResults.ContainsKey("IndoorInfrastructure") && syncResults["IndoorInfrastructure"])
        {
            cache.indoor_synced = true;
        }

        string jsonContent = JsonUtility.ToJson(cache, true);
        if (JSONFileManager.Instance != null)
        {
            JSONFileManager.Instance.WriteJSONFile("static_data_cache.json", jsonContent);
        }
    }

    private void ResetStaticDataFlagsSelectively(Dictionary<string, bool> syncResults, List<string> flagsToReset)
    {
        DocumentReference staticRef = db.Collection(STATIC_DATA_VERSIONS_COLLECTION).Document("GlobalInfo");

        var resetData = new Dictionary<string, object>();
        bool hasUpdates = false;

        foreach (string flag in flagsToReset)
        {
            string collectionName = flag.Replace("_updated", "").Replace("_", "");
            if (flag == "indoor_infrastructure_updated")
            {
                if (syncResults.ContainsKey("IndoorInfrastructure") && syncResults["IndoorInfrastructure"])
                {
                    resetData[flag] = false;
                    hasUpdates = true;
                }
            }
            else
            {
                collectionName = char.ToUpper(collectionName[0]) + collectionName.Substring(1);

                if (syncResults.ContainsKey(collectionName) && syncResults[collectionName])
                {
                    resetData[flag] = false;
                    hasUpdates = true;
                }
            }
        }

        resetData["last_check"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        hasUpdates = true;

        if (hasUpdates)
        {
            staticRef.SetAsync(resetData, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
            {
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
                }
            }
        }
        return null;
    }

    private void UpdateLocalStaticDataCache(StaticDataVersionInfo serverInfo)
    {
        LocalStaticDataCache cache = new LocalStaticDataCache
        {
            infrastructure_synced = true,
            categories_synced = true,
            campus_synced = true,
        };

        string jsonContent = JsonUtility.ToJson(cache, true);
        if (JSONFileManager.Instance != null)
        {
            JSONFileManager.Instance.WriteJSONFile("static_data_cache.json", jsonContent);
        }
    }

    private void ResetStaticDataFlags()
    {
        DocumentReference staticRef = db.Collection(STATIC_DATA_VERSIONS_COLLECTION).Document("GlobalInfo");

        var resetData = new Dictionary<string, object>
        {
            { "infrastructure_updated", false },
            { "categories_updated", false },
            { "campus_updated", false },
            { "last_check", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
        };

        staticRef.SetAsync(resetData, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
        });
    }

    public void SyncCollectionToLocal(string collectionName, System.Action onComplete = null)
    {
        if (!isFirebaseReady)
        {
            onComplete?.Invoke();
            return;
        }

        string fileName = $"{collectionName.ToLower()}.json";

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
                    JSONFileManager.Instance.WriteJSONFile(fileName, jsonArray);
                }

                onComplete?.Invoke();
            }
            else
            {
                onComplete?.Invoke();
            }
        });
    }

    public void GetAvailableMapVersions(string mapId, System.Action<List<string>> onComplete)
    {
        if (!isFirebaseReady)
        {
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

                versions.Sort((a, b) => CompareVersions(a, b));
                onComplete?.Invoke(versions);
            }
            else
            {
                onComplete?.Invoke(new List<string>());
            }
        });
    }

    public void SwitchToMapVersion(string mapId, string version, System.Action onComplete = null)
    {
        MapVersionInfo versionInfo = new MapVersionInfo
        {
            map_id = mapId,
            current_version = version,
            map_name = "Campus Map",
            last_updated = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        SyncSingleMapVersion(versionInfo, () =>
        {
            onComplete?.Invoke();
        });
    }

    public string GetCurrentMapVersion(string mapId)
    {
        LocalVersionCache cache = GetLocalVersionCache(mapId);
        return cache?.cached_version ?? "unknown";
    }

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

    public void FetchDocument(string collectionName, string documentId, System.Action<Dictionary<string, object>> onComplete)
    {
        if (!isFirebaseReady)
        {
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
                    onComplete?.Invoke(null);
                }
            }
            else
            {
                onComplete?.Invoke(null);
            }
        });
    }

    public void ListenToCollection(string collectionName, System.Action<List<Dictionary<string, object>>> onUpdate)
    {
        if (!isFirebaseReady)
        {
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

            onUpdate?.Invoke(documents);
        });
    }
}