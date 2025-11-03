using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine.InputSystem;
using Mapbox.Unity.Map;
using Mapbox.Utils;

public class MapManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI dropdownButtonText;

    [Header("Mapbox Reference")]
    public AbstractMap mapboxMap;

    [Header("Spawner References")]
    public BarrierSpawner barrierSpawner;
    public PathfindingController pathfindingController;
    public InfrastructureSpawner infrastructureSpawner;
    public PathRenderer pathRenderer;

    [Header("Current Map Info")]
    public MapInfo currentMap;
    public List<string> currentCampusIds = new List<string>();

    private List<MapInfo> availableMaps = new List<MapInfo>();
    private Dictionary<string, CampusData> allCampuses = new Dictionary<string, CampusData>();
    private bool isInitialized = false;

    public System.Action<MapInfo> OnMapChanged;
    public System.Action OnMapLoadingComplete;
    public System.Action OnMapLoadingStarted;

    public static MapManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (mapboxMap == null)
        {
            mapboxMap = FindObjectOfType<AbstractMap>();
        }
    }

    void Start()
    {
        StartCoroutine(InitializeMapManager());
    }

    IEnumerator InitializeMapManager()
    {
        while (FirestoreManager.Instance == null || !FirestoreManager.Instance.IsReady)
        {
            yield return new WaitForSeconds(0.1f);
        }

        while (FirestoreManager.Instance.AvailableMaps.Count == 0)
        {
            yield return new WaitForSeconds(0.1f);
        }

        availableMaps = FirestoreManager.Instance.AvailableMaps;

        yield return StartCoroutine(LoadCampusData());

        isInitialized = true;

        LoadLastSelectedMap();
    }

    private void LoadLastSelectedMap()
    {
        string savedMapId = PlayerPrefs.GetString("ARScene_MapId", "");

        if (!string.IsNullOrEmpty(savedMapId))
        {
            MapInfo savedMap = availableMaps.Find(m => m.map_id == savedMapId);

            if (savedMap != null)
            {
                InitializeMapboxWithCenter(savedMap);
                LoadMap(savedMap);
                return;
            }
        }

        if (availableMaps.Count > 0)
        {
            InitializeMapboxWithCenter(availableMaps[0]);
            LoadMap(availableMaps[0]);
        }
    }

    private void InitializeMapboxWithCenter(MapInfo mapInfo)
    {
        if (mapboxMap != null)
        {
            Vector2d newCenter = new Vector2d(mapInfo.center_lat, mapInfo.center_lng);
            mapboxMap.Initialize(newCenter, (int)mapboxMap.Zoom);
        }
    }

    IEnumerator LoadCampusData()
    {
        bool loadCompleted = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile("campus.json",
        (jsonContent) =>
        {
            try
            {
                CampusList campusList = JsonUtility.FromJson<CampusList>("{\"campuses\":" + jsonContent + "}");
                foreach (var campus in campusList.campuses)
                {
                    allCampuses[campus.campus_id] = campus;
                }
                loadCompleted = true;
            }
            catch (System.Exception)
            {
                loadCompleted = true;
            }
        },
        (error) =>
        {
            loadCompleted = true;
        }
        ));

        yield return new WaitUntil(() => loadCompleted);
    }

    public void LoadMap(MapInfo mapInfo)
    {
        if (!isInitialized)
        {
            return;
        }

        if (mapInfo == null)
        {
            return;
        }

        currentMap = mapInfo;
        currentCampusIds.Clear();
        currentCampusIds.AddRange(mapInfo.campus_included);

        UpdateDropdownButtonText(mapInfo.map_name);
        SaveCurrentMapToPlayerPrefs();

        StartCoroutine(LoadMapCoroutine());
    }

    private void UpdateMapboxCenter(MapInfo mapInfo)
    {
        if (mapboxMap != null)
        {
            Vector2d newCenter = new Vector2d(mapInfo.center_lat, mapInfo.center_lng);
            mapboxMap.UpdateMap(newCenter);
        }
    }

    private void SaveCurrentMapToPlayerPrefs()
    {
        if (currentMap != null)
        {
            PlayerPrefs.SetString("ARScene_MapId", currentMap.map_id);
            PlayerPrefs.SetString("ARScene_MapName", currentMap.map_name);
            PlayerPrefs.SetFloat("ARScene_CenterLat", (float)currentMap.center_lat);
            PlayerPrefs.SetFloat("ARScene_CenterLng", (float)currentMap.center_lng);

            string campusIdsJson = string.Join(",", currentCampusIds);
            PlayerPrefs.SetString("ARScene_CampusIds", campusIdsJson);

            PlayerPrefs.Save();
        }
    }

    IEnumerator LoadMapCoroutine()
    {
        OnMapLoadingStarted?.Invoke();
        OnMapChanged?.Invoke(currentMap);

        UpdateMapboxCenter(currentMap);

        yield return StartCoroutine(ClearAllSpawnedObjects());

        UpdateSpawnersForCurrentMap();

        if (pathRenderer != null)
        {
            yield return StartCoroutine(pathRenderer.LoadAndRenderPathsForMap(currentMap.map_id, currentCampusIds));
        }

        if (barrierSpawner != null)
        {
            yield return StartCoroutine(barrierSpawner.LoadAndSpawnForMap(currentMap.map_id, currentCampusIds));
        }

        if (infrastructureSpawner != null)
        {
            yield return StartCoroutine(infrastructureSpawner.LoadAndSpawnForCampuses(currentCampusIds));
        }

        if (pathfindingController != null)
        {
            yield return StartCoroutine(pathfindingController.InitializeForMap(currentMap.map_id, currentCampusIds));
        }

        OnMapLoadingComplete?.Invoke();
    }

    void UpdateSpawnersForCurrentMap()
    {
        if (pathRenderer != null)
        {
            pathRenderer.SetCurrentMapData(currentMap.map_id, currentCampusIds);
        }

        if (barrierSpawner != null)
        {
            barrierSpawner.SetCurrentMapData(currentMap.map_id, currentCampusIds);
        }

        if (infrastructureSpawner != null)
        {
            infrastructureSpawner.SetTargetCampusIds(currentCampusIds);
        }
    }

    void UpdateDropdownButtonText(string mapName)
    {
        if (dropdownButtonText != null)
        {
            dropdownButtonText.text = mapName;
        }
    }

    IEnumerator ClearAllSpawnedObjects()
    {
        if (pathRenderer != null)
        {
            pathRenderer.ClearSpawnedPaths();
            yield return null;
        }

        if (barrierSpawner != null)
        {
            barrierSpawner.ClearSpawnedNodes();
            yield return null;
        }

        if (infrastructureSpawner != null)
        {
            infrastructureSpawner.ClearSpawnedInfrastructure();
            yield return null;
        }

        yield break;
    }

    public List<MapInfo> GetAvailableMaps()
    {
        return availableMaps;
    }

    public MapInfo GetCurrentMap()
    {
        return currentMap;
    }

    public List<string> GetCurrentCampusIds()
    {
        return new List<string>(currentCampusIds);
    }

    public string GetCampusName(string campusId)
    {
        return allCampuses.ContainsKey(campusId) ? allCampuses[campusId].campus_name : campusId;
    }

    public string GetCurrentMapInfo()
    {
        if (currentMap == null) return "No map loaded";

        string campusNames = string.Join(", ", currentCampusIds.Select(id => GetCampusName(id)));
        return $"Map: {currentMap.map_name} | Campuses: {campusNames} | Campus IDs: {string.Join(", ", currentCampusIds)}";
    }

    public void LoadMapById(string mapId)
    {
        MapInfo targetMap = availableMaps.Find(m => m.map_id == mapId);
        if (targetMap != null)
        {
            LoadMap(targetMap);
        }
    }

    public bool IsReady()
    {
        return isInitialized && availableMaps.Count > 0;
    }

    public void RefreshCurrentMap()
    {
        if (currentMap != null && isInitialized)
        {
            LoadMap(currentMap);
        }
    }

    public string GetNodesFileNameForMap(string mapId)
    {
        return $"nodes_{mapId}.json";
    }

    public string GetEdgesFileNameForMap(string mapId)
    {
        return $"edges_{mapId}.json";
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}