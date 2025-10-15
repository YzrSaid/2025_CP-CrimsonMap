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
    public float alternativePathPenalty = 0.3f;

    [Header("Debug Visualization")]
    public bool showPathInScene = true;
    public Color pathDebugColor = Color.green;
    public Color startNodeColor = Color.blue;
    public Color endNodeColor = Color.red;
    public float debugSphereSize = 1f;
    public float debugLineWidth = 0.5f;
    public float pathHeightOffset = 2f;

    private string currentMapId;
    private List<string> currentCampusIds = new List<string>();

    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>();
    private Dictionary<string, List<GraphEdge>> adjacencyList = new Dictionary<string, List<GraphEdge>>();

    private List<RouteData> allRoutes = new List<RouteData>();
    private int activeRouteIndex = 0;

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

    #region MapManager Integration

    public void SetCurrentMapData(string mapId, List<string> campusIds)
    {
        currentMapId = mapId;
        currentCampusIds.Clear();
        if (campusIds != null)
        {
            currentCampusIds.AddRange(campusIds);
        }
    }

    private void OnMapChanged(MapInfo mapInfo)
    {
        SetCurrentMapData(mapInfo.map_id, mapInfo.campus_included);
        ClearCurrentPath();
    }

    private void OnMapLoadingStarted()
    {
        ClearGraphData();
        ClearCurrentPath();
    }

    public IEnumerator LoadGraphDataForMap(string mapId, List<string> campusIds)
    {
        SetCurrentMapData(mapId, campusIds);
        yield return StartCoroutine(LoadGraphData());
    }

    #endregion

    #region Graph Data Loading

    private IEnumerator LoadGraphData()
    {
        if (string.IsNullOrEmpty(currentMapId))
        {
            yield break;
        }

        ClearGraphData();

        yield return StartCoroutine(LoadNodes());

        if (allNodes.Count == 0)
        {
            yield break;
        }

        yield return StartCoroutine(LoadEdges());
    }

    private IEnumerator LoadNodes()
    {
        string fileName = GetNodesFileName();

        bool loadCompleted = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonContent) =>
            {
                try
                {
                    Node[] nodes = JsonHelper.FromJson<Node>(jsonContent);

                    allNodes.Clear();

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

    private IEnumerator LoadEdges()
    {
        string fileName = GetEdgesFileName();

        bool loadCompleted = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonContent) =>
            {
                try
                {
                    Edge[] edges = JsonHelper.FromJson<Edge>(jsonContent);

                    BuildAdjacencyList(edges);

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

    private void BuildAdjacencyList(Edge[] edges)
    {
        adjacencyList.Clear();

        foreach (var nodeId in allNodes.Keys)
        {
            adjacencyList[nodeId] = new List<GraphEdge>();
        }

        int addedEdges = 0;

        foreach (var edge in edges)
        {
            if (edge == null || !edge.is_active) continue;

            if (!allNodes.ContainsKey(edge.from_node) || !allNodes.ContainsKey(edge.to_node))
                continue;

            float distance = edge.distance;

            adjacencyList[edge.from_node].Add(new GraphEdge
            {
                toNodeId = edge.to_node,
                cost = distance,
                edgeData = edge
            });

            adjacencyList[edge.to_node].Add(new GraphEdge
            {
                toNodeId = edge.from_node,
                cost = distance,
                edgeData = edge
            });

            addedEdges += 2;
        }
    }

    #endregion

    #region Multiple Path Finding

    public IEnumerator FindMultiplePaths(string startNodeId, string endNodeId, int maxPaths = 3)
    {
        if (isCalculating)
        {
            yield break;
        }

        isCalculating = true;

        ClearCurrentPath();

        if (!allNodes.ContainsKey(startNodeId))
        {
            isCalculating = false;
            yield break;
        }

        if (!allNodes.ContainsKey(endNodeId))
        {
            isCalculating = false;
            yield break;
        }

        var startNode = allNodes[startNodeId];
        var endNode = allNodes[endNodeId];

        if (startNode.type != "infrastructure")
        {
            isCalculating = false;
            yield break;
        }

        if (endNode.type != "infrastructure")
        {
            isCalculating = false;
            yield break;
        }

        // Find multiple alternative paths
        List<List<string>> paths = FindAlternativePaths(startNodeId, endNodeId, maxPaths);

        if (paths == null || paths.Count == 0)
        {
            isCalculating = false;
            yield break;
        }

        // Convert to RouteData
        foreach (var path in paths)
        {
            var routeData = new RouteData
            {
                path = ConvertToPathNodes(path),
                totalDistance = CalculateTotalDistance(path),
                startNode = startNode,
                endNode = endNode
            };

            routeData.formattedDistance = FormatDistance(routeData.totalDistance);
            routeData.walkingTime = CalculateWalkingTime(routeData.totalDistance);
            routeData.viaMode = DetermineViaMode(path);

            allRoutes.Add(routeData);
        }

        // Mark the shortest route as recommended
        if (allRoutes.Count > 0)
        {
            float shortestDistance = allRoutes.Min(r => r.totalDistance);
            var shortestRoute = allRoutes.FirstOrDefault(r => r.totalDistance == shortestDistance);
            if (shortestRoute != null)
            {
                shortestRoute.isRecommended = true;
            }
        }

        activeRouteIndex = 0;

        isCalculating = false;
    }

    private List<List<string>> FindAlternativePaths(string startId, string goalId, int maxPaths)
    {
        var allPaths = new List<List<string>>();
        var usedEdges = new HashSet<string>();

        for (int i = 0; i < maxPaths; i++)
        {
            // Find path with penalty for used edges
            List<string> path = AStarWithPenalty(startId, goalId, usedEdges);

            if (path == null || path.Count == 0)
            {
                break; // No more paths available
            }

            // Check if this path is significantly different from existing paths
            bool isDifferent = true;
            foreach (var existingPath in allPaths)
            {
                float similarity = CalculatePathSimilarity(path, existingPath);
                if (similarity > 0.7f) // 70% similar
                {
                    isDifferent = false;
                    break;
                }
            }

            if (isDifferent || allPaths.Count == 0)
            {
                allPaths.Add(path);

                // Mark edges as used for next iteration
                for (int j = 0; j < path.Count - 1; j++)
                {
                    string edgeKey = GetEdgeKey(path[j], path[j + 1]);
                    usedEdges.Add(edgeKey);
                }
            }
            else
            {
                // Force marking some edges to find different path
                if (path.Count > 2)
                {
                    int midIndex = path.Count / 2;
                    string edgeKey = GetEdgeKey(path[midIndex], path[midIndex + 1]);
                    usedEdges.Add(edgeKey);
                }
            }
        }

        return allPaths;
    }

    private float CalculatePathSimilarity(List<string> path1, List<string> path2)
    {
        if (path1.Count == 0 || path2.Count == 0)
            return 0f;

        int commonNodes = 0;
        var path2Set = new HashSet<string>(path2);

        foreach (var node in path1)
        {
            if (path2Set.Contains(node))
            {
                commonNodes++;
            }
        }

        return (float)commonNodes / Mathf.Max(path1.Count, path2.Count);
    }

    private string GetEdgeKey(string from, string to)
    {
        // Create bidirectional edge key
        if (string.Compare(from, to) < 0)
            return from + "-" + to;
        else
            return to + "-" + from;
    }

    private List<string> AStarWithPenalty(string startId, string goalId, HashSet<string> penalizedEdges)
    {
        var openSet = new PriorityQueue<AStarNode>();
        var openSetHash = new HashSet<string>();
        var closedSet = new HashSet<string>();
        var gScore = new Dictionary<string, float>();
        var fScore = new Dictionary<string, float>();
        var cameFrom = new Dictionary<string, string>();

        gScore[startId] = 0;
        fScore[startId] = Heuristic(allNodes[startId], allNodes[goalId]);

        openSet.Enqueue(new AStarNode
        {
            nodeId = startId,
            fScore = fScore[startId]
        });
        openSetHash.Add(startId);

        int iterations = 0;
        int maxIterations = 10000;

        while (openSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;

            AStarNode current = openSet.Dequeue();
            openSetHash.Remove(current.nodeId);

            if (current.nodeId == goalId)
            {
                return ReconstructPath(cameFrom, current.nodeId);
            }

            closedSet.Add(current.nodeId);

            if (!adjacencyList.ContainsKey(current.nodeId))
                continue;

            foreach (var edge in adjacencyList[current.nodeId])
            {
                string neighborId = edge.toNodeId;

                if (closedSet.Contains(neighborId))
                    continue;

                float edgeCost = edge.cost;

                // Apply penalty to used edges
                string edgeKey = GetEdgeKey(current.nodeId, neighborId);
                if (penalizedEdges.Contains(edgeKey))
                {
                    edgeCost *= (1.0f + alternativePathPenalty);
                }

                float tentativeGScore = gScore[current.nodeId] + edgeCost;

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

        return null;
    }

    #endregion

    #region Legacy Single Path Finding (for backward compatibility)

    public IEnumerator FindPath(string startNodeId, string endNodeId)
    {
        // Use FindMultiplePaths but only get first result
        yield return StartCoroutine(FindMultiplePaths(startNodeId, endNodeId, 1));
    }

    #endregion

    #region A* Helper Methods

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
    private string DetermineViaMode(List<string> path)
    {
        if (path == null || path.Count <= 1)
            return "Via Walkway";

        HashSet<string> pathTypes = new HashSet<string>();

        for (int i = 0; i < path.Count - 1; i++)
        {
            string fromId = path[i];
            string toId = path[i + 1];

            if (adjacencyList.ContainsKey(fromId))
            {
                var edge = adjacencyList[fromId].FirstOrDefault(e => e.toNodeId == toId);
                if (edge != null && edge.edgeData != null)
                {
                    string pathType = edge.edgeData.path_type;
                    if (!string.IsNullOrEmpty(pathType))
                    {
                        pathTypes.Add(pathType.ToLower());
                    }
                }
            }
        }

        if (pathTypes.Contains("overpass"))
            return "Via Overpass";
        else if (pathTypes.Contains("gate"))
            return "Via Gate";
        else
            return "Via Walkway";
    }

    private float Heuristic(Node a, Node b)
    {
        float dx = b.x_coordinate - a.x_coordinate;
        float dy = b.y_coordinate - a.y_coordinate;

        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private float CalculateTotalDistance(List<string> path)
    {
        float total = 0f;

        for (int i = 0; i < path.Count - 1; i++)
        {
            string fromId = path[i];
            string toId = path[i + 1];

            if (adjacencyList.ContainsKey(fromId))
            {
                var edge = adjacencyList[fromId].FirstOrDefault(e => e.toNodeId == toId);
                if (edge != null)
                {
                    total += edge.cost;
                }
            }
        }

        return total;
    }

    private string FormatDistance(float distance)
    {
        if (distance < 1000)
            return $"{distance:F1}m";
        else
            return $"{distance / 1000:F2}km";
    }

    private string CalculateWalkingTime(float distance)
    {
        float walkingSpeed = 1.4f; // m/s (average walking speed)
        float timeInSeconds = distance / walkingSpeed;
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
        allRoutes.Clear();
        activeRouteIndex = 0;
    }

    private void ClearGraphData()
    {
        allNodes.Clear();
        adjacencyList.Clear();
    }

    #endregion

    #region Public Access Methods - Multiple Routes

    public List<RouteData> GetAllRoutes()
    {
        return new List<RouteData>(allRoutes);
    }

    public void SetActiveRoute(int index)
    {
        if (index >= 0 && index < allRoutes.Count)
        {
            activeRouteIndex = index;
        }
    }

    public RouteData GetActiveRoute()
    {
        if (activeRouteIndex >= 0 && activeRouteIndex < allRoutes.Count)
        {
            return allRoutes[activeRouteIndex];
        }
        return null;
    }

    #endregion

    #region Public Access Methods - Legacy (for backward compatibility)

    public List<PathNode> GetCurrentPath()
    {
        var activeRoute = GetActiveRoute();
        if (activeRoute != null)
        {
            return new List<PathNode>(activeRoute.path);
        }
        return new List<PathNode>();
    }

    public bool HasPath()
    {
        return allRoutes != null && allRoutes.Count > 0;
    }

    public float GetTotalDistance()
    {
        var activeRoute = GetActiveRoute();
        if (activeRoute != null)
        {
            return activeRoute.totalDistance;
        }
        return 0f;
    }

    public string GetFormattedDistance()
    {
        var activeRoute = GetActiveRoute();
        if (activeRoute != null)
        {
            return activeRoute.formattedDistance;
        }
        return "0m";
    }

    public string GetEstimatedWalkingTime()
    {
        var activeRoute = GetActiveRoute();
        if (activeRoute != null)
        {
            return activeRoute.walkingTime;
        }
        return "0 minutes";
    }

    public Node GetStartNode()
    {
        var activeRoute = GetActiveRoute();
        if (activeRoute != null)
        {
            return activeRoute.startNode;
        }
        return null;
    }

    public Node GetEndNode()
    {
        var activeRoute = GetActiveRoute();
        if (activeRoute != null)
        {
            return activeRoute.endNode;
        }
        return null;
    }

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
        if (!showPathInScene || allRoutes == null || allRoutes.Count == 0)
            return;

        var activeRoute = GetActiveRoute();
        if (activeRoute == null || activeRoute.path == null || activeRoute.path.Count == 0)
            return;

        var currentPath = activeRoute.path;

        Gizmos.color = pathDebugColor;
        for (int i = 0; i < currentPath.Count - 1; i++)
        {
            Gizmos.DrawLine(currentPath[i].worldPosition, currentPath[i + 1].worldPosition);

            Color waypointColor = pathDebugColor;
            waypointColor.a = 0.6f;
            Gizmos.color = waypointColor;
            Gizmos.DrawSphere(currentPath[i].worldPosition, debugSphereSize * 0.4f);
        }

        if (currentPath.Count > 0)
        {
            Gizmos.color = startNodeColor;
            Gizmos.DrawSphere(currentPath[0].worldPosition, debugSphereSize);

            Gizmos.color = endNodeColor;
            Gizmos.DrawSphere(currentPath[currentPath.Count - 1].worldPosition, debugSphereSize);
        }
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
}

#region Supporting Classes

public class AStarNode : System.IComparable<AStarNode>
{
    public string nodeId;
    public float fScore;

    public int CompareTo(AStarNode other)
    {
        return fScore.CompareTo(other.fScore);
    }
}

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