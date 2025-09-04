using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Android;

public class GlobalManager : MonoBehaviour
{
    public static GlobalManager Instance { get; private set; }

    // Global Variables
    public bool onboardingComplete = false;
    public bool isDataInitialized = false;

    // Managers
    public GameObject jsonFileManagerPrefab;
    public GameObject firestoreManagerPrefab;

    // Local storage for onboarding
    private string onboardingSavePath;

    void Start()
    {
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
            
            InitializeManagers();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void InitializeManagers()
    {
        StartCoroutine(InitializeManagersCoroutine());
    }

    private IEnumerator InitializeManagersCoroutine()
    {
        Debug.Log("Initializing managers...");

        // Create JSON File Manager if it doesn't exist
        if (JSONFileManager.Instance == null)
        {
            if (jsonFileManagerPrefab != null)
            {
                Instantiate(jsonFileManagerPrefab);
            }
            else
            {
                GameObject jsonManager = new GameObject("JSONFileManager");
                jsonManager.AddComponent<JSONFileManager>();
            }
        }

        // Create Firestore Manager if it doesn't exist
        if (FirestoreManager.Instance == null)
        {
            if (firestoreManagerPrefab != null)
            {
                Instantiate(firestoreManagerPrefab);
            }
            else
            {
                GameObject firestoreManager = new GameObject("FirestoreManager");
                firestoreManager.AddComponent<FirestoreManager>();
            }
        }

        yield return null; // Wait a frame for managers to initialize

        // Load local onboarding data first
        LoadOnboardingData();

        // Initialize JSON files
        if (JSONFileManager.Instance != null)
        {
            bool jsonInitComplete = false;
            JSONFileManager.Instance.InitializeJSONFiles(() => {
                jsonInitComplete = true;
            });
            
            yield return new WaitUntil(() => jsonInitComplete);
        }

        // Initialize Firebase and sync data
        if (FirestoreManager.Instance != null)
        {
            bool firebaseInitComplete = false;
            FirestoreManager.Instance.InitializeFirebase((success) => {
                firebaseInitComplete = true;
                
                if (success)
                {
                    // Sync Firestore data to local JSON files
                    FirestoreManager.Instance.SyncAllCollectionsToLocal(() => {
                        Debug.Log("All data initialization complete!");
                        isDataInitialized = true;
                    });
                }
                else
                {
                    Debug.Log("Firebase failed to initialize, using local data only");
                    isDataInitialized = true;
                }
            });
            
            yield return new WaitUntil(() => firebaseInitComplete);
        }
        else
        {
            Debug.Log("Data initialization complete (local only)");
            isDataInitialized = true;
        }
    }

    private void LoadOnboardingData()
    {
        if (File.Exists(onboardingSavePath))
        {
            string json = File.ReadAllText(onboardingSavePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            this.onboardingComplete = data.onboardingComplete;
            Debug.Log($"Onboarding data loaded: onboardingComplete = {onboardingComplete}");
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

    public void SyncDataFromFirestore(System.Action onComplete = null)
    {
        if (FirestoreManager.Instance != null && FirestoreManager.Instance.IsReady)
        {
            FirestoreManager.Instance.SyncAllCollectionsToLocal(onComplete);
        }
        else
        {
            Debug.LogWarning("Firestore not ready for sync");
            onComplete?.Invoke();
        }
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

    // Check if all systems are ready
    public bool IsSystemReady()
    {
        return isDataInitialized && 
               JSONFileManager.Instance != null && 
               (FirestoreManager.Instance == null || FirestoreManager.Instance.IsReady);
    }
}