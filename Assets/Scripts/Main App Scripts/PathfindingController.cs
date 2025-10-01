using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public class PathfindingController : MonoBehaviour
{
    [Header("References")]
    public AStarPathfinding pathfinding;
    public InfrastructurePopulator infrastructurePopulator; // Reference to your existing populator

    [Header("UI Elements")]
    public TMP_Dropdown fromDropdown;
    public TMP_Dropdown toDropdown;
    public Button findPathButton;
    
    [Header("Result Display")]
    public GameObject resultPanel;
    public TextMeshProUGUI distanceText;
    public TextMeshProUGUI walkingTimeText;
    public TextMeshProUGUI pathInfoText;

    [Header("Static Test Settings")]
    public bool useStaticTesting = true; // Use hardcoded nodes for testing
    public string staticFromNodeId = "ND-025"; // Default: Gate Camp B for employees
    public string staticToNodeId = "ND-017"; // Default: College of Computing Studies

    [Header("Settings")]
    public bool enableDebugLogs = true;

    // Data
    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>(); // All nodes from pathfinding
    private Dictionary<string, string> infraIdToNodeId = new Dictionary<string, string>(); // Map infra_id to node_id
    private string selectedFromNodeId;
    private string selectedToNodeId;

    void Start()
    {
        DebugLog("🎮 PathfindingController initialized");

        // Setup UI listeners
        if (findPathButton != null)
        {
            findPathButton.onClick.AddListener(OnFindPathClicked);
        }

        // DON'T setup dropdown listeners - InfrastructurePopulator handles the dropdowns

        // Hide result panel initially
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        // Subscribe to MapManager events
        if (MapManager.Instance != null)
        {
            MapManager.Instance.OnMapChanged += OnMapChanged;
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        if (findPathButton != null)
        {
            findPathButton.onClick.RemoveListener(OnFindPathClicked);
        }

        if (MapManager.Instance != null)
        {
            MapManager.Instance.OnMapChanged -= OnMapChanged;
        }
    }

    #region MapManager Integration

    private void OnMapChanged(MapInfo mapInfo)
    {
        DebugLog($"🔄 Map changed - loading node data");
        ClearCurrentPath();
    }

    /// <summary>
    /// Called by MapManager when map data is ready
    /// </summary>
    public IEnumerator InitializeForMap(string mapId, List<string> campusIds)
    {
        DebugLog($"🗺️ Initializing for map: {mapId}");

        // Wait for pathfinding to load graph data
        if (pathfinding != null)
        {
            yield return StartCoroutine(pathfinding.LoadGraphDataForMap(mapId, campusIds));
        }

        // Build infrastructure to node mapping
        yield return StartCoroutine(BuildInfrastructureNodeMapping());

        DebugLog("✅ PathfindingController initialized for map");
    }

    #endregion

    #region Infrastructure to Node Mapping

    /// <summary>
    /// Build mapping between infrastructure IDs and node IDs
    /// </summary>
    private IEnumerator BuildInfrastructureNodeMapping()
    {
        if (pathfinding == null)
        {
            Debug.LogError("❌ AStarPathfinding reference not set!");
            yield break;
        }

        // Get all nodes from pathfinding
        allNodes = pathfinding.GetAllNodes();

        if (allNodes == null || allNodes.Count == 0)
        {
            Debug.LogWarning("⚠️ No nodes available from pathfinding");
            yield break;
        }

        // Build mapping: related_infra_id → node_id
        infraIdToNodeId.Clear();
        
        foreach (var kvp in allNodes)
        {
            Node node = kvp.Value;
            
            // Map infrastructure nodes by their related_infra_id
            if (node.type == "infrastructure" && !string.IsNullOrEmpty(node.related_infra_id))
            {
                infraIdToNodeId[node.related_infra_id] = node.node_id;
            }
        }

        DebugLog($"📊 Built infrastructure mapping: {infraIdToNodeId.Count} infrastructure nodes");

        // Set static FROM location if using it
        if (useStaticTesting)
        {
            selectedFromNodeId = staticFromNodeId;
            DebugLog($"📍 FROM set to static: {staticFromNodeId}");
        }

        yield return null;
    }

    #endregion

    #region Dropdown Handlers (Using InfrastructurePopulator)

    /// <summary>
    /// Get the node_id from the selected infrastructure in dropdown
    /// </summary>
    private string GetNodeIdFromDropdown(TMP_Dropdown dropdown)
    {
        if (infrastructurePopulator == null)
        {
            Debug.LogError("❌ InfrastructurePopulator reference not set!");
            return null;
        }

        // Get the selected infrastructure from InfrastructurePopulator
        Infrastructure selectedInfra = infrastructurePopulator.GetSelectedInfrastructure(dropdown);

        if (selectedInfra == null)
        {
            DebugLog("⚠️ No infrastructure selected from dropdown");
            return null;
        }

        // Map infrastructure ID to node ID
        if (infraIdToNodeId.TryGetValue(selectedInfra.infra_id, out string nodeId))
        {
            DebugLog($"✅ Mapped {selectedInfra.name} (infra: {selectedInfra.infra_id}) → node: {nodeId}");
            return nodeId;
        }
        else
        {
            Debug.LogWarning($"⚠️ No node found for infrastructure: {selectedInfra.name} ({selectedInfra.infra_id})");
            return null;
        }
    }

    #endregion

    #region Pathfinding Trigger

    private void OnFindPathClicked()
    {
        DebugLog("🔍 Find Path button clicked");

        string fromNodeId;
        string toNodeId;

        // STATIC TESTING MODE
        if (useStaticTesting)
        {
            fromNodeId = staticFromNodeId;
            toNodeId = staticToNodeId;
            
            DebugLog($"🧪 STATIC TEST MODE: FROM={fromNodeId}, TO={toNodeId}");
        }
        else
        {
            // Normal dropdown mode (for later)
            fromNodeId = GetNodeIdFromDropdown(fromDropdown);
            toNodeId = GetNodeIdFromDropdown(toDropdown);

            if (string.IsNullOrEmpty(fromNodeId))
            {
                Debug.LogWarning("⚠️ No FROM location selected!");
                ShowError("Please select a starting point");
                return;
            }

            if (string.IsNullOrEmpty(toNodeId))
            {
                Debug.LogWarning("⚠️ No TO location selected!");
                ShowError("Please select a destination");
                return;
            }
        }

        // Check if FROM and TO are the same
        if (fromNodeId == toNodeId)
        {
            Debug.LogWarning("⚠️ FROM and TO are the same location!");
            ShowError("You are already at this location!");
            return;
        }

        DebugLog($"🧭 Pathfinding: FROM={fromNodeId}, TO={toNodeId}");

        // Start pathfinding
        StartCoroutine(FindAndDisplayPath(fromNodeId, toNodeId));
    }

    private IEnumerator FindAndDisplayPath(string fromNodeId, string toNodeId)
    {
        if (pathfinding == null)
        {
            Debug.LogError("❌ AStarPathfinding reference not set!");
            yield break;
        }

        DebugLog($"🧭 Finding path: {fromNodeId} → {toNodeId}");

        // Disable button during calculation
        if (findPathButton != null)
        {
            findPathButton.interactable = false;
        }

        // Find the path
        yield return StartCoroutine(pathfinding.FindPath(fromNodeId, toNodeId));

        // Re-enable button
        if (findPathButton != null)
        {
            findPathButton.interactable = true;
        }

        // Check if path was found
        if (!pathfinding.HasPath())
        {
            ShowError("No path found between these locations");
            yield break;
        }

        // Display results
        DisplayPathResults();
    }

    #endregion

    #region Result Display

    private void DisplayPathResults()
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        // Get path data
        var path = pathfinding.GetCurrentPath();
        float distance = pathfinding.GetTotalDistance();
        string formattedDistance = pathfinding.GetFormattedDistance();
        string walkingTime = pathfinding.GetEstimatedWalkingTime();

        // Display distance
        if (distanceText != null)
        {
            distanceText.text = $"Distance: {formattedDistance}";
        }

        // Display walking time
        if (walkingTimeText != null)
        {
            walkingTimeText.text = $"Walking Time: ~{walkingTime}";
        }

        // Display path info
        if (pathInfoText != null)
        {
            var fromNode = pathfinding.GetStartNode();
            var toNode = pathfinding.GetEndNode();

            string pathInfo = $"<b>From:</b> {fromNode.name}\n";
            pathInfo += $"<b>To:</b> {toNode.name}\n\n";
            pathInfo += $"<b>Route ({path.Count} stops):</b>\n";

            for (int i = 0; i < path.Count; i++)
            {
                var node = path[i].node;
                string prefix = i == 0 ? "🔵 " : i == path.Count - 1 ? "🔴 " : "⚪ ";
                
                pathInfo += $"{prefix}{node.name}";
                
                if (i < path.Count - 1 && path[i].distanceToNext > 0)
                {
                    pathInfo += $" → {path[i].distanceToNext:F0}m";
                }
                
                pathInfo += "\n";
            }

            pathInfoText.text = pathInfo;
        }

        DebugLog($"✅ Path displayed: {formattedDistance}, {walkingTime}");
    }

    private void ShowError(string message)
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        if (distanceText != null)
        {
            distanceText.text = "❌ Error";
        }

        if (walkingTimeText != null)
        {
            walkingTimeText.text = "";
        }

        if (pathInfoText != null)
        {
            pathInfoText.text = message;
        }

        Debug.LogWarning($"⚠️ {message}");
    }

    public void HideResults()
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }
    }

    public void ClearCurrentPath()
    {
        selectedToNodeId = null;
        if (pathfinding != null)
        {
            pathfinding.ClearCurrentPath();
        }
        HideResults();
        DebugLog("🧹 Cleared current path");
    }

    #endregion

    #region Public Methods for GPS/QR Integration (Future)

    /// <summary>
    /// Set FROM location from GPS coordinates
    /// </summary>
    public void SetFromLocationByGPS(float latitude, float longitude)
    {
        // TODO: Find nearest node to GPS coordinates
        DebugLog($"📍 GPS location received: ({latitude}, {longitude})");
        
        // For now, just log - implement nearest node search later
        Debug.LogWarning("⚠️ GPS location setting not yet implemented");
    }

    /// <summary>
    /// Set FROM location from QR code scan
    /// </summary>
    public void SetFromLocationByQR(string nodeId)
    {
        DebugLog($"📱 QR code scanned: {nodeId}");

        if (allNodes.ContainsKey(nodeId))
        {
            selectedFromNodeId = nodeId;
            staticFromNodeId = nodeId;
            useStaticTesting = true;

            DebugLog($"✅ FROM location set to: {allNodes[nodeId].name}");
        }
        else
        {
            Debug.LogError($"❌ Invalid node ID from QR: {nodeId}");
        }
    }

    /// <summary>
    /// Enable manual FROM selection (disable static/GPS/QR)
    /// </summary>
    public void EnableManualFromSelection()
    {
        useStaticTesting = false;
        DebugLog("✅ Manual FROM selection enabled");
    }

    #endregion

    #region Utility Methods

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PathfindingController] {message}");
        }
    }

    #endregion

    #region Debug Testing

    void Update()
    {
        // Debug controls (uncomment for testing)
        // if (Application.isEditor && Input.GetKeyDown(KeyCode.T))
        // {
        //     // Test pathfinding with first two infrastructure nodes
        //     var infraNodes = allNodes.Values.Where(n => n.type == "infrastructure").ToList();
        //     if (infraNodes.Count >= 2)
        //     {
        //         Debug.Log($"🧪 Testing path: {infraNodes[0].name} → {infraNodes[1].name}");
        //         StartCoroutine(FindAndDisplayPath(infraNodes[0].node_id, infraNodes[1].node_id));
        //     }
        // }
        //
        // if (Application.isEditor && Input.GetKeyDown(KeyCode.R))
        // {
        //     Debug.Log($"=== PATHFINDING STATUS ===");
        //     Debug.Log($"All nodes: {allNodes.Count}");
        //     Debug.Log($"Infrastructure mapping: {infraIdToNodeId.Count}");
        //     Debug.Log($"Static FROM: {staticFromNodeId}");
        //     Debug.Log($"Selected TO: {selectedToNodeId}");
        // }
    }

    #endregion
}