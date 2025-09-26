using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class PanelManager : MonoBehaviour
{
    [Header("Panels to manage")]
    public List<GameObject> panels = new List<GameObject>();

    void Update()
    {
        // If user clicks/taps
        if (Input.GetMouseButtonDown(0))
        {
            // Ignore clicks on UI elements (like buttons, sliders, etc.)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            // Close all active panels
            foreach (var panel in panels)
            {
                if (panel != null && panel.activeSelf)
                {
                    panel.SetActive(false);
                }
            }
        }
    }
}
