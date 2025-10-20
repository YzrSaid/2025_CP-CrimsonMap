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
    public Color navigationPathColor = new Color(0.74f, 0.06f, 0.18f, 0.9f); // Maroon
    public float navigationPathWidth = 2.5f;
    public Color navigationNodeColor = new Color(0.74f, 0.06f, 0.18f, 1f); // Maroon
    public float navigationNodeSize = 4f;

    [Header("Settings")]
    public bool enableDebugLogs = true;

    private string currentMapId;
    private List<string> currentCampusIds = new List<string>();
    private List<string> navigationNodeIds = new List<string>();
    private HashSet<string> navigationEdgeIds = new HashSet<string>();

    private List<PathEdge> spawnedNavigationPaths = new List<PathEdge>();
    private Dictionary<string, InfrastructureNode> spawnedNavigationNodes = new Dictionary<string, InfrastructureNode>();
    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>();

    private RouteData activeRoute;
    private bool isInitialized = false;

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
            DebugLog("AR Mapbox Map not found!");
            return;
        }

#if UNITY_EDITOR
        if (!PlayerPrefs.HasKey("CurrentMapId"))
        {
            PlayerPrefs.SetString("CurrentMapId", "MAP-01");
            PlayerPrefs.SetString("ARMode", "DirectAR"); // Change to "Navigation" to test route highlighting
            PlayerPrefs.Save();
        }
#endif

        if (pathRenderer == null)
            pathRenderer = arMapboxMap.GetComponent<PathRenderer>();
        if (barrierSpawner == null)
            barrierSpawner = arMapboxMap.GetComponent<BarrierSpawner>();
        if (infrastructureSpawner == null)
            infrastructureSpawner = arMapboxMap.GetComponent<InfrastructureSpawner>();

        isInitialized = true;

        string mapId = PlayerPrefs.GetString("CurrentMapId", "MAP-01");
        List<string> campusIds = new List<string> { "CAMPUS-01" };

        StartCoroutine(InitializeSpawners(mapId, campusIds));
    }

    private IEnumerator InitializeSpawners(string mapId, List<string> campusIds)
    {
        DebugLog($"Spawning all map elements for {mapId}");

        if (pathRenderer != null)
            yield return StartCoroutine(pathRenderer.LoadAndRenderPathsForMap(mapId, campusIds));

        if (barrierSpawner != null)
            yield return StartCoroutine(barrierSpawner.LoadAndSpawnForMap(mapId, campusIds));

        if (infrastructureSpawner != null)
            yield return StartCoroutine(infrastructureSpawner.LoadAndSpawnForCampuses(campusIds));

        DebugLog("All map elements spawned");
    }

    public void InitializeARNavigation(string mapId, List<string> campusIds, RouteData route)
    {
        if (!isInitialized)
        {
            return;
        }

        if (route == null || route.path == null || route.path.Count == 0)
        {
            DebugLog("No valid route provided");
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
        // Extract node IDs from the route
        navigationNodeIds.Clear();
        navigationNodeIds = route.path.Select(pn => pn.node.node_id).ToList();

        // Build edge keys from consecutive nodes
        navigationEdgeIds.Clear();
        for (int i = 0; i < navigationNodeIds.Count - 1; i++)
        {
            string edgeKey = GetEdgeKey(navigationNodeIds[i], navigationNodeIds[i + 1]);
            navigationEdgeIds.Add(edgeKey);
        }

        DebugLog($"Navigation route has {navigationNodeIds.Count} nodes and {navigationEdgeIds.Count} edges");

        // Load all nodes for reference
        yield return StartCoroutine(LoadAllNodesForAR());

        // Highlight navigation paths on existing path renderer
        yield return StartCoroutine(HighlightNavigationPaths());

        // Highlight navigation infrastructure nodes on existing infrastructure spawner
        yield return StartCoroutine(HighlightNavigationNodes());

        DebugLog("AR navigation highlighting complete");
    }

    private IEnumerator LoadAllNodesForAR()
    {
        Debug.Log("YAWA" + currentMapId);
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
                    DebugLog($"Error loading nodes: {e.Message}");
                    loadCompleted = true;
                }
            },
            (error) =>
            {
                DebugLog($"Failed to load nodes file: {error}");
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

        // Wait a frame for pathRenderer to be ready
        yield return null;

        // Get all spawned paths from pathRenderer
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

            // If this edge is part of the navigation route, highlight it
            if (navigationEdgeIds.Contains(edgeKey))
            {
                // Change the path color to maroon
                Renderer[] renderers = pathEdge.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    if (renderer.material != null)
                    {
                        renderer.material.color = navigationPathColor;
                    }
                }

                // Optionally make it slightly thicker by scaling
                pathEdge.transform.localScale = new Vector3(navigationPathWidth / 1f, navigationPathWidth / 1f, pathEdge.transform.localScale.z);

                highlightedCount++;
            }
        }

        DebugLog($"Highlighted {highlightedCount} navigation path edges");
        yield break;
    }

    private IEnumerator HighlightNavigationNodes()
    {
        if (infrastructureSpawner == null)
        {
            yield break;
        }

        // Wait a frame for infrastructureSpawner to be ready
        yield return null;

        // Get all spawned infrastructure nodes
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

            // If this node is part of the navigation route, highlight it
            if (navigationNodeIds.Contains(nodeId))
            {
                // Change the infrastructure color to maroon
                Renderer[] renderers = infraNode.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    if (renderer.material != null)
                    {
                        renderer.material.color = navigationNodeColor;
                    }
                }

                // Scale up slightly to emphasize
                infraNode.transform.localScale = Vector3.one * navigationNodeSize;

                spawnedNavigationNodes[nodeId] = infraNode;
                highlightedCount++;
            }
        }

        DebugLog($"Highlighted {highlightedCount} navigation infrastructure nodes");
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

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ARMapManager] {message}");
        }
    }
}