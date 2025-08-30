using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using System.Linq;

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

    // Track spawned objects for cleanup
    private List<GameObject> spawnedNodes = new List<GameObject>();
    private List<GameObject> spawnedEdges = new List<GameObject>();
    private List<GameObject> spawnedPolygons = new List<GameObject>();

    void Start()
    {
        // Don't auto-load anymore - wait for MapManager
        Debug.Log("🚧 BarrierSpawner ready - waiting for map assignment");
    }

    /// <summary>
    /// Load and spawn barriers for specific campuses
    /// Called by MapManager when switching maps
    /// </summary>
    public IEnumerator LoadAndSpawnForCampuses(List<string> campusIds)
    {
        Debug.Log($"🚧 Loading barriers for campuses: {string.Join(", ", campusIds)}");

        // Wait for coordinate system
        if (MapCoordinateSystem.Instance == null)
        {
            Debug.LogError("❌ MapCoordinateSystem not found!");
            yield break;
        }

        yield return StartCoroutine(MapCoordinateSystem.Instance.WaitForBoundsReady());

        // Load data
        string nodesPath = Path.Combine(Application.streamingAssetsPath, nodesFileName);
        string edgesPath = Path.Combine(Application.streamingAssetsPath, edgesFileName);

        if (!File.Exists(nodesPath) || !File.Exists(edgesPath))
        {
            Debug.LogError("❌ JSON files not found in StreamingAssets!");
            yield break;
        }

        NodeList nodeList = JsonUtility.FromJson<NodeList>("{\"nodes\":" + File.ReadAllText(nodesPath) + "}");
        EdgeList edgeList = JsonUtility.FromJson<EdgeList>("{\"edges\":" + File.ReadAllText(edgesPath) + "}");

        // Convert campus string IDs to integers for comparison
        List<int> campusIntIds = new List<int>();


        // Filter nodes by campus
        var filteredNodes = nodeList.nodes.Where(n =>
     campusIds.Contains(n.campus_id) &&
     n.is_barrier &&
     n.is_active
 ).ToList();


        Debug.Log($"🚧 Found {filteredNodes.Count} barrier nodes for campuses: {string.Join(", ", campusIds)} (IDs: {string.Join(", ", campusIntIds)})");

        Dictionary<string, Node> nodeDict = new Dictionary<string, Node>();

        // Spawn barrier nodes
        foreach (var node in filteredNodes)
        {
            nodeDict[node.node_id] = node;
            Vector2 pos = MapCoordinateSystem.Instance.LatLonToMapPosition(node.latitude, node.longitude);

            GameObject nodeObj = Instantiate(nodePrefab, mapContainer);
            nodeObj.GetComponent<RectTransform>().anchoredPosition = pos;
            nodeObj.name = $"BarrierNode_{node.node_id}";

            spawnedNodes.Add(nodeObj);
            Debug.Log($"🚧 Spawned barrier node {node.name} at {pos}");
        }

        // Filter and spawn barrier edges
        var filteredEdges = edgeList.edges.Where(e =>
            e.path_type == "barrier" &&
            e.is_active &&
            nodeDict.ContainsKey(e.from_node) &&
            nodeDict.ContainsKey(e.to_node)
        ).ToList();

        foreach (var edge in filteredEdges)
        {
            Node fromNode = nodeDict[edge.from_node];
            Node toNode = nodeDict[edge.to_node];
            SpawnEdge(fromNode, toNode, edge.edge_id);
        }

        // Group barrier nodes by campus and create polygons for each campus
        var nodesByCampus = filteredNodes.GroupBy(n => n.campus_id).ToList();

        foreach (var campusGroup in nodesByCampus)
        {
            List<Node> campusBarrierNodes = campusGroup.ToList();
            if (campusBarrierNodes.Count >= 3)
            {
                SpawnBarrierPolygon(campusBarrierNodes, campusGroup.Key);
            }
        }

        Debug.Log($"✅ BarrierSpawner completed: {filteredNodes.Count} barrier nodes, {filteredEdges.Count} edges, {nodesByCampus.Count} campus polygons");
    }

    void SpawnEdge(Node fromNode, Node toNode, string edgeId)
    {
        Vector2 posA = MapCoordinateSystem.Instance.LatLonToMapPosition(fromNode.latitude, fromNode.longitude);
        Vector2 posB = MapCoordinateSystem.Instance.LatLonToMapPosition(toNode.latitude, toNode.longitude);

        GameObject lineObj = Instantiate(connectingLinePrefab, mapContainer);
        lineObj.name = $"BarrierEdge_{edgeId}";

        RectTransform rt = lineObj.GetComponent<RectTransform>();
        Vector2 diff = posB - posA;
        float distance = diff.magnitude;

        rt.anchoredPosition = (posA + posB) / 2f;
        rt.sizeDelta = new Vector2(distance, lineThickness);

        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        rt.rotation = Quaternion.Euler(0, 0, angle);

        spawnedEdges.Add(lineObj);
    }

    void SpawnBarrierPolygon(List<Node> barrierNodes, string campusId)
    {
        if (barrierNodes.Count < 3) return;

        GameObject polyObj = new GameObject($"BarrierPolygon_Campus_{campusId}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(PolygonImage));

        // Get reference to mapImage from MapCoordinateSystem
        RectTransform mapImage = MapCoordinateSystem.Instance.mapImage;

        // Parent to mapImage instead of mapContainer
        polyObj.transform.SetParent(mapImage, false);

        RectTransform rt = polyObj.GetComponent<RectTransform>();

        // Match polygon to mapImage size
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        PolygonImage pg = polyObj.GetComponent<PolygonImage>();
        pg.color = new Color(0.8f, 0.8f, 0.8f, 0.5f);
        pg.material = new Material(Shader.Find("UI/Default"));

        // Now use coordinates directly since polygon is same size as mapImage
        foreach (var node in barrierNodes)
        {
            Vector2 pos = MapCoordinateSystem.Instance.LatLonToMapPosition(node.latitude, node.longitude);
            pg.points.Add(pos);
        }

        polyObj.transform.SetSiblingIndex(1);
        pg.SetVerticesDirty();

        spawnedPolygons.Add(polyObj);

        Debug.Log($"Created barrier polygon for Campus {campusId} with {barrierNodes.Count} points");
    }
    /// <summary>
    /// Clear all spawned objects when switching maps
    /// </summary>
    public void ClearSpawnedObjects()
    {
        // Clear nodes
        foreach (var obj in spawnedNodes)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedNodes.Clear();

        // Clear edges  
        foreach (var obj in spawnedEdges)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedEdges.Clear();

        // Clear polygons
        foreach (var obj in spawnedPolygons)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedPolygons.Clear();

        Debug.Log("🧹 BarrierSpawner: Cleared all spawned objects");
    }

    /// <summary>
    /// Get filtered nodes for current campuses (for use by other spawners)
    /// </summary>
    public List<Node> GetFilteredBarrierNodes()
    {
        string nodesPath = Path.Combine(Application.streamingAssetsPath, nodesFileName);
        if (!File.Exists(nodesPath)) return new List<Node>();

        NodeList nodeList = JsonUtility.FromJson<NodeList>("{\"nodes\":" + File.ReadAllText(nodesPath) + "}");

        // Get current campus IDs from the coordinate system
        List<string> campusIds = MapCoordinateSystem.Instance.GetCurrentCampusIds();

        return nodeList.nodes.Where(n =>
            campusIds.Contains("C-" + n.campus_id.ToString().PadLeft(3, '0')) &&
            n.is_barrier &&
            n.is_active
        ).ToList();
    }
}