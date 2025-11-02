using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mapbox.Utils;
using Mapbox.Unity.Map;
using TMPro;

public class ARMapManager : MonoBehaviour
{
    [Header("Mapbox References")]
    public AbstractMap arMapboxMap;

    [Header("Spawner References")]
    public PathRenderer pathRenderer;
    public BarrierSpawner barrierSpawner;
    public InfrastructureSpawner infrastructureSpawner;

    [Header("AR Navigation Settings")]
    public Color navigationPathColor = new Color(0.74f, 0.06f, 0.18f, 0.9f);
    public float navigationPathWidth = 2.5f;
    public Color navigationNodeColor = new Color(0.74f, 0.06f, 0.18f, 1f);
    public float navigationNodeSize = 4f;

    public static event System.Action OnSpawningComplete;

    private string currentMapId;
    private List<string> currentCampusIds = new List<string>();
    private List<string> navigationNodeIds = new List<string>();
    private HashSet<string> navigationEdgeIds = new HashSet<string>();

    private List<PathEdge> spawnedNavigationPaths = new List<PathEdge>();
    private Dictionary<string, InfrastructureNode> spawnedNavigationNodes = new Dictionary<string, InfrastructureNode>();
    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>();

    private RouteData activeRoute;
    private bool isInitialized = false;
    private bool spawningComplete = false;

    public static ARMapManager Instance { get; private set; }

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

