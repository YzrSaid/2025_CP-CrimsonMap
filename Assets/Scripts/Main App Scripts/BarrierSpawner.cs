using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Collections;


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