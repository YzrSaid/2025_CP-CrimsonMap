using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using System.Collections;
using Mapbox.Unity.Map;
using Mapbox.Utils;

public class MapDropdown : MonoBehaviour
{
    [Header("UI References")]
    public Button dropdownButton;
    public GameObject panel; 
    public GameObject panelForBG;
    public GameObject mapButtonPrefab;
    public Transform buttonContainer;
    
    [Header("Mapbox Reference")]
    public AbstractMap mapboxMap;
    
    private List<MapInfo> availableMaps = new List<MapInfo>();
    private bool isDataLoaded = false;

    void Start()
    {
        if (mapboxMap == null)
        {
            mapboxMap = FindObjectOfType<AbstractMap>();
        }
        
        dropdownButton.onClick.AddListener(TogglePanel);
        StartCoroutine(WaitForMapManagerData());
        panel.SetActive(false);
        panelForBG.SetActive(false);
    }

    IEnumerator WaitForMapManagerData()
    {
        while (MapManager.Instance == null || !MapManager.Instance.IsReady())
        {
            yield return new WaitForSeconds(0.1f);
        }

        availableMaps = MapManager.Instance.GetAvailableMaps();
        isDataLoaded = true;
        PopulatePanel();
    }

    void TogglePanel()
    {
        if (!isDataLoaded)
        {
            return;
        }

        bool isActive = !panel.activeSelf;
        panel.SetActive(isActive);
        panelForBG.SetActive(isActive);
    }

    void PopulatePanel()
    {
        foreach (Transform child in buttonContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (var map in availableMaps)
        {
            GameObject btnObj = Instantiate(mapButtonPrefab, buttonContainer);

            TMPro.TextMeshProUGUI tmpText = btnObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmpText != null)
            {
                tmpText.text = map.map_name;
            }
            else
            {
                Text regularText = btnObj.GetComponentInChildren<Text>();
                if (regularText != null)
                    regularText.text = map.map_name;
            }

            Button button = btnObj.GetComponent<Button>();
            if (button != null)
            {
                MapInfo selectedMap = map;
                button.onClick.AddListener(() => SelectMap(selectedMap));
            }
        }
    }

    void SelectMap(MapInfo map)
    {
        panel.SetActive(false);
        panelForBG.SetActive(false);

        if (MapManager.Instance != null && MapManager.Instance.IsReady())
        {
            MapManager.Instance.LoadMap(map);
            UpdateMapboxCenter(map);
        }
    }

    void UpdateMapboxCenter(MapInfo map)
    {
        if (mapboxMap != null)
        {
            Vector2d newCenter = new Vector2d(map.center_lat, map.center_lng);
            mapboxMap.SetCenterLatitudeLongitude(newCenter);
            mapboxMap.UpdateMap();
        }
    }

    public MapInfo GetCurrentlySelectedMap()
    {
        if (MapManager.Instance != null)
        {
            return MapManager.Instance.GetCurrentMap();
        }
        return null;
    }

    public void RefreshMapList()
    {
        if (MapManager.Instance != null && MapManager.Instance.IsReady())
        {
            availableMaps = MapManager.Instance.GetAvailableMaps();
            PopulatePanel();
        }
    }

    public void SelectMapById(string mapId)
    {
        MapInfo targetMap = availableMaps.Find(m => m.map_id == mapId);
        if (targetMap != null)
        {
            SelectMap(targetMap);
        }
    }

    public void SelectDefaultMap()
    {
        if (availableMaps.Count > 0)
        {
            SelectMap(availableMaps[0]);
        }
    }

    void OnDestroy()
    {
        if (dropdownButton != null)
            dropdownButton.onClick.RemoveAllListeners();
    }
}