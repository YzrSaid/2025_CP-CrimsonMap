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

    private List<string> staticCategories = new List<string> { "Saved", "Recent" };

    void Start()
    {
        // Add static items first
        foreach (string name in staticCategories)
        {
            SpawnAccordionItem(name);
        }

        // Start the coroutine to load dynamic categories
        StartCoroutine(LoadDynamicCategoriesFromFirebase());
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

            Debug.Log($"Successfully loaded {categoryList.categories.Count} categories from file");
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