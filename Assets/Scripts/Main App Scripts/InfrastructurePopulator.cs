using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InfrastructurePopulator : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Dropdown dropdownTo; // Legacy dropdown (still works)
    public ScrollRect destinationScrollView; // NEW: For accordion UI
    public Transform destinationListContent; // NEW: Container for buttons

    [Header("Data")]
    public InfrastructureList infrastructureList;
    public IndoorInfrastructureList indoorList;

    [Header("Settings")]
    public bool useAccordionUI = false; // Toggle: false = dropdown, true = accordion
    public float maxWaitTime = 30f;

    private Dictionary<string, List<IndoorInfrastructure>> infraToRoomsMap = new Dictionary<string, List<IndoorInfrastructure>>();
    private Dictionary<string, GameObject> accordionInstances = new Dictionary<string, GameObject>();
    private string selectedDestinationId = null;
    private string selectedDestinationType = null;

    void Start()
    {
        StartCoroutine(WaitForDataInitializationThenLoad());
    }

    private IEnumerator WaitForDataInitializationThenLoad()
    {
        float waitTime = 0f;
        while (waitTime < maxWaitTime)
        {
            if (GlobalManager.Instance != null && IsDataInitializationComplete())
            {
                yield return StartCoroutine(LoadAllData());
                yield break;
            }

            waitTime += Time.deltaTime;
            yield return new WaitForSeconds(0.1f);
        }

        yield return StartCoroutine(LoadAllData());
    }

    private bool IsDataInitializationComplete()
    {
        string infraPath = GetJsonFilePath("infrastructure.json");
        
        if (!File.Exists(infraPath))
        {
            return false;
        }

        try
        {
            string infraContent = File.ReadAllText(infraPath);
            
            if (string.IsNullOrEmpty(infraContent) || infraContent.Length < 10)
            {
                return false;
            }
        }
        catch
        {
            return false;
        }

        // Indoor.json is optional (some maps might not have indoor data)
        return true;
    }

    private string GetJsonFilePath(string fileName)
    {
#if UNITY_EDITOR
        string streamingPath = Path.Combine(Application.streamingAssetsPath, fileName);
        if (File.Exists(streamingPath))
        {
            return streamingPath;
        }
#endif
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    // ✅ Load both infrastructure and indoor data
    private IEnumerator LoadAllData()
    {
        bool infraLoaded = false;
        bool indoorLoaded = false;

        // Load infrastructure.json
        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "infrastructure.json",
            (jsonContent) => {
                OnInfrastructureDataLoaded(jsonContent);
                infraLoaded = true;
            },
            (error) => {
                Debug.LogError($"Failed to load infrastructure.json: {error}");
                infraLoaded = true;
            }
        ));

        yield return new WaitUntil(() => infraLoaded);

        // Load indoor.json (optional)
        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "indoor.json",
            (jsonContent) => {
                OnIndoorDataLoaded(jsonContent);
                indoorLoaded = true;
            },
            (error) => {
                Debug.LogWarning($"indoor.json not found (optional): {error}");
                indoorLoaded = true;
            }
        ));

        yield return new WaitUntil(() => indoorLoaded);

        // Build the mapping
        BuildInfraToRoomsMapping();

        // Populate UI
        if (useAccordionUI && destinationScrollView != null && destinationListContent != null)
        {
            PopulateAccordionUI();
        }
        else if (dropdownTo != null)
        {
            PopulateDropdown(dropdownTo);
        }
    }

    private void OnInfrastructureDataLoaded(string jsonContent)
    {
        try
        {
            string wrappedJson = "{\"infrastructures\":" + jsonContent + "}";
            infrastructureList = JsonUtility.FromJson<InfrastructureList>(wrappedJson);
            Debug.Log($"[InfrastructurePopulator] ✅ Loaded {infrastructureList.infrastructures.Length} infrastructures");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing infrastructure JSON: {e.Message}");
        }
    }

    private void OnIndoorDataLoaded(string jsonContent)
    {
        try
        {
            IndoorInfrastructure[] indoorArray = JsonHelper.FromJson<IndoorInfrastructure>(jsonContent);
            
            indoorList = new IndoorInfrastructureList { indoors = indoorArray };
            Debug.Log($"[InfrastructurePopulator] ✅ Loaded {indoorArray.Length} indoor infrastructures");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing indoor JSON: {e.Message}");
        }
    }

    // ✅ Build a mapping of infra_id -> list of rooms
    private void BuildInfraToRoomsMapping()
    {
        infraToRoomsMap.Clear();

        if (indoorList == null || indoorList.indoors == null)
        {
            Debug.Log("[InfrastructurePopulator] No indoor data available");
            return;
        }

        foreach (var indoor in indoorList.indoors)
        {
            if (indoor.is_deleted)
                continue;

            if (!infraToRoomsMap.ContainsKey(indoor.infra_id))
            {
                infraToRoomsMap[indoor.infra_id] = new List<IndoorInfrastructure>();
            }

            infraToRoomsMap[indoor.infra_id].Add(indoor);
        }

        Debug.Log($"[InfrastructurePopulator] ✅ Mapped {infraToRoomsMap.Count} infrastructures with indoor rooms");
    }

    // ✅ ACCORDION UI - NO PREFABS NEEDED!
    private void PopulateAccordionUI()
    {
        if (destinationListContent == null)
        {
            Debug.LogError("[InfrastructurePopulator] destinationListContent is null");
            return;
        }

        // Clear existing
        foreach (Transform child in destinationListContent)
        {
            Destroy(child.gameObject);
        }
        accordionInstances.Clear();

        if (infrastructureList == null || infrastructureList.infrastructures.Length == 0)
        {
            return;
        }

        foreach (var infra in infrastructureList.infrastructures)
        {
            bool hasRooms = infraToRoomsMap.ContainsKey(infra.infra_id) && 
                           infraToRoomsMap[infra.infra_id].Count > 0;

            // Create main infrastructure button (programmatically)
            GameObject infraButton = CreateInfrastructureButton(infra.name, hasRooms);
            infraButton.transform.SetParent(destinationListContent, false);

            Button btn = infraButton.GetComponent<Button>();
            GameObject arrowIcon = infraButton.transform.Find("Arrow")?.gameObject;

            if (hasRooms)
            {
                // Create container for rooms
                GameObject roomsContainer = new GameObject("Rooms_" + infra.infra_id);
                roomsContainer.transform.SetParent(destinationListContent, false);
                
                RectTransform containerRect = roomsContainer.AddComponent<RectTransform>();
                containerRect.anchorMin = new Vector2(0, 1);
                containerRect.anchorMax = new Vector2(1, 1);
                containerRect.pivot = new Vector2(0.5f, 1);
                containerRect.sizeDelta = new Vector2(0, 0); // Auto-size

                VerticalLayoutGroup layout = roomsContainer.AddComponent<VerticalLayoutGroup>();
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                layout.spacing = 2f;
                layout.padding = new RectOffset(30, 0, 0, 0); // Indent rooms

                ContentSizeFitter fitter = roomsContainer.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                roomsContainer.SetActive(false); // Hidden by default

                // Create room buttons
                foreach (var room in infraToRoomsMap[infra.infra_id])
                {
                    GameObject roomButton = CreateRoomButton(room.name);
                    roomButton.transform.SetParent(roomsContainer.transform, false);

                    Button roomBtn = roomButton.GetComponent<Button>();
                    string roomId = room.room_id;
                    roomBtn.onClick.AddListener(() => OnDestinationSelected(roomId, "indoorinfra", room.name));
                }

                accordionInstances[infra.infra_id] = roomsContainer;

                // Toggle accordion on click
                string infraId = infra.infra_id;
                btn.onClick.AddListener(() => ToggleAccordion(infraId, arrowIcon));
            }
            else
            {
                // No rooms - select infrastructure directly
                string infraId = infra.infra_id;
                btn.onClick.AddListener(() => OnDestinationSelected(infraId, "infrastructure", infra.name));
            }
        }

        // Force layout rebuild
        Canvas.ForceUpdateCanvases();
    }

    // ✅ Create infrastructure button programmatically (no prefab)
    private GameObject CreateInfrastructureButton(string text, bool hasArrow)
    {
        GameObject buttonObj = new GameObject("Infra_" + text);
        
        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.sizeDelta = new Vector2(0, 50);

        // Background image
        Image bgImage = buttonObj.AddComponent<Image>();
        bgImage.color = new Color(1f, 1f, 1f, 1f);

        // Button component
        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = bgImage;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 1f);
        colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        button.colors = colors;

        // Horizontal layout
        HorizontalLayoutGroup layout = buttonObj.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.padding = new RectOffset(15, 15, 10, 10);
        layout.spacing = 10;
        layout.childControlWidth = false;
        layout.childControlHeight = true;

        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        
        TextMeshProUGUI textComp = textObj.AddComponent<TextMeshProUGUI>();
        textComp.text = text;
        textComp.fontSize = 16;
        textComp.color = Color.black;
        textComp.alignment = TextAlignmentOptions.MidlineLeft;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(300, 30);

        LayoutElement textLayout = textObj.AddComponent<LayoutElement>();
        textLayout.flexibleWidth = 1;
        textLayout.preferredHeight = 30;

        // Arrow icon (if has rooms)
        if (hasArrow)
        {
            GameObject arrowObj = new GameObject("Arrow");
            arrowObj.transform.SetParent(buttonObj.transform, false);
            
            TextMeshProUGUI arrowText = arrowObj.AddComponent<TextMeshProUGUI>();
            arrowText.text = "▶"; // Right arrow
            arrowText.fontSize = 14;
            arrowText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            arrowText.alignment = TextAlignmentOptions.Center;
            
            RectTransform arrowRect = arrowObj.GetComponent<RectTransform>();
            arrowRect.sizeDelta = new Vector2(20, 20);

            LayoutElement arrowLayout = arrowObj.AddComponent<LayoutElement>();
            arrowLayout.minWidth = 20;
            arrowLayout.preferredWidth = 20;
            arrowLayout.preferredHeight = 20;
        }

        return buttonObj;
    }

    // ✅ Create room button programmatically (no prefab)
    private GameObject CreateRoomButton(string text)
    {
        GameObject buttonObj = new GameObject("Room_" + text);
        
        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.sizeDelta = new Vector2(0, 45);

        // Background image
        Image bgImage = buttonObj.AddComponent<Image>();
        bgImage.color = new Color(0.95f, 0.95f, 0.95f, 1f);

        // Button component
        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = bgImage;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        button.colors = colors;

        // Horizontal layout
        HorizontalLayoutGroup layout = buttonObj.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.padding = new RectOffset(15, 15, 10, 10);
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        
        TextMeshProUGUI textComp = textObj.AddComponent<TextMeshProUGUI>();
        textComp.text = text;
        textComp.fontSize = 14;
        textComp.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        textComp.alignment = TextAlignmentOptions.MidlineLeft;
        
        LayoutElement textLayout = textObj.AddComponent<LayoutElement>();
        textLayout.flexibleWidth = 1;
        textLayout.preferredHeight = 25;

        return buttonObj;
    }

    private void ToggleAccordion(string infraId, GameObject arrowIcon)
    {
        if (!accordionInstances.ContainsKey(infraId))
            return;

        GameObject roomsContainer = accordionInstances[infraId];
        bool isOpen = roomsContainer.activeSelf;

        // Close all other accordions
        foreach (var kvp in accordionInstances)
        {
            kvp.Value.SetActive(false);
        }

        // Toggle this one
        roomsContainer.SetActive(!isOpen);

        // Rotate arrow
        if (arrowIcon != null)
        {
            TextMeshProUGUI arrowText = arrowIcon.GetComponent<TextMeshProUGUI>();
            if (arrowText != null)
            {
                arrowText.text = roomsContainer.activeSelf ? "▼" : "▶";
            }
        }

        // Force layout rebuild
        Canvas.ForceUpdateCanvases();
        if (destinationScrollView != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(destinationListContent as RectTransform);
        }
    }

    private void OnDestinationSelected(string id, string type, string displayName)
    {
        selectedDestinationId = id;
        selectedDestinationType = type;

        Debug.Log($"[InfrastructurePopulator] ✅ Selected: {displayName} (ID: {id}, Type: {type})");

        // Notify PathfindingController
        PathfindingController pathfinding = FindObjectOfType<PathfindingController>();
        if (pathfinding != null)
        {
            pathfinding.SetDestination(id, type);
        }
    }

    // ✅ DROPDOWN UI (Original - still works)
    private void PopulateDropdown(TMP_Dropdown dropdown)
    {
        dropdown.ClearOptions();

        if (infrastructureList == null || infrastructureList.infrastructures.Length == 0)
        {
            return;
        }

        List<string> options = new List<string>();
        
        foreach (var infra in infrastructureList.infrastructures)
        {
            options.Add(infra.name);

            // Add rooms under this infrastructure (indented)
            if (infraToRoomsMap.ContainsKey(infra.infra_id))
            {
                foreach (var room in infraToRoomsMap[infra.infra_id])
                {
                    options.Add("    → " + room.name);
                }
            }
        }

        dropdown.AddOptions(options);
    }

    // ✅ Get selected infrastructure (for backward compatibility)
    public Infrastructure GetSelectedInfrastructure(TMP_Dropdown dropdown)
    {
        int index = dropdown.value;
        if (index >= 0 && index < infrastructureList.infrastructures.Length)
        {
            return infrastructureList.infrastructures[index];
        }
        return null;
    }

    // ✅ NEW: Get selected destination from dropdown (supports both infra and indoor)
    public (string id, string type) GetSelectedDestinationFromDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown == null || infrastructureList == null)
        {
            return (null, null);
        }

        int selectedIndex = dropdown.value;
        string selectedText = dropdown.options[selectedIndex].text;

        Debug.Log($"[InfrastructurePopulator] Dropdown index {selectedIndex}: '{selectedText}'");

        // Check if it's an indoor room (has the "    → " prefix)
        if (selectedText.Contains("→"))
        {
            // It's an indoor room
            string roomName = selectedText.Replace("    → ", "").Trim();
            
            // Find the room by name
            if (indoorList != null && indoorList.indoors != null)
            {
                foreach (var indoor in indoorList.indoors)
                {
                    if (!indoor.is_deleted && indoor.name == roomName)
                    {
                        Debug.Log($"[InfrastructurePopulator] Found indoor room: {indoor.name} (ID: {indoor.room_id})");
                        return (indoor.room_id, "indoorinfra");
                    }
                }
            }
            
            Debug.LogWarning($"[InfrastructurePopulator] Indoor room not found: {roomName}");
            return (null, null);
        }
        else
        {
            // It's an infrastructure
            foreach (var infra in infrastructureList.infrastructures)
            {
                if (infra.name == selectedText)
                {
                    Debug.Log($"[InfrastructurePopulator] Found infrastructure: {infra.name} (ID: {infra.infra_id})");
                    return (infra.infra_id, "infrastructure");
                }
            }
            
            Debug.LogWarning($"[InfrastructurePopulator] Infrastructure not found: {selectedText}");
            return (null, null);
        }
    }

    public (string id, string type) GetSelectedDestination()
    {
        return (selectedDestinationId, selectedDestinationType);
    }
}

// ✅ Data structure for indoor list
[System.Serializable]
public class IndoorInfrastructureList
{
    public IndoorInfrastructure[] indoors;
}