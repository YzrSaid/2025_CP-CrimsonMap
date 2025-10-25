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
    private bool hasInitialized = false;

    public bool onboardingComplete = false;
    public bool isDataInitialized = false;
    public Dictionary<string, string> currentMapVersions = new Dictionary<string, string>();
    public List<MapInfo> availableMaps = new List<MapInfo>();

    public GameObject jsonFileManagerPrefab;
    public GameObject firestoreManagerPrefab;

    private string onboardingSavePath;

    private static bool skipFullInitializationOnReturn = false;

    public System.Action OnDataInitializationComplete;
    public System.Action<Dictionary<string, string>> OnMapVersionsChanged;
    public System.Action<List<MapInfo>> OnAvailableMapsChanged;

    void Start()
    {
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);

            onboardingSavePath = Path.Combine(Application.persistentDataPath, "saveData.json");
            if (Application.isFocused)
            {
                hasInitialized = true;
                CheckOnboardingAndNavigate();
            }
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && Instance == this)
        {
            if (!hasInitialized)
            {
                CheckOnboardingAndNavigate();
            }
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
        string[] arScenes = { "ARScene", "ReadQRCode" };
        return System.Array.Exists(arScenes, scene =>
            sceneName.Equals(scene, System.StringComparison.OrdinalIgnoreCase));
    }

    public static void SetSkipFullInitialization(bool skip)
    {
        skipFullInitializationOnReturn = skip;
    }

    public static bool ShouldSkipFullInitialization()
    {
        return skipFullInitializationOnReturn;
    }

    public void InitializeDataSystems()
    {
        if (isInARMode)
        {
            OnDataInitializationComplete?.Invoke();
            return;
        }

        if (isDataInitialized)
        {
            OnDataInitializationComplete?.Invoke();
            return;
        }

        if (skipFullInitializationOnReturn)
        {
            StartCoroutine(QuickInitializationFromAR());
        }
        else
        {
            StartCoroutine(FullInitializationFromScratch());
        }
    }

    private IEnumerator QuickInitializationFromAR()
    {
        skipFullInitializationOnReturn = false;

        if (JSONFileManager.Instance == null)
        {
            yield return StartCoroutine(RecreateJSONManager());
        }

        if (FirestoreManager.Instance == null)
        {
            yield return StartCoroutine(RecreateFirestoreManager());
        }

        isDataInitialized = true;

        OnDataInitializationComplete?.Invoke();
    }

    private IEnumerator FullInitializationFromScratch()
    {
        if (JSONFileManager.Instance == null)
        {
            yield return StartCoroutine(RecreateJSONManager());
        }

        if (FirestoreManager.Instance == null)
        {
            yield return StartCoroutine(RecreateFirestoreManager());
        }

        bool jsonInitComplete = false;
        JSONFileManager.Instance.InitializeJSONFiles(() =>
        {
            jsonInitComplete = true;
        });
        yield return new WaitUntil(() => jsonInitComplete);

        bool firebaseInitComplete = false;
        FirestoreManager.Instance.InitializeFirebase((success) =>
        {
            firebaseInitComplete = true;

            if (success)
            {
                FirestoreManager.Instance.CheckAndSyncData(() =>
                {
                    PostSyncInitialization();
                });
            }
            else
            {
                PostSyncInitialization();
            }
        });

        yield return new WaitUntil(() => firebaseInitComplete);
    }

    private IEnumerator RecreateJSONManager()
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

        DontDestroyOnLoad(jsonManager);
        yield return new WaitUntil(() => JSONFileManager.Instance != null);
    }

    private IEnumerator RecreateFirestoreManager()
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

        DontDestroyOnLoad(firestoreManager);
        yield return new WaitUntil(() => FirestoreManager.Instance != null);
    }

    private void CheckOnboardingAndNavigate()
    {
        LoadOnboardingData();

        if (!onboardingComplete)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("OnboardingScreensScene");
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainAppScene");
        }
    }

    private void PostSyncInitialization()
    {
        LoadAvailableMaps();
        UpdateCurrentMapVersions();

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

        foreach (var kvp in currentMapVersions)
        {
        }

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

                    foreach (var map in availableMaps)
                    {
                    }

                    OnAvailableMapsChanged?.Invoke(availableMaps);
                }
                catch (Exception ex)
                {
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
        }
    }

    private void LoadOnboardingData()
    {
        if (File.Exists(onboardingSavePath))
        {
            try
            {
                string json = File.ReadAllText(onboardingSavePath);

                if (!string.IsNullOrEmpty(json.Trim()))
                {
                    SaveData data = JsonUtility.FromJson<SaveData>(json);
                    this.onboardingComplete = data.onboardingComplete;
                }
                else
                {
                    this.onboardingComplete = false;
                }
            }
            catch (System.Exception ex)
            {
                this.onboardingComplete = false;
            }
        }
        else
        {
            this.onboardingComplete = false;
        }
    }

    public void SaveOnboardingData()
    {
        try
        {
            SaveData data = new SaveData();
            data.onboardingComplete = this.onboardingComplete;

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(onboardingSavePath, json);
        }
        catch (System.Exception ex)
        {
        }
    }

    public void SmartDataSync(System.Action onComplete = null)
    {
        if (FirestoreManager.Instance != null && FirestoreManager.Instance.IsReady)
        {
            var oldVersions = new Dictionary<string, string>(currentMapVersions);

            FirestoreManager.Instance.CheckAndSyncData(() =>
            {
                UpdateCurrentMapVersions();

                bool versionsChanged = false;
                List<string> updatedMaps = new List<string>();

                foreach (var kvp in currentMapVersions)
                {
                    string oldVersion = oldVersions.GetValueOrDefault(kvp.Key, "unknown");
                    if (oldVersion != kvp.Value)
                    {
                        versionsChanged = true;
                        updatedMaps.Add(kvp.Key);
                    }
                }

                if (versionsChanged)
                {
                    OnMapVersionsChanged?.Invoke(currentMapVersions);
                }

                onComplete?.Invoke();
            });
        }
        else
        {
            onComplete?.Invoke();
        }
    }

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

    public string GetMapSpecificData(string collectionName, string mapId)
    {
        if (JSONFileManager.Instance != null)
        {
            return JSONFileManager.Instance.ReadMapSpecificData(collectionName, mapId);
        }
        return null;
    }

    public void ForceDataRefresh(System.Action onComplete = null)
    {
        if (!IsSystemReady())
        {
            onComplete?.Invoke();
            return;
        }

        if (JSONFileManager.Instance != null)
        {
            JSONFileManager.Instance.ClearAllCaches();
        }

        if (FirestoreManager.Instance != null && FirestoreManager.Instance.IsReady)
        {
            var oldVersions = new Dictionary<string, string>(currentMapVersions);

            FirestoreManager.Instance.CheckAndSyncData(() =>
            {
                UpdateCurrentMapVersions();

                bool versionsChanged = false;
                foreach (var kvp in currentMapVersions)
                {
                    string oldVersion = oldVersions.GetValueOrDefault(kvp.Key, "unknown");
                    if (oldVersion != kvp.Value)
                    {
                        versionsChanged = true;
                    }
                }

                if (versionsChanged)
                {
                    OnMapVersionsChanged?.Invoke(currentMapVersions);
                }

                onComplete?.Invoke();
            });
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    public void GetAvailableMapVersions(string mapId, System.Action<List<string>> onComplete)
    {
        if (FirestoreManager.Instance != null && FirestoreManager.Instance.IsReady)
        {
            FirestoreManager.Instance.GetAvailableMapVersions(mapId, onComplete);
        }
        else
        {
            onComplete?.Invoke(new List<string>());
        }
    }

    public void SwitchToMapVersion(string mapId, string version, System.Action onComplete = null)
    {
        if (FirestoreManager.Instance != null && FirestoreManager.Instance.IsReady)
        {
            string oldVersion = currentMapVersions.GetValueOrDefault(mapId, "unknown");

            FirestoreManager.Instance.SwitchToMapVersion(mapId, version, () =>
            {
                currentMapVersions[mapId] = version;

                OnMapVersionsChanged?.Invoke(currentMapVersions);
                onComplete?.Invoke();
            });
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    public string GetCurrentMapVersion(string mapId)
    {
        return currentMapVersions.GetValueOrDefault(mapId, "unknown");
    }

    public Dictionary<string, string> GetAllCurrentMapVersions()
    {
        return new Dictionary<string, string>(currentMapVersions);
    }

    public List<MapInfo> GetAvailableMaps()
    {
        return new List<MapInfo>(availableMaps);
    }

    public void SyncDataFromFirestore(System.Action onComplete = null)
    {
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
            onComplete?.Invoke(null);
        }
    }

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

    public bool IsMapDataFresh(string mapId, int maxAgeHours = 24)
    {
        if (JSONFileManager.Instance != null)
        {
            return JSONFileManager.Instance.IsMapDataFresh(mapId, maxAgeHours);
        }
        return false;
    }

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

    public bool IsSystemReady()
    {
        return isDataInitialized &&
               JSONFileManager.Instance != null &&
               (FirestoreManager.Instance == null || FirestoreManager.Instance.IsReady);
    }

    public void CleanupUnusedFiles()
    {
        if (JSONFileManager.Instance != null)
        {
            JSONFileManager.Instance.CleanupUnusedMapFiles();
        }
    }

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
        List<UnityEngine.XR.ARSubsystems.XRSessionSubsystem> sessionSubsystems = null;
        List<UnityEngine.XR.ARSubsystems.XRPlaneSubsystem> planeSubsystems = null;
        List<UnityEngine.XR.ARSubsystems.XRRaycastSubsystem> raycastSubsystems = null;

        try
        {
            sessionSubsystems = new List<UnityEngine.XR.ARSubsystems.XRSessionSubsystem>();
            planeSubsystems = new List<UnityEngine.XR.ARSubsystems.XRPlaneSubsystem>();
            raycastSubsystems = new List<UnityEngine.XR.ARSubsystems.XRRaycastSubsystem>();

            SubsystemManager.GetInstances(sessionSubsystems);
            SubsystemManager.GetInstances(planeSubsystems);
            SubsystemManager.GetInstances(raycastSubsystems);
        }
        catch (System.Exception ex)
        {
        }

        if (sessionSubsystems != null)
        {
            foreach (var subsystem in sessionSubsystems)
            {
                if (subsystem.running)
                {
                    subsystem.Stop();
                }
            }
        }

        yield return new WaitForSeconds(0.1f);

        if (planeSubsystems != null)
        {
            foreach (var subsystem in planeSubsystems)
            {
                if (subsystem.running)
                {
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
                    subsystem.Stop();
                }
            }
        }

        yield return new WaitForSeconds(0.1f);
    }

    public IEnumerator SafeARCleanupAndExit(string sceneName)
    {
        yield return StartCoroutine(CleanupXRSubsystems());

        yield return new WaitForSeconds(0.2f);

        ARTrackingManager arManager = FindObjectOfType<ARTrackingManager>();
        if (arManager != null)
        {
            Destroy(arManager.gameObject);
        }

        yield return new WaitForSeconds(0.1f);

        if (isInARMode)
        {
            yield return StartCoroutine(ManuallyRecreateManagers(sceneName));
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    public IEnumerator ManuallyRecreateManagers(string targetScene)
    {
        isInARMode = false;

        MonoBehaviour[] arComponents = FindObjectsOfType<MonoBehaviour>();
        foreach (MonoBehaviour component in arComponents)
        {
            if (component != null && component != this)
            {
                if (component.name.Contains("AR") ||
                    component.GetType().Namespace?.Contains("UnityEngine.XR") == true ||
                    component.GetType().Name.Contains("AR"))
                {
                    component.StopAllCoroutines();
                }
            }
        }

        yield return new WaitForEndOfFrame();

        bool managersRecreated = false;
        yield return StartCoroutine(RecreateDestroyedManagersCoroutine((success) => managersRecreated = success));

        SetSkipFullInitialization(true);

        SceneManager.LoadScene(targetScene, LoadSceneMode.Single);
    }

    private IEnumerator RecreateDestroyedManagersCoroutine(Action<bool> onComplete)
    {
        bool success = true;
        bool shouldRecreateJSON = false;
        bool shouldRecreateFirestore = false;

        try
        {
            shouldRecreateJSON = ARManagerCleanup.ShouldRecreateJSONManager() && JSONFileManager.Instance == null;
            shouldRecreateFirestore = ARManagerCleanup.ShouldRecreateFirestoreManager() && FirestoreManager.Instance == null;
        }
        catch (System.Exception ex)
        {
            success = false;
        }

        if (shouldRecreateJSON)
        {
            try
            {
                GameObject jsonManager;
                if (jsonFileManagerPrefab != null)
                {
                    jsonManager = Instantiate(jsonFileManagerPrefab);
                }
                else
                {
                    jsonManager = new GameObject("JSONFileManager");
                    jsonManager.AddComponent<JSONFileManager>();
                }
                DontDestroyOnLoad(jsonManager);
            }
            catch (System.Exception ex)
            {
                success = false;
            }
        }

        if (shouldRecreateFirestore)
        {
            try
            {
                GameObject firestoreManager;
                if (firestoreManagerPrefab != null)
                {
                    firestoreManager = Instantiate(firestoreManagerPrefab);
                }
                else
                {
                    firestoreManager = new GameObject("FirestoreManager");
                    firestoreManager.AddComponent<FirestoreManager>();
                }
                DontDestroyOnLoad(firestoreManager);
            }
            catch (System.Exception ex)
            {
                success = false;
            }
        }

        if (shouldRecreateJSON)
        {
            yield return new WaitUntil(() => JSONFileManager.Instance != null);
        }

        if (shouldRecreateFirestore)
        {
            yield return new WaitUntil(() => FirestoreManager.Instance != null);
        }

        yield return new WaitForSeconds(0.2f);

        onComplete?.Invoke(success);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        wasInARMode = isInARMode;

        if (IsARScene(scene.name))
        {
            isInARMode = true;
        }
        else
        {
            isInARMode = false;

            if (wasInARMode)
            {
                StartCoroutine(EnsureManagersAfterAR());
            }
        }
    }

    private IEnumerator EnsureManagersAfterAR()
    {
        yield return new WaitForSeconds(0.1f);

        bool needsManagerCheck = false;
        bool shouldRecreateJSON = false;
        bool shouldRecreateFirestore = false;

        try
        {
            shouldRecreateJSON = ARManagerCleanup.ShouldRecreateJSONManager() && JSONFileManager.Instance == null;
            shouldRecreateFirestore = ARManagerCleanup.ShouldRecreateFirestoreManager() && FirestoreManager.Instance == null;
        }
        catch (System.Exception ex)
        {
        }

        if (shouldRecreateJSON)
        {
            needsManagerCheck = true;
            try
            {
                GameObject jsonManager;
                if (jsonFileManagerPrefab != null)
                {
                    jsonManager = Instantiate(jsonFileManagerPrefab);
                }
                else
                {
                    jsonManager = new GameObject("JSONFileManager");
                    jsonManager.AddComponent<JSONFileManager>();
                }
                DontDestroyOnLoad(jsonManager);
            }
            catch (System.Exception ex)
            {
            }
        }

        if (shouldRecreateFirestore)
        {
            needsManagerCheck = true;
            try
            {
                GameObject firestoreManager;
                if (firestoreManagerPrefab != null)
                {
                    firestoreManager = Instantiate(firestoreManagerPrefab);
                }
                else
                {
                    firestoreManager = new GameObject("FirestoreManager");
                    firestoreManager.AddComponent<FirestoreManager>();
                }
                DontDestroyOnLoad(firestoreManager);
            }
            catch (System.Exception ex)
            {
            }
        }

        if (needsManagerCheck)
        {
            yield return new WaitUntil(() =>
                (!shouldRecreateJSON || JSONFileManager.Instance != null) &&
                (!shouldRecreateFirestore || FirestoreManager.Instance != null));

            InitializeDataSystems();
        }

        ARManagerCleanup.ResetManagerStates();
    }
}