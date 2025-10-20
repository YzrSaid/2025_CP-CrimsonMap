using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using System.Linq;
using Mapbox.Utils;
using Mapbox.Unity.Map;

public class BarrierSpawner : MonoBehaviour
{
    [Header("Mapbox")]
    public AbstractMap mapboxMap;

    [Header("Prefabs")]
    public GameObject nodePrefab;
    public GameObject edgePrefab;

    [Header("Settings")]
    public bool enableDebugLogs = true;
    public float nodeSize = 2.5f;
    public float heightOffset = 10f;

    [Header("Edge Settings")]
    public float edgeWidth = 0.5f;
    public Color edgeColor = Color.white;

    [Header("Node Settings")]
    public Color nodeColor = Color.white;



    private string currentMapId;
    private List<string> currentCampusIds = new List<string>();

    private List<BarrierNode> spawnedNodes = new List<BarrierNode>();
    private List<BarrierEdge> spawnedEdges = new List<BarrierEdge>();
    private Dictionary<string, BarrierNode> nodeIdToComponent = new Dictionary<string, BarrierNode>();

    private bool isSpawning = false;

    void Awake()
    {
        if (mapboxMap == null)
        {
            mapboxMap = FindObjectOfType<AbstractMap>();
        }
    }

    void Start()
    {
        if (mapboxMap == null)
        {
            return;
        }

        if (MapManager.Instance != null)
        {
            MapManager.Instance.OnMapChanged += OnMapChanged;
            MapManager.Instance.OnMapLoadingStarted += OnMapLoadingStarted;
        }
    }

    void OnDestroy()
    {
        if (MapManager.Instance != null)
        {
            MapManager.Instance.OnMapChanged -= OnMapChanged;
            MapManager.Instance.OnMapLoadingStarted -= OnMapLoadingStarted;
        }
    }

    public void SetCurrentMapData(string mapId, List<string> campusIds)
    {
        currentMapId = mapId;
        currentCampusIds.Clear();
        if (campusIds != null)
        {
            currentCampusIds.AddRange(campusIds);
        }

        DebugLog($"yawa ka campus ids: {string.Join(", ", campusIds)}");
    }

    private void OnMapChanged(MapInfo mapInfo)
    {
        SetCurrentMapData(mapInfo.map_id, mapInfo.campus_included);
    }

    private void OnMapLoadingStarted()
    {
        ClearSpawnedNodes();
    }

    public IEnumerator LoadAndSpawnForMap(string mapId, List<string> campusIds)
    {
        if (isSpawning)
        {
            yield break;
        }

        SetCurrentMapData(mapId, campusIds);

        yield return StartCoroutine(WaitForMapReady());

        yield return StartCoroutine(LoadAndSpawnBarrierNodes());

        yield return StartCoroutine(LoadAndSpawnBarrierEdges());
    }

    private IEnumerator WaitForMapReady()
    {
        float timeout = 30f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (mapboxMap != null && mapboxMap.gameObject.activeInHierarchy)
            {
                break;
            }

            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        if (elapsed >= timeout)
        {
            yield break;
        }

        yield return new WaitForSeconds(1f);
    }

    private string GetNodesFileName()
    {
        if (string.IsNullOrEmpty(currentMapId))
        {
            return "nodes.json";
        }

        string fileName = $"nodes_{currentMapId}.json";
        return fileName;
    }

    private string GetEdgesFileName()
    {
        if (string.IsNullOrEmpty(currentMapId))
        {
            return "edges.json";
        }

        string fileName = $"edges_{currentMapId}.json";
        return fileName;
    }



    private List<Vector2d> CreateConvexHull(List<Vector2d> points)
    {
        if (points.Count < 3) return points;

        Vector2d leftmost = points[0];
        int leftmostIndex = 0;
        for (int i = 1; i < points.Count; i++)
        {
            if (points[i].x < leftmost.x || (points[i].x == leftmost.x && points[i].y < leftmost.y))
            {
                leftmost = points[i];
                leftmostIndex = i;
            }
        }

        List<Vector2d> hull = new List<Vector2d>();
        int currentIndex = leftmostIndex;

        do
        {
            hull.Add(points[currentIndex]);
            int nextIndex = (currentIndex + 1) % points.Count;

            for (int i = 0; i < points.Count; i++)
            {
                if (IsCounterClockwise(points[currentIndex], points[i], points[nextIndex]))
                {
                    nextIndex = i;
                }
            }

            currentIndex = nextIndex;
        } while (currentIndex != leftmostIndex && hull.Count < points.Count);

        return hull;
    }

