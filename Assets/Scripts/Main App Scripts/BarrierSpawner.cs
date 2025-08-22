// using UnityEngine;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;

// [System.Serializable]
// public class Edge
// {
//     public string edge_id;
//     public string from_node;
//     public string to_node;
//     public float distance;
//     public string path_type;
//     public float[] elevations;
//     public bool is_active;
// }

// [System.Serializable]
// public class NodeList
// {
//     public List<Node> nodes;
// }

// [System.Serializable]
// public class EdgeList
// {
//     public List<Edge> edges;
// }

// public class BarrierSpawner : MonoBehaviour
// {
//     [Header("Map Setup")]
//     public RectTransform mapContainer;
//     public RectTransform mapImage;

//     [Header("Prefabs")]
//     public GameObject nodePrefab;
//     public GameObject connectingLinePrefab;
//     public float lineThickness = 5f;

//     [Header("JSON Files")]
//     public string nodesFileName = "nodes.json";
//     public string edgesFileName = "edges.json";

//     [Header("Map Padding")]
//     public float paddingLeft = 20f;
//     public float paddingRight = 20f;
//     public float paddingTop = 50f;
//     public float paddingBottom = 20f;

//     // Dynamic bounds - calculated from actual data
//     private float minLatitude, maxLatitude, minLongitude, maxLongitude;
//     private float mapWidthMeters, mapHeightMeters;

//     void Start()
//     {
//         LoadAndSpawnBarriers();
//     }

//     void LoadAndSpawnBarriers()
//     {
//         string nodesPath = Path.Combine(Application.streamingAssetsPath, nodesFileName);
//         string edgesPath = Path.Combine(Application.streamingAssetsPath, edgesFileName);

//         if (!File.Exists(nodesPath) || !File.Exists(edgesPath))
//         {
//             Debug.LogError("❌ JSON files not found in StreamingAssets!");
//             return;
//         }

//         NodeList nodeList = JsonUtility.FromJson<NodeList>("{\"nodes\":" + File.ReadAllText(nodesPath) + "}");
//         EdgeList edgeList = JsonUtility.FromJson<EdgeList>("{\"edges\":" + File.ReadAllText(edgesPath) + "}");

//         // Calculate bounds from ALL nodes (not just barriers)
//         CalculateBoundsFromData(nodeList.nodes);

//         Dictionary<string, Node> nodeDict = new Dictionary<string, Node>();

//         // Spawn nodes
//         foreach (var node in nodeList.nodes)
//         {
//             nodeDict[node.node_id] = node;
//             if (node.is_barrier && node.is_active)
//                 SpawnNode(node.latitude, node.longitude);
//         }

//         // Spawn edges
//         foreach (var edge in edgeList.edges)
//         {
//             if (edge.path_type == "barrier" && edge.is_active &&
//                 nodeDict.TryGetValue(edge.from_node, out Node fromNode) &&
//                 nodeDict.TryGetValue(edge.to_node, out Node toNode))
//             {
//                 SpawnEdge(fromNode, toNode);
//             }
//         }

//         // Spawn polygon
//         List<Node> barrierNodes = nodeList.nodes.FindAll(n => n.is_barrier && n.is_active);
//         SpawnBarrierPolygon(barrierNodes);
//     }

//     void CalculateBoundsFromData(List<Node> allNodes)
//     {
//         if (allNodes.Count == 0) return;

//         minLatitude = allNodes.Min(n => n.latitude);
//         maxLatitude = allNodes.Max(n => n.latitude);
//         minLongitude = allNodes.Min(n => n.longitude);
//         maxLongitude = allNodes.Max(n => n.longitude);

//         // Add small buffer to avoid edge cases
//         float latBuffer = (maxLatitude - minLatitude) * 0.05f;
//         float lonBuffer = (maxLongitude - minLongitude) * 0.05f;

//         minLatitude -= latBuffer;
//         maxLatitude += latBuffer;
//         minLongitude -= lonBuffer;
//         maxLongitude += lonBuffer;

//         // Calculate map dimensions
//         float midLatitude = (minLatitude + maxLatitude) / 2f;
//         mapWidthMeters = (maxLongitude - minLongitude) * 111320f * Mathf.Cos(midLatitude * Mathf.Deg2Rad);
//         mapHeightMeters = (maxLatitude - minLatitude) * 111320f;

//         Debug.Log($"📍 Calculated bounds: Lat [{minLatitude:F6}, {maxLatitude:F6}], Lon [{minLongitude:F6}, {maxLongitude:F6}]");
//         Debug.Log($"📐 Map dimensions: {mapWidthMeters:F1}m x {mapHeightMeters:F1}m");
//     }

//     void SpawnNode(float lat, float lon)
//     {
//         Vector2 pos = LatLonToMapPosition(lat, lon);
//         GameObject nodeObj = Instantiate(nodePrefab, mapContainer);
//         nodeObj.GetComponent<RectTransform>().anchoredPosition = pos;
//     }

