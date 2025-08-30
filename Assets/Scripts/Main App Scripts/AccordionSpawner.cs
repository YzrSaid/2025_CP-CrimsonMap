using System;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;


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

        LoadDynamicCategoriesFromFirebase();
    }

    void LoadDynamicCategoriesFromFirebase()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "categories.json");

        if (File.Exists(filePath))
        {
            string jsonData = File.ReadAllText(filePath);
            // Create a wrapper for this json utility, we will manually create wrapper for this
            string wrappedJson = "{\"categories\":" + jsonData + "}";
            CategoryList categoryList = JsonUtility.FromJson<CategoryList>(wrappedJson);

            // We will now iterate the categories and show it for each accordion item
            foreach (Category cat in categoryList.categories)
            {
                SpawnAccordionItem(cat.name);
            }   
        }
        else
        {
            Debug.LogError("Categories.json not found in StreamingAssets!");
        }
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
         
        }
    }
}
