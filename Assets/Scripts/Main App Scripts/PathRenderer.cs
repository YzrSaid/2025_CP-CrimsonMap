using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class PathRenderer : MonoBehaviour
{
    [Header("References")]
    public RectTransform mapContainer;

    [Header("Path Colors")]
    public Color pavedRoadColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
    public Color walkwayColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
    public Color barrierColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);

    [Header("Path Widths")]
    public float pavedWidth = 8f;   // thinner by default
    public float walkwayWidth = 5f;
    public float barrierWidth = 3f;

    [Header("Debug")]
    public bool showPathwayNodes = false;
    public GameObject pathwayNodePrefab;

    private List<GameObject> pathLines;
    private Dictionary<string, Node> allNodes;
    private Image mapBackground;  // auto-detected at runtime

    void Start()
    {
        pathLines = new List<GameObject>();
        allNodes = new Dictionary<string, Node>();

        // Auto-detect the map background (first Image child under mapContainer)
        mapBackground = mapContainer.GetComponentInChildren<Image>();
        if (mapBackground == null)
        {
            Debug.LogError("❌ No background image found inside mapContainer!");
        }

        StartCoroutine(LoadAndRenderAllPathwaysCoroutine());
    }

    IEnumerator LoadAndRenderAllPathwaysCoroutine()
    {
        if (MapCoordinateSystem.Instance == null)
        {
            Debug.LogError("❌ MapCoordinateSystem not found! Please add it to the scene first.");
            yield break;
        }

        yield return StartCoroutine(MapCoordinateSystem.Instance.WaitForBoundsReady());
        yield return StartCoroutine(LoadAllNodesCoroutine());

        if (allNodes.Count > 0)
        {
            StartCoroutine(LoadAndRenderEdgesCoroutine());
        }
    }

    IEnumerator LoadAllNodesCoroutine()
    {
        string nodesPath = Path.Combine(Application.streamingAssetsPath, "nodes.json");
        if (!File.Exists(nodesPath))
        {
            Debug.LogError("Nodes JSON not found!");
            yield break;
        }

        string nodesJson = File.ReadAllText(nodesPath);
        NodeList nodeList = JsonUtility.FromJson<NodeList>("{\"nodes\":" + nodesJson + "}");

        allNodes.Clear();
        foreach (var node in nodeList.nodes)
        {
            allNodes[node.node_id] = node;

            if (node.linked_building)
            {
                Debug.Log($"🔗 Building Node: {node.name} (ID: {node.node_id})");
            }
        }

        Debug.Log($"📍 Loaded {allNodes.Count} nodes for pathways");
    }

    IEnumerator LoadAndRenderEdgesCoroutine()
    {
        string edgesPath = Path.Combine(Application.streamingAssetsPath, "edges.json");
        if (!File.Exists(edgesPath))
        {
            Debug.LogError("Edges JSON not found!");
            yield break;
        }

        string edgesJson = File.ReadAllText(edgesPath);
        EdgeList edgeList = JsonUtility.FromJson<EdgeList>("{\"edges\":" + edgesJson + "}");

        int pathwayCount = 0;
        foreach (var edge in edgeList.edges)
        {
            if (!allNodes.ContainsKey(edge.from_node) || !allNodes.ContainsKey(edge.to_node))
            {
                Debug.LogError($"❌ Edge {edge.edge_id} references missing nodes.");
                continue;
            }

            if (edge.path_type != "barrier")
            {
                RenderPathEdge(edge);
                pathwayCount++;
            }
        }

        Debug.Log($"✅ Rendered {pathwayCount} pathway connections");

        if (showPathwayNodes && pathwayNodePrefab != null)
        {
            foreach (var node in allNodes.Values)
                SpawnPathwayNode(node);
        }
    }

    void RenderPathEdge(Edge edge)
    {
        Node fromNode = allNodes[edge.from_node];
        Node toNode = allNodes[edge.to_node];

        Vector2 fromPos = MapCoordinateSystem.Instance.LatLonToMapPosition(fromNode.latitude, fromNode.longitude);
        Vector2 toPos = MapCoordinateSystem.Instance.LatLonToMapPosition(toNode.latitude, toNode.longitude);

        GameObject pathGO = new GameObject($"Path_{edge.edge_id}", typeof(RectTransform), typeof(Image));
        pathGO.transform.SetParent(mapContainer, false);

        RectTransform rt = pathGO.GetComponent<RectTransform>();
        Image img = pathGO.GetComponent<Image>();

        // Pick color/width depending on path type
        Color pathColor = pavedRoadColor;
        float pathWidth = pavedWidth;

        if (edge.path_type.ToLower() == "walkway")
        {
            pathColor = walkwayColor;
            pathWidth = walkwayWidth;
        }

        img.color = pathColor;

        // Position & size
        Vector2 dir = (toPos - fromPos).normalized;
        float distance = Vector2.Distance(fromPos, toPos);

        rt.anchoredPosition = (fromPos + toPos) / 2f;
        float pixelWidth = 8f; // change this to the exact thickness you want
        float scaleFactor = mapContainer.GetComponentInParent<Canvas>().scaleFactor;

        rt.sizeDelta = new Vector2(pixelWidth / scaleFactor, distance);
        Debug.Log($"Path width: {pathWidth}, Applied sizeDelta.x: {rt.sizeDelta.x}");

        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);

        // Order: background < roads < buildings/icons
        if (mapBackground != null)
        {
            int bgIndex = mapBackground.transform.GetSiblingIndex();
            pathGO.transform.SetSiblingIndex(bgIndex + 1);
        }

        pathLines.Add(pathGO);
    }

    void SpawnPathwayNode(Node node)
    {
        Vector2 pos = MapCoordinateSystem.Instance.LatLonToMapPosition(node.latitude, node.longitude);

        GameObject nodeObj = Instantiate(pathwayNodePrefab, mapContainer);
        nodeObj.GetComponent<RectTransform>().anchoredPosition = pos;
        nodeObj.name = $"PathNode_{node.node_id}";
    }

    public void TogglePathwayVisibility()
    {
        foreach (var pathLine in pathLines)
            pathLine.SetActive(!pathLine.activeInHierarchy);
    }

    public void SetPathwayOpacity(float alpha)
    {
        foreach (var pathLine in pathLines)
        {
            var image = pathLine.GetComponent<Image>();
            if (image == null) continue;

            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }
    }
}
