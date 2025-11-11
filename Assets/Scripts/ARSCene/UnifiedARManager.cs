using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;
using TMPro;
using UnityEngine.UI;

public class UnifiedARManager : MonoBehaviour
{
    [SerializeField] private string mainSceneName = "MainAppScene";
    private bool isExitingAR = false;

    [Header("AR Components")]
    public GameObject buildingMarkerPrefab;
    public GameObject userMarkerPrefab;
    public XROrigin xrOrigin;
    public ARRaycastManager arRaycastManager;
    public ARPlaneManager arPlaneManager;
    public ARCameraManager arCameraManager;
    public Camera arCamera;

    [Header("Marker Settings")]
    public float maxVisibleDistance = 500f;
    public float markerScale = 0.3f;
    public float minMarkerDistance = 2f;
    public float markerHeightOffset = 0.1f;

    [Header("Tracking Quality")]
    private TrackingState lastTrackingState = TrackingState.None;
    public bool requireGoodTracking = true;

    [Header("Top Panel UI - Main Display")]
    public TextMeshProUGUI gpsModeText;
    public TextMeshProUGUI fromLocationText;
    public TextMeshProUGUI toDestinationText;
    public TextMeshProUGUI currentLocationText;

    [Header("Debug Panel UI - Toggleable")]
    public GameObject debugPanel;
    public Button debugToggleButton;
    public TextMeshProUGUI trackingStatusText;
    public TextMeshProUGUI debugInfoText;
    public TextMeshProUGUI loadingText;

    [Header("GPS Settings")]
    public int gpsHistorySize = 5;
    public float positionUpdateThreshold = 1f;
    public float positionSmoothingFactor = 0.3f;
    public float nearestNodeSearchRadius = 500f;
    private Queue<Vector2> gpsLocationHistory = new Queue<Vector2>();
    private Vector2 lastStableGPSLocation;
    private bool gpsInitialized = false;

    [Header("GPS Lock Timer")]
    public float gpsLockDuration = 7f;
    private float gpsLockTimer = 0f;
    private bool isGPSLocked = false;

    [Header("Fixed AR Origin")]
    private Vector2 referenceGPS;
    private Vector3 referenceARWorldPosition;
    private float referenceCompassHeading; // NEW: Store compass heading at origin
    private bool arOriginInitialized = false;
    private GameObject userMarkerObject;

    [Header("Data")]
    private List<Node> currentNodes = new List<Node>();
    private List<Infrastructure> currentInfrastructures = new List<Infrastructure>();
    private Dictionary<string, MarkerAnchor> markerAnchors = new Dictionary<string, MarkerAnchor>();

    [Header("Position Tracking")]
    private Vector2 userLocation;
    private Node currentNearestNode;

    private bool isTrackingStarted = false;
    private bool isDebugPanelVisible = false;
    private float groundPlaneY = 0f;

    private string navigationFromNodeId = "";
    private string navigationToNodeId = "";

    private List<ARRaycastHit> arRaycastHits = new List<ARRaycastHit>();

    void Start()
    {
        InitializeComponents();
        SetupDebugToggle();
        LoadNavigationData();

        UpdateLoadingUI("Initializing AR...");
        StartCoroutine(InitializeARScene());
    }

    private void InitializeComponents()
    {
        if (arCamera == null) arCamera = Camera.main;
        if (xrOrigin == null) xrOrigin = FindObjectOfType<XROrigin>();
        if (arCameraManager == null) arCameraManager = FindObjectOfType<ARCameraManager>();
        if (arRaycastManager == null) arRaycastManager = FindObjectOfType<ARRaycastManager>();
        if (arPlaneManager == null) arPlaneManager = FindObjectOfType<ARPlaneManager>();
    }

    private void SetupDebugToggle()
    {
        if (debugPanel != null)
        {
            debugPanel.SetActive(false);
            isDebugPanelVisible = false;
        }

        if (debugToggleButton != null)
        {
            debugToggleButton.onClick.AddListener(ToggleDebugPanel);
        }

        UpdateTopPanelVisibility();
    }

    private void ToggleDebugPanel()
    {
        isDebugPanelVisible = !isDebugPanelVisible;
        if (debugPanel != null)
        {
            debugPanel.SetActive(isDebugPanelVisible);
        }
    }

    private void LoadNavigationData()
    {
        navigationFromNodeId = PlayerPrefs.GetString("ARNavigation_OriginalFromNodeId", "");
        navigationToNodeId = PlayerPrefs.GetString("ARNavigation_OriginalToNodeId", "");
    }

