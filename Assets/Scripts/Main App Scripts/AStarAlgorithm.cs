using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mapbox.Utils;
using Mapbox.Unity.Map;

public class AStarPathfinding : MonoBehaviour
{
    [Header("Mapbox")]
    public AbstractMap mapboxMap;

    [Header("Settings")]
    public bool enableDebugLogs = true;

    [Header("Debug Visualization")]
    public bool showPathInScene = true;
    public Color pathDebugColor = Color.green;
    public Color startNodeColor = Color.blue;
    public Color endNodeColor = Color.red;
    public float debugSphereSize = 1f;
    public float debugLineWidth = 0.5f;
    public float pathHeightOffset = 2f;

    // Current map data
    private string currentMapId;
    private List<string> currentCampusIds = new List<string>();

    // Graph data
    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>();
    private Dictionary<string, List<GraphEdge>> adjacencyList = new Dictionary<string, List<GraphEdge>>();

    // Current path
    private List<PathNode> currentPath = new List<PathNode>();
    private Node startNode;
    private Node endNode;
    private float totalPathDistance = 0f;

    // Pathfinding state
    private bool isCalculating = false;

    void Awake()
    {
        if (mapboxMap == null)
        {
            mapboxMap = FindObjectOfType<AbstractMap>();
        }
    }

