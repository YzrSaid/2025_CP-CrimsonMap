using System.Collections.Generic;
using UnityEngine;

public class AccordionManager : MonoBehaviour
{
    public List<AccordionItem> accordionItems = new List<AccordionItem>();
    


    public void ToggleItem(AccordionItem selectedItem)
    {
        foreach (var item in accordionItems)
        {
            if (item == selectedItem)
            {
                if (item.IsExpanded)
                    item.Collapse();
                else
                    item.Toggle();
            }
            else
            {
                item.Collapse();
            }
        }
    }

}