    private bool IsCounterClockwise(Vector2d a, Vector2d b, Vector2d c)
    {
        return (c.y - a.y) * (b.x - a.x) > (b.y - a.y) * (c.x - a.x);
    }

    public IEnumerator LoadAndSpawnBarrierNodes()
    {
        if (isSpawning)
        {
            yield break;
        }

        isSpawning = true;

        if (string.IsNullOrEmpty(currentMapId))
        {
            isSpawning = false;
            yield break;
        }

        List<Node> barrierNodes = null;
        bool loadingComplete = false;
        string errorMessage = null;

        try
        {
            ClearSpawnedNodes();
        }
        catch (System.Exception e)
        {
            isSpawning = false;
            yield break;
        }

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
                                         GetNodesFileName(),
        (jsonContent) =>
        {
            try
            {
                Node[] nodes = JsonHelper.FromJson<Node>(jsonContent);
                if (nodes == null || nodes.Length == 0)
                {
                    errorMessage = $"Failed to parse nodes from {GetNodesFileName()}";
                    return;
                }

                barrierNodes = FilterBarrierNodes(nodes, currentCampusIds);
                loadingComplete = true;
            }
            catch (System.Exception e)
            {
                errorMessage = $"Error parsing nodes JSON from {GetNodesFileName()}: {e.Message}";
            }
        },
        (error) =>
        {
            errorMessage = $"Error loading {GetNodesFileName()}: {error}";
        }
                                     ));

        while (!loadingComplete && string.IsNullOrEmpty(errorMessage))
        {
            yield return null;
        }

        if (!string.IsNullOrEmpty(errorMessage))
        {
            isSpawning = false;
            yield break;
        }

        if (barrierNodes == null || barrierNodes.Count == 0)
        {
            isSpawning = false;
            yield break;
        }

        isSpawning = false;

        yield return StartCoroutine(SpawnNodes(barrierNodes));
    }

    public IEnumerator LoadAndSpawnBarrierEdges()
    {
        if (spawnedNodes.Count == 0)
        {
            yield break;
        }

        if (string.IsNullOrEmpty(currentMapId))
        {
            yield break;
        }

        List<Edge> validEdges = null;
        bool loadingComplete = false;
        string errorMessage = null;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
                                         GetEdgesFileName(),
        (jsonContent) =>
        {
            try
            {
                Edge[] edges = JsonHelper.FromJson<Edge>(jsonContent);
                if (edges == null || edges.Length == 0)
                {
                    errorMessage = $"No edges found in {GetEdgesFileName()}";
                    return;
                }

                validEdges = FilterValidEdges(edges);
                loadingComplete = true;
            }
            catch (System.Exception e)
            {
                errorMessage = $"Error parsing edges JSON from {GetEdgesFileName()}: {e.Message}";
            }
        },
        (error) =>
        {
            errorMessage = $"Error loading {GetEdgesFileName()}: {error}";
        }
                                     ));

        while (!loadingComplete && string.IsNullOrEmpty(errorMessage))
        {
            yield return null;
        }

        if (!string.IsNullOrEmpty(errorMessage))
        {
            yield break;
        }

        if (validEdges != null && validEdges.Count > 0)
        {
            yield return StartCoroutine(SpawnEdges(validEdges));
        }
    }

    private List<Node> FilterBarrierNodes(Node[] allNodes, List<string> campusIds)
    {
        var filteredNodes = allNodes.Where(n =>
                                            n != null &&
                                            n.type == "barrier" &&
                                            n.is_active &&
                                            (campusIds == null || campusIds.Count == 0 || campusIds.Contains(n.campus_id)) &&
                                            IsValidCoordinate(n.latitude, n.longitude)
                                          ).ToList();

        return filteredNodes;
    }

    private List<Edge> FilterValidEdges(Edge[] allEdges)
    {
        var validEdges = allEdges.Where(edge =>
                                         edge != null &&
                                         edge.is_active &&
                                         nodeIdToComponent.ContainsKey(edge.from_node) &&
                                         nodeIdToComponent.ContainsKey(edge.to_node)
                                       ).ToList();

        return validEdges;
    }

    private IEnumerator SpawnNodes(List<Node> nodes)
    {
        int spawnedCount = 0;
        foreach (var node in nodes)
        {
            bool shouldYield = false;
            try
            {
                if (nodePrefab == null)
                {
                    break;
                }

                GameObject nodeObj = Instantiate(nodePrefab, Vector3.zero, Quaternion.identity, mapboxMap.transform);
                nodeObj.name = $"BarrierNode_{node.node_id}_{node.name}";
                nodeObj.transform.localScale = Vector3.one * nodeSize;

                BarrierNode barrierComponent = nodeObj.AddComponent<BarrierNode>();
                barrierComponent.Initialize(mapboxMap, node, heightOffset, nodeColor);

                spawnedNodes.Add(barrierComponent);
                nodeIdToComponent[node.node_id] = barrierComponent;

                spawnedCount++;

                if (spawnedCount % 10 == 0)
                {
                    shouldYield = true;
                }
            }
            catch (System.Exception e)
            {
            }
            if (shouldYield)
            {
                yield return null;
            }
        }
    }

    private IEnumerator SpawnEdges(List<Edge> edges)
    {
        int spawnedCount = 0;
        foreach (var edge in edges)
        {
            bool shouldYield = false;
            try
            {
                if (edgePrefab == null)
                {
                    continue;
                }

                if (!nodeIdToComponent.TryGetValue(edge.from_node, out BarrierNode fromNode) ||
                        !nodeIdToComponent.TryGetValue(edge.to_node, out BarrierNode toNode))
                {
                    continue;
                }

                GameObject edgeObj = Instantiate(edgePrefab, Vector3.zero, Quaternion.identity, mapboxMap.transform);
                edgeObj.name = $"BarrierEdge_{edge.edge_id}_{edge.from_node}_to_{edge.to_node}";

                BarrierEdge edgeComponent = edgeObj.AddComponent<BarrierEdge>();
                edgeComponent.Initialize(mapboxMap, edge, fromNode, toNode, edgeWidth, heightOffset, edgeColor);

                spawnedEdges.Add(edgeComponent);
                spawnedCount++;

                if (spawnedCount % 5 == 0)
                {
                    shouldYield = true;
                }
            }
            catch (System.Exception e)
            {
            }

            if (shouldYield)
            {
                yield return null;
            }
        }
    }

    public void ClearSpawnedNodes()
    {
        foreach (var edge in spawnedEdges)
        {
            if (edge != null && edge.gameObject != null)
            {
                DestroyImmediate(edge.gameObject);
            }
        }
        spawnedEdges.Clear();

        foreach (var barrierNode in spawnedNodes)
        {
            if (barrierNode != null && barrierNode.gameObject != null)
            {
                DestroyImmediate(barrierNode.gameObject);
            }
        }

        spawnedNodes.Clear();
        nodeIdToComponent.Clear();
    }

    public void ForceUpdateAllEdges()
    {
        foreach (var edge in spawnedEdges)
        {
            if (edge != null)
            {
                edge.ForceUpdate();
            }
        }
    }

    public void ManualSpawn()
    {
        if (!string.IsNullOrEmpty(currentMapId))
        {
            StartCoroutine(LoadAndSpawnBarrierNodes());
        }
    }

    public void ManualSpawnEdges()
    {
        if (!string.IsNullOrEmpty(currentMapId))
        {
            StartCoroutine(LoadAndSpawnBarrierEdges());
        }
    }

    public void ForceResetSpawning()
    {
        isSpawning = false;
        StopAllCoroutines();
    }

    private bool IsValidCoordinate(float lat, float lon)
    {
        return !float.IsNaN(lat) && !float.IsNaN(lon) &&
               !float.IsInfinity(lat) && !float.IsInfinity(lon) &&
               lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[BarrierSpawner] {message}");
        }
    }

    void Update()
    {
    }
}

