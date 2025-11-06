using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class InfrastructureDetailsPanel : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI categoryText;
    public TextMeshProUGUI categoryLegendText;
    public Image infrastructureImage;
    public TextMeshProUGUI emailText;
    public TextMeshProUGUI phoneText;
    public TextMeshProUGUI latAndlngText;
    public TextMeshProUGUI campusText;
    public Button closeButton;

    [Header("Bookmark Buttons")]
    public Button bookmarkEmptyButton;
    public Button bookmarkFilledButton;

    [Header("Indoor Infrastructure ScrollViews")]
    public ScrollRect roomsScrollView;
    public ScrollRect fireExitsScrollView;
    public GameObject roomsListObject;
    public GameObject fireExitsListObject;
    public GameObject noIndoorText;

    [Header("Indoor Item Prefab")]
    public GameObject indoorItemPrefab;

    [Header("Background Settings")]
    public string backgroundPanelName = "BackgroundForExplorePanel";

    private Infrastructure infrastructure;
    private Category category;
    private Node node;
    private CampusData campus;
    private Transform backgroundTransform;
    private List<IndoorInfrastructure> indoorList = new List<IndoorInfrastructure>();
    private bool isBookmarked = false;
    private RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        
        if (bookmarkEmptyButton != null)
            bookmarkEmptyButton.onClick.AddListener(OnBookmarkClicked);
        
        if (bookmarkFilledButton != null)
            bookmarkFilledButton.onClick.AddListener(OnUnbookmarkClicked);
        
        SetupBackground();
    }

    private void SetupBackground()
    {
        backgroundTransform = transform.root.Find(backgroundPanelName);
        if (backgroundTransform == null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                backgroundTransform = SearchAllChildren(canvas.transform, backgroundPanelName);
            }
        }
        if (backgroundTransform != null)
        {
            backgroundTransform.gameObject.SetActive(true);
        }
    }

    private Transform SearchAllChildren(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
            {
                return child;
            }
        }
        return null;
    }

    public void SetData(Infrastructure infra, Category cat, Node nodeData, CampusData campusData)
    {
        infrastructure = infra;
        category = cat;
        node = nodeData;
        campus = campusData;
        
        CheckBookmarkStatus();
        UpdateBookmarkUI();
        PopulateUI();

        StartCoroutine(LoadIndoorInfrastructures());
    }

    private void CheckBookmarkStatus()
    {
        if (infrastructure == null) return;

        BookmarkData bookmarkData = LoadBookmarkData();
        isBookmarked = bookmarkData.bookmarked_infra_ids.Contains(infrastructure.infra_id);
    }

    private void UpdateBookmarkUI()
    {
        if (bookmarkEmptyButton != null)
            bookmarkEmptyButton.gameObject.SetActive(!isBookmarked);
        
        if (bookmarkFilledButton != null)
            bookmarkFilledButton.gameObject.SetActive(isBookmarked);
    }

    private void OnBookmarkClicked()
    {
        if (infrastructure == null) return;

        BookmarkData bookmarkData = LoadBookmarkData();
        
        if (!bookmarkData.bookmarked_infra_ids.Contains(infrastructure.infra_id))
        {
            bookmarkData.bookmarked_infra_ids.Add(infrastructure.infra_id);
            SaveBookmarkData(bookmarkData);
            isBookmarked = true;
            UpdateBookmarkUI();
        }
    }

    private void OnUnbookmarkClicked()
    {
        if (infrastructure == null) return;

        BookmarkData bookmarkData = LoadBookmarkData();
        
        if (bookmarkData.bookmarked_infra_ids.Contains(infrastructure.infra_id))
        {
            bookmarkData.bookmarked_infra_ids.Remove(infrastructure.infra_id);
            SaveBookmarkData(bookmarkData);
            isBookmarked = false;
            UpdateBookmarkUI();
        }
    }

    private BookmarkData LoadBookmarkData()
    {
        string filePath = GetBookmarkFilePath();
        
        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                BookmarkData data = JsonUtility.FromJson<BookmarkData>(json);
                return data ?? new BookmarkData();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading bookmarks: {e.Message}");
                return new BookmarkData();
            }
        }
        
        return new BookmarkData();
    }

    private void SaveBookmarkData(BookmarkData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            string filePath = GetBookmarkFilePath();
            
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.WriteAllText(filePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving bookmarks: {e.Message}");
        }
    }

    private string GetBookmarkFilePath()
    {
        #if UNITY_EDITOR
            return Path.Combine(Application.streamingAssetsPath, "bookmarks.json");
        #else
            return Path.Combine(Application.persistentDataPath, "bookmarks.json");
        #endif
    }

    void PopulateUI()
    {
        if (titleText != null)
            titleText.text = infrastructure.name;

        if (category != null)
        {
            if (categoryText != null)
                categoryText.text = category.name;
            if (categoryLegendText != null && !string.IsNullOrEmpty(category.legend))
            {
                categoryLegendText.text = category.legend;
            }
        }

        if (!string.IsNullOrEmpty(infrastructure.image_url) && infrastructureImage != null)
        {
            LoadBase64Image(infrastructure.image_url);
        }

        if (emailText != null)
            emailText.text = infrastructure.email;

        if (phoneText != null)
            phoneText.text = infrastructure.phone;

        if (node != null)
        {
            if (latAndlngText != null)
                latAndlngText.text = node.latitude.ToString("F6") + " | " + node.longitude.ToString("F6");
        }

        if (campus != null)
        {
            if (campusText != null)
                campusText.text = campus.campus_name;
        }
    }

    private IEnumerator LoadIndoorInfrastructures()
    {
        if (infrastructure == null)
        {
            yield break;
        }

        ClearScrollViewContent(roomsScrollView);
        ClearScrollViewContent(fireExitsScrollView);

        indoorList.Clear();

        bool loadComplete = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "indoor.json",
            (jsonContent) =>
            {
                try
                {
                    IndoorInfrastructure[] allIndoors = JsonHelper.FromJson<IndoorInfrastructure>(jsonContent);

                    foreach (IndoorInfrastructure indoor in allIndoors)
                    {
                        if (indoor.infra_id == infrastructure.infra_id && !indoor.is_deleted)
                        {
                            indoorList.Add(indoor);
                        }
                    }

                    loadComplete = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error parsing indoor.json: {e.Message}");
                    loadComplete = true;
                }
            },
            (error) =>
            {
                Debug.LogError($"Error loading indoor.json: {error}");
                loadComplete = true;
            }
        ));

        yield return new WaitUntil(() => loadComplete);
        PopulateListsByType();
        
        yield return null;
        RecenterPanel();
    }

    private void ClearScrollViewContent(ScrollRect scrollView)
    {
        if (scrollView == null || scrollView.content == null) return;
        
        foreach (Transform child in scrollView.content)
        {
            Destroy(child.gameObject);
        }
    }

    private void PopulateListsByType()
    {
        if (indoorList.Count == 0)
        {
            if (noIndoorText != null)
            {
                noIndoorText.SetActive(true);
            }

            if (roomsListObject != null)
                roomsListObject.SetActive(false);
            if (fireExitsListObject != null)
                fireExitsListObject.SetActive(false);

            return;
        }

        if (noIndoorText != null)
        {
            noIndoorText.SetActive(false);
        }

        List<IndoorInfrastructure> rooms = new List<IndoorInfrastructure>();
        List<IndoorInfrastructure> fireExits = new List<IndoorInfrastructure>();

        foreach (IndoorInfrastructure indoor in indoorList)
        {
            string type = indoor.indoor_type?.ToLower();

            if (type == "room")
            {
                rooms.Add(indoor);
            }
            else if (type == "fire_exit")
            {
                fireExits.Add(indoor);
            }
        }

        PopulateScrollViewList(roomsScrollView, roomsListObject, rooms);
        PopulateScrollViewList(fireExitsScrollView, fireExitsListObject, fireExits);
    }

    private void PopulateScrollViewList(ScrollRect scrollView, GameObject listObject, List<IndoorInfrastructure> items)
    {
        if (scrollView == null || listObject == null)
        {
            return;
        }

        if (items.Count == 0)
        {
            listObject.SetActive(false);
            return;
        }

        listObject.SetActive(true);
        ClearScrollViewContent(scrollView);

        Transform content = scrollView.content;

        foreach (IndoorInfrastructure indoor in items)
        {
            GameObject itemObject = Instantiate(indoorItemPrefab, content);
            IndoorInfrastructureItem itemScript = itemObject.GetComponent<IndoorInfrastructureItem>();
            
            if (itemScript != null)
            {
                itemScript.SetData(indoor);
                
                Button button = itemObject.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() => OnIndoorItemSelected(indoor));
                }
            }
        }
    }

    private void OnIndoorItemSelected(IndoorInfrastructure selectedIndoor)
    {
        Debug.Log($"Selected: {selectedIndoor.name} (Type: {selectedIndoor.indoor_type})");
    }

    private void RecenterPanel()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = Vector2.zero;
        }
    }

    private void LoadBase64Image(string base64String)
    {
        string base64Data = base64String;
        if (base64String.Contains(","))
        {
            base64Data = base64String.Substring(base64String.IndexOf(",") + 1);
        }
        byte[] imageBytes = Convert.FromBase64String(base64Data);

        Texture2D texture = new Texture2D(2, 2);
        if (texture.LoadImage(imageBytes))
        {
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f)
            );

            infrastructureImage.sprite = sprite;
        }
    }

    void Close()
    {
        Destroy(gameObject);
        if (backgroundTransform != null)
        {
            backgroundTransform.gameObject.SetActive(false);
        }
    }
}