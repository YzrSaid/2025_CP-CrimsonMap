using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DirectionItemUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI directionText;
    public GameObject unfilledCheckmark;
    public GameObject filledCheckmark;

    private int directionIndex;
    private NavigationDirection direction;
    private bool isCompleted = false;

    public void Initialize(int index, NavigationDirection dir)
    {
        directionIndex = index;
        direction = dir;

        if (directionText != null)
        {
            directionText.text = $"{index + 1}. {dir.instruction}";
        }

        SetCompleted(false);
    }

    public void SetCompleted(bool completed)
    {
        isCompleted = completed;

        if (unfilledCheckmark != null)
        {
            unfilledCheckmark.SetActive(!completed);
        }

        if (filledCheckmark != null)
        {
            filledCheckmark.SetActive(completed);
        }
    }

    public int GetDirectionIndex()
    {
        return directionIndex;
    }

    public NavigationDirection GetDirection()
    {
        return direction;
    }

    public bool IsCompleted()
    {
        return isCompleted;
    }
}