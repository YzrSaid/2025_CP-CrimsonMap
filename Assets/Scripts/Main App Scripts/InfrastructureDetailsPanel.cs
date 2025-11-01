using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

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

    [Header("Indoor Infrastructure Dropdowns")]
    public TMP_Dropdown roomsDropdown;
    public TMP_Dropdown emergencyExitDropdown;
    public TMP_Dropdown stairsDropdown;
    public GameObject noIndoorText;

    [Header("Background Settings")]
    public string backgroundPanelName = "BackgroundForExplorePanel";

    private Infrastructure infrastructure;
    private Category category;
    private Node node;
    private CampusData campus;
    private Transform backgroundTransform;
    private List<IndoorInfrastructure> indoorList = new List<IndoorInfrastructure>();

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

        // Load indoor infrastructures
        StartCoroutine(LoadIndoorInfrastructures());
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
            Debug.LogWarning("Infrastructure is null");
            yield break;
        }

        // Clear existing dropdown options
        ClearDropdown(roomsDropdown);
        ClearDropdown(emergencyExitDropdown);
        ClearDropdown(stairsDropdown);

        indoorList.Clear();

        bool loadComplete = false;

        // Load indoor.json file
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
                    Debug.Log($"Found {indoorList.Count} indoor items for {infrastructure.name}");
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
        PopulateDropdownsByType();
    }

    private void ClearDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown == null) return;
        dropdown.ClearOptions();
    }

    private void PopulateDropdownsByType()
    {
        if (indoorList.Count == 0)
        {
            // No indoor infrastructures found - show message and hide dropdowns
            if (noIndoorText != null)
            {
                noIndoorText.SetActive(true);
            }

            // Hide all dropdowns
            if (roomsDropdown != null)
                roomsDropdown.gameObject.SetActive(false);
            if (emergencyExitDropdown != null)
                emergencyExitDropdown.gameObject.SetActive(false);
            if (stairsDropdown != null)
                stairsDropdown.gameObject.SetActive(false);

            // Force layout rebuild
            ForceLayoutRebuild();

            Debug.Log($"No indoor infrastructures found for {infrastructure.name}");
            return;
        }

        // Hide no indoor text
        if (noIndoorText != null)
        {
            noIndoorText.SetActive(false);
        }

        // Separate by type
        List<IndoorInfrastructure> rooms = new List<IndoorInfrastructure>();
        List<IndoorInfrastructure> emergencyExits = new List<IndoorInfrastructure>();
        List<IndoorInfrastructure> stairs = new List<IndoorInfrastructure>();

        foreach (IndoorInfrastructure indoor in indoorList)
        {
            string type = indoor.indoor_type?.ToLower();

            switch (type)
            {
                case "room":
                    rooms.Add(indoor);
                    break;
                case "fire_exit":
                    emergencyExits.Add(indoor);
                    break;
                case "stairs":
                    stairs.Add(indoor);
                    break;
                default:
                    Debug.LogWarning($"Unknown indoor type: {indoor.indoor_type} for {indoor.name}");
                    break;
            }
        }

        // Populate each dropdown (also handles showing/hiding based on content)
        PopulateDropdown(roomsDropdown, rooms, "Rooms");
        PopulateDropdown(emergencyExitDropdown, emergencyExits, "Emergency Exits");
        PopulateDropdown(stairsDropdown, stairs, "Stairs");

        // Force layout rebuild after all dropdowns are populated
        ForceLayoutRebuild();

        Debug.Log($"Populated dropdowns - Rooms: {rooms.Count}, Emergency Exits: {emergencyExits.Count}, Stairs: {stairs.Count}");
    }

    private void PopulateDropdown(TMP_Dropdown dropdown, List<IndoorInfrastructure> items, string titleText)
    {
        if (dropdown == null)
        {
            Debug.LogWarning($"Dropdown for {titleText} is not assigned");
            return;
        }

        // If no items, hide the dropdown
        if (items.Count == 0)
        {
            dropdown.gameObject.SetActive(false);
            return;
        }

        // Show the dropdown if it has items
        dropdown.gameObject.SetActive(true);
        dropdown.ClearOptions();

        List<string> options = new List<string>();

        // Add title as first item (will be displayed as label)
        options.Add(titleText);

        // Add all indoor items
        foreach (IndoorInfrastructure indoor in items)
        {
            options.Add(indoor.name);
        }

        dropdown.AddOptions(options);

        // Set dropdown to show title (index 0)
        dropdown.value = 0;
        dropdown.RefreshShownValue();

        // Clear previous listeners to avoid duplicates
        dropdown.onValueChanged.RemoveAllListeners();

        // Make first option (title) non-interactable by listening to value changes
        dropdown.onValueChanged.AddListener((int index) =>
        {
            if (index == 0)
            {
                // User clicked title, reset to title
                dropdown.value = 0;
                dropdown.RefreshShownValue();
            }
            else
            {
                // User selected an actual item
                OnIndoorItemSelected(items[index - 1]); // -1 because first item is title
            }
        });

        Debug.Log($"Added {items.Count} items to {titleText} dropdown");
    }

    private void OnIndoorItemSelected(IndoorInfrastructure selectedIndoor)
    {
        Debug.Log($"Selected indoor item: {selectedIndoor.name} (ID: {selectedIndoor.room_id}, Type: {selectedIndoor.indoor_type})");

        // TODO: Open your panel here with selected indoor data
        // Example: OpenIndoorDetailsPanel(selectedIndoor);
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

    private void ForceLayoutRebuild()
    {
        // Force the layout to rebuild after hiding/showing dropdowns
        Canvas.ForceUpdateCanvases();

        // Rebuild the layout of this panel
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
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