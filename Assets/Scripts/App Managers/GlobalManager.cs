
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Android;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;

public class GlobalManager : MonoBehaviour
{
    public static GlobalManager Instance { get; private set; }
    [Header("AR Scene Compatibility")]
    public bool isInARMode = false;
    private bool wasInARMode = false;


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

            onboardingSavePath = Path.Combine(Application.persistentDataPath, "saveData.json");
            Debug.Log($"Onboarding save path: {onboardingSavePath}");

            CheckOnboardingAndNavigate();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }


    private bool IsARScene(string sceneName)
    {
        // Update this array with your actual AR scene name(s)
        string[] arScenes = { "ARScene" };
        return System.Array.Exists(arScenes, scene =>
            sceneName.Equals(scene, System.StringComparison.OrdinalIgnoreCase));
    }

    // Update your existing InitializeDataSystems method to check AR mode
    public void InitializeDataSystems()
    {
        if (isInARMode)
        {
            Debug.Log("In AR mode - skipping heavy data system initialization");
            OnDataInitializationComplete?.Invoke();
            return;
        }

        if (isDataInitialized)
        {
            Debug.Log("Data systems already initialized");
            OnDataInitializationComplete?.Invoke();
            return;
        }

        Debug.Log("Starting data systems initialization...");
        StartCoroutine(InitializeManagersCoroutine());
    }

    private IEnumerator RecreateDestroyedManagers()
    {
        Debug.Log("Starting manager recreation...");

        // Small delay to ensure clean transition
        yield return new WaitForSeconds(0.1f);

        // Recreate JSONFileManager if destroyed
        if (JSONFileManager.Instance == null)
        {
            Debug.Log("Recreating JSONFileManager...");
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

            DontDestroyOnLoad(jsonManager);
            Debug.Log("JSONFileManager recreated");
        }

        // Recreate FirestoreManager if destroyed
        if (FirestoreManager.Instance == null)
        {
            Debug.Log("Recreating FirestoreManager...");
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

            DontDestroyOnLoad(firestoreManager);
            Debug.Log("FirestoreManager recreated");
        }

        // MainAppLoader will be recreated automatically when MainApp scene loads
        // since it's part of the scene's UI hierarchy

        // Wait for managers to initialize
        yield return new WaitUntil(() => JSONFileManager.Instance != null && FirestoreManager.Instance != null);

        // Reinitialize data systems
        Debug.Log("Reinitializing data systems after AR...");
        InitializeDataSystems();

        Debug.Log("Manager recreation complete!");
    }


    private void CheckOnboardingAndNavigate()
    {
        // Load onboarding status immediately - NO heavy operations
        LoadOnboardingData();

        Debug.Log($"Onboarding check complete: {onboardingComplete}");
        Debug.Log($"Save file exists: {File.Exists(onboardingSavePath)}");

        if (!onboardingComplete)
        {
            Debug.Log("First launch detected or onboarding not complete - loading Onboarding scene...");
            UnityEngine.SceneManagement.SceneManager.LoadScene("OnboardingScreensScene");
        }
        else
        {
            Debug.Log("Onboarding complete - loading Main App scene...");
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainAppScene");
        }
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

    // FIXED: More robust onboarding data loading with explicit debug logging
    private void LoadOnboardingData()
    {
        Debug.Log($"Checking onboarding save file at: {onboardingSavePath}");

        if (File.Exists(onboardingSavePath))
        {
            Debug.Log("Save file found, attempting to load...");
            try
            {
                string json = File.ReadAllText(onboardingSavePath);
                Debug.Log($"Save file content: {json}");

                if (!string.IsNullOrEmpty(json.Trim()))
                {
                    SaveData data = JsonUtility.FromJson<SaveData>(json);
                    this.onboardingComplete = data.onboardingComplete;
                    Debug.Log($"Successfully loaded onboarding data: onboardingComplete = {onboardingComplete}");
                }
                else
                {
                    Debug.LogWarning("Save file is empty, treating as first launch");
                    this.onboardingComplete = false;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load onboarding data: {ex.Message}");
                // Set default values if file is corrupted
                this.onboardingComplete = false;
            }
        }
        else
        {
            Debug.Log("No save file found - this is a first launch");
            // Explicitly set to false for first-time users
            this.onboardingComplete = false;
        }

        Debug.Log($"Final onboarding status: {onboardingComplete}");
    }

    public void SaveOnboardingData()
    {
        try
        {
            SaveData data = new SaveData();
            data.onboardingComplete = this.onboardingComplete;

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(onboardingSavePath, json);
            Debug.Log($"Onboarding data saved: {json}");
            Debug.Log($"Saved to: {onboardingSavePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to save onboarding data: {ex.Message}");
        }
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

    private IEnumerator CleanupXRSubsystems()
    {
        Debug.Log("Cleaning up XR subsystems...");

        List<UnityEngine.XR.ARSubsystems.XRSessionSubsystem> sessionSubsystems = null;
        List<UnityEngine.XR.ARSubsystems.XRPlaneSubsystem> planeSubsystems = null;
        List<UnityEngine.XR.ARSubsystems.XRRaycastSubsystem> raycastSubsystems = null;

        try
        {
            // Get subsystem lists directly
            sessionSubsystems = new List<UnityEngine.XR.ARSubsystems.XRSessionSubsystem>();
            planeSubsystems = new List<UnityEngine.XR.ARSubsystems.XRPlaneSubsystem>();
            raycastSubsystems = new List<UnityEngine.XR.ARSubsystems.XRRaycastSubsystem>();

            SubsystemManager.GetInstances(sessionSubsystems);
            SubsystemManager.GetInstances(planeSubsystems);
            SubsystemManager.GetInstances(raycastSubsystems);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Error during XR subsystem cleanup: {ex.Message}");
        }

        // Stop session subsystem first
        if (sessionSubsystems != null)
        {
            foreach (var subsystem in sessionSubsystems)
            {
                if (subsystem.running)
                {
                    Debug.Log("Stopping XR Session Subsystem from GlobalManager...");
                    subsystem.Stop();
                }
            }
        }

        yield return new WaitForSeconds(0.1f);

        // Stop other subsystems
        if (planeSubsystems != null)
        {
            foreach (var subsystem in planeSubsystems)
            {
                if (subsystem.running)
                {
                    Debug.Log("Stopping XR Plane Subsystem...");
                    subsystem.Stop();
                }
            }
        }

        if (raycastSubsystems != null)
        {
            foreach (var subsystem in raycastSubsystems)
            {
                if (subsystem.running)
                {
                    Debug.Log("Stopping XR Raycast Subsystem...");
                    subsystem.Stop();
                }
            }
        }

        yield return new WaitForSeconds(0.1f);
        Debug.Log("XR subsystem cleanup complete");
    }

    public IEnumerator SafeARCleanupAndExit(string sceneName)
    {
        Debug.Log("GlobalManager: Starting safe AR cleanup...");

        // First, properly cleanup XR/AR subsystems
        yield return StartCoroutine(CleanupXRSubsystems());

        // Small delay to ensure XR cleanup is complete
        yield return new WaitForSeconds(0.2f);

        // Only destroy the ARInfrastructureManager, leave XR/AR Foundation components alone
        ARInfrastructureManager arManager = FindObjectOfType<ARInfrastructureManager>();
        if (arManager != null)
        {
            Debug.Log("GlobalManager: Destroying ARInfrastructureManager...");
            Destroy(arManager.gameObject);
        }

        yield return new WaitForSeconds(0.1f);

        Debug.Log("GlobalManager: Starting manager recreation...");
        if (isInARMode)
        {
            yield return StartCoroutine(ManuallyRecreateManagers(sceneName));
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }

        Debug.Log("GlobalManager: AR cleanup complete");
    }



    public IEnumerator ManuallyRecreateManagers(string targetScene)
    {
        Debug.Log("MANUAL: Starting manager recreation before scene change...");

        // Set AR mode to false
        isInARMode = false;

        // Force cleanup of AR scene components before leaving
        Debug.Log("MANUAL: Cleaning up AR scene...");

        // Stop all AR-related coroutines (but preserve GlobalManager)
        MonoBehaviour[] arComponents = FindObjectsOfType<MonoBehaviour>();
        foreach (MonoBehaviour component in arComponents)
        {
            if (component != null && component != this) // Don't stop GlobalManager
            {
                // Only stop AR-related components to avoid breaking other systems
                if (component.name.Contains("AR") ||
                    component.GetType().Namespace?.Contains("UnityEngine.XR") == true ||
                    component.GetType().Name.Contains("AR"))
                {
                    component.StopAllCoroutines();
                }
            }
        }

        yield return new WaitForEndOfFrame();

        // Recreate managers that were destroyed when entering AR
        bool managersRecreated = false;
        yield return StartCoroutine(RecreateDestroyedManagersCoroutine((success) => managersRecreated = success));

        if (!managersRecreated)
        {
            Debug.LogError("Failed to recreate managers, proceeding with scene load anyway");
        }

        Debug.Log("MANUAL: Managers recreated and ready, now loading MainApp scene...");

        // Load scene - MainAppLoader will automatically exist as part of the scene
        SceneManager.LoadScene(targetScene, LoadSceneMode.Single);
    }
    private IEnumerator RecreateDestroyedManagersCoroutine(Action<bool> onComplete)
    {
        bool success = true;
        bool shouldRecreateJSON = false;
        bool shouldRecreateFirestore = false;

        // Check what needs to be recreated and do the work outside of try-catch
        try
        {
            shouldRecreateJSON = ARManagerCleanup.ShouldRecreateJSONManager() && JSONFileManager.Instance == null;
            shouldRecreateFirestore = ARManagerCleanup.ShouldRecreateFirestoreManager() && FirestoreManager.Instance == null;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error checking manager states: {ex.Message}");
            success = false;
        }

        // Create managers outside try-catch to avoid yield issues
        if (shouldRecreateJSON)
        {
            try
            {
                Debug.Log("MANUAL: Recreating JSONFileManager...");
                GameObject jsonManager = new GameObject("JSONFileManager");
                jsonManager.AddComponent<JSONFileManager>();
                DontDestroyOnLoad(jsonManager);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error recreating JSONFileManager: {ex.Message}");
                success = false;
            }
        }
        else
        {
            Debug.Log("MANUAL: JSONFileManager already exists or wasn't needed");
        }

        if (shouldRecreateFirestore)
        {
            try
            {
                Debug.Log("MANUAL: Recreating FirestoreManager...");
                GameObject firestoreManager = new GameObject("FirestoreManager");
                firestoreManager.AddComponent<FirestoreManager>();
                DontDestroyOnLoad(firestoreManager);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error recreating FirestoreManager: {ex.Message}");
                success = false;
            }
        }
        else
        {
            Debug.Log("MANUAL: FirestoreManager already exists or wasn't needed");
        }

        // Wait for managers to initialize (outside try-catch)
        if (shouldRecreateJSON)
        {
            yield return new WaitUntil(() => JSONFileManager.Instance != null);
            Debug.Log("MANUAL: JSONFileManager recreated");
        }

        if (shouldRecreateFirestore)
        {
            yield return new WaitUntil(() => FirestoreManager.Instance != null);
            Debug.Log("MANUAL: FirestoreManager recreated");
        }

        // Wait a bit more to ensure managers are fully initialized
        yield return new WaitForSeconds(0.2f);

        // Reset the manager states since we've successfully recreated them
        ARManagerCleanup.ResetManagerStates();

        Debug.Log("MANUAL: All managers successfully recreated");

        onComplete?.Invoke(success);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"GlobalManager: Scene loaded - {scene.name}");

        // Store previous AR state
        wasInARMode = isInARMode;

        // Check if we're loading an AR scene
        if (IsARScene(scene.name))
        {
            Debug.Log("AR Scene detected - entering AR mode");
            isInARMode = true;
        }
        else
        {
            Debug.Log("Non-AR Scene - exiting AR mode");
            isInARMode = false;

            // If we were in AR mode and now we're not, make sure managers are ready
            if (wasInARMode)
            {
                Debug.Log("Returned from AR - ensuring managers are ready");
                StartCoroutine(EnsureManagersAfterAR());
            }
        }
    }

    private IEnumerator EnsureManagersAfterAR()
    {
        Debug.Log("Ensuring managers are ready after AR return...");

        // Small delay to let scene fully load
        yield return new WaitForSeconds(0.1f);

        // Check if managers exist, recreate if needed (fallback)
        bool needsManagerCheck = false;
        bool shouldRecreateJSON = false;
        bool shouldRecreateFirestore = false;

        // Check what needs to be recreated
        try
        {
            shouldRecreateJSON = ARManagerCleanup.ShouldRecreateJSONManager() && JSONFileManager.Instance == null;
            shouldRecreateFirestore = ARManagerCleanup.ShouldRecreateFirestoreManager() && FirestoreManager.Instance == null;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error checking manager states in EnsureManagersAfterAR: {ex.Message}");
        }

        // Create managers outside of try-catch
        if (shouldRecreateJSON)
        {
            needsManagerCheck = true;
            try
            {
                Debug.Log("JSONFileManager missing, recreating...");
                GameObject jsonManager = new GameObject("JSONFileManager");
                jsonManager.AddComponent<JSONFileManager>();
                DontDestroyOnLoad(jsonManager);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error recreating JSONFileManager in EnsureManagersAfterAR: {ex.Message}");
            }
        }

        if (shouldRecreateFirestore)
        {
            needsManagerCheck = true;
            try
            {
                Debug.Log("FirestoreManager missing, recreating...");
                GameObject firestoreManager = new GameObject("FirestoreManager");
                firestoreManager.AddComponent<FirestoreManager>();
                DontDestroyOnLoad(firestoreManager);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error recreating FirestoreManager in EnsureManagersAfterAR: {ex.Message}");
            }
        }

        if (needsManagerCheck)
        {
            // Wait for managers to initialize
            yield return new WaitUntil(() =>
                (!shouldRecreateJSON || JSONFileManager.Instance != null) &&
                (!shouldRecreateFirestore || FirestoreManager.Instance != null));

            // Reinitialize data systems
            Debug.Log("Reinitializing data systems after AR...");
            InitializeDataSystems();

            // Reset manager states
            ARManagerCleanup.ResetManagerStates();
        }

        Debug.Log("Manager check complete after AR!");
    }
}