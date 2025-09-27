using System;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using System.Collections;

public class AccordionSpawner : MonoBehaviour
{
    public GameObject accordionItemPrefab;
    public Transform accordionContainer;
    public AccordionManager manager;

    [Header("Loading Check")]
    public float maxWaitTime = 30f; // Max time to wait for data initialization

    private List<string> staticCategories = new List<string> { "Saved", "Recent" };

    void Start()
    {
        // Add static items first
        foreach (string name in staticCategories)
        {
            SpawnAccordionItem(name);
        }

        // Wait for data initialization before loading dynamic categories
        StartCoroutine(WaitForDataInitializationThenLoad());
    }

    // NEW: Wait for MainAppLoader to complete before loading data
    private IEnumerator WaitForDataInitializationThenLoad()
    {
        Debug.Log("AccordionSpawner: Waiting for data initialization to complete...");
        
        float waitTime = 0f;
        
        // Wait for GlobalManager to exist and data initialization to complete
        while (waitTime < maxWaitTime)
        {
            // Check if GlobalManager exists and data initialization is complete
            if (GlobalManager.Instance != null && IsDataInitializationComplete())
            {
                Debug.Log("AccordionSpawner: Data initialization complete! Starting to load...");
                yield return StartCoroutine(LoadDynamicCategoriesFromFirebase());
                yield break;
            }
            
            waitTime += Time.deltaTime;
            yield return new WaitForSeconds(0.1f); // Check every 100ms
        }
        
        // Timeout - still try to load but log warning
        Debug.LogWarning("AccordionSpawner: Timed out waiting for data initialization. Attempting to load anyway...");
        yield return StartCoroutine(LoadDynamicCategoriesFromFirebase());
    }

    // Check if data initialization is complete
    private bool IsDataInitializationComplete()
    {
        // Get the correct file path based on platform
        string filePath = GetJsonFilePath("categories.json");
        
        if (!File.Exists(filePath))
        {
            Debug.Log($"AccordionSpawner: File does not exist at {filePath}");
            return false;
        }
        
        try
        {
            string content = File.ReadAllText(filePath);
            if (string.IsNullOrEmpty(content) || content.Length < 10) // Basic validity check
            {
                Debug.Log($"AccordionSpawner: File exists but content is empty or too short: {content?.Length ?? 0} characters");
                return false;
            }
            
            // Try to parse the JSON to make sure it's valid
            string wrappedJson = "{\"categories\":" + content + "}";
            CategoryList testList = JsonUtility.FromJson<CategoryList>(wrappedJson);
            
            if (testList == null || testList.categories == null || testList.categories.Count == 0)
            {
                Debug.Log("AccordionSpawner: JSON parsed but no valid categories found");
                return false;
            }
            
            Debug.Log($"AccordionSpawner: Data validation successful - found {testList.categories.Count} categories");
            return true;
        }
        catch (Exception e)
        {
            Debug.Log($"AccordionSpawner: Error reading/parsing file: {e.Message}");
            return false;
        }
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

    IEnumerator LoadDynamicCategoriesFromFirebase()
    {
        // Use the CrossPlatformFileLoader to load categories.json
        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "categories.json", 
            OnCategoriesLoadSuccess, 
            OnCategoriesLoadError
        ));
    }

    void OnCategoriesLoadSuccess(string jsonData)
    {
        try
        {
            // Create a wrapper for this json utility, we will manually create wrapper for this
            string wrappedJson = "{\"categories\":" + jsonData + "}";
            CategoryList categoryList = JsonUtility.FromJson<CategoryList>(wrappedJson);

            // We will now iterate the categories and show it for each accordion item
            foreach (Category cat in categoryList.categories)
            {
                SpawnAccordionItem(cat.name);
            }

            Debug.Log($"✅ AccordionSpawner: Successfully loaded {categoryList.categories.Count} categories from file");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing categories JSON: {e.Message}");
        }
    }

    void OnCategoriesLoadError(string errorMessage)
    {
        Debug.LogError($"Failed to load categories: {errorMessage}");
        
        // Optional: Add fallback categories or show error UI
        Debug.LogWarning("Continuing with static categories only");
    }

    void SpawnAccordionItem(string categoryName)
    {
        GameObject newItem = Instantiate(accordionItemPrefab, accordionContainer);
        AccordionItem item = newItem.GetComponent<AccordionItem>();
        item.manager = manager;

        item.headerButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = categoryName;

        item.headerButton.onClick.RemoveAllListeners();
        item.headerButton.onClick.AddListener(() => manager.ToggleItem(item));

        manager.accordionItems.Add(item);

        if (categoryName == "Saved" || categoryName == "Recent")
        {
            // Handle static categories if needed
        }
    }
}