        if (arMapboxMap == null)
        {
            arMapboxMap = FindObjectOfType<AbstractMap>();
        }
    }

    void Start()
    {
        if (arMapboxMap == null)
        {
            return;
        }

        if (pathRenderer == null)
            pathRenderer = arMapboxMap.GetComponent<PathRenderer>();
        if (barrierSpawner == null)
            barrierSpawner = arMapboxMap.GetComponent<BarrierSpawner>();
        if (infrastructureSpawner == null)
            infrastructureSpawner = arMapboxMap.GetComponent<InfrastructureSpawner>();

        isInitialized = true;

        string mapId = PlayerPrefs.GetString("ARScene_MapId", "MAP-01");
        string campusIdsStr = PlayerPrefs.GetString("ARScene_CampusIds", "");
        List<string> campusIds = string.IsNullOrEmpty(campusIdsStr)
            ? new List<string>()
            : new List<string>(campusIdsStr.Split(','));

        RouteData savedRoute = LoadRouteDataFromPlayerPrefs();

        StartCoroutine(InitializeARScene(mapId, campusIds, savedRoute));
    }

    private RouteData LoadRouteDataFromPlayerPrefs()
    {
        int pathNodeCount = PlayerPrefs.GetInt("ARNavigation_PathNodeCount", 0);
        
        if (pathNodeCount == 0)
        {
            return null;
        }

        string startNodeId = PlayerPrefs.GetString("ARNavigation_StartNodeId", "");
        string endNodeId = PlayerPrefs.GetString("ARNavigation_EndNodeId", "");

        if (string.IsNullOrEmpty(startNodeId) || string.IsNullOrEmpty(endNodeId))
        {
            return null;
        }

        RouteData route = new RouteData
        {
            totalDistance = PlayerPrefs.GetFloat("ARNavigation_TotalDistance", 0f),
            formattedDistance = PlayerPrefs.GetString("ARNavigation_FormattedDistance", ""),
            walkingTime = PlayerPrefs.GetString("ARNavigation_WalkingTime", ""),
            viaMode = PlayerPrefs.GetString("ARNavigation_ViaMode", "")
        };

        route.path = new List<PathNode>();
        
        return route;
    }

    private IEnumerator InitializeARScene(string mapId, List<string> campusIds, RouteData route)
    {
        currentMapId = mapId;
        currentCampusIds.Clear();
        currentCampusIds.AddRange(campusIds);

        yield return StartCoroutine(WaitForMapManagerAndUpdateCenter());

        yield return StartCoroutine(InitializeSpawners(mapId, campusIds));

        yield return new WaitUntil(() => spawningComplete);

        if (route != null)
        {
            yield return StartCoroutine(LoadAllNodesForAR());
            
            RouteData fullRoute = ReconstructRouteFromPlayerPrefs();
            
            if (fullRoute != null)
            {
                InitializeARNavigation(mapId, campusIds, fullRoute);
            }
        }
    }

    private IEnumerator WaitForMapManagerAndUpdateCenter()
    {
        float centerLat = PlayerPrefs.GetFloat("ARScene_CenterLat", 6.9131464621345629f);
        float centerLng = PlayerPrefs.GetFloat("ARScene_CenterLng", 122.06465815697084f);
        
        if (arMapboxMap != null)
        {
            Vector2d newCenter = new Vector2d(centerLat, centerLng);
            
            int zoom = (int)arMapboxMap.Zoom;
            if (zoom == 0) zoom = 16;
            
            arMapboxMap.Initialize(newCenter, zoom);
        }
        
        yield break;
    }

    private RouteData ReconstructRouteFromPlayerPrefs()
    {
        int pathNodeCount = PlayerPrefs.GetInt("ARNavigation_PathNodeCount", 0);
        if (pathNodeCount == 0) return null;

        string startNodeId = PlayerPrefs.GetString("ARNavigation_StartNodeId", "");
        string endNodeId = PlayerPrefs.GetString("ARNavigation_EndNodeId", "");

        if (!allNodes.ContainsKey(startNodeId) || !allNodes.ContainsKey(endNodeId))
        {
            return null;
        }

        List<string> pathNodeIds = ExtractPathNodeIdsFromDirections();

        if (pathNodeIds == null || pathNodeIds.Count == 0)
        {
            return null;
        }

        List<PathNode> pathNodes = new List<PathNode>();
        foreach (string nodeId in pathNodeIds)
        {
            if (allNodes.TryGetValue(nodeId, out Node node))
            {
                pathNodes.Add(new PathNode
                {
                    node = node,
                    worldPosition = Vector3.zero,
                    isStart = nodeId == startNodeId,
                    isEnd = nodeId == endNodeId,
                    distanceToNext = 0f
                });
            }
        }

        RouteData route = new RouteData
        {
            path = pathNodes,
            startNode = allNodes[startNodeId],
            endNode = allNodes[endNodeId],
            totalDistance = PlayerPrefs.GetFloat("ARNavigation_TotalDistance", 0f),
            formattedDistance = PlayerPrefs.GetString("ARNavigation_FormattedDistance", ""),
            walkingTime = PlayerPrefs.GetString("ARNavigation_WalkingTime", ""),
            viaMode = PlayerPrefs.GetString("ARNavigation_ViaMode", "")
        };

        return route;
    }

    private List<string> ExtractPathNodeIdsFromDirections()
    {
        int pathNodeCount = PlayerPrefs.GetInt("ARNavigation_PathNodeCount", 0);
        
        if (pathNodeCount == 0)
        {
            return new List<string>();
        }

        List<string> pathNodeIds = new List<string>();
        
        for (int i = 0; i < pathNodeCount; i++)
        {
            string nodeId = PlayerPrefs.GetString($"ARNavigation_PathNode_{i}", "");
            
            if (!string.IsNullOrEmpty(nodeId))
            {
                pathNodeIds.Add(nodeId);
            }
        }

        return pathNodeIds;
    }

    private IEnumerator InitializeSpawners(string mapId, List<string> campusIds)
    {
        spawningComplete = false;

        if (pathRenderer != null)
            pathRenderer.SetCurrentMapData(mapId, campusIds);
        if (barrierSpawner != null)
            barrierSpawner.SetCurrentMapData(mapId, campusIds);
        if (infrastructureSpawner != null)
            infrastructureSpawner.SetCurrentMapData(mapId, campusIds);

        if (pathRenderer != null)
            yield return StartCoroutine(pathRenderer.LoadAndRenderPathsForMap(mapId, campusIds));

        if (barrierSpawner != null)
            yield return StartCoroutine(barrierSpawner.LoadAndSpawnForMap(mapId, campusIds));

        if (infrastructureSpawner != null)
            yield return StartCoroutine(infrastructureSpawner.LoadAndSpawnForCampuses(campusIds));

        yield return null;

        spawningComplete = true;

        OnSpawningComplete?.Invoke();
    }

    public void InitializeARNavigation(string mapId, List<string> campusIds, RouteData route)
    {
        if (!isInitialized)
        {
            return;
        }

        if (route == null || route.path == null || route.path.Count == 0)
        {
            return;
        }

        currentMapId = mapId;
        currentCampusIds.Clear();
        currentCampusIds.AddRange(campusIds);
        activeRoute = route;

        ClearNavigationHighlights();
        StartCoroutine(SetupNavigationHighlighting(route));
    }

    private IEnumerator SetupNavigationHighlighting(RouteData route)
    {
        navigationNodeIds.Clear();
        navigationNodeIds = route.path.Select(pn => pn.node.node_id).ToList();

        navigationEdgeIds.Clear();
        for (int i = 0; i < navigationNodeIds.Count - 1; i++)
        {
            string edgeKey = GetEdgeKey(navigationNodeIds[i], navigationNodeIds[i + 1]);
            navigationEdgeIds.Add(edgeKey);
        }

        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(HighlightNavigationPaths());
        yield return StartCoroutine(HighlightNavigationNodes());
    }

    private IEnumerator LoadAllNodesForAR()
    {
        string fileName = $"nodes_{currentMapId}.json";
        bool loadCompleted = false;

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
                        if (node != null && node.is_active && IsValidCoordinate(node.latitude, node.longitude))
                        {
                            allNodes[node.node_id] = node;
                        }
                    }

                    loadCompleted = true;
                }
                catch (System.Exception e)
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

    private IEnumerator HighlightNavigationPaths()
    {
        if (pathRenderer == null)
        {
            yield break;
        }

        yield return null;

        PathEdge[] allPathEdges = arMapboxMap.GetComponentsInChildren<PathEdge>();
        int highlightedCount = 0;

        foreach (var pathEdge in allPathEdges)
        {
            if (pathEdge == null)
                continue;

            Edge edgeData = pathEdge.GetEdgeData();
            if (edgeData == null)
                continue;

            string edgeKey = GetEdgeKey(edgeData.from_node, edgeData.to_node);

            if (navigationEdgeIds.Contains(edgeKey))
            {
                Renderer[] renderers = pathEdge.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    if (renderer.material != null)
                    {
                        renderer.material.color = navigationPathColor;
                    }
                }

                pathEdge.transform.localScale = new Vector3(
                    navigationPathWidth / 1f, 
                    navigationPathWidth / 1f, 
                    pathEdge.transform.localScale.z
                );

                highlightedCount++;
            }
        }

        yield break;
    }

    private IEnumerator HighlightNavigationNodes()
    {
        if (infrastructureSpawner == null)
        {
            yield break;
        }

        yield return null;

        InfrastructureNode[] allInfraNodes = arMapboxMap.GetComponentsInChildren<InfrastructureNode>();
        int highlightedCount = 0;

        foreach (var infraNode in allInfraNodes)
        {
            if (infraNode == null)
                continue;

            InfrastructureData infraData = infraNode.GetInfrastructureData();
            if (infraData == null || infraData.Node == null)
                continue;

            string nodeId = infraData.Node.node_id;

            if (navigationNodeIds.Contains(nodeId))
            {
                Renderer[] renderers = infraNode.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    if (renderer != null)
                    {
                        Material newMat = new Material(renderer.material);
                        newMat.SetColor("_BaseColor", navigationNodeColor);
                        renderer.material = newMat;
                    }
                }

                spawnedNavigationNodes[nodeId] = infraNode;
                highlightedCount++;
            }
        }

        yield break;
    }

    public void ClearNavigationHighlights()
    {
        spawnedNavigationPaths.Clear();
        spawnedNavigationNodes.Clear();
        navigationNodeIds.Clear();
        navigationEdgeIds.Clear();
        activeRoute = null;
    }

    private string GetEdgeKey(string from, string to)
    {
        if (string.Compare(from, to) < 0)
            return from + "-" + to;
        else
            return to + "-" + from;
    }

    private bool IsValidCoordinate(float lat, float lon)
    {
        return !float.IsNaN(lat) && !float.IsNaN(lon) &&
               !float.IsInfinity(lat) && !float.IsInfinity(lon) &&
               lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180;
    }

    public RouteData GetActiveRoute()
    {
        return activeRoute;
    }

    public List<string> GetNavigationNodeIds()
    {
        return new List<string>(navigationNodeIds);
    }

    public HashSet<string> GetNavigationEdgeIds()
    {
        return new HashSet<string>(navigationEdgeIds);
    }

    public bool IsSpawningComplete()
    {
        return spawningComplete;
    }
}