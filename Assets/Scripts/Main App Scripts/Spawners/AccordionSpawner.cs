using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Collections;

public class AccordionSpawner : MonoBehaviour
{
    public GameObject accordionItemPrefab;
    public Transform accordionContainer;
    public AccordionManager manager;

    [Header("Loading Check")]
    public float maxWaitTime = 30f;

    private List<string> staticCategories = new List<string> { "Saved", "Recent" };

    void Start()
    {
        // Add static items first
        foreach (string name in staticCategories)
        {
            SpawnAccordionItem(name, null); // No category_id for static items
        }

        // Wait for data initialization before loading dynamic categories
        StartCoroutine(WaitForDataInitializationThenLoad());
    }

    private IEnumerator WaitForDataInitializationThenLoad()
    {
        Debug.Log("AccordionSpawner: Waiting for data initialization to complete...");
        
        float waitTime = 0f;
        
        while (waitTime < maxWaitTime)
        {
            if (GlobalManager.Instance != null && IsDataInitializationComplete())
            {
                Debug.Log("AccordionSpawner: Data initialization complete! Starting to load...");
                yield return StartCoroutine(LoadDynamicCategoriesFromFirebase());
                yield break;
            }
            
            waitTime += Time.deltaTime;
            yield return new WaitForSeconds(0.1f);
        }
        
        Debug.LogWarning("AccordionSpawner: Timed out waiting for data initialization. Attempting to load anyway...");
        yield return StartCoroutine(LoadDynamicCategoriesFromFirebase());
    }

    private bool IsDataInitializationComplete()
    {
        string filePath = GetJsonFilePath("categories.json");
        
        if (!File.Exists(filePath))
        {
            Debug.Log($"AccordionSpawner: File does not exist at {filePath}");
            return false;
        }
        
        try
        {
            string content = File.ReadAllText(filePath);
            if (string.IsNullOrEmpty(content) || content.Length < 10)
            {
                Debug.Log($"AccordionSpawner: File exists but content is empty or too short: {content?.Length ?? 0} characters");
                return false;
            }
            
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

    private string GetJsonFilePath(string fileName)
    {
#if UNITY_EDITOR
        string streamingPath = Path.Combine(Application.streamingAssetsPath, fileName);
        if (File.Exists(streamingPath))
        {
            return streamingPath;
        }
#endif
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    IEnumerator LoadDynamicCategoriesFromFirebase()
    {
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
            string wrappedJson = "{\"categories\":" + jsonData + "}";
            CategoryList categoryList = JsonUtility.FromJson<CategoryList>(wrappedJson);

            foreach (Category cat in categoryList.categories)
            {
                SpawnAccordionItem(cat.name, cat.category_id);
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
        Debug.LogWarning("Continuing with static categories only");
    }

    [Header("Prefab References")]
    public GameObject infrastructurePrefab; // Assign ExploreInfrastructurePrefab here

    void SpawnAccordionItem(string categoryName, string categoryId)
    {
        GameObject newItem = Instantiate(accordionItemPrefab, accordionContainer);
        AccordionItem item = newItem.GetComponent<AccordionItem>();
        
        if (item == null)
        {
            Debug.LogError("AccordionSpawner: AccordionItem component not found on prefab!");
            Destroy(newItem);
            return;
        }

        // Force correct RectTransform positioning
        RectTransform itemRect = newItem.GetComponent<RectTransform>();
        if (itemRect != null)
        {
            itemRect.anchorMin = new Vector2(0, 1);
            itemRect.anchorMax = new Vector2(1, 1);
            itemRect.pivot = new Vector2(0.5f, 1);
            itemRect.offsetMin = new Vector2(0, itemRect.offsetMin.y); // Left = 0
            itemRect.offsetMax = new Vector2(0, itemRect.offsetMax.y); // Right = 0
            itemRect.localScale = Vector3.one;
            itemRect.localPosition = new Vector3(0, itemRect.localPosition.y, 0);
        }

        item.manager = manager;

        // CRITICAL: Assign the infrastructure prefab reference
        if (infrastructurePrefab != null)
        {
            item.infrastructurePrefab = infrastructurePrefab;
            Debug.Log($"✅ Assigned infrastructure prefab to {categoryName}");
        }
        else
        {
            Debug.LogWarning("⚠️ Infrastructure prefab not assigned in AccordionSpawner!");
        }

        // Ensure infrastructure container is found
        if (item.infrastructureContainer == null)
        {
            // Try to find it in the spawned item
            Transform contentPanel = item.contentPanel;
            if (contentPanel != null)
            {
                Transform container = contentPanel.Find("InfrastructureContainer");
                if (container == null && contentPanel.childCount > 0)
                {
                    container = contentPanel.GetChild(0);
                }
                
                if (container != null)
                {
                    item.infrastructureContainer = container;
                    Debug.Log($"✅ Found and assigned infrastructure container for {categoryName}");
                }
                else
                {
                    Debug.LogError($"❌ Could not find infrastructure container in {categoryName}!");
                }
            }
        }

        // Set category ID for dynamic categories
        if (!string.IsNullOrEmpty(categoryId))
        {
            item.SetCategoryId(categoryId);
        }

        // Set header text
        if (item.headerButton != null)
        {
            TMPro.TextMeshProUGUI headerText = item.headerButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (headerText != null)
            {
                headerText.text = categoryName;
            }
        }

        // Set up button listener
        item.headerButton.onClick.RemoveAllListeners();
        item.headerButton.onClick.AddListener(() => manager.ToggleItem(item));

        manager.accordionItems.Add(item);

        Debug.Log($"✅ Spawned accordion item: {categoryName} (Category ID: {categoryId ?? "None"})");
    }
}