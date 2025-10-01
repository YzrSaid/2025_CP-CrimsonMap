using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ExploreInfrastructureItem : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    
    [Header("Navigation Buttons")]
    public Button navigateButton;
    public Button viewDetailsButton;
    public Button reportButton;

    private Infrastructure infrastructureData;

    void Awake()
    {
        if (navigateButton != null)
            navigateButton.onClick.AddListener(OnNavigateClicked);
        
        if (viewDetailsButton != null)
            viewDetailsButton.onClick.AddListener(OnViewDetailsClicked);
        
        if (reportButton != null)
            reportButton.onClick.AddListener(OnReportClicked);
    }

    public void SetInfrastructureData(Infrastructure data)
    {
        infrastructureData = data;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (infrastructureData == null)
        {
            return;
        }

        if (titleText != null)
            titleText.text = infrastructureData.name;

        if (descriptionText != null)
        {
            string description = "";
            if (!string.IsNullOrEmpty(infrastructureData.phone))
                description += $"{infrastructureData.phone}";
            if (!string.IsNullOrEmpty(infrastructureData.email))
                description += $"\n{infrastructureData.email}";
            
            descriptionText.text = description;
        }
    }

    void OnNavigateClicked()
    {
        // TODO: Implement navigation logic
        Debug.Log($"Navigate to: {infrastructureData.name}");
    }

    void OnViewDetailsClicked()
    {
        // TODO: Open details panel/modal
        Debug.Log($"View details for: {infrastructureData.name}");
    }

    void OnReportClicked()
    {
        // TODO: Open report dialog
        Debug.Log($"Report issue for: {infrastructureData.name}");
    }

    public Infrastructure GetInfrastructureData()
    {
        return infrastructureData;
    }
}