//     void SpawnEdge(Node fromNode, Node toNode)
//     {
//         Vector2 posA = LatLonToMapPosition(fromNode.latitude, fromNode.longitude);
//         Vector2 posB = LatLonToMapPosition(toNode.latitude, toNode.longitude);

//         GameObject lineObj = Instantiate(connectingLinePrefab, mapContainer);
//         RectTransform rt = lineObj.GetComponent<RectTransform>();

//         Vector2 diff = posB - posA;
//         float distance = diff.magnitude;

//         rt.anchoredPosition = (posA + posB) / 2f;
//         rt.sizeDelta = new Vector2(distance, lineThickness);

//         float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
//         rt.rotation = Quaternion.Euler(0, 0, angle);
//     }

//     void SpawnBarrierPolygon(List<Node> barrierNodes)
//     {
//         if (barrierNodes.Count < 3) return;

//         GameObject polyObj = new GameObject("BarrierPolygon", typeof(RectTransform), typeof(CanvasRenderer), typeof(PolygonImage));
//         polyObj.transform.SetParent(mapContainer, false);

//         RectTransform rt = polyObj.GetComponent<RectTransform>();
//         rt.anchorMin = Vector2.zero;
//         rt.anchorMax = Vector2.one;
//         rt.sizeDelta = Vector2.zero;
//         rt.anchoredPosition = Vector2.zero;

//         PolygonImage pg = polyObj.GetComponent<PolygonImage>();
//         pg.color = new Color(0.8f, 0.8f, 0.8f, 0.5f);
//         pg.material = new Material(Shader.Find("UI/Default"));

//         foreach (var node in barrierNodes)
//         {
//             Vector2 pos = LatLonToMapPosition(node.latitude, node.longitude);
//             pg.points.Add(pos);
//         }

//         polyObj.transform.SetAsLastSibling();
//         pg.SetVerticesDirty();
//     }

//     // FIXED: Consistent coordinate projection
//     public Vector2 LatLonToMapPosition(float lat, float lon)
//     {
//         // Convert to meters from origin
//         float midLatitude = (minLatitude + maxLatitude) / 2f;
//         float xMeters = (lon - minLongitude) * 111320f * Mathf.Cos(midLatitude * Mathf.Deg2Rad);
//         float yMeters = (lat - minLatitude) * 111320f;

//         // Calculate usable area (excluding padding)
//         float usableWidth = mapImage.rect.width - paddingLeft - paddingRight;
//         float usableHeight = mapImage.rect.height - paddingTop - paddingBottom;

//         // Scale meters to pixels
//         float scaleX = usableWidth / mapWidthMeters;
//         float scaleY = usableHeight / mapHeightMeters;

//         // Convert to UI coordinates (centered at 0,0)
//         float pixelX = xMeters * scaleX;
//         float pixelY = yMeters * scaleY;

//         // Adjust for padding and UI coordinate system
//         float anchoredX = pixelX - usableWidth / 2f + paddingLeft;
//         float anchoredY = pixelY - usableHeight / 2f + paddingBottom;

//         return new Vector2(anchoredX, anchoredY);
//     }

//     // Updated static method with dynamic bounds
//     public static Vector2 LatLonToMapPositionStatic(
//         float lat, float lon, RectTransform mapImage,
//         float paddingLeft, float paddingRight, float paddingTop, float paddingBottom,
//         float minLatitude, float maxLatitude, float minLongitude, float maxLongitude)
//     {
//         // Convert to meters from origin
//         float midLatitude = (minLatitude + maxLatitude) / 2f;
//         float mapWidthMeters = (maxLongitude - minLongitude) * 111320f * Mathf.Cos(midLatitude * Mathf.Deg2Rad);
//         float mapHeightMeters = (maxLatitude - minLatitude) * 111320f;

//         float xMeters = (lon - minLongitude) * 111320f * Mathf.Cos(midLatitude * Mathf.Deg2Rad);
//         float yMeters = (lat - minLatitude) * 111320f;

//         // Calculate usable area
//         float usableWidth = mapImage.rect.width - paddingLeft - paddingRight;
//         float usableHeight = mapImage.rect.height - paddingTop - paddingBottom;

//         // Scale to pixels
//         float scaleX = usableWidth / mapWidthMeters;
//         float scaleY = usableHeight / mapHeightMeters;

//         float pixelX = xMeters * scaleX;
//         float pixelY = yMeters * scaleY;

//         // UI coordinates
//         float anchoredX = pixelX - usableWidth / 2f + paddingLeft;
//         float anchoredY = pixelY - usableHeight / 2f + paddingBottom;

//         return new Vector2(anchoredX, anchoredY);
//     }

//     // Public getters for bounds (for BuildingSpawner to use)
//     public float GetMinLatitude() => minLatitude;
//     public float GetMaxLatitude() => maxLatitude;
//     public float GetMinLongitude() => minLongitude;
//     public float GetMaxLongitude() => maxLongitude;
// }


using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Collections;

