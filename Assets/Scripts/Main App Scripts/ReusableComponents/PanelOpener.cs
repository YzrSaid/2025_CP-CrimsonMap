using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(Button))]
public class PanelOpener : MonoBehaviour
{
    [Header("Panels to Open")]
    public List<GameObject> panelsToOpen = new List<GameObject>();
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OpenPanels);
    }

    private void OnDestroy()
    {
        // Clean up listener to prevent leaks
        if (button != null)
        {
            button.onClick.RemoveListener(OpenPanels);
        } 
    }

    private void OpenPanels()
    {
        foreach (GameObject panel in panelsToOpen)
        {
            if (panel != null)
            {
                panel.SetActive(true);
            }
        }
    }
}