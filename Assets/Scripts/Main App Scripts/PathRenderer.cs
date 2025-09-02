using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Linq;

public class PathRenderer : MonoBehaviour
{
    [Header("References")]
    public RectTransform mapContainer;

    [Header("Path Colors")]
    public Color pavedRoadColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
    public Color walkwayColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
    public Color barrierColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);

    [Header("Path Widths")]
    public float pavedWidth = 8f;
    public float walkwayWidth = 5f;
    public float barrierWidth = 3f;

    [Header("Debug")]
    public bool showPathwayNodes = false;
    public GameObject pathwayNodePrefab;

    // Track spawned objects for cleanup
    private List<GameObject> spawnedPaths = new List<GameObject>();
    private List<GameObject> spawnedPathNodes = new List<GameObject>();
    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>();
    private Image mapBackground;

    void Start()
    {
        // Auto-detect map background
        mapBackground = mapContainer.GetComponentInChildren<Image>();
        if (mapBackground == null)
        {
            Debug.LogError("No background image found inside mapContainer!");
        }
        
        Debug.Log("PathRenderer ready - waiting for map assignment");
    }

    /// <summary>
    /// Load and render paths for specific campuses
    /// Called by MapManager when switching maps
    /// </summary>
    public IEnumerator LoadAndRenderForCampuses(List<string> campusIds)
    {
        Debug.Log($"Loading paths for campuses: {string.Join(", ", campusIds)}");

        if (MapCoordinateSystem.Instance == null)
        {
            Debug.LogError("MapCoordinateSystem not found!");
            yield break;
        }

        yield return StartCoroutine(MapCoordinateSystem.Instance.WaitForBoundsReady());
        yield return StartCoroutine(LoadFilteredNodesCoroutine(campusIds));

        if (allNodes.Count > 0)
        {
            yield return StartCoroutine(LoadAndRenderFilteredEdgesCoroutine(campusIds));
        }
    }

    IEnumerator LoadFilteredNodesCoroutine(List<string> campusIds)
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

        // Filter nodes by campus - direct string comparison
        var filteredNodes = nodeList.nodes.Where(n => 
            campusIds.Contains(n.campus_id)
        ).ToList();

        foreach (var node in filteredNodes)
        {
            allNodes[node.node_id] = node;
            if (node.type == "infrastructure")
            {
                Debug.Log($"Building Node: {node.name} (ID: {node.node_id}) Campus: {node.campus_id}");
            }
        }

        Debug.Log($"Loaded {allNodes.Count} nodes for pathways in campuses: {string.Join(", ", campusIds)}");
    }

    IEnumerator LoadAndRenderFilteredEdgesCoroutine(List<string> campusIds)
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

        // Filter edges: both nodes must exist and not be barriers
        var validEdges = edgeList.edges.Where(e => 
            e.path_type != "barrier" && 
            e.is_active &&
            allNodes.ContainsKey(e.from_node) && 
            allNodes.ContainsKey(e.to_node)
        ).ToList();

        foreach (var edge in validEdges)
        {
            RenderPathEdge(edge);
            pathwayCount++;
        }

        Debug.Log($"Rendered {pathwayCount} pathway connections for selected campuses");

        // Spawn pathway nodes if debug mode is enabled
        if (showPathwayNodes && pathwayNodePrefab != null)
        {
            foreach (var node in allNodes.Values)
            {
                if (node.type != "barrier")
                {
                    SpawnPathwayNode(node);
                }
            }
        }
    }

    void RenderPathEdge(Edge edge)
    {
        Node fromNode = allNodes[edge.from_node];
        Node toNode = allNodes[edge.to_node];

        Vector2 posA = MapCoordinateSystem.Instance.LatLonToMapPosition(fromNode.latitude, fromNode.longitude);
        Vector2 posB = MapCoordinateSystem.Instance.LatLonToMapPosition(toNode.latitude, toNode.longitude);

        GameObject lineObj = new GameObject($"Path_{edge.edge_id}", typeof(RectTransform), typeof(Image));
        lineObj.transform.SetParent(mapContainer, false);

        RectTransform rt = lineObj.GetComponent<RectTransform>();
        Image img = lineObj.GetComponent<Image>();

        // Pick color/width based on path type
        Color pathColor = pavedRoadColor;
        float pathWidth = pavedWidth;

        switch (edge.path_type.ToLower())
        {
            case "walkway":
                pathColor = walkwayColor;
                pathWidth = walkwayWidth;
                break;
            case "paved":
                pathColor = pavedRoadColor;
                pathWidth = pavedWidth;
                break;
        }

        img.color = pathColor;

        // Position and rotation
        Vector2 dir = (posB - posA).normalized;
        float distance = Vector2.Distance(posA, posB);

        rt.anchoredPosition = (posA + posB) / 2f;
        
        float scaleFactor = mapContainer.GetComponentInParent<Canvas>().scaleFactor;
        rt.sizeDelta = new Vector2(pathWidth / scaleFactor, distance);
        
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);

        // Set layer order: background < paths < buildings
        if (mapBackground != null)
        {
            int bgIndex = mapBackground.transform.GetSiblingIndex();
            lineObj.transform.SetSiblingIndex(bgIndex + 1);
        }

        spawnedPaths.Add(lineObj);
    }

    void SpawnPathwayNode(Node node)
    {
        Vector2 pos = MapCoordinateSystem.Instance.LatLonToMapPosition(node.latitude, node.longitude);
        
        GameObject nodeObj = Instantiate(pathwayNodePrefab, mapContainer);
        nodeObj.GetComponent<RectTransform>().anchoredPosition = pos;
        nodeObj.name = $"PathNode_{node.node_id}";
        
        spawnedPathNodes.Add(nodeObj);
    }

    /// <summary>
    /// Clear all spawned path objects when switching maps
    /// </summary>
    public void ClearSpawnedObjects()
    {
        // Clear path lines
        foreach (var obj in spawnedPaths)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedPaths.Clear();

        // Clear debug nodes
        foreach (var obj in spawnedPathNodes)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedPathNodes.Clear();

        allNodes.Clear();
        Debug.Log("PathRenderer: Cleared all spawned paths");
    }

    public void TogglePathwayVisibility()
    {
        foreach (var pathLine in spawnedPaths)
        {
            if (pathLine != null)
                pathLine.SetActive(!pathLine.activeInHierarchy);
        }
    }

    public void SetPathwayOpacity(float alpha)
    {
        foreach (var pathLine in spawnedPaths)
        {
            if (pathLine == null) continue;
            
            var image = pathLine.GetComponent<Image>();
            if (image == null) continue;
            
            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }
    }
}