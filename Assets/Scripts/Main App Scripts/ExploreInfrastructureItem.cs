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
    private Node nodeData;
    private CampusData campusData;
    private static Dictionary<string, Category> categoryCache = new Dictionary<string, Category>();
    private static Dictionary<string, CampusData> campusCache = new Dictionary<string, CampusData>();
    private static bool categoriesLoaded = false;
    private static bool campusesLoaded = false;

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

        if (!campusesLoaded)
        {
            StartCoroutine(LoadCampuses());
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
                catch (System.Exception)
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

    private IEnumerator LoadCampuses()
    {
        bool loadComplete = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "campus.json",
            (jsonContent) =>
            {
                try
                {
                    CampusList campusList = JsonUtility.FromJson<CampusList>("{\"campuses\":" + jsonContent + "}");
                    campusCache.Clear();
                    foreach (CampusData campus in campusList.campuses)
                    {
                        campusCache[campus.campus_id] = campus;
                    }
                    campusesLoaded = true;
                    loadComplete = true;
                }
                catch (System.Exception)
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
        StartCoroutine(SearchAndShowDetails());
    }

    private IEnumerator SearchAndShowDetails()
    {
        yield return StartCoroutine(SearchNodeAcrossAllMaps(infrastructureData.infra_id));

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            yield break;
        }

        GameObject detailsPanel = Instantiate(detailsPanelPrefab, canvas.transform);
        InfrastructureDetailsPanel detailsScript = detailsPanel.GetComponent<InfrastructureDetailsPanel>();

        if (detailsScript != null)
        {
            detailsScript.SetData(infrastructureData, categoryData, nodeData, campusData);
        }
    }

    private IEnumerator SearchNodeAcrossAllMaps(string infraId)
    {
        List<string> mapIds = new List<string>();

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "maps.json",
            (jsonContent) =>
            {
                try
                {
                    MapList mapList = JsonUtility.FromJson<MapList>("{\"maps\":" + jsonContent + "}");
                    foreach (MapData map in mapList.maps)
                    {
                        mapIds.Add(map.map_id);
                    }
                }
                catch (System.Exception)
                {
                }
            },
            (error) =>
            {
            }
        ));

        foreach (string mapId in mapIds)
        {
            bool foundNode = false;

            yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
                $"nodes_{mapId}.json",
                (jsonContent) =>
                {
                    try
                    {
                        Node[] nodes = JsonHelper.FromJson<Node>(jsonContent);
                        foreach (Node node in nodes)
                        {
                            if (node.related_infra_id == infraId)
                            {
                                nodeData = node;
                                if (campusesLoaded && campusCache.TryGetValue(node.campus_id, out CampusData campus))
                                {
                                    campusData = campus;
                                }
                                foundNode = true;
                                break;
                            }
                        }
                    }
                    catch (System.Exception)
                    {
                    }
                },
                (error) =>
                {
                }
            ));

            if (foundNode)
                break;
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