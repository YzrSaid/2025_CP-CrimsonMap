using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using System.Linq;
using Mapbox.Utils;
using Mapbox.Unity.Map;

public class PathRenderer : MonoBehaviour
{
    [Header("Mapbox")]
    public AbstractMap mapboxMap;

    [Header("Path Prefabs")]
    public GameObject pathPrefab; // Single prefab for all pathway connections

    [Header("Settings")]
    public bool enableDebugLogs = true;
    public float pathWidth = 1f;
    public float pathHeightOffset = 1f;

    [Header("Path Appearance")]
    public Color pathwayColor = new Color(0.8f, 0.6f, 0.4f, 0.9f); // Default pathway color

    // Current map data - set by MapManager
    private string currentMapId;
    private List<string> currentCampusIds = new List<string>();

    // Track spawned paths with their location components
    private List<PathEdge> spawnedPaths = new List<PathEdge>();
    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>();

    private bool isRendering = false;

    void Awake()
    {
        // Find map if not assigned
        if (mapboxMap == null)
        {
            mapboxMap = FindObjectOfType<AbstractMap>();
        }
    }

    void Start()
    {
        DebugLog("🛤️ PathRenderer started - waiting for MapManager");

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
        else
        {
            Debug.LogWarning("⚠️ MapManager.Instance is null - PathRenderer will not receive map change events");
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from MapManager events
        if (MapManager.Instance != null)
        {
            MapManager.Instance.OnMapChanged -= OnMapChanged;
            MapManager.Instance.OnMapLoadingStarted -= OnMapLoadingStarted;
        }
    }

    #region MapManager Integration

    /// <summary>
    /// Called by MapManager to set current map data
    /// </summary>
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

    /// <summary>
    /// Called when MapManager changes the map
    /// </summary>
    private void OnMapChanged(MapInfo mapInfo)
    {
        DebugLog($"🔄 Map changed to: {mapInfo.map_name} (ID: {mapInfo.map_id})");
        SetCurrentMapData(mapInfo.map_id, mapInfo.campus_included);
    }

    /// <summary>
    /// Called when MapManager starts loading a new map
    /// </summary>
    private void OnMapLoadingStarted()
    {
        DebugLog("🧹 Map loading started - clearing existing paths");
        ClearSpawnedPaths();
    }

    /// <summary>
    /// Main method called by MapManager to load and render paths for a specific map
    /// </summary>
    public IEnumerator LoadAndRenderPathsForMap(string mapId, List<string> campusIds)
    {
        if (isRendering)
        {
            DebugLog("⚠️ Already rendering paths, skipping");
            yield break;
        }

        DebugLog($"🛤️ LoadAndRenderPathsForMap called - Map: {mapId}, Campuses: {string.Join(", ", campusIds ?? new List<string>())}");

        // Update current map data
        SetCurrentMapData(mapId, campusIds);

        // Wait for map to be ready
        yield return StartCoroutine(WaitForMapReady());

        // Start rendering
        yield return StartCoroutine(LoadAndRenderPaths());

        DebugLog($"✅ Map '{mapId}' path rendering completed");
    }

    #endregion

    #region Map Readiness Check

    private IEnumerator WaitForMapReady()
    {
        DebugLog("⏳ Waiting for map to be ready...");

        float timeout = 30f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (mapboxMap != null && mapboxMap.gameObject.activeInHierarchy)
            {
                DebugLog($"🗺️ Map ready after {elapsed:F1}s");
                break;
            }

            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;

            if (elapsed % 5f < 0.6f)
            {
                DebugLog($"⏳ Still waiting for map... ({elapsed:F1}s/{timeout}s)");
            }
        }

        if (elapsed >= timeout)
        {
            Debug.LogError("❌ Map initialization timeout!");
            yield break;
        }

        yield return new WaitForSeconds(1f); // Extra buffer time
    }

    #endregion

    #region File Name Generation

