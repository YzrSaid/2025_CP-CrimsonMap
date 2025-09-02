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
    private List<Button> spawnedItems = new List<Button>();

    void Start()
    {
        panel.SetActive(false);
        PopulatePanel();
    }

    void PopulatePanel()
    {
        // load data
        string path = Path.Combine(Application.streamingAssetsPath, "categories.json");

        // Check if the file exists or not
        if (!File.Exists(path))
        {
            Debug.LogError("categories.json is not found in StreamingAssets!");
            return;
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
            
            // DEBUG: Show all Image components in the prefab
            Debug.Log($"=== DEBUG: All Image components in {category.name} prefab ===");
            Image[] allImages = item.GetComponentsInChildren<Image>(true);
            foreach (var img in allImages)
            {
                Debug.Log($"   - Image: '{img.name}' on GameObject: '{img.gameObject.name}'");
            }
            
            // FIXED: Look for the specific icon image by name, not just any Image
            Image iconImage = null;
            foreach (var img in item.GetComponentsInChildren<Image>(true))
            {
                // Try common names for icon images
                if (img.name == "Image_Icon" || img.name == "Icon" || img.name == "Image" || 
                    img.gameObject.name.Contains("Icon") || img.gameObject.name.Contains("icon"))
                {
                    iconImage = img;
                    Debug.Log($"✅ Found icon image: '{img.name}' on '{img.gameObject.name}'");
                    break;
                }
            }
            
            // If still not found, use the second Image (skip the button background)
            if (iconImage == null && allImages.Length > 1)
            {
                iconImage = allImages[1]; // Use second image (first is usually button background)
                Debug.Log($"⚠️ Using fallback: second Image component '{iconImage.name}' on '{iconImage.gameObject.name}'");
            }

            if (label != null) label.text = category.name;

            if (iconImage != null && !string.IsNullOrEmpty(category.icon))
            {
                // Remove extension if present
                string fileName = Path.GetFileNameWithoutExtension(category.icon);

                Debug.Log($"🔍 CategoryDropdown: Trying to load icon '{fileName}' for category '{category.name}'");

                // Load from Resources/Images/icons/
                Sprite sprite = Resources.Load<Sprite>($"Images/icons/{fileName}");

                if (sprite != null)
                {
                    iconImage.sprite = sprite;
                    Debug.Log($"✅ CategoryDropdown: Successfully loaded icon for {category.name}");
                }
                else
                {
                    Debug.LogWarning($"⚠️ CategoryDropdown: Icon not found in Resources/Images/icons/: {fileName}");
                    
                    // Try alternative paths
                    string[] tryPaths = {
                        fileName, // Just the filename
                        $"icons/{fileName}", // Different folder structure
                        category.icon.Replace(".png", "") // Full path without extension
                    };
                    
                    Debug.Log("🔍 Trying alternative paths:");
                    foreach (string tryPath in tryPaths)
                    {
                        Sprite testSprite = Resources.Load<Sprite>(tryPath);
                        Debug.Log($"   {(testSprite != null ? "✅" : "❌")} '{tryPath}'");
                        if (testSprite != null)
                        {
                            iconImage.sprite = testSprite;
                            Debug.Log($"✅ Success with path: {tryPath}");
                            break;
                        }
                    }
                }
            }
            else if (iconImage == null)
            {
                Debug.LogWarning($"⚠️ No Image component named 'Image_Icon' found in category item prefab for {category.name}");
            }
        }
    }
}