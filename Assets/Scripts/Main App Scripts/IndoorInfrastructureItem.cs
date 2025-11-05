using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class IndoorInfrastructureItem : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI nameText; 

    private IndoorInfrastructure indoorData;

    public void SetData(IndoorInfrastructure data)
    {
        indoorData = data;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (indoorData == null) return;
        if (nameText != null)
        {
            nameText.text = indoorData.name;
        }
        else
        {
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