public class BarrierEdge : MonoBehaviour
{
    private AbstractMap map;
    private Edge edgeData;
    private BarrierNode fromNode;
    private BarrierNode toNode;
    private float baseWidth;
    private float heightOffset;
    private Color edgeColor;

    private float referenceZoomLevel;
    private Vector3 referenceFromPos;
    private Vector3 referenceToPos;
    private float referenceDistance;
    private bool isInitialized = false;

    public Edge GetEdgeData() => edgeData;
    public BarrierNode GetFromNode() => fromNode;
    public BarrierNode GetToNode() => toNode;

    public void Initialize(AbstractMap mapReference, Edge edge, BarrierNode from, BarrierNode to,
                            float width, float height, Color color)
    {
        map = mapReference;
        edgeData = edge;
        fromNode = from;
        toNode = to;
        baseWidth = width;
        heightOffset = height;
        edgeColor = color;

        if (map != null)
        {
            referenceZoomLevel = map.Zoom;

            Node fromNodeData = fromNode.GetNodeData();
            Node toNodeData = toNode.GetNodeData();

            referenceFromPos = map.GeoToWorldPosition(new Vector2d(fromNodeData.latitude, fromNodeData.longitude), false);
            referenceToPos = map.GeoToWorldPosition(new Vector2d(toNodeData.latitude, toNodeData.longitude), false);
            referenceDistance = Vector3.Distance(referenceFromPos, referenceToPos);

            isInitialized = true;
        }

        ApplyColorToEdge();
        UpdateEdgeTransform();
    }

