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
    public GameObject polygonPrefab; // For background polygons

    [Header("JSON Files")]
    public string nodesFileName = "nodes.json";
    public string edgesFileName = "edges.json";
    public string polygonsFileName = "polygons.json"; // Optional polygon data

    [Header("Settings")]
    public bool enableDebugLogs = true;
    public List<string> targetCampusIds = new List<string>();
    public float nodeSize = 2.5f;
    public float heightOffset = 10f;

    [Header("Edge Settings")]
    public float edgeWidth = 0.5f;
    public Material edgeMaterial;

    [Header("Polygon Background Settings")]
    public bool enablePolygonBackgrounds = true;
    public float polygonHeightOffset = 0.5f;
    public Color defaultPolygonColor = new Color(0.2f, 0.8f, 0.2f, 0.3f);
    public Material polygonMaterial;

    private List<BarrierNode> spawnedNodes = new List<BarrierNode>();
    private List<BarrierEdge> spawnedEdges = new List<BarrierEdge>();
    private List<CampusPolygon> spawnedPolygons = new List<CampusPolygon>();
    private Dictionary<string, BarrierNode> nodeIdToComponent = new Dictionary<string, BarrierNode>();

    private bool hasSpawned = false;
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
        DebugLog("🚧 BarrierSpawner started");

        if (mapboxMap == null)
        {
            Debug.LogError("No AbstractMap found! Please assign mapboxMap in inspector");
            return;
        }

        StartCoroutine(WaitForMapAndSpawn());
    }

    private IEnumerator WaitForMapAndSpawn()
    {
        DebugLog("⏳ Waiting for map to be ready...");

        float timeout = 30f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (mapboxMap != null && mapboxMap.gameObject.activeInHierarchy)
            {
                DebugLog($"🗺️ Map seems ready after {elapsed:F1}s, attempting spawn...");
                break;
            }

            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;

            if (elapsed % 5f < 0.6f)
            {
                DebugLog($"⏳ Still waiting for map... ({elapsed:F1}s/{timeout}s)");
            }
        }

        if (elapsed >= timeout)
        {
            Debug.LogError("Map initialization timeout!");
            yield break;
        }

        yield return new WaitForSeconds(2f);

        // Spawn polygons first (background layer)
        if (enablePolygonBackgrounds)
        {
            yield return StartCoroutine(LoadAndSpawnPolygons());
        }

        yield return StartCoroutine(LoadAndSpawnBarrierNodes());
        yield return StartCoroutine(LoadAndSpawnBarrierEdges());
    }

    public IEnumerator LoadAndSpawnPolygons()
    {
        DebugLog("🔷 Starting LoadAndSpawnPolygons...");

        List<string> campusIds = GetTargetCampusIds();

        // Create simple polygon backgrounds for each campus
        foreach (string campusId in campusIds)
        {
            yield return StartCoroutine(CreateCampusPolygon(campusId));
        }

        DebugLog($"✅ Polygon spawning completed: {spawnedPolygons.Count} polygons created");
    }

    private IEnumerator CreateCampusPolygon(string campusId)
    {
        DebugLog($"🔷 Creating polygon for campus: {campusId}");

        // Get all barrier nodes for this campus to create a polygon boundary
        List<Vector2d> campusPoints = new List<Vector2d>();

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            nodesFileName,
            (jsonContent) =>
            {
                try
                {
                    Node[] nodes = JsonHelper.FromJson<Node>(jsonContent);
                    if (nodes != null)
                    {
                        var campusBarriers = nodes.Where(n =>
                            n != null && n.type == "barrier" && n.is_active &&
                            n.campus_id == campusId &&
                            IsValidCoordinate(n.latitude, n.longitude)
                        ).ToList();

                        foreach (var node in campusBarriers)
                        {
                            campusPoints.Add(new Vector2d(node.latitude, node.longitude));
                        }

                        DebugLog($"Found {campusPoints.Count} barrier points for campus {campusId}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error parsing nodes for polygon: {e.Message}");
                }
            },
            (error) =>
            {
                Debug.LogError($"Error loading nodes for polygon: {error}");
            }
        ));

        if (campusPoints.Count >= 3)
        {
            // Create convex hull or simple bounding polygon
            List<Vector2d> hullPoints = CreateConvexHull(campusPoints);

            if (hullPoints.Count >= 3)
            {
                GameObject polygonObj = new GameObject($"CampusPolygon_{campusId}");
                polygonObj.transform.SetParent(mapboxMap.transform);

                CampusPolygon polygonComponent = polygonObj.AddComponent<CampusPolygon>();
                polygonComponent.Initialize(mapboxMap, campusId, hullPoints, polygonHeightOffset, defaultPolygonColor, polygonMaterial);

                spawnedPolygons.Add(polygonComponent);
                DebugLog($"✅ Created polygon for campus {campusId} with {hullPoints.Count} points");
            }
        }
        else
        {
            DebugLog($"⚠️ Not enough points to create polygon for campus {campusId}");
        }
    }

    // Simple convex hull algorithm (Gift Wrapping)
    private List<Vector2d> CreateConvexHull(List<Vector2d> points)
    {
        if (points.Count < 3) return points;

        // Find the leftmost point
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
            DebugLog("Already spawning barriers");
            yield break;
        }

        isSpawning = true;
        DebugLog("🚧 Starting LoadAndSpawnBarrierNodes...");

        List<Node> barrierNodes = null;
        bool loadingComplete = false;
        string errorMessage = null;

        List<string> campusIds = null;
        try
        {
            campusIds = GetTargetCampusIds();
            if (campusIds.Count == 0)
            {
                DebugLog("No specific campus IDs set, will load all barriers");
            }
            ClearSpawnedNodes();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error in LoadAndSpawnBarrierNodes: {e.Message}");
            isSpawning = false;
            yield break;
        }

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            nodesFileName,
            (jsonContent) =>
            {
                try
                {
                    Node[] nodes = JsonHelper.FromJson<Node>(jsonContent);
                    if (nodes == null || nodes.Length == 0)
                    {
                        errorMessage = "Failed to parse nodes from JSON";
                        return;
                    }

                    barrierNodes = FilterBarrierNodes(nodes, campusIds);
                    loadingComplete = true;
                }
                catch (System.Exception e)
                {
                    errorMessage = $"Error parsing nodes JSON: {e.Message}";
                }
            },
            (error) =>
            {
                errorMessage = error;
            }
        ));

        while (!loadingComplete && string.IsNullOrEmpty(errorMessage))
        {
            yield return null;
        }

        if (!string.IsNullOrEmpty(errorMessage))
        {
            Debug.LogError($"❌ Error loading nodes: {errorMessage}");
            isSpawning = false;
            yield break;
        }

        if (barrierNodes == null || barrierNodes.Count == 0)
        {
            Debug.LogWarning("⚠️ No barrier nodes found matching criteria");
            isSpawning = false;
            yield break;
        }

        isSpawning = false;

        yield return StartCoroutine(SpawnNodes(barrierNodes));

        hasSpawned = true;
        Debug.Log($"✅ BarrierSpawner completed: {spawnedNodes.Count} barrier nodes spawned");
    }

    public IEnumerator LoadAndSpawnBarrierEdges()
    {
        DebugLog("🔗 Starting LoadAndSpawnBarrierEdges...");

        if (spawnedNodes.Count == 0)
        {
            Debug.LogWarning("⚠️ No nodes spawned yet, cannot create edges");
            yield break;
        }

        List<Edge> validEdges = null;
        bool loadingComplete = false;
        string errorMessage = null;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            edgesFileName,
            (jsonContent) =>
            {
                try
                {
                    Edge[] edges = JsonHelper.FromJson<Edge>(jsonContent);
                    if (edges == null || edges.Length == 0)
                    {
                        errorMessage = "No edges found in JSON";
                        return;
                    }

                    validEdges = FilterValidEdges(edges);
                    DebugLog($"🔗 Found {validEdges.Count} valid edges to spawn");
                    loadingComplete = true;
                }
                catch (System.Exception e)
                {
                    errorMessage = $"Error parsing edges JSON: {e.Message}";
                }
            },
            (error) =>
            {
                errorMessage = error;
            }
        ));

        while (!loadingComplete && string.IsNullOrEmpty(errorMessage))
        {
            yield return null;
        }

        if (!string.IsNullOrEmpty(errorMessage))
        {
            Debug.LogWarning($"⚠️ {errorMessage}");
            yield break;
        }

        if (validEdges != null && validEdges.Count > 0)
        {
            yield return StartCoroutine(SpawnEdges(validEdges));
            Debug.Log($"✅ EdgeSpawner completed: {spawnedEdges.Count} edges spawned");
        }
    }

    private List<string> GetTargetCampusIds()
    {
        if (targetCampusIds != null && targetCampusIds.Count > 0)
        {
            return targetCampusIds.Where(id => !string.IsNullOrEmpty(id)).ToList();
        }

        DebugLog("📱 No campus IDs set in inspector - will load all barriers");
        return new List<string>();
    }

    private List<Node> FilterBarrierNodes(Node[] allNodes, List<string> campusIds)
    {
        var filteredNodes = allNodes.Where(n =>
            n != null &&
            n.type == "barrier" &&
            n.is_active &&
            (campusIds.Count == 0 || campusIds.Contains(n.campus_id)) &&
            IsValidCoordinate(n.latitude, n.longitude)
        ).ToList();

        DebugLog($"🔍 Filtering process:");
        DebugLog($"  - Total nodes: {allNodes.Length}");
        DebugLog($"  - Barrier nodes: {allNodes.Count(n => n?.type == "barrier")}");
        DebugLog($"  - Active barriers: {allNodes.Count(n => n?.type == "barrier" && n.is_active)}");
        DebugLog($"  - Campus matched: {filteredNodes.Count}");

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

        DebugLog($"🔍 Edge filtering process:");
        DebugLog($"  - Total edges: {allEdges.Length}");
        DebugLog($"  - Active edges: {allEdges.Count(e => e?.is_active == true)}");
        DebugLog($"  - Valid connections: {validEdges.Count}");

        return validEdges;
    }

    private IEnumerator SpawnNodes(List<Node> nodes)
    {
        DebugLog($"🚧 Spawning {nodes.Count} barrier nodes...");

        int spawnedCount = 0;
        foreach (var node in nodes)
        {
            bool shouldYield = false;
            try
            {
                if (nodePrefab == null)
                {
                    Debug.LogError("❌ Node prefab is null!");
                    break;
                }

                GameObject nodeObj = Instantiate(nodePrefab, Vector3.zero, Quaternion.identity, mapboxMap.transform);
                nodeObj.name = $"BarrierNode_{node.node_id}_{node.name}";
                nodeObj.transform.localScale = Vector3.one * nodeSize;

                BarrierNode barrierComponent = nodeObj.AddComponent<BarrierNode>();
                barrierComponent.Initialize(mapboxMap, node, heightOffset);

                spawnedNodes.Add(barrierComponent);
                nodeIdToComponent[node.node_id] = barrierComponent;

                spawnedCount++;

                DebugLog($"🚧 Spawned barrier node: {node.name} (ID: {node.node_id})");

                if (spawnedCount % 10 == 0)
                {
                    shouldYield = true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Error spawning node {node.node_id}: {e.Message}");
            }
            if (shouldYield)
            {
                yield return null;
            }
        }

        DebugLog($"✅ Successfully spawned {spawnedCount} barrier nodes");
    }

    private IEnumerator SpawnEdges(List<Edge> edges)
    {
        DebugLog($"🔗 Spawning {edges.Count} edges...");

        int spawnedCount = 0;
        foreach (var edge in edges)
        {
            bool shouldYield = false;
            try
            {
                if (edgePrefab == null)
                {
                    Debug.LogError("❌ Edge prefab is null! Skipping edge creation.");
                    continue;
                }

                if (!nodeIdToComponent.TryGetValue(edge.from_node, out BarrierNode fromNode) ||
                    !nodeIdToComponent.TryGetValue(edge.to_node, out BarrierNode toNode))
                {
                    Debug.LogError($"❌ Could not find nodes for edge {edge.edge_id}: {edge.from_node} -> {edge.to_node}");
                    continue;
                }

                GameObject edgeObj = Instantiate(edgePrefab, Vector3.zero, Quaternion.identity, mapboxMap.transform);
                edgeObj.name = $"BarrierEdge_{edge.edge_id}_{edge.from_node}_to_{edge.to_node}";

                BarrierEdge edgeComponent = edgeObj.AddComponent<BarrierEdge>();
                edgeComponent.Initialize(mapboxMap, edge, fromNode, toNode, edgeWidth, heightOffset, edgeMaterial);

                spawnedEdges.Add(edgeComponent);
                spawnedCount++;

                DebugLog($"🔗 Spawned edge: {edge.edge_id} ({edge.from_node} -> {edge.to_node})");

                if (spawnedCount % 5 == 0)
                {
                    shouldYield = true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Error spawning edge {edge.edge_id}: {e.Message}");
            }

            if (shouldYield)
            {
                yield return null;
            }
        }

        DebugLog($"✅ Successfully spawned {spawnedCount} edges");
    }

    public void ClearSpawnedNodes()
    {
        DebugLog($"🧹 Clearing {spawnedNodes.Count} barrier nodes, {spawnedEdges.Count} edges, and {spawnedPolygons.Count} polygons");

        foreach (var polygon in spawnedPolygons)
        {
            if (polygon != null && polygon.gameObject != null)
            {
                DestroyImmediate(polygon.gameObject);
            }
        }
        spawnedPolygons.Clear();

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
        hasSpawned = false;

        DebugLog("✅ Cleared all barrier nodes, edges, and polygons");
    }

    public void ManualSpawn()
    {
        DebugLog("🔄 Manual spawn triggered");
        StartCoroutine(LoadAndSpawnBarrierNodes());
    }

    public void ManualSpawnEdges()
    {
        DebugLog("🔄 Manual edge spawn triggered");
        StartCoroutine(LoadAndSpawnBarrierEdges());
    }

    [System.Obsolete("Debug method - remove in production")]
    public void ForceResetSpawning()
    {
        DebugLog("🔄 Force resetting spawning state");
        isSpawning = false;
        hasSpawned = false;
        StopAllCoroutines();
        DebugLog($"✅ Reset complete");
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
        if (Application.isEditor && enableDebugLogs)
        {
            if (Input.GetKeyDown(KeyCode.B))
            {
                Debug.Log($"=== BARRIER SPAWNER STATUS ===");
                Debug.Log($"Has spawned: {hasSpawned}");
                Debug.Log($"Is spawning: {isSpawning}");
                Debug.Log($"Nodes spawned: {spawnedNodes.Count}");
                Debug.Log($"Edges spawned: {spawnedEdges.Count}");
                Debug.Log($"Polygons spawned: {spawnedPolygons.Count}");
                Debug.Log($"Map assigned: {mapboxMap != null}");
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ForceResetSpawning();
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                ManualSpawn();
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                ManualSpawnEdges();
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                ClearSpawnedNodes();
            }

            if (Input.GetKeyDown(KeyCode.U))
            {
                ForceUpdateAllEdges();
            }
        }
    }
}

// CORRECTED BarrierEdge class - exact same approach as your working PathRenderer
public class BarrierEdge : MonoBehaviour
{
    private AbstractMap map;
    private Edge edgeData;
    private BarrierNode fromNode;
    private BarrierNode toNode;
    private float baseWidth;
    private float heightOffset;

    // Cache for consistent scaling - EXACT same approach as PathRenderer
    private float referenceZoomLevel;
    private Vector3 referenceFromPos;
    private Vector3 referenceToPos;
    private float referenceDistance;
    private bool isInitialized = false;

    public Edge GetEdgeData() => edgeData;
    public BarrierNode GetFromNode() => fromNode;
    public BarrierNode GetToNode() => toNode;

    public void Initialize(AbstractMap mapReference, Edge edge, BarrierNode from, BarrierNode to,
                          float width, float height, Material material = null)
    {
        map = mapReference;
        edgeData = edge;
        fromNode = from;
        toNode = to;
        baseWidth = width;
        heightOffset = height;

        // EXACT same initialization as PathRenderer
        if (map != null)
        {
            referenceZoomLevel = map.Zoom;

            Node fromNodeData = fromNode.GetNodeData();
            Node toNodeData = toNode.GetNodeData();

            // Calculate reference positions and distance at current zoom - SAME AS PathRenderer
            referenceFromPos = map.GeoToWorldPosition(new Vector2d(fromNodeData.latitude, fromNodeData.longitude), false);
            referenceToPos = map.GeoToWorldPosition(new Vector2d(toNodeData.latitude, toNodeData.longitude), false);
            referenceDistance = Vector3.Distance(referenceFromPos, referenceToPos);

            isInitialized = true;

            Debug.Log($"BarrierEdge {edge.edge_id} initialized at zoom {referenceZoomLevel:F1} with reference distance {referenceDistance:F3}");
        }

        if (material != null)
        {
            ApplyMaterialToEdge(material);
        }

        // Initial transform update
        UpdateEdgeTransform();
    }

    private void ApplyMaterialToEdge(Material material)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.material = material;
        }
    }

    void LateUpdate()
    {
        if (map != null && fromNode != null && toNode != null && isInitialized)
        {
            // Update every few frames for smooth movement - SAME AS PathRenderer
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

        // Get current positions - SAME AS PathRenderer
        Vector3 fromPos = map.GeoToWorldPosition(new Vector2d(fromNodeData.latitude, fromNodeData.longitude), false);
        Vector3 toPos = map.GeoToWorldPosition(new Vector2d(toNodeData.latitude, toNodeData.longitude), false);

        // Apply height offset
        fromPos.y = heightOffset;
        toPos.y = heightOffset;

        // Calculate direction
        Vector3 direction = toPos - fromPos;
        float currentDistance = direction.magnitude;

        // Hide if nodes are too close (prevents visual glitches)
        if (currentDistance < 0.001f)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        // Position at the center between nodes - SAME AS PathRenderer
        Vector3 centerPos = (fromPos + toPos) * 0.5f;
        transform.position = centerPos;

        // Rotate to face the correct direction
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        // KEY FIX: Use the reference distance instead of current distance - EXACT SAME AS PathRenderer
        // This keeps the visual length consistent regardless of zoom
        float visualDistance = referenceDistance;

        // Apply the scale using the reference distance - EXACT SAME AS PathRenderer
        transform.localScale = new Vector3(baseWidth, baseWidth, visualDistance);

        // Debug info
        Debug.Log($"BarrierEdge {edgeData.edge_id}: current={currentDistance:F3}, reference={referenceDistance:F3}, zoom={map.Zoom:F1}");
    }

    public void ForceUpdate()
    {
        if (map != null && isInitialized)
        {
            UpdateEdgeTransform();
        }
    }

    // Optional: Debug visualization in Scene view
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

            // Show the center point
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.1f);
        }
    }
}

