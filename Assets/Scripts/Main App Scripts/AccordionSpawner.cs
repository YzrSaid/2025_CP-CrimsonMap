using UnityEngine;
using System.Collections.Generic;

public class AccordionSpawner : MonoBehaviour
{
    public GameObject accordionItemPrefab;
    public Transform accordionContainer; // where to parent items
    public AccordionManager manager;

    // Simulated category names (from database or API)
    private List<string> categoryNames = new List<string> { "Academics", "Clinics", "Offices" };

    void Start()
    {
        foreach (string name in categoryNames)
        {
            GameObject newItem = Instantiate(accordionItemPrefab, accordionContainer);

            AccordionItem item = newItem.GetComponent<AccordionItem>();
            item.manager = manager;

            // Set header label
            item.headerButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = name;

            // Add to manager’s list
            manager.accordionItems.Add(item);
        }
    }
}
