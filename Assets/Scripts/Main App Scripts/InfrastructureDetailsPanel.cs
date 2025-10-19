using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InfrastructureDetailsPanel : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI categoryText;
    public Image categoryColorImage;
    public Image infrastructureImage;
    public TextMeshProUGUI emailText;
    public TextMeshProUGUI phoneText;
    public Button closeButton;

    [Header("Background Settings")]
    public string backgroundPanelName = "BackgroundForExplorePanel";

    private Infrastructure infrastructure;
    private Category category;
    private Transform backgroundTransform;

    void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

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
        else
        {
            Debug.Log("Background panel not found!");
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

    public void SetData(Infrastructure infra, Category cat)
    {
        infrastructure = infra;
        category = cat;
        PopulateUI();
    }

    void PopulateUI()
    {
        if (titleText != null)
            titleText.text = infrastructure.name;

        if (category != null)
        {
            if (categoryText != null)
                categoryText.text = category.name;

            if (categoryColorImage != null && !string.IsNullOrEmpty(category.color))
            {
                if (ColorUtility.TryParseHtmlString(category.color, out Color catColor))
                {
                    categoryColorImage.color = catColor;
                }
            }
        }

        if (!string.IsNullOrEmpty(infrastructure.image_url))
        {
            Texture2D texture = Resources.Load<Texture2D>(infrastructure.image_url);
            if (texture != null && infrastructureImage != null)
            {
                infrastructureImage.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);
            }
        }

        if (emailText != null)
            emailText.text = infrastructure.email;

        if (phoneText != null)
            phoneText.text = infrastructure.phone;
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