    /// <summary>
    /// Get the appropriate nodes file name based on current map
    /// </summary>
    private string GetNodesFileName()
    {
        if (string.IsNullOrEmpty(currentMapId))
        {
            Debug.LogWarning("⚠️ No current map ID set, using default nodes.json");
            return "nodes.json";
        }
        
        string fileName = $"nodes_{currentMapId}.json";
        DebugLog($"📁 Using nodes file: {fileName}");
        return fileName;
    }

    /// <summary>
    /// Get the appropriate edges file name based on current map
    /// </summary>
    private string GetEdgesFileName()
    {
        if (string.IsNullOrEmpty(currentMapId))
        {
            Debug.LogWarning("⚠️ No current map ID set, using default edges.json");
            return "edges.json";
        }
        
        string fileName = $"edges_{currentMapId}.json";
        DebugLog($"📁 Using edges file: {fileName}");
        return fileName;
    }

    #endregion

    #region Main Loading and Rendering

    public IEnumerator LoadAndRenderPaths()
    {
        if (isRendering)
        {
            DebugLog("⚠️ Already rendering paths");
            yield break;
        }

        isRendering = true;
        DebugLog($"🛤️ Starting LoadAndRenderPaths for map: {currentMapId}");

        if (string.IsNullOrEmpty(currentMapId))
        {
            Debug.LogError("❌ No current map ID set - cannot load paths");
            isRendering = false;
            yield break;
        }

        List<Edge> validEdges = null;
        bool errorOccurred = false;

        try
        {
            if (currentCampusIds == null || currentCampusIds.Count == 0)
            {
                Debug.LogWarning("⚠️ No campus IDs available for path loading");
            }

            // Clear existing paths first
            ClearSpawnedPaths();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error in LoadAndRenderPaths: {e.Message}");
            errorOccurred = true;
        }
        finally
        {
            isRendering = false;
        }

        if (errorOccurred)
        {
            yield break;
        }

        // Load and filter nodes from map-specific file
        yield return StartCoroutine(LoadFilteredNodes(currentCampusIds));

        if (allNodes.Count == 0)
        {
            Debug.LogError("❌ No pathway or infrastructure nodes loaded");
            yield break;
        }

        // Load and filter edges from map-specific file
        yield return StartCoroutine(LoadEdgesFromJSONAsync((edges) => {
            if (edges == null || edges.Length == 0)
            {
                Debug.LogError($"❌ Failed to load edges from {GetEdgesFileName()}");
                return;
            }

            // Filter valid pathway edges
            validEdges = FilterValidPathwayEdges(edges);
        }));

        if (validEdges == null)
        {
            yield break;
        }

        DebugLog($"🛤️ Found {validEdges.Count} valid pathway edges to render");

        if (validEdges.Count == 0)
        {
            Debug.LogWarning("⚠️ No valid pathway edges found matching criteria");
            yield break;
        }

        // Render the paths
        yield return StartCoroutine(RenderPathEdges(validEdges));

        Debug.Log($"✅ PathRenderer completed: {spawnedPaths.Count} paths rendered for map {currentMapId}");
    }

    #endregion

    #region Node and Edge Loading

