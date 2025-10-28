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
    public InfrastructurePopulator infrastructurePopulator;
    public GPSManager gpsManager;

    [Header("UI Elements")]
    public TMP_Dropdown toDropdown;
    public Button findPathButton;

    [Header("Static Testing - Navigation Scenarios")]
    public bool enableStaticTesting = false;
    public int testScenarioIndex = 0; // 0-3 for different scenarios

    [Header("Custom Test Node IDs (Override Scenario)")]
    public bool useCustomNodeIds = false;
    [Tooltip("Leave empty to use scenario defaults")]
    public string customFromNodeId = "";
    [Tooltip("Leave empty to use scenario defaults")]
    public string customToNodeId = "";

    [System.Serializable]
    public class TestScenario
    {
        public string scenarioName;
        public string fromNodeId;
        public string toNodeId;
        public string description;
        [TextArea(2, 4)]
        public string expectedBehavior;
    }

    [ContextMenu("Run Test Scenario")]
    public void RunTestFromInspector()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("⚠️ Must be in Play Mode to run tests");
            return;
        }

        RunStaticTest();
    }

    [ContextMenu("Print All Scenarios")]
    public void PrintAllScenarios()
    {
        Debug.Log("=== AVAILABLE TEST SCENARIOS ===");
        for (int i = 0; i < testScenarios.Count; i++)
        {
            var scenario = testScenarios[i];
            Debug.Log($"[{i}] {scenario.scenarioName}\n" +
                      $"    FROM: {scenario.fromNodeId} → TO: {scenario.toNodeId}\n" +
                      $"    {scenario.description}\n" +
                      $"    Expected: {scenario.expectedBehavior}\n");
        }
    }

    [ContextMenu("Validate Test Node IDs")]
    public void ValidateTestNodeIds()
    {
        if (!nodesLoaded)
        {
            Debug.LogWarning("⚠️ Nodes not loaded yet. Wait for map initialization.");
            return;
        }

        Debug.Log("=== VALIDATING TEST NODE IDs ===");

        foreach (var scenario in testScenarios)
        {
            bool fromExists = allNodes.ContainsKey(scenario.fromNodeId);
            bool toExists = allNodes.ContainsKey(scenario.toNodeId);

            string status = (fromExists && toExists) ? "✅ VALID" : "❌ INVALID";

            Debug.Log($"{status} {scenario.scenarioName}");
            if (!fromExists) Debug.LogError($"  ❌ FROM node not found: {scenario.fromNodeId}");
            if (!toExists) Debug.LogError($"  ❌ TO node not found: {scenario.toNodeId}");
        }

        // Validate custom IDs if enabled
        if (useCustomNodeIds)
        {
            bool customFromExists = allNodes.ContainsKey(customFromNodeId);
            bool customToExists = allNodes.ContainsKey(customToNodeId);

            Debug.Log("\n=== CUSTOM NODE IDs ===");
            Debug.Log($"FROM: {customFromNodeId} - {(customFromExists ? "✅ EXISTS" : "❌ NOT FOUND")}");
            Debug.Log($"TO: {customToNodeId} - {(customToExists ? "✅ EXISTS" : "❌ NOT FOUND")}");
        }
    }

    public void RunStaticTest()
    {
        if (!enableStaticTesting)
        {
            Debug.LogWarning("⚠️ Static testing is disabled. Enable 'enableStaticTesting' in Inspector.");
            return;
        }

        if (testScenarios.Count == 0)
        {
            Debug.LogError("❌ No test scenarios available");
            return;
        }

        if (testScenarioIndex < 0 || testScenarioIndex >= testScenarios.Count)
        {
            Debug.LogError($"❌ Invalid scenario index: {testScenarioIndex} (Valid range: 0-{testScenarios.Count - 1})");
            return;
        }

        string fromNodeId;
        string toNodeId;

        // Use custom node IDs if enabled, otherwise use scenario defaults
        if (useCustomNodeIds && !string.IsNullOrEmpty(customFromNodeId) && !string.IsNullOrEmpty(customToNodeId))
        {
            fromNodeId = customFromNodeId;
            toNodeId = customToNodeId;

            Debug.Log("=== 🧪 RUNNING CUSTOM TEST ===");
            Debug.Log($"FROM: {fromNodeId}");
            Debug.Log($"TO: {toNodeId}");
        }
        else
        {
            TestScenario scenario = testScenarios[testScenarioIndex];
            fromNodeId = scenario.fromNodeId;
            toNodeId = scenario.toNodeId;

            Debug.Log($"=== 🧪 RUNNING TEST SCENARIO [{testScenarioIndex}] ===");
            Debug.Log($"Scenario: {scenario.scenarioName}");
            Debug.Log($"FROM: {fromNodeId} → TO: {toNodeId}");
            Debug.Log($"Description: {scenario.description}");
            Debug.Log($"Expected: {scenario.expectedBehavior}");
        }

        // Validate nodes exist
        if (!allNodes.ContainsKey(fromNodeId))
        {
            Debug.LogError($"❌ FROM node not found: {fromNodeId}");
            return;
        }

        if (!allNodes.ContainsKey(toNodeId))
        {
            Debug.LogError($"❌ TO node not found: {toNodeId}");
            return;
        }

        // Get node details
        Node fromNode = allNodes[fromNodeId];
        Node toNode = allNodes[toNodeId];

        Debug.Log($"FROM Node: {fromNode.name} (Type: {fromNode.type})");
        Debug.Log($"TO Node: {toNode.name} (Type: {toNode.type})");

        // Check if indoor nodes
        bool fromIsIndoor = IsIndoorNode(fromNodeId);
        bool toIsIndoor = IsIndoorNode(toNodeId);

        Debug.Log($"FROM is Indoor: {fromIsIndoor}");
        Debug.Log($"TO is Indoor: {toIsIndoor}");

        if (fromIsIndoor)
        {
            Debug.Log($"FROM Building: {fromNode.related_infra_id} (Room: {fromNode.related_room_id})");
        }

        if (toIsIndoor)
        {
            Debug.Log($"TO Building: {toNode.related_infra_id} (Room: {toNode.related_room_id})");
        }

        // Override selected node IDs
        selectedFromNodeId = fromNodeId;
        selectedToNodeId = toNodeId;

        Debug.Log("🚀 Starting pathfinding...\n");

        // Trigger pathfinding
        StartCoroutine(FindAndDisplayPaths(fromNodeId, toNodeId));
    }

    // Helper: Get node info for debugging
    public string GetNodeDebugInfo(string nodeId)
    {
        if (!allNodes.ContainsKey(nodeId))
        {
            return $"❌ Node not found: {nodeId}";
        }

        Node node = allNodes[nodeId];
        string info = $"Node: {node.name} ({nodeId})\n";
        info += $"Type: {node.type}\n";

        if (node.type == "infrastructure")
        {
            info += $"GPS: ({node.latitude}, {node.longitude})\n";
            info += $"Infra ID: {node.related_infra_id}";
        }
        else if (node.type == "indoorinfra")
        {
            info += $"Building: {node.related_infra_id}\n";
            info += $"Room: {node.related_room_id}\n";
            if (node.indoor != null)
            {
                info += $"Indoor Pos: ({node.indoor.x}m, {node.indoor.y}m)\n";
                info += $"Floor: {node.indoor.floor}";
            }
        }

        return info;
    }

    [ContextMenu("Debug Selected Nodes")]
    public void DebugSelectedNodes()
    {
        if (useCustomNodeIds)
        {
            Debug.Log("=== CUSTOM FROM NODE ===");
            Debug.Log(GetNodeDebugInfo(customFromNodeId));
            Debug.Log("\n=== CUSTOM TO NODE ===");
            Debug.Log(GetNodeDebugInfo(customToNodeId));
        }
        else if (testScenarioIndex >= 0 && testScenarioIndex < testScenarios.Count)
        {
            var scenario = testScenarios[testScenarioIndex];
            Debug.Log($"=== SCENARIO {testScenarioIndex}: {scenario.scenarioName} ===");
            Debug.Log("\n=== FROM NODE ===");
            Debug.Log(GetNodeDebugInfo(scenario.fromNodeId));
            Debug.Log("\n=== TO NODE ===");
            Debug.Log(GetNodeDebugInfo(scenario.toNodeId));
        }
    }

    [Header("Location Lock Display")]
    public GameObject locationLockDisplay;
    public TextMeshProUGUI locationLockText;
    public Button locationLockButton;

    [Header("Confirmation Panel")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmFromText;
    public TextMeshProUGUI confirmToText;
    public TextMeshProUGUI confirmErrorText;
    public Button confirmButton;
    public Button cancelButton;

    [Header("Location Conflict Panel")]
    public GameObject locationConflictPanel;
    public TextMeshProUGUI conflictMessageText;
    public Button conflictConfirmButton;
    public Button conflictCancelButton;

    [Header("Result Display")]
    public GameObject resultPanel;
    public GameObject destinationPanel;
    public TextMeshProUGUI fromText;
    public TextMeshProUGUI toText;

    [Header("Route List")]
    public Transform routeListContainer;
    public GameObject routeItemPrefab;
    public ScrollRect routeScrollView;
    public Button confirmRouteButton;

    [Header("Indoor Data")]
    private Dictionary<string, IndoorInfrastructure> indoorInfrastructures = new Dictionary<string, IndoorInfrastructure>();
    private Dictionary<string, Node> indoorNodes = new Dictionary<string, Node>();

    [Header("Static Test Settings")]
    public bool useStaticTesting = false;
    public string staticFromNodeId = "ND-025";
    public string staticToNodeId = "ND-017";

    [Header("GPS Settings")]
    public bool useGPSForFromLocation = true;
    public float nearestNodeSearchRadius = 500f;
    public bool autoUpdateGPSLocation = true;
    public float gpsUpdateInterval = 5f;
    public float qrConflictThresholdMeters = 100f;

    [Header("Settings")]
    public bool enableDebugLogs = true;

    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>();
    private Dictionary<string, string> infraIdToNodeId = new Dictionary<string, string>();
    private string selectedFromNodeId;
    private string selectedToNodeId;
    private Node currentNearestNode;
    private Node qrScannedNode;
    private bool isQRLocationLocked = false;
    private bool hasShownConflictPanel = false;
    private float lastGPSUpdateTime;
    private bool nodesLoaded = false;

    private string currentMapId;
    private List<string> currentCampusIds;

    private List<RouteData> currentRoutes = new List<RouteData>();
    private List<RouteItem> routeItemInstances = new List<RouteItem>();
    private int selectedRouteIndex = -1;



    void Start()
    {
        if (findPathButton != null)
        {
            findPathButton.onClick.AddListener(OnFindPathClicked);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
        }

        if (toDropdown != null)
        {
            toDropdown.onValueChanged.AddListener(OnToDropdownChanged);
        }
        if (locationLockButton != null)
        {
            locationLockButton.onClick.AddListener(UnlockFromQR);
        }

        if (confirmRouteButton != null)
        {
            confirmRouteButton.onClick.AddListener(OnConfirmRouteClicked);
            confirmRouteButton.gameObject.SetActive(false);
        }

        if (conflictConfirmButton != null)
        {
            conflictConfirmButton.onClick.AddListener(OnLocationConflictConfirm);
        }

        if (conflictCancelButton != null)
        {
            conflictCancelButton.onClick.AddListener(OnLocationConflictCancel);
        }

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        if (destinationPanel != null)
        {
            destinationPanel.SetActive(false);
        }

        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }

        if (locationConflictPanel != null)
        {
            locationConflictPanel.SetActive(false);
        }

        if (MapManager.Instance != null)
        {
            MapManager.Instance.OnMapChanged += OnMapChanged;
        }

        if (gpsManager == null)
        {
            gpsManager = GPSManager.Instance;
        }
        if (enableStaticTesting)
        {
            // You can manually call RunStaticTest() from inspector or create a button
            Debug.Log($"[PathfindingController] 🧪 Static Testing ENABLED - {testScenarios.Count} scenarios loaded");
            Debug.Log($"[PathfindingController] Current Scenario [{testScenarioIndex}]: {testScenarios[testScenarioIndex].scenarioName}");
        }
    }

    void OnDestroy()
    {
        if (findPathButton != null)
            findPathButton.onClick.RemoveListener(OnFindPathClicked);
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelClicked);
        if (toDropdown != null)
            toDropdown.onValueChanged.RemoveListener(OnToDropdownChanged);
        if (confirmRouteButton != null)
            confirmRouteButton.onClick.RemoveListener(OnConfirmRouteClicked);
        if (conflictConfirmButton != null)
            conflictConfirmButton.onClick.RemoveListener(OnLocationConflictConfirm);
        if (conflictCancelButton != null)
            conflictCancelButton.onClick.RemoveListener(OnLocationConflictCancel);
        if (MapManager.Instance != null)
            MapManager.Instance.OnMapChanged -= OnMapChanged;

        ClearQRData();
    }

    void Update()
    {
        if (useGPSForFromLocation && autoUpdateGPSLocation && !useStaticTesting && nodesLoaded)
        {
            if (Time.time - lastGPSUpdateTime >= gpsUpdateInterval)
            {
                UpdateFromLocationByGPS();
                lastGPSUpdateTime = Time.time;
            }
        }
    }

    #region QR Data Handling

    private void CheckForScannedQRData()
    {
        string scannedNodeId = PlayerPrefs.GetString("ScannedNodeID", "");

        if (!string.IsNullOrEmpty(scannedNodeId) && nodesLoaded)
        {
            LoadQRScannedNode(scannedNodeId);
        }
    }
    public List<TestScenario> testScenarios = new List<TestScenario>
{
    // Scenario 0: Outdoor to Outdoor
    new TestScenario
    {
        scenarioName = "Outdoor → Outdoor",
        fromNodeId = "ND-025", // Medicine Building
        toNodeId = "ND-017",   // Engineering Building
        description = "Normal A* pathfinding between two infrastructure buildings",
        expectedBehavior = "Should generate routes using GPS coordinates. Directions show outdoor navigation only."
    },
    
    // Scenario 1: Outdoor to Indoor (Same Building)
    new TestScenario
    {
        scenarioName = "Outdoor → Indoor (Same Building)",
        fromNodeId = "ND-017", // Engineering Building (outdoor)
        toNodeId = "ND-119",   // Room inside Engineering (indoorinfra)
        description = "Routes to building entrance, then indoor navigation",
        expectedBehavior = "Path ends at building entrance. Indoor directions added: 'Navigate to Floor 1 Stairs. Room is on floor 1.'"
    },
    
    // Scenario 2: Outdoor to Indoor (Different Building)
    new TestScenario
    {
        scenarioName = "Outdoor → Indoor (Different Building)",
        fromNodeId = "ND-025", // Medicine Building
        toNodeId = "ND-119",   // Room inside Engineering
        description = "Routes Medicine → Engineering entrance → Indoor navigation",
        expectedBehavior = "GPS route to entrance, then indoor directions. System switches to X,Y mode at destination building."
    },
    
    // Scenario 3: Indoor to Indoor (Different Building)
    new TestScenario
    {
        scenarioName = "Indoor → Indoor (Different Building)",
        fromNodeId = "ND-120", // Room in Medicine (indoorinfra)
        toNodeId = "ND-119",   // Room in Engineering (indoorinfra)
        description = "Exit Medicine → Route to Engineering → Enter Engineering",
        expectedBehavior = "Indoor exit instructions → GPS outdoor route → Indoor entry instructions. All indoor directions grouped."
    }
};

    private IEnumerator LoadIndoorData()
    {
        // Load indoor.json
        bool loadComplete = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "indoor.json",
            (jsonContent) =>
            {
                try
                {
                    IndoorInfrastructure[] indoorArray = JsonHelper.FromJson<IndoorInfrastructure>(jsonContent);

                    indoorInfrastructures.Clear();
                    foreach (var indoor in indoorArray)
                    {
                        if (!indoor.is_deleted)
                        {
                            indoorInfrastructures[indoor.room_id] = indoor;
                        }
                    }

                    loadComplete = true;
                    Debug.Log($"[PathfindingController] Loaded {indoorInfrastructures.Count} indoor infrastructures");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[PathfindingController] Error loading indoor data: {e.Message}");
                    loadComplete = true;
                }
            },
            (error) =>
            {
                Debug.LogError($"[PathfindingController] Failed to load indoor.json: {error}");
                loadComplete = true;
            }
        ));

        yield return new WaitUntil(() => loadComplete);

        // Load indoor nodes from nodes JSON
        foreach (var node in allNodes.Values)
        {
            if (node.type == "indoorinfra")
            {
                indoorNodes[node.node_id] = node;
            }
        }

        Debug.Log($"[PathfindingController] Loaded {indoorNodes.Count} indoor nodes");
    }
    private bool IsIndoorNode(string nodeId)
    {
        if (allNodes.TryGetValue(nodeId, out Node node))
        {
            return node.type == "indoorinfra";
        }
        return indoorNodes.ContainsKey(nodeId);
    }

    // Helper: Get building entrance node from indoor node
    private Node GetBuildingEntranceNode(Node indoorNode)
    {
        if (indoorNode.type != "indoorinfra" || string.IsNullOrEmpty(indoorNode.related_infra_id))
        {
            return null;
        }

        // Find the infrastructure node with matching infra_id
        foreach (var node in allNodes.Values)
        {
            if (node.type == "infrastructure" && node.related_infra_id == indoorNode.related_infra_id)
            {
                return node;
            }
        }

        return null;
    }

    private void LoadQRScannedNode(string nodeId)
    {
        if (allNodes.TryGetValue(nodeId, out Node node))
        {
            qrScannedNode = node;
            selectedFromNodeId = nodeId;
            isQRLocationLocked = true;
            hasShownConflictPanel = false;

            if (locationLockDisplay != null)
            {
                locationLockDisplay.SetActive(true);

                if (locationLockText != null)
                {
                    locationLockText.text = $"{node.name} 🔒";
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log($"QR scanned node loaded: {node.name} ({nodeId}) - Location LOCKED 🔒");
            }
        }
        else
        {
            Debug.LogWarning($"Scanned node ID {nodeId} not found in loaded nodes");
        }
    }

    public void ClearQRData()
    {
        PlayerPrefs.DeleteKey("ScannedNodeID");
        PlayerPrefs.DeleteKey("ScannedLocationName");
        PlayerPrefs.DeleteKey("ScannedLat");
        PlayerPrefs.DeleteKey("ScannedLng");
        PlayerPrefs.DeleteKey("ScannedCampusID");
        PlayerPrefs.DeleteKey("ScannedX");
        PlayerPrefs.DeleteKey("ScannedY");
        PlayerPrefs.Save();

        qrScannedNode = null;
        isQRLocationLocked = false;
    }

    public void UnlockFromQR()
    {
        isQRLocationLocked = false;
        qrScannedNode = null;

        if (locationLockDisplay != null)
        {
            locationLockDisplay.SetActive(false);
        }

        UpdateFromLocationByGPS();

        ClearQRData();

        Debug.Log("YAWA KA BAI");
    }

    #endregion

    #region MapManager Integration

    private void OnMapChanged(MapInfo mapInfo)
    {
        ClearCurrentPath();
    }

    public IEnumerator InitializeForMap(string mapId, List<string> campusIds)
    {
        currentMapId = mapId;
        currentCampusIds = campusIds;

        yield return StartCoroutine(LoadNodesFromJSON(mapId));

        yield return StartCoroutine(BuildInfrastructureNodeMapping());

        if (nodesLoaded)
        {
            CheckForScannedQRData();
        }

        if (useGPSForFromLocation && !useStaticTesting)
        {
            UpdateFromLocationByGPS();
        }

        if (pathfinding != null)
        {
            yield return StartCoroutine(pathfinding.LoadGraphDataForMap(mapId, campusIds));
        }
    }

    #endregion

    #region Node Loading from JSON

    private IEnumerator LoadNodesFromJSON(string mapId)
    {
        string fileName = $"nodes_{mapId}.json";
        bool loadSuccess = false;
        string errorMsg = "";

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
                                         fileName,
        (jsonContent) =>
        {
            try
            {
                Node[] nodesArray = JsonHelper.FromJson<Node>(jsonContent);

                allNodes.Clear();
                foreach (Node node in nodesArray)
                {
                    allNodes[node.node_id] = node;
                }

                nodesLoaded = true;
                loadSuccess = true;
            }
            catch (System.Exception e)
            {
                errorMsg = $"Error parsing nodes JSON: {e.Message}";
            }
        },
        (error) =>
        {
            errorMsg = $"Failed to load {fileName}: {error}";
        }
                                     ));

        yield return null;
    }

    #endregion

    #region Infrastructure to Node Mapping

    private IEnumerator BuildInfrastructureNodeMapping()
    {
        if (allNodes == null || allNodes.Count == 0)
        {
            yield break;
        }

        infraIdToNodeId.Clear();

        foreach (var kvp in allNodes)
        {
            Node node = kvp.Value;

            if (node.type == "infrastructure" && !string.IsNullOrEmpty(node.related_infra_id))
            {
                infraIdToNodeId[node.related_infra_id] = node.node_id;
            }
        }

        yield return null;
    }

    #endregion

    #region GPS Location Handling

    private void UpdateFromLocationByGPS()
    {
        if (gpsManager == null)
        {
            return;
        }

        if (!nodesLoaded || allNodes == null || allNodes.Count == 0)
        {
            return;
        }

        Vector2 coords = gpsManager.GetSmoothedCoordinates();

        Node nearestNode = FindNearestNode(coords.x, coords.y);

        if (nearestNode != null)
        {
            // If QR location is locked, check for conflict
            if (isQRLocationLocked && qrScannedNode != null)
            {
                float distanceFromQR = CalculateDistance(
                                           qrScannedNode.latitude, qrScannedNode.longitude,
                                           nearestNode.latitude, nearestNode.longitude
                                       );

                if (distanceFromQR > qrConflictThresholdMeters)
                {
                    // Only show panel once per QR lock
                    if (!hasShownConflictPanel)
                    {
                        hasShownConflictPanel = true;
                        ShowLocationConflictPanel(qrScannedNode, nearestNode, distanceFromQR);
                    }
                    return; // Don't update location, stay locked
                }
            }
            else
            {
                // No QR lock - update normally
                selectedFromNodeId = nearestNode.node_id;
                currentNearestNode = nearestNode;
            }
        }
    }

    private Node FindNearestNode(float latitude, float longitude)
    {
        if (allNodes == null || allNodes.Count == 0)
        {
            return null;
        }

        Node nearestNode = null;
        float nearestDistance = float.MaxValue;

        foreach (var kvp in allNodes)
        {
            Node node = kvp.Value;

            float distance = CalculateDistance(latitude, longitude, node.latitude, node.longitude);

            if (distance < nearestDistance && distance <= nearestNodeSearchRadius)
            {
                nearestDistance = distance;
                nearestNode = node;
            }
        }

        return nearestNode;
    }

    private float CalculateDistance(float lat1, float lon1, float lat2, float lon2)
    {
        const float R = 6371000f;

        float dLat = (lat2 - lat1) * Mathf.Deg2Rad;
        float dLon = (lon2 - lon1) * Mathf.Deg2Rad;

        float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                  Mathf.Cos(lat1 * Mathf.Deg2Rad) * Mathf.Cos(lat2 * Mathf.Deg2Rad) *
                  Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);

        float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));

        return R * c;
    }

    #endregion

    #region Location Conflict Panel

    private void ShowLocationConflictPanel(Node qrNode, Node gpsNode, float distanceMeters)
    {
        if (locationConflictPanel == null)
        {
            return;
        }

        if (conflictMessageText != null)
        {
            conflictMessageText.text =
                $"Your location has changed!\n\n" +
                $"<b>QR Location:</b> {qrNode.name}\n" +
                $"<b>Current GPS:</b> {gpsNode.name}\n" +
                $"<b>Distance:</b> {distanceMeters:F0}m apart\n\n" +
                $"Would you like to update to your current GPS location?";
        }

        locationConflictPanel.SetActive(true);
    }

    private void OnLocationConflictConfirm()
    {
        if (locationConflictPanel != null)
        {
            locationConflictPanel.SetActive(false);
        }
        UnlockFromQR();
    }

    private void OnLocationConflictCancel()
    {
        if (locationConflictPanel != null)
        {
            locationConflictPanel.SetActive(false);
        }
    }

    #endregion

    #region Dropdown Handlers

    private void OnToDropdownChanged(int index)
    {
        if (infrastructurePopulator == null || toDropdown == null)
        {
            return;
        }

        Infrastructure selectedInfra = infrastructurePopulator.GetSelectedInfrastructure(toDropdown);

        if (selectedInfra == null)
        {
            selectedToNodeId = null;
            return;
        }

        if (infraIdToNodeId.TryGetValue(selectedInfra.infra_id, out string nodeId))
        {
            selectedToNodeId = nodeId;
        }
        else
        {
            selectedToNodeId = null;
        }
    }

    #endregion

    #region Confirmation Panel

    private void OnFindPathClicked()
    {
        string fromNodeId;
        string toNodeId;

        if (useStaticTesting)
        {
            fromNodeId = staticFromNodeId;
            toNodeId = staticToNodeId;
        }
        else
        {
            UpdateFromLocationByGPS();
            fromNodeId = selectedFromNodeId;
            toNodeId = selectedToNodeId;
        }

        if (string.IsNullOrEmpty(toNodeId))
        {
            ShowConfirmationError("Please select a destination");
            return;
        }

        if (string.IsNullOrEmpty(fromNodeId))
        {
            ShowConfirmationError("Cannot determine your location. Please check GPS.");
            return;
        }

        if (fromNodeId == toNodeId)
        {
            ShowConfirmationError("You are already at this location!");
            return;
        }

        ShowConfirmationPanel(fromNodeId, toNodeId);
    }

    private void ShowConfirmationPanel(string fromNodeId, string toNodeId)
    {
        if (confirmationPanel != null)
        {
            confirmationPanel.transform.Find("Title").GetComponent<TextMeshProUGUI>().text = "Confirm Location";
            confirmButton.gameObject.SetActive(true);
            cancelButton.GetComponentInChildren<TextMeshProUGUI>().text = "Cancel";
            confirmationPanel.SetActive(true);
            confirmErrorText.gameObject.SetActive(false);
        }

        if (allNodes.TryGetValue(fromNodeId, out Node fromNode))
        {
            if (confirmFromText != null)
            {
                confirmFromText.text = $"<b>From:</b> {fromNode.name}";
            }
        }

        if (allNodes.TryGetValue(toNodeId, out Node toNode))
        {
            if (confirmToText != null)
            {
                confirmToText.text = $"<b>To:</b> {toNode.name}";
            }
        }

        if (confirmErrorText != null)
        {
            confirmErrorText.text = "";
        }
    }

    private void ShowConfirmationError(string message)
    {
        if (confirmationPanel != null)
        {
            confirmationPanel.transform.Find("Title").GetComponent<TextMeshProUGUI>().text = "Error";
            cancelButton.GetComponentInChildren<TextMeshProUGUI>().text = "Close";
            confirmationPanel.SetActive(true);
            confirmButton.gameObject.SetActive(false);
            confirmErrorText.gameObject.SetActive(true);
        }

        if (confirmFromText != null)
        {
            confirmFromText.text = "";
        }

        if (confirmToText != null)
        {
            confirmToText.text = "";
        }

        if (confirmErrorText != null)
        {
            confirmErrorText.text = message;
        }
    }

    private void OnConfirmClicked()
    {
        string fromNodeId;
        string toNodeId;

        if (useStaticTesting)
        {
            fromNodeId = staticFromNodeId;
            toNodeId = staticToNodeId;
        }
        else
        {
            fromNodeId = selectedFromNodeId;
            toNodeId = selectedToNodeId;
        }

        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }

        StartCoroutine(FindAndDisplayPaths(fromNodeId, toNodeId));
    }

    private void OnCancelClicked()
    {
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }
    }

    #endregion

    #region Pathfinding Trigger

    private IEnumerator FindAndDisplayPaths(string fromNodeId, string toNodeId)
    {
        if (pathfinding == null)
        {
            yield break;
        }

        if (findPathButton != null)
        {
            findPathButton.interactable = false;
        }

        // Check if FROM or TO are indoor nodes
        bool fromIsIndoor = IsIndoorNode(fromNodeId);
        bool toIsIndoor = IsIndoorNode(toNodeId);

        string pathStartNodeId = fromNodeId;
        string pathEndNodeId = toNodeId;

        // If FROM is indoor, use building entrance as start
        if (fromIsIndoor && allNodes.TryGetValue(fromNodeId, out Node fromIndoorNode))
        {
            Node entranceNode = GetBuildingEntranceNode(fromIndoorNode);
            if (entranceNode != null)
            {
                pathStartNodeId = entranceNode.node_id;
                Debug.Log($"[PathfindingController] FROM is indoor, using entrance: {entranceNode.name}");
            }
        }

        // If TO is indoor, use building entrance as end
        if (toIsIndoor && allNodes.TryGetValue(toNodeId, out Node toIndoorNode))
        {
            Node entranceNode = GetBuildingEntranceNode(toIndoorNode);
            if (entranceNode != null)
            {
                pathEndNodeId = entranceNode.node_id;
                Debug.Log($"[PathfindingController] TO is indoor, using entrance: {entranceNode.name}");
            }
        }

        // Run A* pathfinding (only outdoor nodes)
        yield return StartCoroutine(pathfinding.FindMultiplePaths(pathStartNodeId, pathEndNodeId, 3));

        if (findPathButton != null)
        {
            findPathButton.interactable = true;
        }

        var routes = pathfinding.GetAllRoutes();

        if (routes == null || routes.Count == 0)
        {
            ShowConfirmationError("No path found between these locations");
            yield break;
        }

        // Store original FROM/TO for indoor direction generation
        PlayerPrefs.SetString("ARNavigation_OriginalFromNodeId", fromNodeId);
        PlayerPrefs.SetString("ARNavigation_OriginalToNodeId", toNodeId);
        PlayerPrefs.SetInt("ARNavigation_FromIsIndoor", fromIsIndoor ? 1 : 0);
        PlayerPrefs.SetInt("ARNavigation_ToIsIndoor", toIsIndoor ? 1 : 0);
        PlayerPrefs.Save();

        currentRoutes = routes;
        DisplayAllRoutes();
    }
    #endregion

    #region Result Display

    private void DisplayAllRoutes()
    {
        if (resultPanel != null && destinationPanel != null)
        {
            resultPanel.SetActive(true);
            destinationPanel.SetActive(true);
        }

        ClearRouteItems();

        if (currentRoutes.Count == 0)
        {
            return;
        }

        var firstRoute = currentRoutes[0];

        if (fromText != null)
        {
            string lockIndicator = isQRLocationLocked ? " 🔒" : "";
            fromText.text = $"<b>From:</b> {firstRoute.startNode.name}{lockIndicator}";
        }

        if (toText != null)
        {
            toText.text = $"<b>To:</b> {firstRoute.endNode.name}";
        }

        for (int i = 0; i < currentRoutes.Count; i++)
        {
            CreateRouteItem(i, currentRoutes[i]);
        }

        if (currentRoutes.Count > 0)
        {
            OnRouteSelected(0);
        }
        else
        {
            if (confirmRouteButton != null)
            {
                confirmRouteButton.gameObject.SetActive(false);
            }
        }

        if (routeScrollView != null)
        {
            Canvas.ForceUpdateCanvases();
            routeScrollView.verticalNormalizedPosition = 1f;
        }
    }

    private void CreateRouteItem(int index, RouteData routeData)
    {
        if (routeItemPrefab == null || routeListContainer == null)
        {
            return;
        }

        GameObject itemObj = Instantiate(routeItemPrefab, routeListContainer);
        RouteItem routeItem = itemObj.GetComponent<RouteItem>();

        if (routeItem != null)
        {
            routeItem.Initialize(index, routeData, OnRouteSelected);
            routeItemInstances.Add(routeItem);
        }
    }

    private void ClearRouteItems()
    {
        if (routeListContainer == null)
        {
            return;
        }

        routeItemInstances.Clear();

        foreach (Transform child in routeListContainer)
        {
            Destroy(child.gameObject);
        }
    }

    private void OnRouteSelected(int routeIndex)
    {
        if (routeIndex < 0 || routeIndex >= currentRoutes.Count)
        {
            return;
        }

        selectedRouteIndex = routeIndex;

        for (int i = 0; i < routeItemInstances.Count; i++)
        {
            routeItemInstances[i].SetSelected(i == routeIndex);
        }

        if (confirmRouteButton != null)
        {
            confirmRouteButton.gameObject.SetActive(true);
        }

        if (pathfinding != null)
        {
            pathfinding.SetActiveRoute(routeIndex);
        }
    }

    private void OnConfirmRouteClicked()
    {
        if (selectedRouteIndex < 0 || selectedRouteIndex >= currentRoutes.Count)
        {
            Debug.Log("YAWA 0");
            return;
        }

        RouteData selectedRoute = currentRoutes[selectedRouteIndex];

        // Generate directions from the route
        DirectionGenerator directionGen = GetComponent<DirectionGenerator>();
        if (directionGen == null)
        {
            directionGen = gameObject.AddComponent<DirectionGenerator>();
        }

        List<NavigationDirection> directions = directionGen.GenerateDirections(selectedRoute);

        // Save route data and directions to PlayerPrefs for AR scene
        SaveRouteDataForAR(selectedRoute, directions);
    }

    private void SaveRouteDataForAR(RouteData route, List<NavigationDirection> directions)
    {
        // ✅ CLEAR OLD DIRECTION DATA FIRST
        int oldDirectionCount = PlayerPrefs.GetInt("ARNavigation_DirectionCount", 0);
        for (int i = 0; i < oldDirectionCount; i++)
        {
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_Instruction");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_Turn");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_Distance");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_DestNodeId");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_DestNode");
        }

        // Save map/campus data so AR scene can access it
        if (MapManager.Instance != null && MapManager.Instance.GetCurrentMap() != null)
        {
            MapInfo currentMap = MapManager.Instance.GetCurrentMap();
            PlayerPrefs.SetString("ARScene_MapId", currentMap.map_id);
            PlayerPrefs.SetString("ARScene_MapName", currentMap.map_name);

            string campusIdsJson = string.Join(",", MapManager.Instance.GetCurrentCampusIds());
            PlayerPrefs.SetString("ARScene_CampusIds", campusIdsJson);
        }

        // Save route info
        PlayerPrefs.SetString("ARNavigation_StartNodeId", route.startNode.node_id);
        PlayerPrefs.SetString("ARNavigation_EndNodeId", route.endNode.node_id);
        PlayerPrefs.SetString("ARNavigation_StartNodeName", route.startNode.name);
        PlayerPrefs.SetString("ARNavigation_EndNodeName", route.endNode.name);
        PlayerPrefs.SetFloat("ARNavigation_TotalDistance", route.totalDistance);
        PlayerPrefs.SetString("ARNavigation_FormattedDistance", route.formattedDistance);
        PlayerPrefs.SetString("ARNavigation_WalkingTime", route.walkingTime);
        PlayerPrefs.SetString("ARNavigation_ViaMode", route.viaMode);

        PlayerPrefs.SetInt("ARNavigation_PathNodeCount", route.path.Count);

        for (int i = 0; i < route.path.Count; i++)
        {
            PlayerPrefs.SetString($"ARNavigation_PathNode_{i}", route.path[i].node.node_id);
        }

        int edgeCount = route.path.Count - 1;
        PlayerPrefs.SetInt("ARNavigation_EdgeCount", edgeCount);

        for (int i = 0; i < edgeCount; i++)
        {
            string fromNode = route.path[i].node.node_id;
            string toNode = route.path[i + 1].node.node_id;

            PlayerPrefs.SetString($"ARNavigation_Edge_{i}_From", fromNode);
            PlayerPrefs.SetString($"ARNavigation_Edge_{i}_To", toNode);
        }

        // ✅ Save directions count FIRST (so AR scene knows how many to expect)
        PlayerPrefs.SetInt("ARNavigation_DirectionCount", directions.Count);

        // Save each direction
        for (int i = 0; i < directions.Count; i++)
        {
            var dir = directions[i];
            PlayerPrefs.SetString($"ARNavigation_Direction_{i}_Instruction", dir.instruction);
            PlayerPrefs.SetString($"ARNavigation_Direction_{i}_Turn", dir.turn.ToString());
            PlayerPrefs.SetFloat($"ARNavigation_Direction_{i}_Distance", dir.distanceInMeters);
            PlayerPrefs.SetString($"ARNavigation_Direction_{i}_DestNodeId", dir.destinationNode.node_id);
            PlayerPrefs.SetString($"ARNavigation_Direction_{i}_DestNode", dir.destinationNode.name);
        }

        PlayerPrefs.SetString("ARMode", "Navigation");

        // ✅ FORCE SAVE - This is critical!
        PlayerPrefs.Save();

        if (enableDebugLogs)
        {
            Debug.Log($"[PathfindingController] ✅ Route data saved for AR: {route.startNode.name} → {route.endNode.name}");
            Debug.Log($"[PathfindingController] ✅ Saved {route.path.Count} path nodes");
            Debug.Log($"[PathfindingController] ✅ Saved {directions.Count} navigation directions");

            // ✅ Verify save immediately
            Debug.Log($"[PathfindingController] ✅ Verification: PlayerPrefs has {PlayerPrefs.GetInt("ARNavigation_DirectionCount", 0)} directions");
        }
    }
    public void HideResults()
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        if (destinationPanel != null)
        {
            destinationPanel.SetActive(false);
        }

        ClearRouteItems();
    }

    public void ClearCurrentPath()
    {
        selectedToNodeId = null;
        currentRoutes.Clear();
        routeItemInstances.Clear();
        selectedRouteIndex = -1;

        if (confirmRouteButton != null)
        {
            confirmRouteButton.gameObject.SetActive(false);
        }

        if (pathfinding != null)
        {
            pathfinding.ClearCurrentPath();
        }

        HideResults();
    }

    #endregion

    #region Public Methods for External Integration

    public void RefreshGPSLocation()
    {
        if (useGPSForFromLocation && !useStaticTesting && nodesLoaded)
        {
            UpdateFromLocationByGPS();
        }
    }

    public void SetFromLocationByQR(string nodeId)
    {
        if (allNodes.ContainsKey(nodeId))
        {
            selectedFromNodeId = nodeId;
            currentNearestNode = allNodes[nodeId];
        }
    }

    public void ToggleGPSMode(bool useGPS)
    {
        useGPSForFromLocation = useGPS;

        if (useGPS && nodesLoaded)
        {
            UpdateFromLocationByGPS();
        }
    }

    public RouteData GetSelectedRoute()
    {
        if (selectedRouteIndex >= 0 && selectedRouteIndex < currentRoutes.Count)
        {
            return currentRoutes[selectedRouteIndex];
        }
        return null;
    }

    public List<RouteData> GetAllRoutes()
    {
        return new List<RouteData>(currentRoutes);
    }

    public bool IsLocationLocked()
    {
        return isQRLocationLocked;
    }

    #endregion

    #region Utility Methods

    public string GetCurrentFromLocationName()
    {
        if (currentNearestNode != null)
        {
            return currentNearestNode.name;
        }
        return "Unknown";
    }

    public bool IsReadyForPathfinding()
    {
        return nodesLoaded &&
               !string.IsNullOrEmpty(selectedFromNodeId) &&
               !string.IsNullOrEmpty(selectedToNodeId);
    }

    #endregion
}