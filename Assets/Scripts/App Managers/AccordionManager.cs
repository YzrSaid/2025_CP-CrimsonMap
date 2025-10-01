using System.Collections.Generic;
using UnityEngine;

public class AccordionManager : MonoBehaviour
{
    public List<AccordionItem> accordionItems = new List<AccordionItem>();

    public void ToggleItem(AccordionItem selectedItem)
    {
        bool willExpand = !selectedItem.IsExpanded;
        
        // First, collapse all OTHER items (skip if already collapsed)
        foreach (var item in accordionItems)
        {
            if (item != selectedItem && item.IsExpanded)
            {
                item.Collapse();
            }
        }
        
        // Then toggle the selected item
        if (willExpand)
        {
            selectedItem.Expand();
        }
        else
        {
            selectedItem.Collapse();
        }
    }
}