    private IEnumerator LoadFilteredNodes(List<string> campusIds)
    {
        DebugLog($"📂 Loading filtered nodes from: {GetNodesFileName()}");

        bool loadCompleted = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            GetNodesFileName(),
            // onSuccess
            (jsonContent) => {
                try
                {
                    DebugLog($"📄 Read {jsonContent.Length} characters from nodes file");

                    Node[] nodes = JsonHelper.FromJson<Node>(jsonContent);
                    DebugLog($"📊 Parsed {nodes?.Length ?? 0} nodes from JSON");

                    allNodes.Clear();

                    // Filter for pathway nodes only
                    var pathwayNodes = nodes.Where(n =>
                        n != null &&
                        n.is_active &&
                        (n.type == "pathway" || n.type == "infrastructure") &&
                        (campusIds == null || campusIds.Count == 0 || campusIds.Contains(n.campus_id)) &&
                        IsValidCoordinate(n.latitude, n.longitude)
                    ).ToList();

                    foreach (var node in pathwayNodes)
                    {
                        allNodes[node.node_id] = node;
                    }

                    DebugLog($"🔍 Node filtering process for map {currentMapId}:");
                    DebugLog($"  - Total nodes: {nodes.Length}");
                    DebugLog($"  - Active nodes: {nodes.Count(n => n?.is_active == true)}");
                    DebugLog($"  - Pathway nodes in target campuses ({string.Join(", ", campusIds ?? new List<string>())}): {allNodes.Count}");

                    loadCompleted = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ Error parsing nodes: {e.Message}");
                    loadCompleted = true;
                }
            },
            // onError
            (error) => {
                Debug.LogError($"❌ Error loading nodes: {error}");
                loadCompleted = true;
            }
        ));

        // Wait for load to complete
        yield return new WaitUntil(() => loadCompleted);
    }

    private IEnumerator LoadEdgesFromJSONAsync(System.Action<Edge[]> onComplete)
    {
        DebugLog($"📂 Loading edges from: {GetEdgesFileName()}");

        bool loadCompleted = false;
        Edge[] edges = null;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            GetEdgesFileName(),
            // onSuccess
            (jsonContent) => {
                try
                {
                    DebugLog($"📄 Read {jsonContent.Length} characters from edges file");

                    edges = JsonHelper.FromJson<Edge>(jsonContent);
                    DebugLog($"📊 Parsed {edges?.Length ?? 0} edges from JSON");

                    loadCompleted = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ Error parsing edges JSON: {e.Message}");
                    loadCompleted = true;
                }
            },
            // onError
            (error) => {
                Debug.LogError($"❌ Error loading edges: {error}");
                loadCompleted = true;
            }
        ));

        // Wait for load to complete
        yield return new WaitUntil(() => loadCompleted);
        onComplete?.Invoke(edges);
    }

    #endregion

    #region Edge Filtering and Rendering

    private List<Edge> FilterValidPathwayEdges(Edge[] allEdges)
    {
        DebugLog($"🔍 Available pathway nodes: {allNodes.Count} nodes loaded");

        var validEdges = new List<Edge>();
        var activeEdges = allEdges.Where(e => e != null && e.is_active).ToList();

        foreach (var edge in activeEdges)
        {
            bool hasFromNode = allNodes.ContainsKey(edge.from_node);
            bool hasToNode = allNodes.ContainsKey(edge.to_node);

            if (hasFromNode && hasToNode)
            {
                validEdges.Add(edge);
                DebugLog($"✅ Valid edge: {edge.edge_id} ({edge.from_node} -> {edge.to_node})");
            }
            else
            {
                DebugLog($"❌ Invalid edge {edge.edge_id}: from_node={edge.from_node} (exists: {hasFromNode}), to_node={edge.to_node} (exists: {hasToNode})");
            }
        }

        DebugLog($"🔍 Edge filtering process for map {currentMapId}:");
        DebugLog($"  - Total edges: {allEdges.Length}");
        DebugLog($"  - Active edges: {activeEdges.Count}");
        DebugLog($"  - Valid pathway connections: {validEdges.Count}");

        return validEdges;
    }

    private IEnumerator RenderPathEdges(List<Edge> edges)
    {
        DebugLog($"🛤️ Rendering {edges.Count} pathway edges for map {currentMapId}...");

        int renderedCount = 0;
        foreach (var edge in edges)
        {
            bool shouldYield = false;
            try
            {
                if (pathPrefab == null)
                {
                    Debug.LogError("❌ Path prefab is null!");
                    break;
                }

                // Get the connected nodes
                if (!allNodes.TryGetValue(edge.from_node, out Node fromNode) ||
                    !allNodes.TryGetValue(edge.to_node, out Node toNode))
                {
                    Debug.LogError($"❌ Could not find nodes for edge {edge.edge_id}: {edge.from_node} -> {edge.to_node}");
                    continue;
                }

                // Create the path GameObject
                GameObject pathObj = Instantiate(pathPrefab, Vector3.zero, Quaternion.identity, mapboxMap.transform);
                pathObj.name = $"Pathway_{edge.edge_id}_{edge.from_node}_to_{edge.to_node}";

                // Add the path component
                PathEdge pathComponent = pathObj.AddComponent<PathEdge>();
                pathComponent.Initialize(mapboxMap, edge, fromNode, toNode, pathWidth, pathHeightOffset, pathwayColor);

                spawnedPaths.Add(pathComponent);
                renderedCount++;

                DebugLog($"🛤️ Rendered pathway: {edge.edge_id} ({edge.from_node} -> {edge.to_node})");

                // Mark for yielding periodically to avoid frame drops
                if (renderedCount % 10 == 0)
                {
                    shouldYield = true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Error rendering path {edge.edge_id}: {e.Message}");
            }

            if (shouldYield)
            {
                yield return null;
            }
        }

        DebugLog($"✅ Successfully rendered {renderedCount} pathway edges for map {currentMapId}");
    }

    #endregion

    #region Public Utility Methods

    public void ClearSpawnedPaths()
    {
        DebugLog($"🧹 Clearing {spawnedPaths.Count} pathway edges");

        foreach (var path in spawnedPaths)
        {
            if (path != null && path.gameObject != null)
            {
                DestroyImmediate(path.gameObject);
            }
        }

        spawnedPaths.Clear();
        allNodes.Clear();

        DebugLog("✅ Cleared all pathway edges");
    }

    public void ForceUpdateAllPaths()
    {
        foreach (var path in spawnedPaths)
        {
            if (path != null)
            {
                path.ForceUpdate();
            }
        }
    }

    #endregion

    #region Legacy/Debug Methods

    // Manual render methods for testing
    public void ManualRender()
    {
        DebugLog("🔄 Manual render triggered");
        if (!string.IsNullOrEmpty(currentMapId))
        {
            StartCoroutine(LoadAndRenderPaths());
        }
        else
        {
            Debug.LogWarning("⚠️ No current map set - cannot manually render");
        }
    }

    [System.Obsolete("Debug method - remove in production")]
    public void ForceResetRendering()
    {
        DebugLog("🔄 Force resetting rendering state");
        isRendering = false;
        StopAllCoroutines();
        DebugLog($"✅ Reset complete");
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
            Debug.Log($"[PathRenderer] {message}");
        }
    }

    #endregion

    #region Debug Input Handling

    void Update()
    {
        // Debug controls
        if (Application.isEditor && enableDebugLogs)
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                Debug.Log($"=== PATH RENDERER STATUS ===");
                Debug.Log($"Current Map ID: {currentMapId ?? "None"}");
                Debug.Log($"Current Campus IDs: {string.Join(", ", currentCampusIds)}");
                Debug.Log($"Is rendering: {isRendering}");
                Debug.Log($"Paths rendered: {spawnedPaths.Count}");
                Debug.Log($"Pathway nodes loaded: {allNodes.Count}");
                Debug.Log($"Map assigned: {mapboxMap != null}");
                Debug.Log($"Nodes file: {GetNodesFileName()}");
                Debug.Log($"Edges file: {GetEdgesFileName()}");
            }

            if (Input.GetKeyDown(KeyCode.O))
            {
                ManualRender();
            }

            if (Input.GetKeyDown(KeyCode.L))
            {
                ClearSpawnedPaths();
            }
        }
    }

    #endregion
}

