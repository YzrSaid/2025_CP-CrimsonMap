using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public class ExploreInfrastructureItem : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;

    [Header("Navigation Buttons")]
    public Button navigateButton;
    public Button viewDetailsButton;

    [Header("Details Panel")]
    public GameObject detailsPanelPrefab;

    private Infrastructure infrastructureData;
    private Category categoryData;
    private static Dictionary<string, Category> categoryCache = new Dictionary<string, Category>();
    private static bool categoriesLoaded = false;

    void Awake()
    {
        if (navigateButton != null)
            navigateButton.onClick.AddListener(OnNavigateClicked);
        if (viewDetailsButton != null)
            viewDetailsButton.onClick.AddListener(OnViewDetailsClicked);

        if (!categoriesLoaded)
        {
            StartCoroutine(LoadCategories());
        }
    }

    private IEnumerator LoadCategories()
    {
        bool loadComplete = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
                                         "categories.json",
        (jsonContent) =>
        {
            try
            {
                Category[] categories = JsonHelper.FromJson<Category>(jsonContent);
                categoryCache.Clear();
                foreach (Category cat in categories)
                {
                    categoryCache[cat.category_id] = cat;
                }
                categoriesLoaded = true;
                loadComplete = true;
            }
            catch (System.Exception e)
            {
                loadComplete = true;
            }
        },
        (error) =>
        {
            loadComplete = true;
        }
                                     ));

        yield return new WaitUntil(() => loadComplete);
    }

    public void SetInfrastructureData(Infrastructure data)
    {
        infrastructureData = data;

        if (categoriesLoaded && categoryCache.TryGetValue(data.category_id, out Category category))
        {
            categoryData = category;
        }

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
        Debug.Log($"Navigate to: {infrastructureData.name}");
    }

    void OnViewDetailsClicked()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        GameObject detailsPanel = Instantiate(detailsPanelPrefab, canvas.transform);
        InfrastructureDetailsPanel detailsScript = detailsPanel.GetComponent<InfrastructureDetailsPanel>();

        if (detailsScript != null)
        {
            detailsScript.SetData(infrastructureData, categoryData);
        }
    }

    public Infrastructure GetInfrastructureData()
    {
        return infrastructureData;
    }

    public Category GetCategoryData()
    {
        return categoryData;
    }
}