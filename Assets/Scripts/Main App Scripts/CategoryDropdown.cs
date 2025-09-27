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

    [Header("Loading Check")]
    public float maxWaitTime = 30f; // Max time to wait for data initialization

    void Start()
    {
        panel.SetActive(false);
        StartCoroutine(WaitForDataInitializationThenPopulate());
    }

    // NEW: Wait for MainAppLoader to complete before loading data
    private IEnumerator WaitForDataInitializationThenPopulate()
    {
        Debug.Log("CategoryDropdown: Waiting for data initialization to complete...");
        
        float waitTime = 0f;
        
        // Wait for GlobalManager to exist and data initialization to complete
        while (waitTime < maxWaitTime)
        {
            // Check if GlobalManager exists and data initialization is complete
            if (GlobalManager.Instance != null && IsDataInitializationComplete())
            {
                Debug.Log("CategoryDropdown: Data initialization complete! Starting to populate...");
                yield return StartCoroutine(PopulatePanel());
                yield break;
            }
            
            waitTime += Time.deltaTime;
            yield return new WaitForSeconds(0.1f); // Check every 100ms
        }
        
        // Timeout - still try to load but log warning
        Debug.LogWarning("CategoryDropdown: Timed out waiting for data initialization. Attempting to load anyway...");
        yield return StartCoroutine(PopulatePanel());
    }

    // Check if data initialization is complete
    private bool IsDataInitializationComplete()
    {
        // Get the correct file path based on platform
        string filePath = GetJsonFilePath("categories.json");
        
        if (!File.Exists(filePath))
        {
            return false;
        }
        
        try
        {
            string content = File.ReadAllText(filePath);
            if (string.IsNullOrEmpty(content) || content.Length < 10) // Basic validity check
            {
                return false;
            }
        }
        catch
        {
            return false;
        }
        
        return true; // File exists and has content
    }

    // Helper method to get correct file path based on platform
    private string GetJsonFilePath(string fileName)
    {
#if UNITY_EDITOR
        // In Unity Editor, check StreamingAssets first, then persistent data
        string streamingPath = Path.Combine(Application.streamingAssetsPath, fileName);
        if (File.Exists(streamingPath))
        {
            return streamingPath;
        }
#endif
        // On device/runtime, use persistent data path
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    IEnumerator PopulatePanel()
    {
        bool dataLoaded = false;
        CategoryList categoryList = null;
        string errorMessage = "";

        // Use the CrossPlatformFileLoader to load the JSON
        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile("categories.json", 
            (jsonContent) => {
                try
                {
                    // wrap it manually
                    categoryList = JsonUtility.FromJson<CategoryList>("{\"categories\":" + jsonContent + "}");
                    dataLoaded = true;
                }
                catch (System.Exception e)
                {
                    errorMessage = $"Error parsing JSON: {e.Message}";
                }
            },
            (error) => {
                errorMessage = error;
            }));

        // Check if data was loaded successfully
        if (!dataLoaded)
        {
            Debug.LogError($"CategoryDropdown: Failed to load categories.json - {errorMessage}");
            yield break;
        }

        if (categoryList == null || categoryList.categories == null)
        {
            Debug.LogError("CategoryDropdown: categoryList or categories array is null!");
            yield break;
        }

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
        
        Debug.Log($"✅ CategoryDropdown: Successfully populated {categoryList.categories.Count} categories!");
    }
}