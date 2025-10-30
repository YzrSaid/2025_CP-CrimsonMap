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
    public string staticFromNodeId = "ND-015";
    public string staticToNodeId = "ND-033";

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

        if (useStaticTesting)
        {
            Debug.Log($"[PathfindingController] 🧪 Static Testing ENABLED");
            Debug.Log($"[PathfindingController] FROM: {staticFromNodeId} → TO: {staticToNodeId}");
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

    private IEnumerator LoadIndoorData()
    {
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

    private Node GetBuildingEntranceNode(Node indoorNode)
    {
        if (indoorNode.type != "indoorinfra" || string.IsNullOrEmpty(indoorNode.related_infra_id))
        {
            return null;
        }

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
    }

    #endregion

    #region Dropdown Handlers

    private void OnToDropdownChanged(int index)
    {
        if (infrastructurePopulator == null || toDropdown == null)
        {
            return;
        }

        // ✅ NEW: Get the selected destination (can be infrastructure or indoor room)
        var (destinationId, destinationType) = infrastructurePopulator.GetSelectedDestinationFromDropdown(toDropdown);

        if (string.IsNullOrEmpty(destinationId))
        {
            selectedToNodeId = null;
            Debug.LogWarning("[PathfindingController] No valid destination selected from dropdown");
            return;
        }

        // ✅ Use the SetDestination method you already have
        SetDestination(destinationId, destinationType);

        Debug.Log($"[PathfindingController] Dropdown changed to: {toDropdown.options[index].text}");
        Debug.Log($"  - Destination ID: {destinationId}");
        Debug.Log($"  - Type: {destinationType}");
        Debug.Log($"  - Selected Node ID: {selectedToNodeId}");
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

        // ✅ NEW: Load indoor data
        yield return StartCoroutine(LoadIndoorData());

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

    public void SetDestination(string destinationId, string destinationType)
    {
        Debug.Log($"[PathfindingController] SetDestination: {destinationId} (Type: {destinationType})");

        if (destinationType == "infrastructure")
        {
            // Find the node with this infra_id
            var infraNode = allNodes.Values.FirstOrDefault(n =>
                n.type == "infrastructure" && n.related_infra_id == destinationId);

            if (infraNode != null)
            {
                selectedToNodeId = infraNode.node_id;
                Debug.Log($"[PathfindingController] Selected infrastructure node: {infraNode.name}");
            }
            else
            {
                Debug.LogError($"[PathfindingController] Infrastructure node not found for: {destinationId}");
            }
        }
        else if (destinationType == "indoorinfra")
        {
            // Find the node with this room_id
            var indoorNode = allNodes.Values.FirstOrDefault(n =>
                n.type == "indoorinfra" && n.related_room_id == destinationId);

            if (indoorNode != null)
            {
                selectedToNodeId = indoorNode.node_id;
                Debug.Log($"[PathfindingController] Selected indoor node: {indoorNode.name}");
            }
            else
            {
                Debug.LogError($"[PathfindingController] Indoor node not found for: {destinationId}");
            }
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
            if (isQRLocationLocked && qrScannedNode != null)
            {
                float distanceFromQR = CalculateDistance(
                    qrScannedNode.latitude, qrScannedNode.longitude,
                    nearestNode.latitude, nearestNode.longitude
                );

                if (distanceFromQR > qrConflictThresholdMeters)
                {
                    if (!hasShownConflictPanel)
                    {
                        hasShownConflictPanel = true;
                        ShowLocationConflictPanel(qrScannedNode, nearestNode, distanceFromQR);
                    }
                    return;
                }
            }
            else
            {
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

    // ✅ THIS IS THE MAIN METHOD - APPLIES TO REAL USAGE AND TESTING
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

        // ✅ CRITICAL: Ensure nodes exist
        if (!allNodes.ContainsKey(fromNodeId))
        {
            Debug.LogError($"[PathfindingController] FROM node not found: {fromNodeId}");
            ShowConfirmationError($"FROM node not found: {fromNodeId}");
            if (findPathButton != null) findPathButton.interactable = true;
            yield break;
        }

        if (!allNodes.ContainsKey(toNodeId))
        {
            Debug.LogError($"[PathfindingController] TO node not found: {toNodeId}");
            ShowConfirmationError($"TO node not found: {toNodeId}");
            if (findPathButton != null) findPathButton.interactable = true;
            yield break;
        }

        // Check if FROM or TO are indoor nodes
        bool fromIsIndoor = IsIndoorNode(fromNodeId);
        bool toIsIndoor = IsIndoorNode(toNodeId);

        string pathStartNodeId = fromNodeId;
        string pathEndNodeId = toNodeId;

        Node fromNode = allNodes[fromNodeId];
        Node toNode = allNodes[toNodeId];

        Debug.Log($"[PathfindingController] FROM: {fromNode.name} (Indoor: {fromIsIndoor})");
        Debug.Log($"[PathfindingController] TO: {toNode.name} (Indoor: {toIsIndoor})");

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

        // ✅ SPECIAL CASE: Same building (outdoor to indoor, same building)
        bool isSameBuilding = pathStartNodeId == pathEndNodeId;

        if (isSameBuilding)
        {
            Debug.Log($"[PathfindingController] ✅ Same building navigation detected");

            // Create a single-node "route" for same building
            var singleNodeRoute = CreateSameBuildingRoute(pathStartNodeId, fromNode, toNode);

            // Store original FROM/TO for indoor direction generation
            PlayerPrefs.SetString("ARNavigation_OriginalFromNodeId", fromNodeId);
            PlayerPrefs.SetString("ARNavigation_OriginalToNodeId", toNodeId);
            PlayerPrefs.SetInt("ARNavigation_FromIsIndoor", fromIsIndoor ? 1 : 0);
            PlayerPrefs.SetInt("ARNavigation_ToIsIndoor", toIsIndoor ? 1 : 0);
            PlayerPrefs.SetString("ARNavigation_SameBuilding", "true");
            PlayerPrefs.Save();

            Debug.Log($"[PathfindingController] ✅ Saved PlayerPrefs for same building");
            Debug.Log($"  - OriginalFromNodeId: {fromNodeId}");
            Debug.Log($"  - OriginalToNodeId: {toNodeId}");
            Debug.Log($"  - FromIsIndoor: {fromIsIndoor}");
            Debug.Log($"  - ToIsIndoor: {toIsIndoor}");

            currentRoutes = new List<RouteData> { singleNodeRoute };

            if (findPathButton != null)
            {
                findPathButton.interactable = true;
            }

            DisplayAllRoutes();
            yield break;
        }

        // Normal pathfinding for different buildings
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
        PlayerPrefs.SetString("ARNavigation_SameBuilding", "false");
        PlayerPrefs.Save();

        Debug.Log("NAKASAVE NA");

        currentRoutes = routes;
        DisplayAllRoutes();
    }

    // ✅ NEW: Create a "fake" route for same building navigation
    private RouteData CreateSameBuildingRoute(string buildingNodeId, Node fromNode, Node toNode)
    {
        Node buildingNode = allNodes[buildingNodeId];

        var routeData = new RouteData
        {
            startNode = buildingNode,
            endNode = buildingNode,
            path = new List<PathNode>
            {
                new PathNode
                {
                    node = buildingNode,
                    worldPosition = Vector3.zero,
                    isStart = true,
                    isEnd = true,
                    distanceToNext = 0f
                }
            },
            totalDistance = 0f,
            formattedDistance = "Already at building",
            walkingTime = "< 1 minute",
            viaMode = "Indoor Navigation",
            isRecommended = true
        };

        return routeData;
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

        // ✅ Get original FROM and TO nodes for display
        string originalFromId = PlayerPrefs.GetString("ARNavigation_OriginalFromNodeId", "");
        string originalToId = PlayerPrefs.GetString("ARNavigation_OriginalToNodeId", "");

        Node displayFromNode = firstRoute.startNode;
        Node displayToNode = firstRoute.endNode;

        // If we have original IDs, use those for display
        if (!string.IsNullOrEmpty(originalFromId) && allNodes.ContainsKey(originalFromId))
        {
            displayFromNode = allNodes[originalFromId];
        }

        if (!string.IsNullOrEmpty(originalToId) && allNodes.ContainsKey(originalToId))
        {
            displayToNode = allNodes[originalToId];
        }

        if (fromText != null)
        {
            string lockIndicator = isQRLocationLocked ? " 🔒" : "";
            fromText.text = $"<b>From:</b> {displayFromNode.name}{lockIndicator}";
        }

        if (toText != null)
        {
            string toDisplay = displayToNode.name;

            // ✅ If destination is indoor, show building name + room name
            if (displayToNode.type == "indoorinfra")
            {
                string buildingName = GetBuildingNameFromInfraId(displayToNode.related_infra_id);
                toDisplay = $"{buildingName} ({displayToNode.name})";
            }

            toText.text = $"<b>To:</b> {toDisplay}";
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

    // ✅ NEW: Get building name from infra_id
    private string GetBuildingNameFromInfraId(string infraId)
    {
        // Find the infrastructure node with this infra_id
        foreach (var node in allNodes.Values)
        {
            if (node.type == "infrastructure" && node.related_infra_id == infraId)
            {
                return node.name;
            }
        }
        return "Building";
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
        Debug.Log("[PathfindingController] 🔵 OnConfirmRouteClicked START");

        if (selectedRouteIndex < 0 || selectedRouteIndex >= currentRoutes.Count)
        {
            Debug.LogError("❌ Invalid route index");
            return;
        }

        RouteData selectedRoute = currentRoutes[selectedRouteIndex];
        Debug.Log($"[PathfindingController] Selected route: {selectedRoute.startNode.name} → {selectedRoute.endNode.name}");

        // Get or create DirectionGenerator
        DirectionGenerator directionGen = GetComponent<DirectionGenerator>();
        if (directionGen == null)
        {
            Debug.Log("[PathfindingController] Adding DirectionGenerator component");
            directionGen = gameObject.AddComponent<DirectionGenerator>();
        }

        // ✅ FIX: Wait for DirectionGenerator to be ready before generating
        StartCoroutine(GenerateAndSaveDirections(directionGen, selectedRoute));
    }

    private IEnumerator GenerateAndSaveDirections(DirectionGenerator directionGen, RouteData selectedRoute)
    {
        Debug.Log("[PathfindingController] Waiting for DirectionGenerator to load data...");

        // Wait for DirectionGenerator to finish loading data (max 5 seconds)
        float timeout = 5f;
        float elapsed = 0f;

        while (!directionGen.IsDataLoaded() && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (!directionGen.IsDataLoaded())
        {
            Debug.LogError("[PathfindingController] ❌ DirectionGenerator failed to load data!");
            yield break;
        }

        Debug.Log("[PathfindingController] ✅ DirectionGenerator ready, generating directions...");

        List<NavigationDirection> directions = directionGen.GenerateDirections(selectedRoute);
        Debug.Log($"[PathfindingController] Generated {directions?.Count ?? 0} directions");

        if (directions == null || directions.Count == 0)
        {
            Debug.LogError("[PathfindingController] ❌ No directions generated!");
            yield break;
        }

        // Log each direction
        for (int i = 0; i < directions.Count; i++)
        {
            Debug.Log($"[PathfindingController] Direction {i}: {directions[i].instruction} (Indoor: {directions[i].isIndoorGrouped})");
        }

        Debug.Log("[PathfindingController] 🔵 Calling SaveRouteDataForAR...");
        SaveRouteDataForAR(selectedRoute, directions);
        Debug.Log("[PathfindingController] 🔵 SaveRouteDataForAR completed");

        // Load AR scene
        Debug.Log("[PathfindingController] Loading AR scene...");
        ARManagerCleanup arCleanup = FindObjectOfType<ARManagerCleanup>();
        if (arCleanup != null)
        {
            arCleanup.LoadARNavigation();
        }
        else
        {
            Debug.LogError("[PathfindingController] ❌ ARManagerCleanup not found!");
        }
    }


    // private void SaveRouteDataForAR(RouteData route, List<NavigationDirection> directions)
    // {
    //     // MapId is already saved in OnConfirmRouteClicked(), no need to duplicate here

    //     // ✅ CLEAR OLD DIRECTION DATA FIRST
    //     int oldDirectionCount = PlayerPrefs.GetInt("ARNavigation_DirectionCount", 0);
    //     for (int i = 0; i < oldDirectionCount; i++)
    //     {
    //         PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_Instruction");
    //         PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_Turn");
    //         PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_Distance");
    //         PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_DestNodeId");
    //         PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_DestNode");
    //     }

    //     // Save route info
    //     PlayerPrefs.SetString("ARNavigation_StartNodeId", route.startNode.node_id);
    //     PlayerPrefs.SetString("ARNavigation_EndNodeId", route.endNode.node_id);
    //     PlayerPrefs.SetString("ARNavigation_StartNodeName", route.startNode.name);
    //     PlayerPrefs.SetString("ARNavigation_EndNodeName", route.endNode.name);
    //     PlayerPrefs.SetFloat("ARNavigation_TotalDistance", route.totalDistance);
    //     PlayerPrefs.SetString("ARNavigation_FormattedDistance", route.formattedDistance);
    //     PlayerPrefs.SetString("ARNavigation_WalkingTime", route.walkingTime);
    //     PlayerPrefs.SetString("ARNavigation_ViaMode", route.viaMode);

    //     PlayerPrefs.SetInt("ARNavigation_PathNodeCount", route.path.Count);

    //     for (int i = 0; i < route.path.Count; i++)
    //     {
    //         PlayerPrefs.SetString($"ARNavigation_PathNode_{i}", route.path[i].node.node_id);
    //     }

    //     int edgeCount = route.path.Count - 1;
    //     PlayerPrefs.SetInt("ARNavigation_EdgeCount", edgeCount);

    //     for (int i = 0; i < edgeCount; i++)
    //     {
    //         string fromNode = route.path[i].node.node_id;
    //         string toNode = route.path[i + 1].node.node_id;

    //         PlayerPrefs.SetString($"ARNavigation_Edge_{i}_From", fromNode);
    //         PlayerPrefs.SetString($"ARNavigation_Edge_{i}_To", toNode);
    //     }

    //     // ✅ Save directions count FIRST
    //     PlayerPrefs.SetInt("ARNavigation_DirectionCount", directions.Count);

    //     // Save each direction
    //     for (int i = 0; i < directions.Count; i++)
    //     {
    //         var dir = directions[i];
    //         PlayerPrefs.SetString($"ARNavigation_Direction_{i}_Instruction", dir.instruction);
    //         PlayerPrefs.SetString($"ARNavigation_Direction_{i}_Turn", dir.turn.ToString());
    //         PlayerPrefs.SetFloat($"ARNavigation_Direction_{i}_Distance", dir.distanceInMeters);
    //         PlayerPrefs.SetString($"ARNavigation_Direction_{i}_DestNodeId", dir.destinationNode.node_id);
    //         PlayerPrefs.SetString($"ARNavigation_Direction_{i}_DestNode", dir.destinationNode.name);
    //     }

    //     PlayerPrefs.SetString("ARMode", "Navigation");

    //     // ✅ FORCE SAVE
    //     PlayerPrefs.Save();

    //     if (enableDebugLogs)
    //     {
    //         Debug.Log($"[PathfindingController] ✅ Route data saved for AR: {route.startNode.name} → {route.endNode.name}");
    //         Debug.Log($"[PathfindingController] ✅ Saved {route.path.Count} path nodes");
    //         Debug.Log($"[PathfindingController] ✅ Saved {directions.Count} navigation directions");
    //         Debug.Log($"[PathfindingController] ✅ MapId: {PlayerPrefs.GetString("ARScene_MapId", "NONE")}");

    //         // ✅ NEW: Verify the first direction was saved correctly
    //         if (directions.Count > 0)
    //         {
    //             Debug.Log($"[PathfindingController] ✅ First direction: {PlayerPrefs.GetString("ARNavigation_Direction_0_Instruction", "NOT FOUND")}");
    //         }
    //     }
    // }
    private void SaveRouteDataForAR(RouteData route, List<NavigationDirection> directions)
    {
        Debug.Log("[PathfindingController] 🔵 SaveRouteDataForAR START");
        Debug.Log($"[PathfindingController] 🔵 Route: {route.startNode.name} → {route.endNode.name}");
        Debug.Log($"[PathfindingController] 🔵 Directions count: {directions?.Count ?? 0}");

        if (directions == null || directions.Count == 0)
        {
            Debug.LogError("[PathfindingController] ❌ Cannot save - no directions provided!");
            return;
        }

        // ✅ CLEAR OLD DIRECTION DATA FIRST
        int oldDirectionCount = PlayerPrefs.GetInt("ARNavigation_DirectionCount", 0);
        Debug.Log($"[PathfindingController] 🔵 Clearing {oldDirectionCount} old directions");

        for (int i = 0; i < oldDirectionCount; i++)
        {
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_Instruction");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_Turn");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_Distance");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_DestNodeId");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_DestNode");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_IsIndoorGrouped");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_IsIndoorDirection");
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

        // ✅ Save directions count FIRST
        PlayerPrefs.SetInt("ARNavigation_DirectionCount", directions.Count);
        Debug.Log($"[PathfindingController] ✅ Saving {directions.Count} directions");

        // ✅ Save each direction WITH all flags
        for (int i = 0; i < directions.Count; i++)
        {
            var dir = directions[i];

            PlayerPrefs.SetString($"ARNavigation_Direction_{i}_Instruction", dir.instruction);
            PlayerPrefs.SetString($"ARNavigation_Direction_{i}_Turn", dir.turn.ToString());
            PlayerPrefs.SetFloat($"ARNavigation_Direction_{i}_Distance", dir.distanceInMeters);
            PlayerPrefs.SetString($"ARNavigation_Direction_{i}_DestNodeId", dir.destinationNode?.node_id ?? "");
            PlayerPrefs.SetString($"ARNavigation_Direction_{i}_DestNode", dir.destinationNode?.name ?? "Unknown");

            // ✅ CRITICAL: Save indoor flags
            PlayerPrefs.SetInt($"ARNavigation_Direction_{i}_IsIndoorGrouped", dir.isIndoorGrouped ? 1 : 0);
            PlayerPrefs.SetInt($"ARNavigation_Direction_{i}_IsIndoorDirection", dir.isIndoorDirection ? 1 : 0);

            Debug.Log($"[PathfindingController] 📝 Direction {i}: {dir.instruction}");
            Debug.Log($"  - Turn: {dir.turn}");
            Debug.Log($"  - Indoor Grouped: {dir.isIndoorGrouped}");
            Debug.Log($"  - Indoor Direction: {dir.isIndoorDirection}");
        }

        PlayerPrefs.SetString("ARMode", "Navigation");

        // ✅ FORCE SAVE
        PlayerPrefs.Save();

        Debug.Log($"[PathfindingController] ✅ Route data saved for AR: {route.startNode.name} → {route.endNode.name}");
        Debug.Log($"[PathfindingController] ✅ Saved {route.path.Count} path nodes");
        Debug.Log($"[PathfindingController] ✅ Saved {directions.Count} navigation directions");
        Debug.Log($"[PathfindingController] ✅ MapId: {PlayerPrefs.GetString("ARScene_MapId", "NONE")}");

        // ✅ Verify the first direction was saved correctly
        if (directions.Count > 0)
        {
            string savedInstruction = PlayerPrefs.GetString("ARNavigation_Direction_0_Instruction", "NOT FOUND");
            Debug.Log($"[PathfindingController] ✅ First direction verified: {savedInstruction}");

            int savedCount = PlayerPrefs.GetInt("ARNavigation_DirectionCount", -1);
            Debug.Log($"[PathfindingController] ✅ Direction count verified: {savedCount}");
        }

        Debug.Log("[PathfindingController] 🔵 SaveRouteDataForAR END");
    }

    // ✅ Also add this method to debug what's being called
    // private void OnConfirmRouteClicked()
    // {
    //     Debug.Log("[PathfindingController] 🔵 OnConfirmRouteClicked START");

    //     if (selectedRouteIndex < 0 || selectedRouteIndex >= currentRoutes.Count)
    //     {
    //         Debug.LogError("[PathfindingController] ❌ Invalid route index");
    //         return;
    //     }

    //     RouteData selectedRoute = currentRoutes[selectedRouteIndex];
    //     Debug.Log($"[PathfindingController] 🔵 Selected route: {selectedRoute.startNode.name} → {selectedRoute.endNode.name}");

    //     // Generate directions from the route
    //     DirectionGenerator directionGen = GetComponent<DirectionGenerator>();
    //     if (directionGen == null)
    //     {
    //         Debug.Log("[PathfindingController] 🔵 DirectionGenerator not found, adding component");
    //         directionGen = gameObject.AddComponent<DirectionGenerator>();
    //     }

    //     Debug.Log("[PathfindingController] 🔵 Calling GenerateDirections...");
    //     List<NavigationDirection> directions = directionGen.GenerateDirections(selectedRoute);
    //     Debug.Log($"[PathfindingController] 🔵 GenerateDirections returned {directions?.Count ?? 0} directions");

    //     if (directions == null || directions.Count == 0)
    //     {
    //         Debug.LogError("[PathfindingController] ❌ No directions generated!");
    //         return;
    //     }

    //     // Log each direction
    //     for (int i = 0; i < directions.Count; i++)
    //     {
    //         Debug.Log($"[PathfindingController] Direction {i}: {directions[i].instruction} (Indoor: {directions[i].isIndoorGrouped})");
    //     }

    //     Debug.Log("[PathfindingController] 🔵 Calling SaveRouteDataForAR...");
    //     SaveRouteDataForAR(selectedRoute, directions);
    //     Debug.Log("[PathfindingController] 🔵 OnConfirmRouteClicked END");
    // }

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