    private void ApplyColorToEdge()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            Material mat = renderer.material;
            mat.color = edgeColor;
        }
    }

    void LateUpdate()
    {
        if (map != null && fromNode != null && toNode != null && isInitialized)
        {
            if (Time.frameCount % 2 == 0)
            {
                UpdateEdgeTransform();
            }
        }
    }

    void UpdateEdgeTransform()
    {
        if (fromNode == null || toNode == null || map == null || !isInitialized) return;

        Node fromNodeData = fromNode.GetNodeData();
        Node toNodeData = toNode.GetNodeData();
        if (fromNodeData == null || toNodeData == null) return;

        Vector3 fromPos = map.GeoToWorldPosition(new Vector2d(fromNodeData.latitude, fromNodeData.longitude), false);
        Vector3 toPos = map.GeoToWorldPosition(new Vector2d(toNodeData.latitude, toNodeData.longitude), false);

        fromPos.y = heightOffset;
        toPos.y = heightOffset;

        Vector3 direction = toPos - fromPos;
        float currentDistance = direction.magnitude;

        if (currentDistance < 0.001f)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        Vector3 centerPos = (fromPos + toPos) * 0.5f;
        transform.position = centerPos;

        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        float visualDistance = referenceDistance;

        transform.localScale = new Vector3(baseWidth, baseWidth, visualDistance);
    }

    public void ForceUpdate()
    {
        if (map != null && isInitialized)
        {
            UpdateEdgeTransform();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (fromNode != null && toNode != null && map != null)
        {
            Node fromNodeData = fromNode.GetNodeData();
            Node toNodeData = toNode.GetNodeData();
            if (fromNodeData == null || toNodeData == null) return;

            Vector3 fromPos = map.GeoToWorldPosition(new Vector2d(fromNodeData.latitude, fromNodeData.longitude), false);
            Vector3 toPos = map.GeoToWorldPosition(new Vector2d(toNodeData.latitude, toNodeData.longitude), false);

            fromPos.y = heightOffset;
            toPos.y = heightOffset;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(fromPos, 0.2f);
            Gizmos.DrawWireSphere(toPos, 0.2f);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(fromPos, toPos);

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.1f);
        }
    }
}

public class BarrierNode : MonoBehaviour
{
    private AbstractMap map;
    private Node nodeData;
    private Vector2d geoLocation;
    private float heightOffset;
    private Color nodeColor;

    public Node GetNodeData() => nodeData;
    public Vector2d GetGeoLocation() => geoLocation;

    public void Initialize(AbstractMap mapReference, Node node, float height, Color color)
    {
        map = mapReference;
        nodeData = node;
        geoLocation = new Vector2d(node.latitude, node.longitude);
        heightOffset = height;
        nodeColor = color;

        ApplyColorToNode();
        UpdatePosition();
    }

    private void ApplyColorToNode()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            Material mat = renderer.material;
            mat.color = nodeColor;
        }
    }

    void Update()
    {
        if (map != null)
        {
            UpdatePosition();
        }
    }

    void UpdatePosition()
    {
        Vector3 worldPos = map.GeoToWorldPosition(geoLocation, true);
        worldPos.y += heightOffset;
        transform.position = worldPos;
    }
}
