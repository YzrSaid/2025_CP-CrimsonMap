using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class IndoorMapManager : MonoBehaviour
{
    [Header("Indoor Map UI")]
    public RectTransform indoorMapContainer;
    public RawImage indoorMapBackground;
    public Transform markersContainer;

    [Header("Marker Prefabs")]
    public GameObject roomMarkerPrefab;
    public GameObject stairsMarkerPrefab;
    public GameObject fireExitMarkerPrefab;
    public GameObject entranceMarkerPrefab;

    [Header("Map Settings")]
    public float mapWidth = 1000f;
    public float mapHeight = 1000f;
    public float pixelsPerMeter = 20f;
    public Color backgroundColor = Color.white;

    [Header("Pan & Zoom Settings")]
    public float minZoom = 0.5f;
    public float maxZoom = 3f;
    public float zoomSpeed = 0.1f;

    private string currentInfraId;
    private Node currentInfraNode;
    private int currentFloor = 1;
    private List<int> availableFloors = new List<int>();

    private Dictionary<string, GameObject> spawnedMarkers = new Dictionary<string, GameObject>();
    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>();
    private Dictionary<string, IndoorInfrastructure> indoorInfrastructures = new Dictionary<string, IndoorInfrastructure>();

    private Vector2 dragStartPos;
    private Vector2 mapStartPos;
    private bool isDragging = false;

    void Start()
    {
        if (indoorMapBackground != null)
        {
            Texture2D whiteTex = new Texture2D(1, 1);
            whiteTex.SetPixel(0, 0, backgroundColor);
            whiteTex.Apply();
            indoorMapBackground.texture = whiteTex;
        }
    }

    public void LoadIndoorMap(string infraId, Node infraNode)
    {
        currentInfraId = infraId;
        currentInfraNode = infraNode;

        ClearAllMarkers();

        StartCoroutine(LoadIndoorMapData());
    }

    private IEnumerator LoadIndoorMapData()
    {
        if (MapManager.Instance != null && MapManager.Instance.GetCurrentMap() != null)
        {
            string mapId = MapManager.Instance.GetCurrentMap().map_id;
            yield return StartCoroutine(LoadNodes(mapId));
        }

        yield return StartCoroutine(LoadIndoorData());

        DetermineAvailableFloors();

        if (availableFloors.Count > 0)
        {
            currentFloor = availableFloors[0];
        }

        SpawnMarkersForCurrentFloor();
    }

    private IEnumerator LoadNodes(string mapId)
    {
        string fileName = $"nodes_{mapId}.json";
        bool loadComplete = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonContent) =>
            {
                try
                {
                    Node[] nodes = JsonHelper.FromJson<Node>(jsonContent);
                    allNodes.Clear();

                    foreach (var node in nodes)
                    {
                        allNodes[node.node_id] = node;
                    }

                    loadComplete = true;
                }
                catch (System.Exception)
                {
                    loadComplete = true;
                }
            },
            (error) =>
            {
                loadComplete = true;
            }
        ));

        yield return new WaitUntil(() => loadComplete);
    }

    private IEnumerator LoadIndoorData()
    {
        bool loadComplete = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "indoor.json",
            (jsonContent) =>
            {
                try
                {
                    IndoorInfrastructure[] indoorArray = JsonHelper.FromJson<IndoorInfrastructure>(jsonContent);
                    indoorInfrastructures.Clear();

                    foreach (var indoor in indoorArray)
                    {
                        if (!indoor.is_deleted)
                        {
                            indoorInfrastructures[indoor.room_id] = indoor;
                        }
                    }

                    loadComplete = true;
                }
                catch (System.Exception)
                {
                    loadComplete = true;
                }
            },
            (error) =>
            {
                loadComplete = true;
            }
        ));

        yield return new WaitUntil(() => loadComplete);
    }

    private void DetermineAvailableFloors()
    {
        availableFloors.Clear();

        var indoorNodes = allNodes.Values.Where(n => 
            n.type == "indoorinfra" && 
            n.HasRelatedRoomId && 
            indoorInfrastructures.ContainsKey(n.related_room_id) &&
            indoorInfrastructures[n.related_room_id].infra_id == currentInfraId &&
            n.indoor != null &&
            !string.IsNullOrEmpty(n.indoor.floor)
        );

        foreach (var node in indoorNodes)
        {
            if (int.TryParse(node.indoor.floor, out int floor))
            {
                if (!availableFloors.Contains(floor))
                {
                    availableFloors.Add(floor);
                }
            }
        }

        availableFloors.Sort();
    }

    private void SpawnMarkersForCurrentFloor()
    {
        ClearAllMarkers();

        Node infraNode = allNodes.Values.FirstOrDefault(n => 
            n.type == "infrastructure" && n.related_infra_id == currentInfraId);

        if (infraNode != null && entranceMarkerPrefab != null)
        {
            GameObject entranceMarker = Instantiate(entranceMarkerPrefab, markersContainer);
            Vector2 entrancePos = WorldToMapPosition(0f, 0f);
            entranceMarker.GetComponent<RectTransform>().anchoredPosition = entrancePos;
            spawnedMarkers["entrance"] = entranceMarker;
        }

        var indoorNodes = allNodes.Values.Where(n => 
            n.type == "indoorinfra" && 
            n.HasRelatedRoomId && 
            indoorInfrastructures.ContainsKey(n.related_room_id) &&
            indoorInfrastructures[n.related_room_id].infra_id == currentInfraId &&
            n.indoor != null &&
            n.indoor.floor == currentFloor.ToString()
        );

        foreach (var node in indoorNodes)
        {
            SpawnMarkerForNode(node);
        }
    }

    private void SpawnMarkerForNode(Node node)
    {
        if (node.indoor == null || !indoorInfrastructures.ContainsKey(node.related_room_id))
            return;

        IndoorInfrastructure indoor = indoorInfrastructures[node.related_room_id];

        GameObject markerPrefab = GetMarkerPrefabForType(indoor.indoor_type);

        if (markerPrefab == null)
            return;

        GameObject marker = Instantiate(markerPrefab, markersContainer);

        Vector2 mapPos = WorldToMapPosition(node.indoor.x, node.indoor.y);
        marker.GetComponent<RectTransform>().anchoredPosition = mapPos;

        spawnedMarkers[node.node_id] = marker;
    }

    private GameObject GetMarkerPrefabForType(string indoorType)
    {
        switch (indoorType.ToLower())
        {
            case "room":
                return roomMarkerPrefab;
            case "stairs":
                return stairsMarkerPrefab;
            case "fire_exit":
                return fireExitMarkerPrefab;
            default:
                return roomMarkerPrefab;
        }
    }

    private Vector2 WorldToMapPosition(float x, float y)
    {
        float mapX = x * pixelsPerMeter;
        float mapY = y * pixelsPerMeter;

        return new Vector2(mapX, mapY);
    }

    public void ChangeFloor(int direction)
    {
        if (availableFloors.Count == 0)
            return;

        int currentIndex = availableFloors.IndexOf(currentFloor);

        if (currentIndex == -1)
            return;

        int newIndex = currentIndex + direction;

        if (newIndex < 0 || newIndex >= availableFloors.Count)
            return;

        currentFloor = availableFloors[newIndex];

        SpawnMarkersForCurrentFloor();
    }

    private void ClearAllMarkers()
    {
        foreach (var marker in spawnedMarkers.Values)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }

        spawnedMarkers.Clear();
    }

    public int GetCurrentFloor()
    {
        return currentFloor;
    }

    public List<int> GetAvailableFloors()
    {
        return new List<int>(availableFloors);
    }
}