// Component that keeps a barrier node at its geographic location
public class BarrierNode : MonoBehaviour
{
    private AbstractMap map;
    private Node nodeData;
    private Vector2d geoLocation;
    private float heightOffset;

    public Node GetNodeData() => nodeData;
    public Vector2d GetGeoLocation() => geoLocation;

    public void Initialize(AbstractMap mapReference, Node node, float height)
    {
        map = mapReference;
        nodeData = node;
        geoLocation = new Vector2d(node.latitude, node.longitude);
        heightOffset = height;

        UpdatePosition();
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

// Component for creating 3D polygon backgrounds for campuses
public class CampusPolygon : MonoBehaviour
{
    private AbstractMap map;
    private string campusId;
    private List<Vector2d> geoPoints;
    private float heightOffset;
    private Color polygonColor;
    private Material polygonMaterial;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;

    public void Initialize(AbstractMap mapReference, string campus, List<Vector2d> points,
                          float height, Color color, Material material = null)
    {
        map = mapReference;
        campusId = campus;
        geoPoints = new List<Vector2d>(points);
        heightOffset = height;
        polygonColor = color;
        polygonMaterial = material;

        CreatePolygonMesh();
    }

    private void CreatePolygonMesh()
    {
        if (geoPoints.Count < 3) return;

        // Add MeshFilter and MeshRenderer components
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();

        // Create or assign material
        if (polygonMaterial != null)
        {
            meshRenderer.material = polygonMaterial;
        }
        else
        {
            // Create a default material with transparency support
            Material defaultMat = new Material(Shader.Find("Standard"));
            defaultMat.SetFloat("_Mode", 3); // Set rendering mode to Transparent
            defaultMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            defaultMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            defaultMat.SetInt("_ZWrite", 0);
            defaultMat.DisableKeyword("_ALPHATEST_ON");
            defaultMat.EnableKeyword("_ALPHABLEND_ON");
            defaultMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            defaultMat.renderQueue = 3000;
            defaultMat.color = polygonColor;
            meshRenderer.material = defaultMat;
        }

        UpdatePolygonMesh();
    }

    void Update()
    {
        if (map != null && geoPoints.Count >= 3)
        {
            // Update mesh less frequently for performance
            if (Time.frameCount % 5 == 0)
            {
                UpdatePolygonMesh();
            }
        }
    }

    private void UpdatePolygonMesh()
    {
        if (geoPoints.Count < 3) return;

        // Convert geographic points to world positions
        List<Vector3> worldPoints = new List<Vector3>();
        foreach (var geoPoint in geoPoints)
        {
            Vector3 worldPos = map.GeoToWorldPosition(geoPoint, false);
            worldPos.y = heightOffset;
            worldPoints.Add(worldPos);
        }

        // Calculate center point for positioning
        Vector3 center = Vector3.zero;
        foreach (var point in worldPoints)
        {
            center += point;
        }
        center /= worldPoints.Count;
        transform.position = center;

        // Convert world points to local coordinates relative to center
        List<Vector3> localPoints = new List<Vector3>();
        foreach (var worldPoint in worldPoints)
        {
            localPoints.Add(worldPoint - center);
        }

        // Create mesh using triangulation
        Mesh mesh = new Mesh();
        mesh.name = $"CampusPolygon_{campusId}";

        // Simple fan triangulation from center
        List<Vector3> vertices = new List<Vector3> { Vector3.zero }; // Center vertex
        vertices.AddRange(localPoints);

        List<int> triangles = new List<int>();
        for (int i = 1; i < vertices.Count - 1; i++)
        {
            triangles.Add(0); // Center
            triangles.Add(i);
            triangles.Add(i + 1);
        }
        // Close the polygon
        if (vertices.Count > 2)
        {
            triangles.Add(0);
            triangles.Add(vertices.Count - 1);
            triangles.Add(1);
        }

        // Generate UVs
        List<Vector2> uvs = new List<Vector2>();
        Vector3 bounds = GetLocalBounds(localPoints);
        foreach (var vertex in vertices)
        {
            float u = bounds.x != 0 ? (vertex.x - bounds.x * -0.5f) / bounds.x : 0.5f;
            float v = bounds.z != 0 ? (vertex.z - bounds.z * -0.5f) / bounds.z : 0.5f;
            uvs.Add(new Vector2(u, v));
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
    }

    private Vector3 GetLocalBounds(List<Vector3> points)
    {
        if (points.Count == 0) return Vector3.one;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var point in points)
        {
            if (point.x < minX) minX = point.x;
            if (point.x > maxX) maxX = point.x;
            if (point.z < minZ) minZ = point.z;
            if (point.z > maxZ) maxZ = point.z;
        }

        return new Vector3(maxX - minX, 0, maxZ - minZ);
    }

    // Optional: Debug visualization
    void OnDrawGizmosSelected()
    {
        if (geoPoints != null && geoPoints.Count >= 3 && map != null)
        {
            Gizmos.color = polygonColor;

            List<Vector3> worldPoints = new List<Vector3>();
            foreach (var geoPoint in geoPoints)
            {
                Vector3 worldPos = map.GeoToWorldPosition(geoPoint, false);
                worldPos.y = heightOffset;
                worldPoints.Add(worldPos);
            }

            // Draw polygon outline
            for (int i = 0; i < worldPoints.Count; i++)
            {
                Vector3 current = worldPoints[i];
                Vector3 next = worldPoints[(i + 1) % worldPoints.Count];
                Gizmos.DrawLine(current, next);
                Gizmos.DrawWireSphere(current, 0.5f);
            }
        }
    }
}