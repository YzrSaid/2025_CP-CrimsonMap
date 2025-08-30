using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using System.Collections;
using System.IO;

public class CategoryDropdown : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panel;
    public Button categoryButton;
    public Transform panelContent;

    [Header("CategoryItem Prefab")]
    public Button categoryItemPrefab;

    // Since in fresh start of the app the panel is close
    private bool isOpen = false;
    private List<Button> spawnedItems = new List<Button>();

    void Start()
    {
        categoryButton.onClick.AddListener(TogglePanel);
        panel.SetActive(false);
    }

    void TogglePanel()
    {
        if (!isOpen)
        {
            panel.SetActive(true);
        }
        else
        {
            // If clicked again, close it
            panel.SetActive(false);
        }
    }

    void PopulatePanel()
    {
        // load data
        string path = Path.Combine(Application.streamingAssetsPath, "categories.json");

        // Check if the file exists or not
        if (!File.Exists(path))
        {
            Debug.LogError("categories.json is not found in StreamingAssets!");
        }

        string json = File.ReadAllText(path);

        // wrap it manually
        CategoryList categoryList = JsonUtility.FromJson<CategoryList>("{\"categories\":" + json + "}");

        // instantiate it and generate prefabs
        foreach (Category category in categoryList.categories)
        {
            Button item = Instantiate(categoryItemPrefab, panelContent);
            spawnedItems.Add(item);

            // assign UI
            TMP_Text label = item.GetComponentInChildren<TMP_Text>();
            Image iconImage = item.GetComponentInChildren<Image>();

            if (label != null) label.text = category.name;

            if (iconImage != null)
            {
                // load sprite from StreamingAssets
                string iconPath = Path.Combine(Application.streamingAssetsPath, category.icon);
                if (File.Exists(iconPath))
                {
                    byte[] pngData = File.ReadAllBytes(iconPath);
                    Texture2D tex = new Texture2D(2, 2);
                    tex.LoadImage(pngData);
                    Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    iconImage.sprite = sprite;
                }
                else
                {
                    Debug.LogWarning($"Icon not found: {iconPath}");
                }
            }

        }
    }
}