    public void ExitARScene()
    {
        if (isExitingAR) return;

        isExitingAR = true;

        ClearMarkers();
        CancelInvoke();

        if (GlobalManager.Instance != null)
        {
            GlobalManager.Instance.StartCoroutine(GlobalManager.Instance.SafeARCleanupAndExit(mainSceneName));
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(mainSceneName);
        }
    }

    IEnumerator InitializeARScene()
    {
        UpdateLoadingUI("Waiting for AR Session...");
        yield return new WaitForSeconds(0.5f);

        UpdateLoadingUI("Waiting for GPS Manager...");
        while (GPSManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }
        UpdateLoadingUI("GPS Manager found, starting location services...");
        yield return new WaitForSeconds(0.5f);

        groundPlaneY = arCamera.transform.position.y - 1.6f;

        UpdateLoadingUI("Loading map data...");
        string currentMapId = GetCurrentMapId();
        yield return StartCoroutine(LoadCurrentMapData(currentMapId));

        UpdateLoadingUI("Establishing AR world origin...");
        yield return StartCoroutine(InitializeFixedAROrigin());

        UpdateLoadingUI("Starting tracking...");
        StartMarkerTracking();
        HideLoadingUI();
    }

    private IEnumerator InitializeFixedAROrigin()
    {
        // Use "from" node as reference GPS
        if (!string.IsNullOrEmpty(navigationFromNodeId))
        {
            Node fromNode = currentNodes.FirstOrDefault(n => n.node_id == navigationFromNodeId);
            if (fromNode != null && fromNode.type != "indoorinfra")
            {
                referenceGPS = new Vector2(fromNode.latitude, fromNode.longitude);
                Debug.Log($"[UnifiedARManager] Using FROM node as AR origin: {referenceGPS}");
            }
            else
            {
                referenceGPS = GPSManager.Instance.GetSmoothedCoordinates();
                Debug.Log($"[UnifiedARManager] FROM node not found or indoor, using current GPS: {referenceGPS}");
            }
        }
        else
        {
            referenceGPS = GPSManager.Instance.GetSmoothedCoordinates();
            Debug.Log($"[UnifiedARManager] Using current GPS: {referenceGPS}");
        }

        referenceARWorldPosition = arCamera.transform.position;
        
        // NEW: Store the compass heading at initialization
        // This tells us which direction the AR camera was facing when we set the origin
        referenceCompassHeading = GPSManager.Instance.GetHeading();
        Debug.Log($"[UnifiedARManager] AR world origin set at: {referenceARWorldPosition}, Compass: {referenceCompassHeading}°");

        userLocation = referenceGPS;
        lastStableGPSLocation = referenceGPS;
        gpsLocationHistory.Enqueue(referenceGPS);
        gpsInitialized = true;

        isGPSLocked = true;
        gpsLockTimer = gpsLockDuration;

        arOriginInitialized = true;

        CreateUserMarker();

        yield break;
    }

    private void CreateUserMarker()
    {
        if (userMarkerPrefab != null)
        {
            userMarkerObject = Instantiate(userMarkerPrefab);
            userMarkerObject.transform.position = referenceARWorldPosition;
            userMarkerObject.transform.position = new Vector3(
                userMarkerObject.transform.position.x,
                groundPlaneY + markerHeightOffset,
                userMarkerObject.transform.position.z
            );
            userMarkerObject.transform.localScale = Vector3.one * markerScale;
            Debug.Log("[UnifiedARManager] User marker created at origin");
        }
    }

    private void UpdateUserMarkerPosition()
    {
        if (userMarkerObject == null) return;

        Vector3 userWorldPos = GPSToWorldPosition(userLocation.x, userLocation.y);
        userWorldPos.y = groundPlaneY + markerHeightOffset;

        userMarkerObject.transform.position = userWorldPos;
    }

    private void StartMarkerTracking()
    {
        if (isTrackingStarted)
        {
            return;
        }

        isTrackingStarted = true;
        InvokeRepeating(nameof(UpdateMarkers), 0.5f, 0.2f);
        InvokeRepeating(nameof(UpdateNearestNode), 0.5f, 2f);
    }

