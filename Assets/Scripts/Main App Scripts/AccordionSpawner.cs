using System.Collections.Generic;
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

        // Now load the dynamic categories from Firebase
        LoadDynamicCategoriesFromFirebase();
    }

    void LoadDynamicCategoriesFromFirebase()
    {
        // 🔥 EXAMPLE ONLY — you’ll replace this with actual Firebase fetching
        List<string> dynamicCategoriesFromFirebase = new List<string> { "Academics", "Clinics", "Offices" };

        foreach (string name in dynamicCategoriesFromFirebase)
        {
            SpawnAccordionItem(name);
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

        // Optional: preload or tag static items
        if (categoryName == "Saved" || categoryName == "Recent")
        {
            // e.g., change icon, preload content, etc.
        }
    }
}
