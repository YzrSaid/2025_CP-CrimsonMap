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
    public float alternativePathPenalty = 0.5f;

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
        }
    }

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

        List<List<string>> paths = FindAlternativePaths(startNodeId, endNodeId, maxPaths);

        if (paths == null || paths.Count == 0)
        {
            isCalculating = false;
            yield break;
        }

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

        if (allRoutes.Count > 0)
        {
            float shortestDistance = allRoutes.Min(r => r.totalDistance);
            var shortestRoute = allRoutes.FirstOrDefault(r => r.totalDistance == shortestDistance);
            if (shortestRoute != null)
            {
                shortestRoute.isRecommended = true;
                shortestRoute.routeName = $"Route 1 (Recommended)";
            }
            
            for (int i = 0; i < allRoutes.Count; i++)
            {
                if (!allRoutes[i].isRecommended)
                {
                    allRoutes[i].routeName = $"Route {i + 1}";
                }
            }
        }

        activeRouteIndex = 0;

        isCalculating = false;
    }

    private List<List<string>> FindAlternativePaths(string startId, string goalId, int maxPaths)
    {
        var allPaths = new List<List<string>>();
        var usedEdges = new HashSet<string>();
        var blockedEdges = new HashSet<string>();
        var usedCrossings = new HashSet<string>();
        var foundPathTypes = new HashSet<string>();

        int maxAttempts = maxPaths * 5;
        
        for (int i = 0; i < maxAttempts && allPaths.Count < maxPaths; i++)
        {
            List<string> path = AStarWithPenalty(startId, goalId, usedEdges, blockedEdges);

            if (path == null || path.Count == 0)
            {
                break;
            }

            string pathType = DetermineViaMode(path);
            string crossingEdge = FindCrossCampusEdge(path);
            
            bool isNewPathType = !foundPathTypes.Contains(pathType);
            bool isNewCrossing = !string.IsNullOrEmpty(crossingEdge) && !usedCrossings.Contains(crossingEdge);
            
            bool isDifferent = true;
            if (allPaths.Count > 0 && !isNewPathType && !isNewCrossing)
            {
                foreach (var existingPath in allPaths)
                {
                    float similarity = CalculatePathSimilarity(path, existingPath);
                    if (similarity > 0.5f)
                    {
                        isDifferent = false;
                        break;
                    }
                }
            }

            if (allPaths.Count == 0 || isNewPathType || isNewCrossing || isDifferent)
            {
                allPaths.Add(path);
                foundPathTypes.Add(pathType);
                
                if (!string.IsNullOrEmpty(crossingEdge))
                {
                    usedCrossings.Add(crossingEdge);
                    blockedEdges.Add(crossingEdge);
                }
                
                for (int j = 0; j < path.Count - 1; j++)
                {
                    string edgeKey = GetEdgeKey(path[j], path[j + 1]);
                    usedEdges.Add(edgeKey);
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(crossingEdge) && usedCrossings.Contains(crossingEdge))
                {
                    blockedEdges.Add(crossingEdge);
                }
                
                if (path.Count > 4)
                {
                    int[] positions = { path.Count / 4, path.Count / 2, (path.Count * 3) / 4 };
                    foreach (int pos in positions)
                    {
                        if (pos > 0 && pos < path.Count - 1)
                        {
                            string edgeKey = GetEdgeKey(path[pos], path[pos + 1]);
                            usedEdges.Add(edgeKey);
                        }
                    }
                }
            }
        }

        return allPaths;
    }
    
    private string FindCrossCampusEdge(List<string> path)
    {
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
                    if (pathType == "via_gate" || pathType == "via_overpass")
                    {
                        return GetEdgeKey(fromId, toId);
                    }
                }
            }
        }
        
        return null;
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
        if (string.Compare(from, to) < 0)
            return from + "-" + to;
        else
            return to + "-" + from;
    }

    private List<string> AStarWithPenalty(string startId, string goalId, HashSet<string> penalizedEdges, HashSet<string> blockedEdges = null)
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

                string edgeKey = GetEdgeKey(current.nodeId, neighborId);
                
                if (blockedEdges != null && blockedEdges.Contains(edgeKey))
                {
                    continue;
                }

                float edgeCost = edge.cost;

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

    public IEnumerator FindPath(string startNodeId, string endNodeId)
    {
        yield return StartCoroutine(FindMultiplePaths(startNodeId, endNodeId, 1));
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

        if (pathTypes.Contains("via_overpass"))
            return "Via Overpass";
        else if (pathTypes.Contains("via_gate"))
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
        float walkingSpeed = 1.4f;
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

    private bool IsValidCoordinate(float lat, float lon)
    {
        return !float.IsNaN(lat) && !float.IsNaN(lon) &&
               !float.IsInfinity(lat) && !float.IsInfinity(lon) &&
               lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180;
    }
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