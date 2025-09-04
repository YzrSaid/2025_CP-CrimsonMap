using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;
using System;

public class FirestoreManager : MonoBehaviour
{
    public static FirestoreManager Instance { get; private set; }

    private FirebaseFirestore db;
    private bool isFirebaseReady = false;

    // Collections that need to be synced with local JSON files
    private readonly string[] collectionsToSync = {
        "Campus",
        "Categories",
        "Infrastructure",
        "Maps",
        "Nodes"
    };

    public bool IsReady => isFirebaseReady;

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

    public void SyncAllCollectionsToLocal(System.Action onComplete = null)
    {
        if (!isFirebaseReady)
        {
            Debug.LogWarning("Firebase not ready, cannot sync collections");
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(SyncAllCollectionsCoroutine(onComplete));
    }

    private IEnumerator SyncAllCollectionsCoroutine(System.Action onComplete)
    {
        Debug.Log("Starting sync of all collections...");

        int completedSyncs = 0;
        int totalSyncs = collectionsToSync.Length;

        foreach (string collectionName in collectionsToSync)
        {
            SyncCollectionToLocal(collectionName, () =>
            {
                completedSyncs++;
            });
        }

        // Wait for all syncs to complete
        yield return new WaitUntil(() => completedSyncs >= totalSyncs);

        Debug.Log("All collections synced successfully");
        onComplete?.Invoke();
    }

    public void SyncCollectionToLocal(string collectionName, System.Action onComplete = null)
    {
        if (!isFirebaseReady)
        {
            Debug.LogWarning($"Firebase not ready, cannot sync {collectionName}");
            onComplete?.Invoke();
            return;
        }

        // Force lowercase filenames
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
        string indent = new string(' ', indentLevel * 4); // 4 spaces per level
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


    // Method to fetch a specific document
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

    // Method to listen to real-time updates for a collection
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