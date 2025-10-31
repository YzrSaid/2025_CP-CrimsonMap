using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

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

    [Header("Background Settings")]
    public string backgroundPanelName = "BackgroundForExplorePanel";

    private Infrastructure infrastructure;
    private Category category;
    private Node node;
    private CampusData campus;
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