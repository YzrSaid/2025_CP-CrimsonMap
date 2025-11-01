using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple component for indoor infrastructure button items
/// Just displays the name of the indoor infrastructure
/// Attach this to your indoor item button prefab
/// </summary>
public class IndoorInfrastructureItem : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI nameText; // Name of the room/exit/stair

    private IndoorInfrastructure indoorData;

    public void SetData(IndoorInfrastructure data)
    {
        indoorData = data;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (indoorData == null) return;

        // Set name on button text
        if (nameText != null)
        {
            nameText.text = indoorData.name;
        }
        else
        {
            // Fallback: try to find TextMeshProUGUI in children
            TextMeshProUGUI text = GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = indoorData.name;
            }
        }
    }

    public IndoorInfrastructure GetData()
    {
        return indoorData;
    }
}