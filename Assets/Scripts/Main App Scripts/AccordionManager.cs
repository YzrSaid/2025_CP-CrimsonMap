using System.Collections.Generic;
using UnityEngine;

public class AccordionManager : MonoBehaviour
{
    public List<AccordionItem> accordionItems;

    public void ToggleItem(AccordionItem selectedItem)
    {
        foreach (var item in accordionItems)
        {
            if (item == selectedItem)
                item.Toggle();
            else
                item.Collapse();
        }
    }
}
