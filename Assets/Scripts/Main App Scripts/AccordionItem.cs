using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AccordionItem : MonoBehaviour
{
    [Header("UI References")]
    public Button headerButton;
    public RectTransform contentPanel;
    public Transform infrastructureContainer;
    public string infrastructureContainerName = "Content_Panel";

    [Header("Prefab Reference")]
    [HideInInspector]
    public GameObject infrastructurePrefab;

    [Header("Animation Settings")]
    public float animationSpeed = 5f;
    public float minHeight = 60f;
    public float itemHeight = 120f;
    public float padding = 20f;
    public float emptyMessageHeight = 80f;

    [Header("Empty State")]
    public bool showEmptyMessage = true;
    public string emptyMessageText = "No infrastructures available";
    public TMP_FontAsset emptyMessageFont;
    public float emptyMessageFontSize = 16f;
    public Color emptyMessageColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    public TextAlignmentOptions emptyMessageAlignment = TextAlignmentOptions.Center;
    public FontStyles emptyMessageFontStyle = FontStyles.Normal;
    public float emptyMessagePaddingTop = 20f;
    public float emptyMessagePaddingBottom = 20f;
    public float emptyMessagePaddingLeft = 20f;
    public float emptyMessagePaddingRight = 20f;

    [HideInInspector]
    public AccordionManager manager;

    private List<GameObject> spawnedInfrastructures = new List<GameObject>();
    private RectTransform rectTransform;
    private float targetHeight;
    private bool isExpanded = false;
    private string categoryId;
    private bool infrastructuresLoaded = false;
    private GameObject emptyMessageObject;

    public bool IsExpanded => isExpanded;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (contentPanel == null)
        {
            contentPanel = transform.Find("Content")?.GetComponent<RectTransform>();
            if (contentPanel == null)
            {
                contentPanel = transform.Find("Content_Panel")?.GetComponent<RectTransform>();
            }
        }

        if (infrastructureContainer == null && contentPanel != null)
        {
            infrastructureContainer = FindInfrastructureContainer();
        }

        rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, minHeight);
        
        if (contentPanel != null)
            contentPanel.gameObject.SetActive(false);
    }

    private Transform FindInfrastructureContainer()
    {
        Transform found = contentPanel.Find(infrastructureContainerName);
        if (found != null)
        {
            return found;
        }

        if (contentPanel.childCount > 0)
        {
            return contentPanel.GetChild(0);
        }

        return null;
    }

    public void SetCategoryId(string catId)
    {
        categoryId = catId;
    }

    public IEnumerator LoadInfrastructures()
    {
        if (infrastructuresLoaded)
        {
            yield break;
        }

        if (string.IsNullOrEmpty(categoryId))
        {
            infrastructuresLoaded = true;
            
            if (isExpanded)
            {
                yield return new WaitForEndOfFrame();
                ShowEmptyMessage();
                targetHeight = minHeight + emptyMessageHeight;
                rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
            }
            yield break;
        }

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "infrastructure.json",
            OnInfrastructuresLoadSuccess,
            OnInfrastructuresLoadError
        ));
    }

    void OnInfrastructuresLoadSuccess(string jsonData)
    {
        try
        {
            string wrappedJson = "{\"infrastructures\":" + jsonData + "}";
            InfrastructureList infrastructureList = JsonUtility.FromJson<InfrastructureList>(wrappedJson);

            int loadedCount = 0;
            foreach (Infrastructure infra in infrastructureList.infrastructures)
            {
                if (!infra.is_deleted && infra.category_id == categoryId)
                {
                    SpawnInfrastructureItem(infra);
                    loadedCount++;
                }
            }

            infrastructuresLoaded = true;

            Debug.Log($"AccordionItem: Loaded {loadedCount} infrastructures for category {categoryId}");

            StartCoroutine(FinishExpandAfterLoad());
        }
        catch (System.Exception e)
        {
            Debug.LogError($"AccordionItem: Error parsing infrastructure JSON: {e.Message}");
            infrastructuresLoaded = true;
            
            if (isExpanded)
            {
                StartCoroutine(ShowEmptyStateAfterError());
            }
        }
    }

    IEnumerator ShowEmptyStateAfterError()
    {
        yield return new WaitForEndOfFrame();
        ShowEmptyMessage();
        targetHeight = minHeight + emptyMessageHeight;
        rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
        ForceLayoutUpdate();
    }

    IEnumerator FinishExpandAfterLoad()
    {
        if (infrastructureContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(infrastructureContainer.GetComponent<RectTransform>());
        }

        yield return null;

        if (spawnedInfrastructures.Count > 0)
        {
            HideEmptyMessage();
            UpdateContentHeight();
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
        }
        else
        {
            ShowEmptyMessage();
            targetHeight = minHeight + emptyMessageHeight;
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
        }
        
        ForceLayoutUpdate();
    }

    void OnInfrastructuresLoadError(string errorMessage)
    {
        Debug.LogError($"AccordionItem: Failed to load infrastructures: {errorMessage}");
        infrastructuresLoaded = true;
        
        if (isExpanded)
        {
            StartCoroutine(ShowEmptyStateAfterError());
        }
    }

    void SpawnInfrastructureItem(Infrastructure infra)
    {
        if (infrastructurePrefab == null || infrastructureContainer == null)
        {
            Debug.LogWarning("AccordionItem: Cannot spawn infrastructure - missing prefab or container");
            return;
        }

        GameObject newItem = Instantiate(infrastructurePrefab, infrastructureContainer);

        ExploreInfrastructureItem itemScript = newItem.GetComponent<ExploreInfrastructureItem>();
        if (itemScript != null)
        {
            itemScript.SetInfrastructureData(infra);
        }

        spawnedInfrastructures.Add(newItem);
    }

    void UpdateContentHeight()
    {
        int itemCount = spawnedInfrastructures.Count;

        if (itemCount == 0)
        {
            targetHeight = minHeight + emptyMessageHeight;
        }
        else
        {
            targetHeight = minHeight + (itemHeight * itemCount) + padding;
        }

        targetHeight = Mathf.Max(targetHeight, minHeight);
    }

    void ShowEmptyMessage()
    {
        if (!showEmptyMessage || contentPanel == null) return;

        HideEmptyMessage();

        GameObject emptyObj = new GameObject("EmptyMessage");
        emptyObj.transform.SetParent(infrastructureContainer != null ? infrastructureContainer : contentPanel, false);

        RectTransform emptyRect = emptyObj.AddComponent<RectTransform>();
        emptyRect.anchorMin = new Vector2(0, 1);
        emptyRect.anchorMax = new Vector2(1, 1);
        emptyRect.pivot = new Vector2(0.5f, 1);
        emptyRect.sizeDelta = new Vector2(0, emptyMessageHeight);
        emptyRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI emptyText = emptyObj.AddComponent<TextMeshProUGUI>();
        emptyText.text = emptyMessageText;
        
        if (emptyMessageFont != null)
            emptyText.font = emptyMessageFont;
        
        emptyText.fontSize = emptyMessageFontSize;
        emptyText.color = emptyMessageColor;
        emptyText.alignment = emptyMessageAlignment;
        emptyText.fontStyle = emptyMessageFontStyle;
        emptyText.enableWordWrapping = true;
        emptyText.margin = new Vector4(
            emptyMessagePaddingLeft, 
            emptyMessagePaddingTop, 
            emptyMessagePaddingRight, 
            emptyMessagePaddingBottom
        );

        emptyMessageObject = emptyObj;
    }

    void HideEmptyMessage()
    {
        if (emptyMessageObject != null)
        {
            Destroy(emptyMessageObject);
            emptyMessageObject = null;
        }
    }

    public void Toggle()
    {
        if (!isExpanded)
        {
            Expand();
        }
        else
        {
            Collapse();
        }
    }

    public void Expand()
    {
        if (isExpanded) return;

        isExpanded = true;
        
        Debug.Log($"Expanding accordion item. InfrastructuresLoaded: {infrastructuresLoaded}, SpawnedCount: {spawnedInfrastructures.Count}");

        if (contentPanel != null)
            contentPanel.gameObject.SetActive(true);

        StopAllCoroutines();

        if (infrastructuresLoaded)
        {
            if (spawnedInfrastructures.Count > 0)
            {
                HideEmptyMessage();
                UpdateContentHeight();
                rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
            }
            else
            {
                ShowEmptyMessage();
                targetHeight = minHeight + emptyMessageHeight;
                rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
            }
            
            ForceLayoutUpdate();
        }
        else
        {
            targetHeight = minHeight + emptyMessageHeight;
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
            
            ForceLayoutUpdate();
            
            StartCoroutine(LoadInfrastructures());
        }
    }

    public void Collapse()
    {
        if (!isExpanded) return;

        isExpanded = false;

        Debug.Log("Collapsing accordion item");

        StopAllCoroutines();

        HideEmptyMessage();

        rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, minHeight);
        
        ForceLayoutUpdate();
        
        if (contentPanel != null)
            contentPanel.gameObject.SetActive(false);
    }

    void ForceLayoutUpdate()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        
        if (transform.parent != null)
        {
            RectTransform parentRect = transform.parent.GetComponent<RectTransform>();
            if (parentRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }
        }
        
        LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
    }

    void OnDestroy()
    {
        foreach (GameObject item in spawnedInfrastructures)
        {
            if (item != null)
                Destroy(item);
        }
        spawnedInfrastructures.Clear();

        if (emptyMessageObject != null)
            Destroy(emptyMessageObject);
    }
}