    void Start()
    {
        DebugLog("🧭 A* Pathfinding initialized");

        if (mapboxMap == null)
        {
            Debug.LogError("❌ No AbstractMap found! Please assign mapboxMap in inspector");
            return;
        }

        // Subscribe to MapManager events
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

    #region MapManager Integration

    public void SetCurrentMapData(string mapId, List<string> campusIds)
    {
        DebugLog($"🗺️ Setting map data - Map ID: {mapId}, Campuses: {string.Join(", ", campusIds)}");
        currentMapId = mapId;
        currentCampusIds.Clear();
        if (campusIds != null)
        {
            currentCampusIds.AddRange(campusIds);
        }
    }

    private void OnMapChanged(MapInfo mapInfo)
    {
        DebugLog($"🔄 Map changed to: {mapInfo.map_name}");
        SetCurrentMapData(mapInfo.map_id, mapInfo.campus_included);
        ClearCurrentPath();
    }

    private void OnMapLoadingStarted()
    {
        DebugLog("🧹 Map loading started - clearing pathfinding data");
        ClearGraphData();
        ClearCurrentPath();
    }

    public IEnumerator LoadGraphDataForMap(string mapId, List<string> campusIds)
    {
        DebugLog($"📊 Loading graph data for map: {mapId}");
        SetCurrentMapData(mapId, campusIds);
        yield return StartCoroutine(LoadGraphData());
    }

    #endregion

    #region Graph Data Loading

    private IEnumerator LoadGraphData()
    {
        if (string.IsNullOrEmpty(currentMapId))
        {
            Debug.LogError("❌ No current map ID set");
            yield break;
        }

        DebugLog($"📂 Loading graph data for map: {currentMapId}");

        ClearGraphData();

        // Load nodes
        yield return StartCoroutine(LoadNodes());

        if (allNodes.Count == 0)
        {
            Debug.LogError("❌ No nodes loaded - cannot build graph");
            yield break;
        }

        // Load edges and build adjacency list
        yield return StartCoroutine(LoadEdges());

        DebugLog($"✅ Graph loaded: {allNodes.Count} nodes, {adjacencyList.Sum(kvp => kvp.Value.Count)} edges");
    }

    private IEnumerator LoadNodes()
    {
        string fileName = GetNodesFileName();
        DebugLog($"📂 Loading nodes from: {fileName}");

        bool loadCompleted = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonContent) =>
            {
                try
                {
                    Node[] nodes = JsonHelper.FromJson<Node>(jsonContent);
                    DebugLog($"📊 Parsed {nodes?.Length ?? 0} nodes");

                    allNodes.Clear();

                    // ADD THIS DEBUG - Check if ND-021 is in the parsed data
                    var nd021Raw = nodes.FirstOrDefault(n => n != null && n.node_id == "ND-021");
                    if (nd021Raw != null)
                    {
                        DebugLog($"🔍 ND-021 found in JSON:");
                        DebugLog($"   - is_active: {nd021Raw.is_active}");
                        DebugLog($"   - type: {nd021Raw.type}");
                        DebugLog($"   - campus_id: {nd021Raw.campus_id}");
                        DebugLog($"   - lat: {nd021Raw.latitude}, lon: {nd021Raw.longitude}");
                        DebugLog($"   - IsValidCoordinate: {IsValidCoordinate(nd021Raw.latitude, nd021Raw.longitude)}");
                        DebugLog($"   - currentCampusIds: [{string.Join(", ", currentCampusIds)}]");
                        DebugLog($"   - Campus match: {currentCampusIds == null || currentCampusIds.Count == 0 || currentCampusIds.Contains(nd021Raw.campus_id)}");
                    }
                    else
                    {
                        DebugLog($"❌ ND-021 NOT in parsed JSON array");
                    }

                    // Load all active nodes (infrastructure, pathway, intermediate)
                    var validNodes = nodes.Where(n =>
                        n != null &&
                        n.is_active &&
                        (n.type == "infrastructure" || n.type == "pathway" || n.type == "intermediate") &&
                        (currentCampusIds == null || currentCampusIds.Count == 0 || currentCampusIds.Contains(n.campus_id)) &&
                        IsValidCoordinate(n.latitude, n.longitude)
                    ).ToList();

                    foreach (var node in validNodes)
                    {
                        allNodes[node.node_id] = node;
                    }

                    // ADD THIS DEBUG - Check if ND-021 made it to allNodes
                    if (allNodes.ContainsKey("ND-021"))
                    {
                        DebugLog($"✅ ND-021 successfully added to allNodes");
                    }
                    else
                    {
                        DebugLog($"❌ ND-021 was FILTERED OUT during loading");
                    }

                    DebugLog($"✅ Loaded {allNodes.Count} valid nodes");
                    DebugLog($"   - Infrastructure: {allNodes.Values.Count(n => n.type == "infrastructure")}");
                    DebugLog($"   - Pathway: {allNodes.Values.Count(n => n.type == "pathway")}");
                    DebugLog($"   - Intermediate: {allNodes.Values.Count(n => n.type == "intermediate")}");

                    loadCompleted = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ Error parsing nodes: {e.Message}");
                    loadCompleted = true;
                }
            },
            (error) =>
            {
                Debug.LogError($"❌ Error loading nodes: {error}");
                loadCompleted = true;
            }
        ));

        yield return new WaitUntil(() => loadCompleted);
    }

    private IEnumerator LoadEdges()
    {
        string fileName = GetEdgesFileName();
        DebugLog($"📂 Loading edges from: {fileName}");

        bool loadCompleted = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonContent) =>
            {
                try
                {
                    Edge[] edges = JsonHelper.FromJson<Edge>(jsonContent);
                    DebugLog($"📊 Parsed {edges?.Length ?? 0} edges");

                    BuildAdjacencyList(edges);

                    DebugLog($"✅ Built adjacency list with {adjacencyList.Count} nodes");
                    loadCompleted = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ Error parsing edges: {e.Message}");
                    loadCompleted = true;
                }
            },
            (error) =>
            {
                Debug.LogError($"❌ Error loading edges: {error}");
                loadCompleted = true;
            }
        ));

        yield return new WaitUntil(() => loadCompleted);
    }

    private void BuildAdjacencyList(Edge[] edges)
    {
        adjacencyList.Clear();

        // Initialize adjacency list for all nodes
        foreach (var nodeId in allNodes.Keys)
        {
            adjacencyList[nodeId] = new List<GraphEdge>();
        }

        int addedEdges = 0;

        // Build bidirectional edges using pre-calculated distances
        foreach (var edge in edges)
        {
            if (edge == null || !edge.is_active) continue;

            // Check if both nodes exist
            if (!allNodes.ContainsKey(edge.from_node) || !allNodes.ContainsKey(edge.to_node))
                continue;

            // Use the distance from the edge data (already in meters)
            float distance = edge.distance;

            // Add forward edge
            adjacencyList[edge.from_node].Add(new GraphEdge
            {
                toNodeId = edge.to_node,
                cost = distance,
                edgeData = edge
            });

            // Add reverse edge (for bidirectional pathfinding)
            adjacencyList[edge.to_node].Add(new GraphEdge
            {
                toNodeId = edge.from_node,
                cost = distance,
                edgeData = edge
            });

            addedEdges += 2; // Count both directions
        }

        DebugLog($"📊 Adjacency list built: {addedEdges} total connections (bidirectional)");
    }

    #endregion

    #region A* Pathfinding Algorithm

    /// <summary>
    /// Find shortest path from start infrastructure to end infrastructure
    /// </summary>
    public IEnumerator FindPath(string startNodeId, string endNodeId)
    {
        if (isCalculating)
        {
            Debug.LogWarning("⚠️ Pathfinding already in progress");
            yield break;
        }

        isCalculating = true;
        DebugLog($"🧭 Finding path from {startNodeId} to {endNodeId}");

        // Clear previous path
        ClearCurrentPath();

        // Validate nodes exist
        if (!allNodes.ContainsKey(startNodeId))
        {
            Debug.LogError($"❌ Start node not found: {startNodeId}");
            isCalculating = false;
            yield break;
        }

        if (!allNodes.ContainsKey(endNodeId))
        {
            Debug.LogError($"❌ End node not found: {endNodeId}");
            isCalculating = false;
            yield break;
        }

        startNode = allNodes[startNodeId];
        endNode = allNodes[endNodeId];

        // Validate start and end are infrastructure (outdoor pathfinding)
        if (startNode.type != "infrastructure")
        {
            Debug.LogError($"❌ Start node must be infrastructure type, got: {startNode.type}");
            isCalculating = false;
            yield break;
        }

        if (endNode.type != "infrastructure")
        {
            Debug.LogError($"❌ End node must be infrastructure type, got: {endNode.type}");
            isCalculating = false;
            yield break;
        }

        DebugLog($"📍 Start: {startNode.name} ({startNode.node_id}) at ({startNode.x_coordinate:F2}, {startNode.y_coordinate:F2})");
        DebugLog($"📍 End: {endNode.name} ({endNode.node_id}) at ({endNode.x_coordinate:F2}, {endNode.y_coordinate:F2})");

        // Run A* algorithm
        List<string> path = AStar(startNodeId, endNodeId);

        if (path == null || path.Count == 0)
        {
            Debug.LogWarning($"⚠️ No path found from {startNodeId} to {endNodeId}");
            isCalculating = false;
            yield break;
        }

        // Convert node IDs to PathNode objects
        currentPath = ConvertToPathNodes(path);

        // Calculate total distance from edges
        totalPathDistance = CalculateTotalDistance(path);

        DebugLog($"✅ Path found!");
        DebugLog($"   - Nodes: {path.Count}");
        DebugLog($"   - Distance: {totalPathDistance:F2}m ({totalPathDistance / 1000:F2}km)");
        DebugLog($"   - Estimated walking time: {(totalPathDistance / 1.4f / 60):F1} minutes"); // Average walking speed ~1.4 m/s
        DebugLog($"🗺️ Path: {string.Join(" → ", path.Select(id => allNodes[id].name))}");

        isCalculating = false;
    }

    private List<string> AStar(string startId, string goalId)
    {
        // Priority queue (open set)
        var openSet = new PriorityQueue<AStarNode>();
        var openSetHash = new HashSet<string>();

        // Closed set
        var closedSet = new HashSet<string>();

        // Cost from start
        var gScore = new Dictionary<string, float>();

        // Estimated total cost
        var fScore = new Dictionary<string, float>();

        // Parent tracking for path reconstruction
        var cameFrom = new Dictionary<string, string>();

        // Initialize start node
        gScore[startId] = 0;
        fScore[startId] = Heuristic(allNodes[startId], allNodes[goalId]);

        openSet.Enqueue(new AStarNode
        {
            nodeId = startId,
            fScore = fScore[startId]
        });
        openSetHash.Add(startId);

        int iterations = 0;
        int maxIterations = 10000; // Safety limit

        while (openSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;

            // Get node with lowest fScore
            AStarNode current = openSet.Dequeue();
            openSetHash.Remove(current.nodeId);

            // Goal reached!
            if (current.nodeId == goalId)
            {
                DebugLog($"🎯 Goal reached in {iterations} iterations");
                return ReconstructPath(cameFrom, current.nodeId);
            }

            closedSet.Add(current.nodeId);

            // Check all neighbors
            if (!adjacencyList.ContainsKey(current.nodeId))
                continue;

            foreach (var edge in adjacencyList[current.nodeId])
            {
                string neighborId = edge.toNodeId;

                if (closedSet.Contains(neighborId))
                    continue;

                // Calculate tentative gScore using actual edge distance
                float tentativeGScore = gScore[current.nodeId] + edge.cost;

                // Discover new node or find better path
                if (!gScore.ContainsKey(neighborId) || tentativeGScore < gScore[neighborId])
                {
                    cameFrom[neighborId] = current.nodeId;
                    gScore[neighborId] = tentativeGScore;
                    fScore[neighborId] = gScore[neighborId] + Heuristic(allNodes[neighborId], allNodes[goalId]);

                    if (!openSetHash.Contains(neighborId))
                    {
                        openSet.Enqueue(new AStarNode
                        {
                            nodeId = neighborId,
                            fScore = fScore[neighborId]
                        });
                        openSetHash.Add(neighborId);
                    }
                }
            }
        }

        if (iterations >= maxIterations)
        {
            Debug.LogWarning($"⚠️ A* reached maximum iterations ({maxIterations})");
        }

        // No path found
        return null;
    }

    private List<string> ReconstructPath(Dictionary<string, string> cameFrom, string current)
    {
        var path = new List<string> { current };

        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }

        return path;
    }

    #endregion

    #region Heuristic and Distance Calculations

    /// <summary>
    /// Heuristic function: Euclidean distance using x_coordinate and y_coordinate
    /// This gives us a straight-line distance estimate in the local coordinate system
    /// </summary>
    private float Heuristic(Node a, Node b)
    {
        float dx = b.x_coordinate - a.x_coordinate;
        float dy = b.y_coordinate - a.y_coordinate;

        // Euclidean distance
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Calculate total distance of the path using actual edge distances
    /// </summary>
    private float CalculateTotalDistance(List<string> path)
    {
        float total = 0f;

        for (int i = 0; i < path.Count - 1; i++)
        {
            string fromId = path[i];
            string toId = path[i + 1];

            // Find the edge between these nodes
            if (adjacencyList.ContainsKey(fromId))
            {
                var edge = adjacencyList[fromId].FirstOrDefault(e => e.toNodeId == toId);
                if (edge != null)
                {
                    total += edge.cost; // Use actual edge distance
                }
            }
        }

        return total;
    }

    #endregion

    #region Path Conversion and Management

    private List<PathNode> ConvertToPathNodes(List<string> nodeIds)
    {
        var pathNodes = new List<PathNode>();

        for (int i = 0; i < nodeIds.Count; i++)
        {
            string nodeId = nodeIds[i];
            if (allNodes.TryGetValue(nodeId, out Node node))
            {
                // Get edge distance to next node (if exists)
                float distanceToNext = 0f;
                if (i < nodeIds.Count - 1)
                {
                    string nextNodeId = nodeIds[i + 1];
                    if (adjacencyList.ContainsKey(nodeId))
                    {
                        var edge = adjacencyList[nodeId].FirstOrDefault(e => e.toNodeId == nextNodeId);
                        if (edge != null)
                        {
                            distanceToNext = edge.cost;
                        }
                    }
                }

                pathNodes.Add(new PathNode
                {
                    node = node,
                    worldPosition = GetWorldPosition(node),
                    isStart = i == 0,
                    isEnd = i == nodeIds.Count - 1,
                    distanceToNext = distanceToNext
                });
            }
        }

        return pathNodes;
    }

    private Vector3 GetWorldPosition(Node node)
    {
        if (mapboxMap == null) return Vector3.zero;

        Vector3 pos = mapboxMap.GeoToWorldPosition(
            new Vector2d(node.latitude, node.longitude),
            false
        );
        pos.y = pathHeightOffset;
        return pos;
    }

    public void ClearCurrentPath()
    {
        currentPath.Clear();
        startNode = null;
        endNode = null;
        totalPathDistance = 0f;
        DebugLog("🧹 Cleared current path");
    }

    private void ClearGraphData()
    {
        allNodes.Clear();
        adjacencyList.Clear();
        DebugLog("🧹 Cleared graph data");
    }

    #endregion

    #region Public Access Methods

    public List<PathNode> GetCurrentPath()
    {
        return new List<PathNode>(currentPath);
    }

    public bool HasPath()
    {
        return currentPath != null && currentPath.Count > 0;
    }

    public float GetTotalDistance()
    {
        return totalPathDistance;
    }

    public string GetFormattedDistance()
    {
        if (totalPathDistance < 1000)
            return $"{totalPathDistance:F1}m";
        else
            return $"{totalPathDistance / 1000:F2}km";
    }

    public string GetEstimatedWalkingTime()
    {
        float walkingSpeed = 1.4f; // m/s (average walking speed)
        float timeInSeconds = totalPathDistance / walkingSpeed;
        float timeInMinutes = timeInSeconds / 60f;

        if (timeInMinutes < 1)
            return "< 1 minute";
        else if (timeInMinutes < 60)
            return $"{Mathf.CeilToInt(timeInMinutes)} minutes";
        else
        {
            int hours = Mathf.FloorToInt(timeInMinutes / 60);
            int minutes = Mathf.CeilToInt(timeInMinutes % 60);
            return $"{hours}h {minutes}m";
        }
    }

    public Node GetStartNode() => startNode;
    public Node GetEndNode() => endNode;

    public Dictionary<string, Node> GetAllNodes() => new Dictionary<string, Node>(allNodes);

    #endregion

    #region File Name Generation

    private string GetNodesFileName()
    {
        if (string.IsNullOrEmpty(currentMapId))
            return "nodes.json";
        return $"nodes_{currentMapId}.json";
    }

    private string GetEdgesFileName()
    {
        if (string.IsNullOrEmpty(currentMapId))
            return "edges.json";
        return $"edges_{currentMapId}.json";
    }

    #endregion

    #region Debug Visualization

    void OnDrawGizmos()
    {
        if (!showPathInScene || currentPath == null || currentPath.Count == 0)
            return;

        // Draw path line
        Gizmos.color = pathDebugColor;
        for (int i = 0; i < currentPath.Count - 1; i++)
        {
            Gizmos.DrawLine(currentPath[i].worldPosition, currentPath[i + 1].worldPosition);

            // Draw waypoint spheres
            Color waypointColor = pathDebugColor;
            waypointColor.a = 0.6f;
            Gizmos.color = waypointColor;
            Gizmos.DrawSphere(currentPath[i].worldPosition, debugSphereSize * 0.4f);
        }

        // Draw start node
        if (currentPath.Count > 0)
        {
            Gizmos.color = startNodeColor;
            Gizmos.DrawSphere(currentPath[0].worldPosition, debugSphereSize);

            // Draw end node
            Gizmos.color = endNodeColor;
            Gizmos.DrawSphere(currentPath[currentPath.Count - 1].worldPosition, debugSphereSize);
        }

        // Draw labels in editor
#if UNITY_EDITOR
        if (currentPath.Count > 0)
        {
            UnityEditor.Handles.Label(
                currentPath[0].worldPosition + Vector3.up * 2f,
                $"START\n{startNode?.name}\n{GetFormattedDistance()}"
            );

            UnityEditor.Handles.Label(
                currentPath[currentPath.Count - 1].worldPosition + Vector3.up * 2f,
                $"END\n{endNode?.name}\n{GetEstimatedWalkingTime()}"
            );
        }
#endif
    }

    #endregion

    #region Utility Methods

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
            Debug.Log($"[AStarPathfinding] {message}");
        }
    }

    #endregion

    #region Debug Testing

    void Update()
    {
        // Debug controls (uncomment for testing)
        // if (Application.isEditor && Input.GetKeyDown(KeyCode.F))
        // {
        //     // Test pathfinding between two infrastructure nodes
        //     if (allNodes.Count > 0)
        //     {
        //         var infraNodes = allNodes.Values.Where(n => n.type == "infrastructure").ToList();
        //         if (infraNodes.Count >= 2)
        //         {
        //             string start = infraNodes[0].node_id;
        //             string end = infraNodes[1].node_id;
        //             Debug.Log($"🧪 Testing path: {infraNodes[0].name} → {infraNodes[1].name}");
        //             StartCoroutine(FindPath(start, end));
        //         }
        //     }
        // }
        //
        // if (Application.isEditor && Input.GetKeyDown(KeyCode.G))
        // {
        //     Debug.Log("🔄 Reloading graph data...");
        //     StartCoroutine(LoadGraphData());
        // }
        //
        // if (Application.isEditor && Input.GetKeyDown(KeyCode.C))
        // {
        //     ClearCurrentPath();
        //     Debug.Log("🧹 Cleared current path");
        // }
    }

    #endregion
}