    public void OnQRCodeScanned(Node scannedNode)
    {
        // Only handle outdoor QR codes now (no indoor switching)
        bool isIndoorNode = scannedNode.type == "indoorinfra";

        if (isIndoorNode)
        {
            Debug.LogWarning("[UnifiedARManager] Indoor QR codes are not supported in Navigation mode");
            return;
        }

        ClearMarkers();

        // Update reference GPS and AR origin
        referenceGPS = new Vector2(scannedNode.latitude, scannedNode.longitude);
        referenceARWorldPosition = arCamera.transform.position;
        referenceCompassHeading = GPSManager.Instance.GetHeading(); // NEW: Update compass heading
        arOriginInitialized = true;

        Debug.Log($"[UnifiedARManager] QR Recalibration - GPS: {referenceGPS}, Compass: {referenceCompassHeading}°");

        userLocation = referenceGPS;
        lastStableGPSLocation = referenceGPS;
        gpsLocationHistory.Clear();
        gpsLocationHistory.Enqueue(referenceGPS);
        gpsInitialized = true;

        isGPSLocked = true;
        gpsLockTimer = gpsLockDuration;

        if (userMarkerObject != null)
        {
            userMarkerObject.SetActive(true);
            userMarkerObject.transform.position = referenceARWorldPosition;
        }
        else
        {
            CreateUserMarker();
        }

        StartCoroutine(InitializeOutdoorMarkersAfterScan());

        UpdateTopPanelUI();
        UpdateTrackingStatusUI();
        UpdateDebugInfo();
    }

    private IEnumerator InitializeOutdoorMarkersAfterScan()
    {
        yield return null;
        yield return null;

        ReconcileVisibleMarkersGPS();

        yield return new WaitForSeconds(1.5f);
    }

    public string GetInfrastructureName(string infraId)
    {
        Infrastructure infra = currentInfrastructures.FirstOrDefault(i => i.infra_id == infraId);
        return infra != null ? infra.name : infraId;
    }

    private string GetCurrentMapId()
    {
        if (PlayerPrefs.HasKey("ARScene_MapId"))
        {
            return PlayerPrefs.GetString("ARScene_MapId");
        }

        return null;
    }

    IEnumerator LoadCurrentMapData(string currentMapId)
    {
        bool nodesLoaded = false;
        bool infraLoaded = false;

        UpdateLoadingUI($"Loading nodes for map {currentMapId}...");

        yield return StartCoroutine(LoadNodesData(currentMapId, (success) =>
        {
            nodesLoaded = success;
        }));

        UpdateLoadingUI("Loading infrastructure data...");

        yield return StartCoroutine(LoadInfrastructureData((success) =>
        {
            infraLoaded = success;
        }));
    }

