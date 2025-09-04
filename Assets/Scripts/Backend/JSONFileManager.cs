using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;

public class JSONFileManager : MonoBehaviour
{
    public static JSONFileManager Instance { get; private set; }

    private string dataPath;
    private string streamingAssetsPath;
    private bool useStreamingAssets;
    
    // List of required JSON files
    private readonly string[] requiredFiles = {
        "campus.json",
        "categories.json", 
        "edges.json",
        "infrastructure.json",
        "map.json",
        "nodes.json",
        "recent_destination.json",
        "rooms.json",
        "saved_destinations.json"
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
                streamingAssetsPath = Path.Combine(Application.streamingAssetsPath, "Data");
                dataPath = streamingAssetsPath;
                
                // Create StreamingAssets/Data folder if it doesn't exist
                if (!Directory.Exists(streamingAssetsPath))
                {
                    Directory.CreateDirectory(streamingAssetsPath);
                    Debug.Log($"Created StreamingAssets/Data folder at: {streamingAssetsPath}");
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
        
        foreach (string fileName in requiredFiles)
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
        
        Debug.Log("All JSON files checked/created successfully");
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

    private string GetDefaultJSONContent(string fileName)
    {
        switch (fileName)
        {
            case "campus.json":
                return "[]"; // Will be populated from Firestore
                
            case "categories.json":
                return "[]"; // Will be populated from Firestore
                
            case "edges.json":
                return "[]"; // Empty array initially
                
            case "infrastructure.json":
                return "[]"; // Will be populated from Firestore
                
            case "map.json":
                return "[]"; // Will be populated from Firestore
                
            case "nodes.json":
                return "[]"; // Will be populated from Firestore
                
            case "recent_destination.json":
                return CreateDefaultRecentDestinations();
                
            case "rooms.json":
                return "[]"; // Empty array initially
                
            case "saved_destinations.json":
                return CreateDefaultSavedDestinations();
                
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

    public void AddRecentDestination(object destination)
    {
        // Implementation for adding to recent destinations
        // This is just a template - adjust based on your data structure
        string jsonContent = ReadJSONFile("recent_destination.json");
        if (!string.IsNullOrEmpty(jsonContent))
        {
            // Parse, modify, and save back
            // Implementation depends on your destination object structure
            Debug.Log("Adding recent destination...");
        }
    }

    public void AddSavedDestination(object destination)
    {
        // Implementation for adding to saved destinations
        // This is just a template - adjust based on your data structure
        string jsonContent = ReadJSONFile("saved_destinations.json");
        if (!string.IsNullOrEmpty(jsonContent))
        {
            // Parse, modify, and save back
            // Implementation depends on your destination object structure
            Debug.Log("Adding saved destination...");
        }
    }
}