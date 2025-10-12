using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class RouteItem : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI distanceText;
    public TextMeshProUGUI walkingTimeText;
    public TextMeshProUGUI viaModeText;
    public TextMeshProUGUI pathInfoText;
    public Button itemButton;

    [Header("Visual Feedback")]
    public Image backgroundImage;
    public Outline outlineComponent;
    public Color normalColor = new Color(1f, 1f, 1f, 0.1f);
    public Color selectedColor = new Color(0.2f, 0.8f, 0.3f, 0.3f);

    private int routeIndex;
    private System.Action<int> onRouteSelected;
    private bool isSelected = false;

    public void Initialize(int index, RouteData routeData, System.Action<int> selectCallback)
    {
        routeIndex = index;
        onRouteSelected = selectCallback;

        if (titleText != null)
        {
            titleText.text = $"Route #{index + 1}";
        }

        if (distanceText != null)
        {
            distanceText.text = $"<b>Distance:</b> {routeData.formattedDistance}";
        }

        if (walkingTimeText != null)
        {
            walkingTimeText.text = $"<b>Time:</b> ~{routeData.walkingTime}";
        }

        if (pathInfoText != null)
        {
            string pathInfo = $"<b>Path ({routeData.path.Count} stops):</b>\n";

            for (int i = 0; i < routeData.path.Count; i++)
            {
                var node = routeData.path[i].node;
                pathInfo += $"{i + 1}. {node.name}\n";
            }

            pathInfoText.text = pathInfo;
        }

        if (itemButton != null)
        {
            itemButton.onClick.RemoveAllListeners();
            itemButton.onClick.AddListener(OnItemClicked);
        }

        // Initialize visual state
        SetSelected(false);
        
        // Make sure outline is disabled by default
        if (outlineComponent != null)
        {
            outlineComponent.enabled = false;
        }
    }

    private void OnItemClicked()
    {
        onRouteSelected?.Invoke(routeIndex);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (backgroundImage != null)
        {
            backgroundImage.color = selected ? selectedColor : normalColor;
        }

        if (outlineComponent != null)
        {
            outlineComponent.enabled = selected;
        }
    }

    public bool IsSelected()
    {
        return isSelected;
    }

    void OnDestroy()
    {
        if (itemButton != null)
        {
            itemButton.onClick.RemoveAllListeners();
        }
    }
}

[System.Serializable]
public class RouteData
{
    public List<PathNode> path;
    public float totalDistance;
    public string formattedDistance;
    public string walkingTime;
    public Node startNode;
    public string viaMode;
    public bool isRecommended;
    public Node endNode;
}