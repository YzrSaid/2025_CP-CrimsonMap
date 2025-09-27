using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class InfrastructurePopulator : MonoBehaviour
{
    [Header("Dropdowns")]
    public TMP_Dropdown dropdownFrom;
    public TMP_Dropdown dropdownTo;

    [Header("Data")]
    public InfrastructureList infrastructureList;

    [Header("Loading Check")]
    public float maxWaitTime = 30f; // Max time to wait for data initialization

    void Start()
    {
        StartCoroutine(WaitForDataInitializationThenLoad());
    }

    // NEW: Wait for MainAppLoader to complete before loading data
    private IEnumerator WaitForDataInitializationThenLoad()
    {
        Debug.Log("InfrastructurePopulator: Waiting for data initialization to complete...");
        
        float waitTime = 0f;
        
        // Wait for GlobalManager to exist and data initialization to complete
        while (waitTime < maxWaitTime)
        {
            // Check if GlobalManager exists and data initialization is complete
            if (GlobalManager.Instance != null && IsDataInitializationComplete())
            {
                Debug.Log("InfrastructurePopulator: Data initialization complete! Starting to load...");
                yield return StartCoroutine(LoadInfrastructuresCoroutine());
                yield break;
            }
            
            waitTime += Time.deltaTime;
            yield return new WaitForSeconds(0.1f); // Check every 100ms
        }
        
        // Timeout - still try to load but log warning
        Debug.LogWarning("InfrastructurePopulator: Timed out waiting for data initialization. Attempting to load anyway...");
        yield return StartCoroutine(LoadInfrastructuresCoroutine());
    }

    // Check if data initialization is complete
    private bool IsDataInitializationComplete()
    {
        // Get the correct file path based on platform
        string filePath = GetJsonFilePath("infrastructure.json");
        
        if (!File.Exists(filePath))
        {
            return false;
        }
        
        try
        {
            string content = File.ReadAllText(filePath);
            if (string.IsNullOrEmpty(content) || content.Length < 10) // Basic validity check
            {
                return false;
            }
        }
        catch
        {
            return false;
        }
        
        return true; // File exists and has content
    }

    // Helper method to get correct file path based on platform
    private string GetJsonFilePath(string fileName)
    {
#if UNITY_EDITOR
        // In Unity Editor, check StreamingAssets first, then persistent data
        string streamingPath = Path.Combine(Application.streamingAssetsPath, fileName);
        if (File.Exists(streamingPath))
        {
            return streamingPath;
        }
#endif
        // On device/runtime, use persistent data path
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    private IEnumerator LoadInfrastructuresCoroutine()
    {
        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "infrastructure.json",
            OnInfrastructureDataLoaded,
            OnInfrastructureDataError
        ));
    }

    private void OnInfrastructureDataLoaded(string jsonContent)
    {
        try
        {
            // wrap it
            string wrappedJson = "{\"infrastructures\":" + jsonContent + "}";

            infrastructureList = JsonUtility.FromJson<InfrastructureList>(wrappedJson);

            if (infrastructureList == null || infrastructureList.infrastructures.Length == 0)
            {
                Debug.LogWarning("No infrastructure data loaded.");
                return;
            }

            // Populate dropdowns after successful loading
            PopulateDropdown(dropdownFrom);
            PopulateDropdown(dropdownTo);

            Debug.Log($"✅ InfrastructurePopulator: Successfully loaded {infrastructureList.infrastructures.Length} infrastructures");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing infrastructure JSON: {e.Message}");
        }
    }

    private void OnInfrastructureDataError(string errorMessage)
    {
        Debug.LogError($"Failed to load infrastructure data: {errorMessage}");
    }

    private void PopulateDropdown(TMP_Dropdown dropdown)
    {
        // Clear first the dropdown
        dropdown.ClearOptions();

        if (infrastructureList == null || infrastructureList.infrastructures.Length == 0)
        {
            return;
        }

        List<string> options = new List<string>();
        foreach (var i in infrastructureList.infrastructures)
        {
            options.Add(i.name);
        }

        dropdown.AddOptions(options);
    }

    // Get the selected building object
    public Infrastructure GetSelectedInfrastructure(TMP_Dropdown dropdown)
    {
        int index = dropdown.value;
        if (index >= 0 && index < infrastructureList.infrastructures.Length)
        {
            return infrastructureList.infrastructures[index];
        }
        return null;
    }
}