using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Android;
using System.Linq;
using Newtonsoft.Json;

public class GlobalManager : MonoBehaviour
{
    public static GlobalManager Instance { get; private set; }

    // Global Variables
    public bool onboardingComplete = false;
    public bool isDataInitialized = false;
    public Dictionary<string, string> currentMapVersions = new Dictionary<string, string>();
    public List<MapInfo> availableMaps = new List<MapInfo>();

    // Managers - these will be created later in MainApp
    public GameObject jsonFileManagerPrefab;
    public GameObject firestoreManagerPrefab;

    // Local storage for onboarding
    private string onboardingSavePath;

    // Events for data updates
    public System.Action OnDataInitializationComplete;
    public System.Action<Dictionary<string, string>> OnMapVersionsChanged;
    public System.Action<List<MapInfo>> OnAvailableMapsChanged;

    void Start()
    {
        // Request location permission for map features
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);
        }
    }

    void Awake()
    {
        // Singleton pattern to ensure only one instance exists
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);

            onboardingSavePath = Application.persistentDataPath + "/saveData.json";

            //check onboarding status and navigate - NO DATA SYNC HERE
            CheckOnboardingAndNavigate();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void CheckOnboardingAndNavigate()
    {
        // Load onboarding status immediately - NO heavy operations
        LoadOnboardingData();
        
        Debug.Log($"Onboarding check complete: {onboardingComplete}");
        
        if (!onboardingComplete)
        {
            Debug.Log("First launch detected - loading Onboarding scene...");
            UnityEngine.SceneManagement.SceneManager.LoadScene("OnboardingScene");
        }
        else
        {
            Debug.Log("Onboarding complete - loading Main App scene...");
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainAppScene");
        }
    }

    public void InitializeDataSystems()
    {
        if (isDataInitialized)
        {
            Debug.Log("Data systems already initialized");
            OnDataInitializationComplete?.Invoke();
            return;
        }

        Debug.Log("Starting data systems initialization in MainApp...");
        StartCoroutine(InitializeManagersCoroutine());
    }

    private IEnumerator InitializeManagersCoroutine()
    {
        Debug.Log("Creating and initializing managers...");

        // Create JSON File Manager if it doesn't exist
        if (JSONFileManager.Instance == null)
        {
            GameObject jsonManager;
            if (jsonFileManagerPrefab != null)
            {
                jsonManager = Instantiate(jsonFileManagerPrefab);
                if (jsonManager.GetComponent<JSONFileManager>() == null)
                {
                    jsonManager.AddComponent<JSONFileManager>();
                }
            }
            else
            {
                jsonManager = new GameObject("JSONFileManager");
                jsonManager.AddComponent<JSONFileManager>();
            }
            
            // Make it persist across scenes (DontDestroyOnLoad handled in JSONFileManager's Awake)
            DontDestroyOnLoad(jsonManager);
        }

        // Create Firestore Manager if it doesn't exist
        if (FirestoreManager.Instance == null)
        {
            GameObject firestoreManager;
            if (firestoreManagerPrefab != null)
            {
                firestoreManager = Instantiate(firestoreManagerPrefab);
                if (firestoreManager.GetComponent<FirestoreManager>() == null)
                {
                    firestoreManager.AddComponent<FirestoreManager>();
                }
            }
            else
            {
                firestoreManager = new GameObject("FirestoreManager");
                firestoreManager.AddComponent<FirestoreManager>();
            }
            
            // Make it persist across scenes (DontDestroyOnLoad handled in FirestoreManager's Awake)
            DontDestroyOnLoad(firestoreManager);
        }

        // Wait for managers to properly initialize their singletons
        yield return new WaitUntil(() => JSONFileManager.Instance != null && FirestoreManager.Instance != null);

        Debug.Log("Managers created, continuing initialization...");

        // Initialize base JSON files (creates default files if they don't exist)
        bool jsonInitComplete = false;
        JSONFileManager.Instance.InitializeJSONFiles(() =>
        {
            jsonInitComplete = true;
        });

        yield return new WaitUntil(() => jsonInitComplete);

        // Initialize Firebase and perform comprehensive sync
        bool firebaseInitComplete = false;
        FirestoreManager.Instance.InitializeFirebase((success) =>
        {
            firebaseInitComplete = true;

            if (success)
            {
                Debug.Log("Firebase initialized successfully - starting comprehensive data sync...");
                FirestoreManager.Instance.CheckAndSyncData(() =>
                {
                    Debug.Log("Comprehensive sync completed!");
                    PostSyncInitialization();
                });
            }
            else
            {
                Debug.Log("Firebase failed to initialize - using cached/local data only");
                PostSyncInitialization();
            }
        });

        yield return new WaitUntil(() => firebaseInitComplete);
    }

    private void PostSyncInitialization()
    {
        // Load available maps and update current versions
        LoadAvailableMaps();
        UpdateCurrentMapVersions();

        // Initialize map-specific files now that we know what maps exist
        if (JSONFileManager.Instance != null && availableMaps.Count > 0)
        {
            List<string> mapIds = availableMaps.Select(m => m.map_id).ToList();
            JSONFileManager.Instance.InitializeMapSpecificFiles(mapIds, () =>
            {
                FinalizeDataInitialization();
            });
        }
        else
        {
            FinalizeDataInitialization();
        }
    }

    private void FinalizeDataInitialization()
    {
        isDataInitialized = true;
        Debug.Log($"All systems ready! Available maps: {availableMaps.Count}");

        // Log map versions
        foreach (var kvp in currentMapVersions)
        {
            Debug.Log($"Map {kvp.Key}: Version {kvp.Value}");
        }

        // Notify other systems that data is ready
        OnDataInitializationComplete?.Invoke();
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

                    OnAvailableMapsChanged?.Invoke(availableMaps);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to load available maps: {ex.Message}\nJSON Content:\n{mapsJson}");
                }
            }
        }
    }

    private void UpdateCurrentMapVersions()
    {
        currentMapVersions.Clear();

        if (JSONFileManager.Instance != null && FirestoreManager.Instance != null)
        {
            foreach (var map in availableMaps)
            {
                LocalVersionCache cache = JSONFileManager.Instance.GetMapVersionCache(map.map_id);
                currentMapVersions[map.map_id] = cache?.cached_version ?? "none";
            }

            Debug.Log($"Updated current map versions: {currentMapVersions.Count} maps");
        }
    }

    // LIGHTWEIGHT method - only loads onboarding data from local file, no heavy operations
    private void LoadOnboardingData()
    {
        if (File.Exists(onboardingSavePath))
        {
            try
            {
                string json = File.ReadAllText(onboardingSavePath);
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                this.onboardingComplete = data.onboardingComplete;
                Debug.Log($"Onboarding data loaded: onboardingComplete = {onboardingComplete}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to load onboarding data: {ex.Message}");
                // Set default values if file is corrupted
                this.onboardingComplete = false;
            }
        }
        else
        {
            // Set default values for first-time users
            this.onboardingComplete = false;
            Debug.Log("No onboarding save file found, using default values");
        }
    }

    public void SaveOnboardingData()
    {
        SaveData data = new SaveData();
        data.onboardingComplete = this.onboardingComplete;

        string json = JsonUtility.ToJson(data);
        File.WriteAllText(onboardingSavePath, json);
        Debug.Log("Onboarding data saved locally");
    }

    // All the other methods remain the same...
    // Smart data sync method
    public void SmartDataSync(System.Action onComplete = null)
    {
        if (FirestoreManager.Instance != null && FirestoreManager.Instance.IsReady)
        {
            var oldVersions = new Dictionary<string, string>(currentMapVersions);

            FirestoreManager.Instance.CheckAndSyncData(() =>
            {
                UpdateCurrentMapVersions();

                // Check if any versions changed
                bool versionsChanged = false;
                List<string> updatedMaps = new List<string>();

                foreach (var kvp in currentMapVersions)
                {
                    string oldVersion = oldVersions.GetValueOrDefault(kvp.Key, "unknown");
                    if (oldVersion != kvp.Value)
                    {
                        versionsChanged = true;
                        updatedMaps.Add(kvp.Key);
                        Debug.Log($"Map {kvp.Key} updated from {oldVersion} to {kvp.Value}");
                    }
                }

                if (versionsChanged)
                {
                    Debug.Log($"Map versions updated for: {string.Join(", ", updatedMaps)}");
                    OnMapVersionsChanged?.Invoke(currentMapVersions);
                }
                else
                {
                    Debug.Log("All map data is already up to date");
                }

                onComplete?.Invoke();
            });
        }
        else
        {
            Debug.LogWarning("Firestore not ready for sync");
            onComplete?.Invoke();
        }
    }

    // Get system status for debugging
    public string GetSystemStatus()
    {
        string status = "=== CRIMSON MAP SYSTEM STATUS ===\n";
        status += $"Data Initialized: {isDataInitialized}\n";
        status += $"Available Maps: {availableMaps.Count}\n";

        foreach (var map in availableMaps)
        {
            string version = currentMapVersions.GetValueOrDefault(map.map_id, "unknown");
            status += $"  - {map.map_id} ({map.map_name}): v{version}\n";
        }

        status += $"JSON Manager Ready: {JSONFileManager.Instance != null}\n";
        status += $"Firestore Manager Ready: {FirestoreManager.Instance?.IsReady ?? false}\n";
        status += $"Onboarding Complete: {onboardingComplete}\n";
        status += $"System Ready: {IsSystemReady()}";

        return status;
    }

    // Helper methods to access data through the managers
    public string GetJSONData(string fileName)
    {
        if (JSONFileManager.Instance != null)
        {
            return JSONFileManager.Instance.ReadJSONFile(fileName);
        }
        return null;
    }

    public void SaveJSONData(string fileName, string jsonContent)
    {
        if (JSONFileManager.Instance != null)
        {
            JSONFileManager.Instance.WriteJSONFile(fileName, jsonContent);
        }
    }

    // Get map-specific data
    public string GetMapSpecificData(string collectionName, string mapId)
    {
        if (JSONFileManager.Instance != null)
        {
            return JSONFileManager.Instance.ReadMapSpecificData(collectionName, mapId);
        }
        return null;
    }

    // Force data refresh for all maps (bypasses version checking)
    public void ForceDataRefresh(System.Action onComplete = null)
    {
        if (!IsSystemReady())
        {
            Debug.LogWarning("System not ready for data refresh");
            onComplete?.Invoke();
            return;
        }

        Debug.Log("Force refreshing all map data...");

        // Clear all version caches to force re-download
        if (JSONFileManager.Instance != null)
        {
            JSONFileManager.Instance.ClearAllCaches();
        }

        // Re-sync all data
        if (FirestoreManager.Instance != null && FirestoreManager.Instance.IsReady)
        {
            var oldVersions = new Dictionary<string, string>(currentMapVersions);

            FirestoreManager.Instance.CheckAndSyncData(() =>
            {
                UpdateCurrentMapVersions();

                // Check if any versions changed
                bool versionsChanged = false;
                foreach (var kvp in currentMapVersions)
                {
                    string oldVersion = oldVersions.GetValueOrDefault(kvp.Key, "unknown");
                    if (oldVersion != kvp.Value)
                    {
                        versionsChanged = true;
                        Debug.Log($"Map {kvp.Key} updated from {oldVersion} to {kvp.Value}");
                    }
                }

                if (versionsChanged)
                {
                    OnMapVersionsChanged?.Invoke(currentMapVersions);
                }

                Debug.Log("Force refresh completed!");
                onComplete?.Invoke();
            });
        }
        else
        {
            Debug.LogWarning("Firestore not ready for refresh");
            onComplete?.Invoke();
        }
    }

    // Get available versions for a specific map
    public void GetAvailableMapVersions(string mapId, System.Action<List<string>> onComplete)
    {
        if (FirestoreManager.Instance != null && FirestoreManager.Instance.IsReady)
        {
            FirestoreManager.Instance.GetAvailableMapVersions(mapId, onComplete);
        }
        else
        {
            Debug.LogWarning("Firestore not ready");
            onComplete?.Invoke(new List<string>());
        }
    }

    // Switch to a specific version of a specific map
    public void SwitchToMapVersion(string mapId, string version, System.Action onComplete = null)
    {
        if (FirestoreManager.Instance != null && FirestoreManager.Instance.IsReady)
        {
            string oldVersion = currentMapVersions.GetValueOrDefault(mapId, "unknown");

            FirestoreManager.Instance.SwitchToMapVersion(mapId, version, () =>
            {
                // Update the specific map version
                currentMapVersions[mapId] = version;

                Debug.Log($"Switched map {mapId} from {oldVersion} to {version}");
                OnMapVersionsChanged?.Invoke(currentMapVersions);
                onComplete?.Invoke();
            });
        }
        else
        {
            Debug.LogWarning("Firestore not ready for version switch");
            onComplete?.Invoke();
        }
    }

    // Get current version of a specific map
    public string GetCurrentMapVersion(string mapId)
    {
        return currentMapVersions.GetValueOrDefault(mapId, "unknown");
    }

    // Get all current map versions
    public Dictionary<string, string> GetAllCurrentMapVersions()
    {
        return new Dictionary<string, string>(currentMapVersions);
    }

    // Get available maps
    public List<MapInfo> GetAvailableMaps()
    {
        return new List<MapInfo>(availableMaps);
    }

    // Legacy method for backward compatibility
    public void SyncDataFromFirestore(System.Action onComplete = null)
    {
        Debug.LogWarning("SyncDataFromFirestore is deprecated. Use SmartDataSync or ForceDataRefresh instead.");
        SmartDataSync(onComplete);
    }

    public void FetchFirestoreDocument(string collection, string documentId, System.Action<Dictionary<string, object>> onComplete)
    {
        if (FirestoreManager.Instance != null && FirestoreManager.Instance.IsReady)
        {
            FirestoreManager.Instance.FetchDocument(collection, documentId, onComplete);
        }
        else
        {
            Debug.LogWarning("Firestore not ready");
            onComplete?.Invoke(null);
        }
    }

    // Enhanced destination management
    public void AddToRecentDestinations(Dictionary<string, object> destination)
    {
        if (JSONFileManager.Instance != null)
        {
            JSONFileManager.Instance.AddRecentDestination(destination);
        }
    }

    public void AddToSavedDestinations(Dictionary<string, object> destination)
    {
        if (JSONFileManager.Instance != null)
        {
            JSONFileManager.Instance.AddSavedDestination(destination);
        }
    }

    public void RemoveFromSavedDestinations(string destinationId)
    {
        if (JSONFileManager.Instance != null)
        {
            JSONFileManager.Instance.RemoveSavedDestination(destinationId);
        }
    }

    // Data freshness checking - now supports per-map checking
    public bool IsMapDataFresh(string mapId, int maxAgeHours = 24)
    {
        if (JSONFileManager.Instance != null)
        {
            return JSONFileManager.Instance.IsMapDataFresh(mapId, maxAgeHours);
        }
        return false;
    }

    // Check if all map data is fresh
    public bool IsAllMapDataFresh(int maxAgeHours = 24)
    {
        if (JSONFileManager.Instance != null && availableMaps.Count > 0)
        {
            foreach (var map in availableMaps)
            {
                if (!JSONFileManager.Instance.IsMapDataFresh(map.map_id, maxAgeHours))
                {
                    return false;
                }
            }
            return true;
        }
        return false;
    }

    // Check if all systems are ready
    public bool IsSystemReady()
    {
        return isDataInitialized &&
               JSONFileManager.Instance != null &&
               (FirestoreManager.Instance == null || FirestoreManager.Instance.IsReady);
    }

    // Cleanup unused files when maps are removed
    public void CleanupUnusedFiles()
    {
        if (JSONFileManager.Instance != null)
        {
            JSONFileManager.Instance.CleanupUnusedMapFiles();
        }
    }

    // Get comprehensive system status
    public string GetComprehensiveStatus()
    {
        string systemStatus = GetSystemStatus();

        if (JSONFileManager.Instance != null)
        {
            systemStatus += "\n\n" + JSONFileManager.Instance.GetFileSystemStatus();
        }

        return systemStatus;
    }
}