#region Supporting Classes

[System.Serializable]
public class GraphEdge
{
    public string toNodeId;
    public float cost; // Distance in meters from edge.distance
    public Edge edgeData;
}

[System.Serializable]
public class PathNode
{
    public Node node;
    public Vector3 worldPosition;
    public bool isStart;
    public bool isEnd;
    public float distanceToNext; // Distance to next node in path (in meters)
}

public class AStarNode : System.IComparable<AStarNode>
{
    public string nodeId;
    public float fScore;

    public int CompareTo(AStarNode other)
    {
        return fScore.CompareTo(other.fScore);
    }
}

/// <summary>
/// Simple priority queue implementation for A*
/// </summary>
public class PriorityQueue<T> where T : System.IComparable<T>
{
    private List<T> data;

    public PriorityQueue()
    {
        data = new List<T>();
    }

    public void Enqueue(T item)
    {
        data.Add(item);
        int ci = data.Count - 1;

        while (ci > 0)
        {
            int pi = (ci - 1) / 2;
            if (data[ci].CompareTo(data[pi]) >= 0)
                break;

            T tmp = data[ci];
            data[ci] = data[pi];
            data[pi] = tmp;
            ci = pi;
        }
    }

    public T Dequeue()
    {
        int li = data.Count - 1;
        T frontItem = data[0];
        data[0] = data[li];
        data.RemoveAt(li);

        --li;
        int pi = 0;

        while (true)
        {
            int ci = pi * 2 + 1;
            if (ci > li) break;

            int rc = ci + 1;
            if (rc <= li && data[rc].CompareTo(data[ci]) < 0)
                ci = rc;

            if (data[pi].CompareTo(data[ci]) <= 0)
                break;

            T tmp = data[pi];
            data[pi] = data[ci];
            data[ci] = tmp;
            pi = ci;
        }

        return frontItem;
    }

    public int Count
    {
        get { return data.Count; }
    }
}

#endregion