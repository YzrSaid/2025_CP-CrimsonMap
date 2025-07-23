using UnityEngine;
using UnityEngine.UI;

public class AccordionItem : MonoBehaviour
{
    public Button headerButton;
    public GameObject content;
    public AccordionManager manager;

    private bool isExpanded = false;

    void Start()
    {
        headerButton.onClick.AddListener(() => manager.ToggleItem(this));
        content.SetActive(false); // Make sure it's hidden by default
    }

    public void Toggle()
    {
        isExpanded = true;
        content.SetActive(true);
    }

    public void Collapse()
    {
        isExpanded = false;
        content.SetActive(false);
    }
}
