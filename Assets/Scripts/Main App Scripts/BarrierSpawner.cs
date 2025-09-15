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
    public GameObject edgePrefab; // Add this for edge connections

    [Header("JSON Files")]
    public string nodesFileName = "nodes.json";
    public string edgesFileName = "edges.json"; // Add this

    [Header("Settings")]
    public bool enableDebugLogs = true;
    public List<string> targetCampusIds = new List<string>();
    public float nodeSize = 2.5f;
    public float heightOffset = 10f;
    
    [Header("Edge Settings")]
    public float edgeWidth = 0.5f;
    public Material edgeMaterial; // Optional: custom material for edges

    // Track spawned nodes and edges with their location components
    private List<BarrierNode> spawnedNodes = new List<BarrierNode>();
    private List<BarrierEdge> spawnedEdges = new List<BarrierEdge>();
    private Dictionary<string, BarrierNode> nodeIdToComponent = new Dictionary<string, BarrierNode>();

    private bool hasSpawned = false;
    private bool isSpawning = false;

    void Awake()
    {
        // Find map if not assigned
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
            Debug.LogError("❌ No AbstractMap found! Please assign mapboxMap in inspector");
            return;
        }

        DebugLog("📍 Found AbstractMap, starting automatic spawn process");
        
        // Start the spawn process immediately
        StartCoroutine(WaitForMapAndSpawn());
    }

    private IEnumerator WaitForMapAndSpawn()
    {
        DebugLog("⏳ Waiting for map to be ready...");
        
        // Wait for map initialization
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
            Debug.LogError("❌ Map initialization timeout!");
            yield break;
        }

        // Additional delay to ensure map is fully ready
        yield return new WaitForSeconds(2f);
        
        // Start spawning
        yield return StartCoroutine(LoadAndSpawnBarrierNodes());
        
        // After nodes are spawned, spawn edges
        yield return StartCoroutine(LoadAndSpawnBarrierEdges());
    }

    public IEnumerator LoadAndSpawnBarrierNodes()
    {
        if (isSpawning)
        {
            DebugLog("⚠️ Already spawning barriers");
            yield break;
        }

        isSpawning = true;
        DebugLog("🚧 Starting LoadAndSpawnBarrierNodes...");

        List<Node> barrierNodes = null;
        try
        {
            // Get campus IDs to spawn
            List<string> campusIds = GetTargetCampusIds();
            if (campusIds.Count == 0)
            {
                Debug.LogError("❌ No campus IDs found in data");
                yield break;
            }

            DebugLog($"🏫 Target campus IDs: {string.Join(", ", campusIds)}");

            // Clear existing objects first
            ClearSpawnedNodes();

            // Load and parse JSON
            Node[] nodes = LoadNodesFromJSON();
            if (nodes == null || nodes.Length == 0)
            {
                Debug.LogError("❌ Failed to load nodes from JSON");
                yield break;
            }

            // Filter barrier nodes
            barrierNodes = FilterBarrierNodes(nodes, campusIds);
            DebugLog($"🚧 Found {barrierNodes.Count} barrier nodes to spawn");

            if (barrierNodes.Count == 0)
            {
                Debug.LogWarning("⚠️ No barrier nodes found matching criteria");
                yield break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error in LoadAndSpawnBarrierNodes: {e.Message}");
            yield break;
        }
        finally
        {
            isSpawning = false;
        }

        // Spawn the nodes outside the try-catch block
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
        bool shouldSpawnEdges = false;
        try
        {
            // Load edges from JSON
            Edge[] edges = LoadEdgesFromJSON();
            if (edges == null || edges.Length == 0)
            {
                Debug.LogWarning("⚠️ No edges found in JSON");
                yield break;
            }

            // Filter active edges that connect to our spawned nodes
            validEdges = FilterValidEdges(edges);
            DebugLog($"🔗 Found {validEdges.Count} valid edges to spawn");

            if (validEdges.Count == 0)
            {
                Debug.LogWarning("⚠️ No valid edges found for spawned nodes");
                yield break;
            }

            shouldSpawnEdges = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error in LoadAndSpawnBarrierEdges: {e.Message}");
        }

        if (shouldSpawnEdges && validEdges != null)
        {
            yield return StartCoroutine(SpawnEdges(validEdges));
            Debug.Log($"✅ EdgeSpawner completed: {spawnedEdges.Count} edges spawned");
        }
    }

    private Edge[] LoadEdgesFromJSON()
    {
        string edgesPath = Path.Combine(Application.streamingAssetsPath, edgesFileName);
        DebugLog($"📂 Loading edges from: {edgesPath}");

        if (!File.Exists(edgesPath))
        {
            Debug.LogError($"❌ Edges file not found: {edgesPath}");
            return null;
        }

        try
        {
            string jsonContent = File.ReadAllText(edgesPath);
            DebugLog($"📄 Read {jsonContent.Length} characters from edges file");
            
            Edge[] edges = JsonHelper.FromJson<Edge>(jsonContent);
            DebugLog($"📊 Parsed {edges?.Length ?? 0} edges from JSON");
            
            return edges;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error loading edges JSON: {e.Message}");
            return null;
        }
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

                // Get the connected nodes
                if (!nodeIdToComponent.TryGetValue(edge.from_node, out BarrierNode fromNode) ||
                    !nodeIdToComponent.TryGetValue(edge.to_node, out BarrierNode toNode))
                {
                    Debug.LogError($"❌ Could not find nodes for edge {edge.edge_id}: {edge.from_node} -> {edge.to_node}");
                    continue;
                }

                // Create the edge GameObject
                GameObject edgeObj = Instantiate(edgePrefab, Vector3.zero, Quaternion.identity, mapboxMap.transform);
                edgeObj.name = $"BarrierEdge_{edge.edge_id}_{edge.from_node}_to_{edge.to_node}";

                // Add the edge component
                BarrierEdge edgeComponent = edgeObj.AddComponent<BarrierEdge>();
                edgeComponent.Initialize(mapboxMap, edge, fromNode, toNode, edgeWidth, heightOffset, edgeMaterial);
                
                spawnedEdges.Add(edgeComponent);
                spawnedCount++;

                DebugLog($"🔗 Spawned edge: {edge.edge_id} ({edge.from_node} -> {edge.to_node})");

                // Mark for yielding periodically to avoid frame drops
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

    private List<string> GetTargetCampusIds()
    {
        // If specific campus IDs are set in inspector, use those
        if (targetCampusIds != null && targetCampusIds.Count > 0)
        {
            return targetCampusIds.Where(id => !string.IsNullOrEmpty(id)).ToList();
        }

        // Otherwise, get all available campus IDs from the data
        return GetAllCampusIdsFromData();
    }

    private List<string> GetAllCampusIdsFromData()
    {
        string nodesPath = Path.Combine(Application.streamingAssetsPath, nodesFileName);
        DebugLog($"📂 Looking for nodes file at: {nodesPath}");
        
        if (!File.Exists(nodesPath))
        {
            Debug.LogError($"❌ Nodes file not found: {nodesPath}");
            return new List<string>();
        }

        try
        {
            string jsonContent = File.ReadAllText(nodesPath);
            Node[] nodes = JsonHelper.FromJson<Node>(jsonContent);
            
            if (nodes == null || nodes.Length == 0)
            {
                Debug.LogError("❌ No nodes found in JSON file");
                return new List<string>();
            }

            var campusIds = nodes
                .Where(n => n != null && n.type == "barrier" && n.is_active && !string.IsNullOrEmpty(n.campus_id))
                .Select(n => n.campus_id)
                .Distinct()
                .ToList();

            DebugLog($"🏫 Found {campusIds.Count} unique campus IDs with barriers");
            return campusIds;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error reading nodes file: {e.Message}");
            return new List<string>();
        }
    }

    private Node[] LoadNodesFromJSON()
    {
        string nodesPath = Path.Combine(Application.streamingAssetsPath, nodesFileName);
        DebugLog($"📂 Loading nodes from: {nodesPath}");

        if (!File.Exists(nodesPath))
        {
            Debug.LogError($"❌ Nodes file not found: {nodesPath}");
            return null;
        }

        try
        {
            string jsonContent = File.ReadAllText(nodesPath);
            DebugLog($"📄 Read {jsonContent.Length} characters from nodes file");
            
            Node[] nodes = JsonHelper.FromJson<Node>(jsonContent);
            DebugLog($"📊 Parsed {nodes?.Length ?? 0} nodes from JSON");
            
            return nodes;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error loading nodes JSON: {e.Message}");
            return null;
        }
    }

    private List<Node> FilterBarrierNodes(Node[] allNodes, List<string> campusIds)
    {
        var filteredNodes = allNodes.Where(n =>
            n != null &&
            n.type == "barrier" &&
            n.is_active &&
            campusIds.Contains(n.campus_id) &&
            IsValidCoordinate(n.latitude, n.longitude)
        ).ToList();

        DebugLog($"🔍 Filtering process:");
        DebugLog($"  - Total nodes: {allNodes.Length}");
        DebugLog($"  - Barrier nodes: {allNodes.Count(n => n?.type == "barrier")}");
        DebugLog($"  - Active barriers: {allNodes.Count(n => n?.type == "barrier" && n.is_active)}");
        DebugLog($"  - Campus matched: {filteredNodes.Count}");

        return filteredNodes;
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

                // Create the barrier node GameObject
                GameObject nodeObj = Instantiate(nodePrefab, Vector3.zero, Quaternion.identity, mapboxMap.transform);
                nodeObj.name = $"BarrierNode_{node.node_id}_{node.name}";
                nodeObj.transform.localScale = Vector3.one * nodeSize;

                // Add the location-tracking component
                BarrierNode barrierComponent = nodeObj.AddComponent<BarrierNode>();
                barrierComponent.Initialize(mapboxMap, node, heightOffset);
                
                spawnedNodes.Add(barrierComponent);
                
                // Add to lookup dictionary for edge connections
                nodeIdToComponent[node.node_id] = barrierComponent;
                
                spawnedCount++;

                DebugLog($"🚧 Spawned barrier node: {node.name} (ID: {node.node_id})");
                DebugLog($"   Geo: ({node.latitude}, {node.longitude})");

                // Mark for yielding periodically to avoid frame drops
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

    public void ClearSpawnedNodes()
    {
        DebugLog($"🧹 Clearing {spawnedNodes.Count} barrier nodes and {spawnedEdges.Count} edges");

        // Clear edges first
        foreach (var edge in spawnedEdges)
        {
            if (edge != null && edge.gameObject != null)
            {
                DestroyImmediate(edge.gameObject);
            }
        }
        spawnedEdges.Clear();

        // Clear nodes
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
        
        DebugLog("✅ Cleared all barrier nodes and edges");
    }

    // Manual spawn methods for testing
    [System.Obsolete("Debug method - remove in production")]
    public void ForceResetSpawning()
    {
        DebugLog("🔄 Force resetting spawning state");
        isSpawning = false;
        hasSpawned = false;
        StopAllCoroutines();
        DebugLog($"✅ Reset complete");
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
        // Debug controls
        if (Application.isEditor && enableDebugLogs)
        {
            if (Input.GetKeyDown(KeyCode.B))
            {
                Debug.Log($"=== BARRIER SPAWNER STATUS ===");
                Debug.Log($"Has spawned: {hasSpawned}");
                Debug.Log($"Is spawning: {isSpawning}");
                Debug.Log($"Nodes spawned: {spawnedNodes.Count}");
                Debug.Log($"Edges spawned: {spawnedEdges.Count}");
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
        
        // Set initial position
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
        // Convert geo coordinate to current world position
        Vector3 worldPos = map.GeoToWorldPosition(geoLocation, true);
        worldPos.y += heightOffset;
        
        // Update our position to stay locked to geographic location
        transform.position = worldPos;
    }
}

// Component that maintains a 3D connection between two barrier nodes
public class BarrierEdge : MonoBehaviour
{
    private AbstractMap map;
    private Edge edgeData;
    private BarrierNode fromNode;
    private BarrierNode toNode;
    private float heightOffset;
    private float edgeWidth;
    
    // Cache for consistent scaling - same fix as PathEdge
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
        heightOffset = height;
        edgeWidth = width;
        
        // KEY FIX: Calculate reference distance at initialization using local coordinates
        if (map != null && fromNode != null && toNode != null)
        {
            // Get the node data to calculate reference distance
            Node fromNodeData = fromNode.GetNodeData();
            Node toNodeData = toNode.GetNodeData();
            
            if (fromNodeData != null && toNodeData != null)
            {
                // Use local coordinates (false parameter) to get consistent distance
                Vector3 refFromPos = map.GeoToWorldPosition(new Vector2d(fromNodeData.latitude, fromNodeData.longitude), false);
                Vector3 refToPos = map.GeoToWorldPosition(new Vector2d(toNodeData.latitude, toNodeData.longitude), false);
                referenceDistance = Vector3.Distance(refFromPos, refToPos);
                
                isInitialized = true;
                Debug.Log($"BarrierEdge {edge.edge_id} initialized with reference distance {referenceDistance:F3}");
            }
        }
        
        // Set initial scale based on edge width
        transform.localScale = new Vector3(edgeWidth, transform.localScale.y, transform.localScale.z);
        
        // Apply custom material if provided
        if (material != null)
        {
            ApplyMaterialToEdge(material);
        }
        
        UpdateEdgeTransform();
    }
    
    private void ApplyMaterialToEdge(Material material)
    {
        // Apply material to all renderers in the edge prefab
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.material = material;
        }
    }
    
    void Update()
    {
        if (map != null && fromNode != null && toNode != null && isInitialized)
        {
            // Update every few frames for performance
            if (Time.frameCount % 2 == 0)
            {
                UpdateEdgeTransform();
            }
        }
    }
    
    void UpdateEdgeTransform()
    {
        if (fromNode == null || toNode == null || !isInitialized) return;
        
        Vector3 fromPos = fromNode.transform.position;
        Vector3 toPos = toNode.transform.position;
        
        // Calculate direction
        Vector3 direction = toPos - fromPos;
        
        if (direction.magnitude < 0.01f)
        {
            gameObject.SetActive(false);
            return;
        }
        
        gameObject.SetActive(true);
        
        // Get node sizes for edge adjustment
        float fromNodeRadius = fromNode.transform.lossyScale.x * 0.5f;
        float toNodeRadius = toNode.transform.lossyScale.x * 0.5f;
        
        // Add a small buffer to prevent overlap
        float buffer = 0.1f;
        fromNodeRadius += buffer;
        toNodeRadius += buffer;
        
        // Adjust positions to stop at node edges
        Vector3 directionNormalized = direction.normalized;
        Vector3 adjustedFromPos = fromPos + directionNormalized * fromNodeRadius;
        Vector3 adjustedToPos = toPos - directionNormalized * toNodeRadius;
        
        // Calculate how much we need to subtract from reference distance for node radii
        float totalRadiusOffset = fromNodeRadius + toNodeRadius;
        float adjustedReferenceDistance = Mathf.Max(0.1f, referenceDistance - totalRadiusOffset);
        
        // Position at center between adjusted positions
        Vector3 centerPos = Vector3.Lerp(adjustedFromPos, adjustedToPos, 0.5f);
        centerPos.y = heightOffset; // Set proper height
        
        transform.position = centerPos;
        
        // Rotate to face the correct direction
        transform.rotation = Quaternion.LookRotation(directionNormalized);
        transform.localScale = new Vector3(edgeWidth, transform.localScale.y, adjustedReferenceDistance);
    }
    
    // Force update method for zoom changes
    public void ForceUpdate()
    {
        if (map != null && isInitialized)
        {
            UpdateEdgeTransform();
        }
    }
}