// PathEdge class remains the same as before
public class PathEdge : MonoBehaviour
{
    private AbstractMap map;
    private Edge edgeData;
    private Node fromNode;
    private Node toNode;
    private float baseWidth;
    private float heightOffset;
    private Color pathColor;

    // Cache for consistent scaling
    private float referenceZoomLevel;
    private Vector3 referenceFromPos;
    private Vector3 referenceToPos;
    private float referenceDistance;
    private bool isInitialized = false;

    public Edge GetEdgeData() => edgeData;
    public Node GetFromNode() => fromNode;
    public Node GetToNode() => toNode;

    public void Initialize(AbstractMap mapReference, Edge edge, Node from, Node to,
                        float pathWidth, float height, Color color)
    {
        map = mapReference;
        edgeData = edge;
        fromNode = from;
        toNode = to;
        baseWidth = pathWidth;
        heightOffset = height;
        pathColor = color;

        // Store reference zoom level and positions
        if (map != null)
        {
            referenceZoomLevel = map.Zoom;
            
            // Calculate reference positions and distance at current zoom
            referenceFromPos = map.GeoToWorldPosition(new Vector2d(fromNode.latitude, fromNode.longitude), false);
            referenceToPos = map.GeoToWorldPosition(new Vector2d(toNode.latitude, toNode.longitude), false);
            referenceDistance = Vector3.Distance(referenceFromPos, referenceToPos);
            
            isInitialized = true;
            
            Debug.Log($"Path {edge.edge_id} initialized at zoom {referenceZoomLevel:F1} with reference distance {referenceDistance:F3}");
        }

        // Apply path color
        ApplyColorToPath(pathColor);

        // Initial transform update
        UpdatePathTransform();
    }