[System.Serializable]
public class Edge
{
    public string edge_id;
    public string from_node;
    public string to_node;
    public float distance;
    public string path_type;
    public float[] elevations;
    public bool is_active;
}

[System.Serializable]
public class NodeList
{
    public List<Node> nodes;
}

[System.Serializable]
public class EdgeList
{
    public List<Edge> edges;
}

public class BarrierSpawner : MonoBehaviour
{
    [Header("Map Setup")]
    public RectTransform mapContainer;

    [Header("Prefabs")]
    public GameObject nodePrefab;
    public GameObject connectingLinePrefab;
    public float lineThickness = 5f;

    [Header("JSON Files")]
    public string nodesFileName = "nodes.json";
    public string edgesFileName = "edges.json";

    void Start()
    {
        StartCoroutine(LoadAndSpawnBarriersCoroutine());
    }

    IEnumerator LoadAndSpawnBarriersCoroutine()
    {
        // Wait for MapCoordinateSystem to be ready
        if (MapCoordinateSystem.Instance == null)
        {
            Debug.LogError("❌ MapCoordinateSystem not found! Please add it to the scene first.");
            yield break;
        }

        yield return StartCoroutine(MapCoordinateSystem.Instance.WaitForBoundsReady());

        LoadAndSpawnBarriers();
    }

    void LoadAndSpawnBarriers()
    {
        string nodesPath = Path.Combine(Application.streamingAssetsPath, nodesFileName);
        string edgesPath = Path.Combine(Application.streamingAssetsPath, edgesFileName);

        if (!File.Exists(nodesPath) || !File.Exists(edgesPath))
        {
            Debug.LogError("❌ JSON files not found in StreamingAssets!");
            return;
        }

        NodeList nodeList = JsonUtility.FromJson<NodeList>("{\"nodes\":" + File.ReadAllText(nodesPath) + "}");
        EdgeList edgeList = JsonUtility.FromJson<EdgeList>("{\"edges\":" + File.ReadAllText(edgesPath) + "}");

        Dictionary<string, Node> nodeDict = new Dictionary<string, Node>();

        // Spawn nodes using centralized coordinate system
        foreach (var node in nodeList.nodes)
        {
            nodeDict[node.node_id] = node;
            if (node.is_barrier && node.is_active)
            {
                Vector2 pos = MapCoordinateSystem.Instance.LatLonToMapPosition(node.latitude, node.longitude);
                GameObject nodeObj = Instantiate(nodePrefab, mapContainer);
                nodeObj.GetComponent<RectTransform>().anchoredPosition = pos;

                Debug.Log($"🚧 Spawned barrier node {node.name} at {pos}");
            }
        }

        // Spawn edges
        foreach (var edge in edgeList.edges)
        {
            if (edge.path_type == "barrier" && edge.is_active &&
                nodeDict.TryGetValue(edge.from_node, out Node fromNode) &&
                nodeDict.TryGetValue(edge.to_node, out Node toNode))
            {
                SpawnEdge(fromNode, toNode);
            }
        }

        // Spawn polygon
        List<Node> barrierNodes = nodeList.nodes.FindAll(n => n.is_barrier && n.is_active);
        SpawnBarrierPolygon(barrierNodes);

        Debug.Log($"✅ BarrierSpawner completed: {barrierNodes.Count} barrier nodes processed");
    }

    void SpawnEdge(Node fromNode, Node toNode)
    {
        Vector2 posA = MapCoordinateSystem.Instance.LatLonToMapPosition(fromNode.latitude, fromNode.longitude);
        Vector2 posB = MapCoordinateSystem.Instance.LatLonToMapPosition(toNode.latitude, toNode.longitude);

        GameObject lineObj = Instantiate(connectingLinePrefab, mapContainer);
        RectTransform rt = lineObj.GetComponent<RectTransform>();

        Vector2 diff = posB - posA;
        float distance = diff.magnitude;

        rt.anchoredPosition = (posA + posB) / 2f;
        rt.sizeDelta = new Vector2(distance, lineThickness);

        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        rt.rotation = Quaternion.Euler(0, 0, angle);
    }

    void SpawnBarrierPolygon(List<Node> barrierNodes)
    {
        if (barrierNodes.Count < 3) return;

        GameObject polyObj = new GameObject("BarrierPolygon", typeof(RectTransform), typeof(CanvasRenderer), typeof(PolygonImage));
        polyObj.transform.SetParent(mapContainer, false);

        RectTransform rt = polyObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        PolygonImage pg = polyObj.GetComponent<PolygonImage>();
        pg.color = new Color(0.8f, 0.8f, 0.8f, 0.5f);
        pg.material = new Material(Shader.Find("UI/Default"));

        foreach (var node in barrierNodes)
        {
            Vector2 pos = MapCoordinateSystem.Instance.LatLonToMapPosition(node.latitude, node.longitude);
            pg.points.Add(pos);
        }

        polyObj.transform.SetAsLastSibling();
        pg.SetVerticesDirty();
    }
}