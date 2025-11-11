using UnityEngine;
using UnityEngine.UI;

public class ARUIManager : MonoBehaviour
{
    [Header("UI Elements - Navigation Mode Only")]
    public GameObject mapPanel;
    public GameObject directionPanel;
    public GameObject topPanel;
    public GameObject destinationPanel;

    [Header("Settings")]
    public bool enableDebugLogs = true;

    void Start()
    {
        ConfigureUIForNavigationMode();
    }
    
    private void ConfigureUIForNavigationMode()
    {
        // Only Navigation mode now - show all navigation UI
        SetUIElementActive(mapPanel, true);
        SetUIElementActive(directionPanel, true);
        SetUIElementActive(destinationPanel, true);
        SetUIElementActive(topPanel, true);
        
        RebuildTopPanelLayout();
    }

    private void SetUIElementActive(GameObject uiElement, bool active)
    {
        if (uiElement != null)
        {
            uiElement.SetActive(active);
        }
    }

    private void RebuildTopPanelLayout()
    {
        if (topPanel != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(topPanel.GetComponent<RectTransform>());
        }
    }
}