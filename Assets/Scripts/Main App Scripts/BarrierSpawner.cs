using UnityEngine;
using System.Collections.Generic;
using System.IO;


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
    public RectTransform mapContainer; // Parent for all nodes and edges
    public RectTransform mapImage;     // The background map image

    [Header("Prefabs")]
    public GameObject nodePrefab;
    public GameObject connectingLinePrefab;
    public float lineThickness = 5f;

    [Header("JSON Files")]
    public string nodesFileName = "nodes.json";
    public string edgesFileName = "edges.json";

    [Header("Map Coordinates")]
    public float minLatitude = 6.911234f;
    public float maxLatitude = 6.923000f;
    public float minLongitude = 122.077000f;
    public float maxLongitude = 122.081000f;

    [Header("Map Padding")]
    public float paddingTop = 50f;
    public float paddingBottom = 20f;
    public float paddingLeft = 20f;
    public float paddingRight = 20f;

    private float mapWidthMeters;
    private float mapHeightMeters;

    void Start()
    {
        mapWidthMeters = LonToMeters(minLongitude, maxLongitude, minLatitude);
        mapHeightMeters = LatToMeters(minLatitude, maxLatitude);

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

        // Spawn nodes
        foreach (var node in nodeList.nodes)
        {
            nodeDict[node.node_id] = node;
            if (node.is_barrier && node.is_active)
                SpawnNode(node.latitude, node.longitude);
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
    }

    void SpawnNode(float lat, float lon)
    {
        Vector2 pos = LatLonToMapPosition(lat, lon);
        GameObject nodeObj = Instantiate(nodePrefab, mapContainer);
        nodeObj.GetComponent<RectTransform>().anchoredPosition = pos;
    }

    void SpawnEdge(Node fromNode, Node toNode)
    {
        Vector2 posA = LatLonToMapPosition(fromNode.latitude, fromNode.longitude);
        Vector2 posB = LatLonToMapPosition(toNode.latitude, toNode.longitude);

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

        PolygonImage pg = polyObj.GetComponent<PolygonImage>();
        pg.color = new Color(0.8f, 0.8f, 0.8f, 0.5f);
        pg.material = new Material(Shader.Find("UI/Default"));

        foreach (var node in barrierNodes)
        {
            Vector2 pos = LatLonToMapPosition(node.latitude, node.longitude);
            pg.points.Add(pos);
        }

        polyObj.transform.SetAsLastSibling();
        pg.SetVerticesDirty();
    }

    Vector2 GetNonOverlappingPosition(Vector2 originalPos, float minDistance = 20f)
    {
        // Keep a static list of used positions
        if (_usedPositions == null) _usedPositions = new List<Vector2>();

        Vector2 newPos = originalPos;

        // Try to find a position not too close to existing ones
        int tries = 0;
        while (_usedPositions.Exists(p => Vector2.Distance(p, newPos) < minDistance) && tries < 10)
        {
            newPos += new Vector2(Random.Range(-minDistance, minDistance), Random.Range(-minDistance, minDistance));
            tries++;
        }

        _usedPositions.Add(newPos);
        return newPos;
    }

    private static List<Vector2> _usedPositions;


    public static Vector2 LatLonToMapPositionStatic(
        float lat, float lon, RectTransform mapImage,
        float paddingLeft, float paddingRight, float paddingTop, float paddingBottom,
        float minLatitude = 6.911234f, float maxLatitude = 6.923f,
        float minLongitude = 122.077f, float maxLongitude = 122.081f)
    {
        float mapWidthMeters = (maxLongitude - minLongitude) * 111320f * Mathf.Cos(minLatitude * Mathf.Deg2Rad);
        float mapHeightMeters = (maxLatitude - minLatitude) * 111320f;

        float xMeters = (lon - minLongitude) * 111320f * Mathf.Cos(minLatitude * Mathf.Deg2Rad);
        float yMeters = (lat - minLatitude) * 111320f;

        float usableWidth = mapImage.rect.width - paddingLeft - paddingRight;
        float usableHeight = mapImage.rect.height - paddingTop - paddingBottom;

        float scaleX = usableWidth / mapWidthMeters;
        float scaleY = usableHeight / mapHeightMeters;

        float anchoredX = xMeters * scaleX - usableWidth / 2f + paddingLeft;
        float anchoredY = yMeters * scaleY - usableHeight / 2f + paddingBottom;
        anchoredY -= paddingTop;

        anchoredX = Mathf.Clamp(anchoredX, -mapImage.rect.width / 2f + paddingLeft, mapImage.rect.width / 2f - paddingRight);
        anchoredY = Mathf.Clamp(anchoredY, -mapImage.rect.height / 2f + paddingBottom, mapImage.rect.height / 2f - paddingTop);

        return new Vector2(anchoredX, anchoredY);
    }
    

    Vector2 LatLonToMapPosition(float lat, float lon)
    {
        float xMeters = LonToMeters(minLongitude, lon, minLatitude);
        float yMeters = LatToMeters(minLatitude, lat);

        float usableWidth = mapImage.rect.width - paddingLeft - paddingRight;
        float usableHeight = mapImage.rect.height - paddingTop - paddingBottom;

        float scaleX = usableWidth / mapWidthMeters;
        float scaleY = usableHeight / mapHeightMeters;

        float anchoredX = xMeters * scaleX - usableWidth / 2f + paddingLeft;
        float anchoredY = yMeters * scaleY - usableHeight / 2f + paddingBottom;
        anchoredY -= paddingTop;

        anchoredX = Mathf.Clamp(anchoredX, -mapImage.rect.width / 2f + paddingLeft, mapImage.rect.width / 2f - paddingRight);
        anchoredY = Mathf.Clamp(anchoredY, -mapImage.rect.height / 2f + paddingBottom, mapImage.rect.height / 2f - paddingTop);

        return new Vector2(anchoredX, anchoredY);
    }

    float LatToMeters(float lat1, float lat2) => (lat2 - lat1) * 111320f;
    float LonToMeters(float lon1, float lon2, float refLat) => (lon2 - lon1) * 111320f * Mathf.Cos(refLat * Mathf.Deg2Rad);
}
