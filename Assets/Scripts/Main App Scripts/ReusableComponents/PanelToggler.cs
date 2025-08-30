using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(Button))]
public class PanelToggler : MonoBehaviour
{
    [Header("Panels To Toggle")]
    public List<GameObject> panelsToToggle = new List<GameObject>();

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(TogglePanels);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(TogglePanels);
        }
    }

    private void TogglePanels()
    {
        foreach (GameObject panel in panelsToToggle)
        {
            if (panel != null)
            {
                panel.SetActive(!panel.activeSelf);
            }
        }
    }
}