    IEnumerator LoadNodesData(string mapId, System.Action<bool> onComplete)
    {
        string fileName = $"nodes_{mapId}.json";
        bool loadSuccess = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonData) =>
            {
                try
                {
                    Node[] nodes = JsonHelper.FromJson<Node>(jsonData);

                    // Only load infrastructure and intermediate nodes (outdoor only)
                    currentNodes = nodes.Where(n =>
                        (n.type == "infrastructure" || n.type == "intermediate") && n.is_active
                    ).ToList();

                    loadSuccess = true;
                }
                catch (System.Exception)
                {
                    loadSuccess = false;
                }
            },
            (error) =>
            {
                loadSuccess = false;
            }
        ));

        onComplete?.Invoke(loadSuccess);
    }

    IEnumerator LoadInfrastructureData(System.Action<bool> onComplete)
    {
        string fileName = "infrastructure.json";
        bool loadSuccess = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonData) =>
            {
                try
                {
                    Infrastructure[] infrastructures = JsonHelper.FromJson<Infrastructure>(jsonData);
                    currentInfrastructures = infrastructures.Where(i => !i.is_deleted).ToList();
                    loadSuccess = true;
                }
                catch (System.Exception)
                {
                    loadSuccess = false;
                }
            },
            (error) =>
            {
                loadSuccess = false;
            }
        ));

        onComplete?.Invoke(loadSuccess);
    }

    void UpdateMarkers()
    {
        if (isExitingAR) return;

        UpdateMarkersGPS();
    }

    void UpdateMarkersGPS()
    {
        if (!arOriginInitialized)
        {
            Debug.LogWarning("[UnifiedARManager] AR origin not initialized yet!");
            return;
        }

        if (isGPSLocked)
        {
            gpsLockTimer -= Time.deltaTime;
            if (gpsLockTimer <= 0)
            {
                isGPSLocked = false;
                Debug.Log("GPS unlocked, using real GPS coordinates now");
            }
        }

        Vector2 rawGpsLocation = GPSManager.Instance.GetSmoothedCoordinates();

        if (rawGpsLocation.magnitude < 0.0001f)
        {
            Debug.LogWarning("UpdateMarkersGPS: GPS location is zero/invalid!");
            return;
        }

        if (!isGPSLocked)
        {
            userLocation = StabilizeGPSLocation(rawGpsLocation);
        }

        UpdateUserMarkerPosition();

        UpdateTrackingStatusUI();
        ReconcileVisibleMarkersGPS();
        UpdateDebugInfo();
        UpdateTopPanelUI();
    }

    private Vector2 StabilizeGPSLocation(Vector2 rawLocation)
    {
        if (!gpsInitialized && rawLocation.magnitude > 0.0001f)
        {
            lastStableGPSLocation = rawLocation;
            gpsLocationHistory.Enqueue(rawLocation);
            gpsInitialized = true;
            return lastStableGPSLocation;
        }

        gpsLocationHistory.Enqueue(rawLocation);
        if (gpsLocationHistory.Count > gpsHistorySize)
        {
            gpsLocationHistory.Dequeue();
        }

        Vector2 averagedLocation = Vector2.zero;
        foreach (Vector2 loc in gpsLocationHistory)
        {
            averagedLocation += loc;
        }
        averagedLocation /= gpsLocationHistory.Count;

        float distanceFromLast = Vector2.Distance(averagedLocation, lastStableGPSLocation);

        if (distanceFromLast >= positionUpdateThreshold)
        {
            lastStableGPSLocation = Vector2.Lerp(lastStableGPSLocation, averagedLocation, positionSmoothingFactor);
        }

        return lastStableGPSLocation;
    }

    private void UpdateNearestNode()
    {
        if (currentNodes == null || currentNodes.Count == 0)
        {
            currentNearestNode = null;
            return;
        }

        Node nearestNode = null;
        float nearestDistance = float.MaxValue;

        foreach (Node node in currentNodes)
        {
            if (node.type == "indoorinfra") continue;

            float distance = CalculateDistanceGPS(userLocation, new Vector2(node.latitude, node.longitude));

            if (distance < nearestDistance && distance <= nearestNodeSearchRadius)
            {
                nearestDistance = distance;
                nearestNode = node;
            }
        }

        currentNearestNode = nearestNode;
        UpdateTopPanelUI();
    }

    private void ReconcileVisibleMarkersGPS()
    {
        if (currentNodes == null || currentNodes.Count == 0)
        {
            Debug.LogWarning("ReconcileVisibleMarkersGPS: No nodes loaded!");
            return;
        }

        List<string> nodesToRemove = new List<string>();

        foreach (var kvp in markerAnchors)
        {
            MarkerAnchor anchor = kvp.Value;

            if (!ShouldShowMarkerGPS(anchor.node))
            {
                if (anchor.markerGameObject != null)
                    Destroy(anchor.markerGameObject);
                nodesToRemove.Add(kvp.Key);
            }
        }

        foreach (string nodeId in nodesToRemove)
        {
            markerAnchors.Remove(nodeId);
        }

        int shouldShowCount = 0;
        int alreadyExistsCount = 0;
        
        foreach (Node node in currentNodes)
        {
            if (node.type == "indoorinfra")
                continue;

            if (markerAnchors.ContainsKey(node.node_id))
            {
                alreadyExistsCount++;
                continue;
            }

            if (ShouldShowMarkerGPS(node))
            {
                shouldShowCount++;
                CreateMarkerForNodeGPS(node);
            }
        }
        
        if (shouldShowCount > 0)
        {
            Debug.Log($"ReconcileVisibleMarkersGPS: Created {shouldShowCount} new markers (Already exist: {alreadyExistsCount}, Total: {markerAnchors.Count})");
        }
    }

    bool ShouldShowMarkerGPS(Node node)
    {
        if (node.type == "indoorinfra")
            return false;

        float distance = CalculateDistanceGPS(userLocation, new Vector2(node.latitude, node.longitude));

        return distance <= maxVisibleDistance && distance >= minMarkerDistance;
    }

    void CreateMarkerForNodeGPS(Node node)
    {
        if (buildingMarkerPrefab == null)
        {
            Debug.LogError("buildingMarkerPrefab is NULL! Cannot create markers!");
            return;
        }

        Infrastructure infra = currentInfrastructures.FirstOrDefault(i => i.infra_id == node.related_infra_id);
        if (infra == null)
        {
            Debug.LogWarning($"No infrastructure found for node {node.node_id} with infra_id {node.related_infra_id}");
            return;
        }

        Vector3 worldPosition = GPSToWorldPosition(node.latitude, node.longitude);
        
        worldPosition.y = groundPlaneY + markerHeightOffset;

        Debug.Log($"Creating marker for {infra.name} at fixed world position: {worldPosition}");

        GameObject marker = Instantiate(buildingMarkerPrefab);
        marker.transform.position = worldPosition;
        marker.transform.localScale = Vector3.one * markerScale;

        UpdateMarkerText(marker, infra);

        MarkerAnchor anchor = new MarkerAnchor
        {
            node = node,
            nodeLatitude = node.latitude,
            nodeLongitude = node.longitude,
            nodeX = node.x_coordinate,
            nodeY = node.y_coordinate,
            markerGameObject = marker
        };

        markerAnchors[node.node_id] = anchor;
        
        Debug.Log($"Total markers now: {markerAnchors.Count}");
    }

    Vector3 GPSToWorldPosition(float latitude, float longitude)
    {
        float deltaLat = latitude - referenceGPS.x;
        float deltaLng = longitude - referenceGPS.y;

        float meterPerDegree = 111000f;
        
        // Calculate offset in meters (North/East relative to GPS coordinates)
        float offsetEast = deltaLng * meterPerDegree * Mathf.Cos(referenceGPS.x * Mathf.Deg2Rad);
        float offsetNorth = deltaLat * meterPerDegree;

        // NEW: Apply compass rotation to align with real-world directions
        // When AR world was created, the camera was facing referenceCompassHeading
        // We need to rotate the offset to match Unity's coordinate system
        float headingRad = referenceCompassHeading * Mathf.Deg2Rad;
        
        // Rotate the offset by the compass heading
        float rotatedX = offsetEast * Mathf.Cos(headingRad) - offsetNorth * Mathf.Sin(headingRad);
        float rotatedZ = offsetEast * Mathf.Sin(headingRad) + offsetNorth * Mathf.Cos(headingRad);

        // Position relative to FIXED AR world origin
        Vector3 worldPos = referenceARWorldPosition;
        worldPos.x += rotatedX;
        worldPos.z += rotatedZ;

        return worldPos;
    }

    float CalculateDistanceGPS(Vector2 coord1, Vector2 coord2)
    {
        float lat1Rad = coord1.x * Mathf.Deg2Rad;
        float lat2Rad = coord2.x * Mathf.Deg2Rad;
        float deltaLatRad = (coord2.x - coord1.x) * Mathf.Deg2Rad;
        float deltaLngRad = (coord2.y - coord1.y) * Mathf.Deg2Rad;

        float a = Mathf.Sin(deltaLatRad / 2) * Mathf.Sin(deltaLatRad / 2) +
                  Mathf.Cos(lat1Rad) * Mathf.Cos(lat2Rad) *
                  Mathf.Sin(deltaLngRad / 2) * Mathf.Sin(deltaLngRad / 2);

        float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));

        return 6371000 * c;
    }

    void UpdateMarkerText(GameObject marker, Infrastructure infra)
    {
        TextMeshPro textMeshPro = marker.GetComponentInChildren<TextMeshPro>();
        if (textMeshPro != null)
        {
            textMeshPro.text = infra.name;
            textMeshPro.fontSize = 8;

            if (gameObject != null && gameObject.activeInHierarchy && !isExitingAR)
            {
                StartCoroutine(UpdateTextRotation(textMeshPro.transform));
            }
        }

        Text nameText = marker.GetComponentInChildren<Text>();
        if (nameText != null && textMeshPro == null)
        {
            nameText.text = infra.name;
            nameText.fontSize = 12;
        }
    }

    IEnumerator UpdateTextRotation(Transform textTransform)
    {
        while (textTransform != null && !isExitingAR && gameObject != null && gameObject.activeInHierarchy)
        {
            if (arCamera != null)
            {
                textTransform.LookAt(arCamera.transform);
                textTransform.Rotate(0, 180, 0);
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void UpdateTopPanelUI()
    {
        UpdateGPSModeText();
        UpdateCurrentLocationText();
        UpdateNavigationTexts();
    }

    private void UpdateTopPanelVisibility()
    {
        if (fromLocationText != null)
        {
            fromLocationText.gameObject.SetActive(true);
        }

        if (toDestinationText != null)
        {
            toDestinationText.gameObject.SetActive(true);
        }

        if (currentLocationText != null)
        {
            currentLocationText.gameObject.SetActive(true);
        }
    }

    private void UpdateGPSModeText()
    {
        if (gpsModeText == null) return;

        gpsModeText.text = "GPS | AR NAVIGATION";
    }

    private void UpdateCurrentLocationText()
    {
        if (currentLocationText == null) return;

        currentLocationText.gameObject.SetActive(true);

        Vector2 coords = userLocation;
        string locationDisplay = $"({coords.x:F5}, {coords.y:F5})";

        if (currentNearestNode != null)
        {
            locationDisplay += $" | {currentNearestNode.name}";
        }

        currentLocationText.text = locationDisplay;
    }

    private void UpdateNavigationTexts()
    {
        if (fromLocationText != null)
        {
            fromLocationText.gameObject.SetActive(true);

            if (!string.IsNullOrEmpty(navigationFromNodeId) && currentNodes != null)
            {
                Node fromNode = currentNodes.FirstOrDefault(n => n.node_id == navigationFromNodeId);
                if (fromNode != null)
                {
                    fromLocationText.text = $"FROM: {fromNode.name}";
                }
                else
                {
                    fromLocationText.text = "FROM: Unknown";
                }
            }
            else
            {
                fromLocationText.text = "FROM: Not Set";
            }
        }

        if (toDestinationText != null)
        {
            toDestinationText.gameObject.SetActive(true);

            if (!string.IsNullOrEmpty(navigationToNodeId) && currentNodes != null)
            {
                Node toNode = currentNodes.FirstOrDefault(n => n.node_id == navigationToNodeId);
                if (toNode != null)
                {
                    toDestinationText.text = $"TO: {toNode.name}";
                }
                else
                {
                    toDestinationText.text = "TO: Unknown";
                }
            }
            else
            {
                toDestinationText.text = "TO: Not Set";
            }
        }
    }

    void UpdateTrackingStatusUI()
    {
        if (trackingStatusText != null)
        {
            Vector2 coords = GPSManager.Instance.GetCoordinates();
            string lockStatus = isGPSLocked ? $" 🔒 Locked ({gpsLockTimer:F0}s)" : "";

            if (coords.magnitude > 0)
            {
                trackingStatusText.text = $"🌍 Outdoor (GPS){lockStatus}\n{coords.x:F5}, {coords.y:F5}";
                trackingStatusText.color = isGPSLocked ? Color.yellow : Color.green;
            }
            else
            {
                trackingStatusText.text = "🌍 Outdoor\nGPS: No Signal";
                trackingStatusText.color = Color.red;
            }
        }
    }

    void UpdateDebugInfo()
    {
        if (debugInfoText != null)
        {
            string lockStatus = isGPSLocked ? $" (Locked: {gpsLockTimer:F1}s)" : "";
            debugInfoText.text = $"🧭 Navigation + 🌍 Outdoor (GPS){lockStatus}\n" +
                                 $"User: {userLocation.x:F5}, {userLocation.y:F5}\n" +
                                 $"Reference: {referenceGPS.x:F5}, {referenceGPS.y:F5}\n" +
                                 $"Active Markers: {markerAnchors.Count}";
        }
    }

    void UpdateLoadingUI(string message)
    {
        if (loadingText != null)
        {
            loadingText.text = message;
            loadingText.gameObject.SetActive(true);
        }
    }

    void HideLoadingUI()
    {
        if (loadingText != null)
        {
            loadingText.gameObject.SetActive(false);
        }
    }

    void ClearMarkers()
    {
        foreach (var kvp in markerAnchors)
        {
            if (kvp.Value.markerGameObject != null)
                Destroy(kvp.Value.markerGameObject);
        }
        markerAnchors.Clear();
    }

    void OnDestroy()
    {
        isExitingAR = true;
        CancelInvoke();
        ClearMarkers();
        StopAllCoroutines();

        if (userMarkerObject != null)
        {
            Destroy(userMarkerObject);
        }

        if (debugToggleButton != null)
        {
            debugToggleButton.onClick.RemoveListener(ToggleDebugPanel);
        }
    }

    public Vector2 GetUserXY()
    {
        return userLocation;
    }

    public bool IsIndoorMode()
    {
        return false; // Always outdoor now
    }

    public string GetCurrentIndoorInfraId()
    {
        return ""; // No indoor mode
    }

    public Vector2 GetReferenceGPS()
    {
        return referenceGPS;
    }

    public Vector3 GetReferenceARWorldPosition()
    {
        return referenceARWorldPosition;
    }
}