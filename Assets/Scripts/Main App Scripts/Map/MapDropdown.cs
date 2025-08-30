// using UnityEngine;
// using UnityEngine.UI;
// using TMPro;
// using System.Collections.Generic;

// public class MapDropdown : MonoBehaviour
// {
//    [Header("UI References")]
// public Button dropdownButton;
// public GameObject panelForBG;      // semi-black overlay
// public GameObject panel;           // map options panel
// public GameObject mapButtonPrefab;
// public Transform buttonContainer;

// private List<MapData> availableMaps = new List<MapData>();

// void Start()
// {
//     dropdownButton.onClick.AddListener(TogglePanel);
//     PopulatePanel();
//     panel.SetActive(false);
//     panelForBG.SetActive(false);
// }

//     void TogglePanel()
//     {
//         bool isActive = !panel.activeSelf;
//         panel.SetActive(isActive);
//         panelForBG.SetActive(isActive);
//     }
// void PopulatePanel()
// {
//     foreach (Transform child in buttonContainer)
//     {
//         Destroy(child.gameObject); // clear old buttons (optional)
//     }

//     foreach (var map in availableMaps)
//     {
//         GameObject btnObj = Instantiate(mapButtonPrefab, buttonContainer);

//         // Handle TMP or legacy Text
//         TMPro.TextMeshProUGUI tmpText = btnObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
//         if (tmpText != null)
//             tmpText.text = map.map_name;
//         else
//             btnObj.GetComponentInChildren<Text>().text = map.map_name;

//         btnObj.GetComponent<Button>().onClick.AddListener(() =>
//         {
//             SelectMap(map);
//         });
//     }
// }


// void SelectMap(MapData map)
// {
//     Debug.Log("Selected Map: " + map.map_name);

//     // Hide menu + background overlay
//     panel.SetActive(false);
//     panelForBG.SetActive(false);

//     // TODO: Notify MainAppManager to switch maps here
// }

// }


using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using System.Collections;

public class MapDropdown : MonoBehaviour
{
    [Header("UI References")]
    public Button dropdownButton;
    public GameObject panel;
    public GameObject panelForBG;
    public GameObject mapButtonPrefab; // Prefab for each map button
    public Transform buttonContainer; // Parent transform for buttons

    [Header("Manager Reference")]
    public MapManager mapManager; // Reference to the MapManager

    private List<MapData> availableMaps = new List<MapData>();

    void Start()
    {
        dropdownButton.onClick.AddListener(TogglePanel);
        StartCoroutine(WaitForMapManagerData());
        panel.SetActive(false);
        panelForBG.SetActive(false);
    }

    IEnumerator WaitForMapManagerData()
    {
        // Wait until MapManager has loaded its data
        while (mapManager == null || mapManager.GetAvailableMaps().Count == 0)
        {
            yield return new WaitForSeconds(0.1f);
        }

        availableMaps = mapManager.GetAvailableMaps();
        PopulatePanel();

        Debug.Log($"MapDropdown: Loaded {availableMaps.Count} maps for dropdown");
    }
    void TogglePanel()
    {
        bool isActive = !panel.activeSelf;
        panel.SetActive(isActive);
        panelForBG.SetActive(isActive);
    }

    void PopulatePanel()
    {
        // Clear existing buttons
        foreach (Transform child in buttonContainer)
        {
            Destroy(child.gameObject);
        }

        // Create button for each available map
        foreach (var map in availableMaps)
        {
            GameObject btnObj = Instantiate(mapButtonPrefab, buttonContainer);

            // Set button text
            TMPro.TextMeshProUGUI tmpText = btnObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmpText != null)
                tmpText.text = map.map_name;
            else
                btnObj.GetComponentInChildren<Text>().text = map.map_name;

            // Add click listener
            btnObj.GetComponent<Button>().onClick.AddListener(() =>
            {
                SelectMap(map);
            });
        }
    }

    void SelectMap(MapData map)
    {
        Debug.Log("Selected Map: " + map.map_name);

        // Close dropdown
        panel.SetActive(false);
        panelForBG.SetActive(false);

        // Tell MapManager to load the selected map
        if (mapManager != null)
        {
            mapManager.LoadMap(map);
        }
        else
        {
            Debug.LogError("❌ MapManager reference not set in MapDropdown!");
        }
    }
}