    private void ApplyColorToPath(Color color)
    {
        // Apply color to all renderers in the path prefab
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (renderer.material != null)
            {
                renderer.material.color = color;
            }
        }
    }

    void LateUpdate()
    {
        if (map != null && fromNode != null && toNode != null && isInitialized)
        {
            // Update every few frames for smooth movement
            if (Time.frameCount % 2 == 0)
            {
                UpdatePathTransform();
            }
        }
    }

    void UpdatePathTransform()
    {
        if (fromNode == null || toNode == null || map == null || !isInitialized) return;

        // Get current positions
        Vector3 fromPos = map.GeoToWorldPosition(new Vector2d(fromNode.latitude, fromNode.longitude), false);
        Vector3 toPos = map.GeoToWorldPosition(new Vector2d(toNode.latitude, toNode.longitude), false);

        // Apply height offset
        fromPos.y = heightOffset;
        toPos.y = heightOffset;

        // Calculate direction
        Vector3 direction = toPos - fromPos;
        float currentDistance = direction.magnitude;

        // Hide if nodes are too close (prevents visual glitches)
        if (currentDistance < 0.001f)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        // Position at the center between nodes
        Vector3 centerPos = (fromPos + toPos) * 0.5f;
        transform.position = centerPos;

        // Rotate to face the correct direction
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        // KEY FIX: Use the reference distance instead of current distance
        // This keeps the visual length consistent regardless of zoom
        float visualDistance = referenceDistance;
        
        // Apply the scale using the reference distance
        transform.localScale = new Vector3(baseWidth, baseWidth, visualDistance);

        // Debug info (uncomment if needed)
        //Debug.Log($"Path {edgeData.edge_id}: current={currentDistance:F3}, reference={referenceDistance:F3}, zoom={map.Zoom:F1}");
    }

    public void ForceUpdate()
    {
        if (map != null && isInitialized)
        {
            UpdatePathTransform();
        }
    }

    // Optional: Debug visualization in Scene view
    void OnDrawGizmosSelected()
    {
        if (fromNode != null && toNode != null && map != null)
        {
            Vector3 fromPos = map.GeoToWorldPosition(new Vector2d(fromNode.latitude, fromNode.longitude), false);
            Vector3 toPos = map.GeoToWorldPosition(new Vector2d(toNode.latitude, toNode.longitude), false);

            fromPos.y = heightOffset;
            toPos.y = heightOffset;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.TransformPoint(fromPos), 0.2f);
            Gizmos.DrawWireSphere(transform.TransformPoint(toPos), 0.2f);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.TransformPoint(fromPos), transform.TransformPoint(toPos));
            
            // Show the center point
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.1f);
        }
    }
}