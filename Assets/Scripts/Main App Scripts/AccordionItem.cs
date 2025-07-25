using UnityEngine;
using UnityEngine.UI;

public class AccordionItem : MonoBehaviour
{
    public Button headerButton;
    public GameObject content;
    public AccordionManager manager;

    public Image arrowImage;
    public Sprite arrowDownSprite;
    public Sprite arrowUpSprite;

    private bool isExpanded = false;

    public bool IsExpanded => isExpanded;

    public void Toggle()
    {
        isExpanded = true;
        content.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        arrowImage.sprite = arrowUpSprite;
    }

    public void Collapse()
    {
        isExpanded = false;
        content.SetActive(false);
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        arrowImage.sprite = arrowDownSprite;
    }
}
