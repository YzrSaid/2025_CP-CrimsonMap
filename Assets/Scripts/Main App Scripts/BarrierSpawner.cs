using UnityEngine;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public class Node
{
    public string node_id;
    public string name;
    public float latitude;
    public float longitude;
    public bool linked_building;
    public bool is_barrier;
    public bool is_active;
}

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
    public float lineThickness = 5f;   // Height of the line prefab

    [Header("JSON Files")]
    public string nodesFileName = "nodes.json";
    public string edgesFileName = "edges.json";

    [Header("Map Coordinates")]
    public float minLatitude = 6.911234f;
    public float maxLatitude = 6.923000f;
    public float minLongitude = 122.077000f;
    public float maxLongitude = 122.081000f;

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
        // Load JSON
        string nodesPath = Path.Combine(Application.streamingAssetsPath, nodesFileName);
        string edgesPath = Path.Combine(Application.streamingAssetsPath, edgesFileName);

        NodeList nodeList = JsonUtility.FromJson<NodeList>("{\"nodes\":" + File.ReadAllText(nodesPath) + "}");
        EdgeList edgeList = JsonUtility.FromJson<EdgeList>("{\"edges\":" + File.ReadAllText(edgesPath) + "}");

        Dictionary<string, Node> nodeDict = new Dictionary<string, Node>();
        foreach (var node in nodeList.nodes)
        {
            nodeDict[node.node_id] = node;

            // Spawn barrier node if it is a barrier
            if (node.is_barrier && node.is_active)
            {
                SpawnNode(node.latitude, node.longitude);
            }
        }

        // Spawn barrier edges
        foreach (var edge in edgeList.edges)
        {
            if (edge.path_type == "barrier" && edge.is_active)
            {
                if (nodeDict.TryGetValue(edge.from_node, out Node fromNode) &&
                    nodeDict.TryGetValue(edge.to_node, out Node toNode))
                {
                    SpawnEdge(fromNode, toNode);
                }
            }
        }

        List<Node> barrierNodes = nodeList.nodes.FindAll(n => n.is_barrier && n.is_active);
        SpawnBarrierPolygon(barrierNodes);

    }

    void SpawnNode(float lat, float lon)
    {
        Vector2 pos = LatLonToMapPosition(lat, lon);
        GameObject nodeObj = Instantiate(nodePrefab, mapContainer);
        RectTransform rt = nodeObj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
    }

    void SpawnBarrierPolygon(List<Node> barrierNodes)
    {
        if (barrierNodes.Count < 3) return;

        GameObject polyObj = new GameObject("BarrierPolygon", typeof(RectTransform), typeof(CanvasRenderer), typeof(PolygonImage));
        polyObj.transform.SetParent(mapContainer, false);

        PolygonImage pg = polyObj.GetComponent<PolygonImage>();
        pg.color = new Color(0.8f, 0.8f, 0.8f, 0.5f); // light gray, semi-transparent
        pg.material = new Material(Shader.Find("UI/Default")); // assign default UI shader


        // Convert node lat/lon to UI positions
        foreach (var node in barrierNodes)
        {
            Vector2 pos = LatLonToMapPosition(node.latitude, node.longitude);
            pg.points.Add(pos);
        }

        polyObj.transform.SetAsLastSibling(); // behind nodes and edges
        pg.SetVerticesDirty();
    }


    // void SpawnEdge(Node fromNode, Node toNode)
    // {
    //     Vector2 posA = LatLonToMapPosition(fromNode.latitude, fromNode.longitude);
    //     Vector2 posB = LatLonToMapPosition(toNode.latitude, toNode.longitude);

    //     GameObject lineObj = Instantiate(connectingLinePrefab, mapContainer);
    //     RectTransform rt = lineObj.GetComponent<RectTransform>();

    //     // Position: center between two nodes
    //     rt.anchoredPosition = (posA + posB) / 2f;

    //     // Scale line length to distance between nodes
    //     Vector2 diff = posB - posA;
    //     rt.sizeDelta = new Vector2(diff.magnitude, lineThickness);

    //     // Rotate line to match direction
    //     float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
    //     rt.rotation = Quaternion.Euler(0, 0, angle);
    // }

    void SpawnEdge(Node fromNode, Node toNode)
    {
        Vector2 posA = LatLonToMapPosition(fromNode.latitude, fromNode.longitude);
        Vector2 posB = LatLonToMapPosition(toNode.latitude, toNode.longitude);

        GameObject lineObj = Instantiate(connectingLinePrefab, mapContainer);
        RectTransform rt = lineObj.GetComponent<RectTransform>();

        Vector2 diff = posB - posA;
        float distance = diff.magnitude;

        // Position: center
        rt.anchoredPosition = (posA + posB) / 2f;

        // Stretch along X axis
        rt.sizeDelta = new Vector2(distance, lineThickness);

        // Rotate around center
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        rt.rotation = Quaternion.Euler(0, 0, angle);
    }


    [Header("Map Padding & Offset")]
    public float paddingTop = 50f;       // space at top for UI
    public float paddingBottom = 20f;    // space at bottom
    public float paddingLeft = 20f;      // space at left
    public float paddingRight = 20f;     // space at right

    Vector2 LatLonToMapPosition(float lat, float lon)
    {
        float xMeters = LonToMeters(minLongitude, lon, minLatitude);
        float yMeters = LatToMeters(minLatitude, lat);

        // Map container usable width/height after padding
        float usableWidth = mapImage.rect.width - paddingLeft - paddingRight;
        float usableHeight = mapImage.rect.height - paddingTop - paddingBottom;

        // Scale to usable area
        float scaleX = usableWidth / mapWidthMeters;
        float scaleY = usableHeight / mapHeightMeters;

        // Map pivot is center, so we need to shift by -width/2 + left padding etc.
        float anchoredX = xMeters * scaleX - usableWidth / 2f + paddingLeft;
        float anchoredY = yMeters * scaleY - usableHeight / 2f + paddingBottom;

        // Add explicit **top margin** by shifting downward
        anchoredY -= paddingTop;

        // Clamp to stay inside container
        anchoredX = Mathf.Clamp(anchoredX, -mapImage.rect.width / 2f + paddingLeft, mapImage.rect.width / 2f - paddingRight);
        anchoredY = Mathf.Clamp(anchoredY, -mapImage.rect.height / 2f + paddingBottom, mapImage.rect.height / 2f - paddingTop);

        return new Vector2(anchoredX, anchoredY);
    }



    float LatToMeters(float lat1, float lat2)
    {
        return (lat2 - lat1) * 111320f; // 1 degree latitude ≈ 111.32 km
    }

    float LonToMeters(float lon1, float lon2, float refLat)
    {
        return (lon2 - lon1) * 111320f * Mathf.Cos(refLat * Mathf.Deg2Rad);
    }


}


