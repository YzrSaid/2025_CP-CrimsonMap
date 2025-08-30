using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(Button))]
public class PanelCloser : MonoBehaviour
{
    [Header("Panels to Close")]
    public List<GameObject> panelsToClose = new List<GameObject>();
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(ClosePanels);
    }

    private void OnDestroy()
    {
        // Clean up listener to prevent leaks
        if (button != null)
        {
            button.onClick.RemoveListener(ClosePanels);

        }
    }
    private void ClosePanels()
    {
        foreach (GameObject panel in panelsToClose)
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }
    } 
}