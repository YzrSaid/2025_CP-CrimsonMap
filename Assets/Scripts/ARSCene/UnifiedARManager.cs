using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;
public class UnifiedARManager : MonoBehaviour
{
    [Header("AR Exit Settings")]
    [SerializeField] private Button backToMainButton;
    [SerializeField] private string mainSceneName = "MainAppScene";
    private bool isExitingAR = false;

    [Header("AR Components")]
    public GameObject buildingMarkerPrefab;
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

    [Header("Visibility Settings")]
    public float fieldOfViewAngle = 90f;
    public float forwardDotThreshold = 0.3f;

    [Header("Tracking Quality")]
    private TrackingState lastTrackingState = TrackingState.None;
    public bool requireGoodTracking = true;

    [Header("UI References")]
    public TextMeshProUGUI trackingStatusText;
    public TextMeshProUGUI debugText;
    public TextMeshProUGUI loadingText;

    [Header("GPS Settings (for GPS modes)")]
    public int gpsHistorySize = 5;
    public float positionUpdateThreshold = 1f;
    public float positionSmoothingFactor = 0.3f;
    private Queue<Vector2> gpsLocationHistory = new Queue<Vector2>();
    private Vector2 lastStableGPSLocation;
    private bool gpsInitialized = false;

    [Header("Offline (X,Y) Settings")]
    public Vector2 referenceNodeXY = Vector2.zero;
    private Vector3 referenceWorldPosition;
    private bool referencePointSet = false;
    private float groundPlaneY = 0f;
    public int positionHistorySize = 5;
    private Queue<Vector3> positionHistory = new Queue<Vector3>();

    [Header("Data")]
    private List<Node> currentNodes = new List<Node>();
    private List<Infrastructure> currentInfrastructures = new List<Infrastructure>();
    private Dictionary<string, MarkerAnchor> markerAnchors = new Dictionary<string, MarkerAnchor>();

    [Header("Position Tracking")]
    private Vector2 userLocation; // GPS: lat/lng, Offline: x/y
    private Vector2 userXY; // For offline mode
    private Vector3 lastARCameraPosition;
    private Vector3 smoothedARPosition;

    [Header("Mode Configuration")]
    private ARMode currentARMode = ARMode.DirectAR;
    private LocalizationMode currentLocalizationMode = LocalizationMode.GPS;
    private bool isTrackingStarted = false;

    private enum ARMode { DirectAR, Navigation }
    private enum LocalizationMode { GPS, Offline }

    private List<ARRaycastHit> arRaycastHits = new List<ARRaycastHit>();

    void Start()
    {
        InitializeComponents();
        DetermineModes();
        SetupBackButton();

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

    private void DetermineModes()
    {
        // Determine AR Mode
        string arModeString = PlayerPrefs.GetString("ARMode", "DirectAR");
        currentARMode = arModeString == "Navigation" ? ARMode.Navigation : ARMode.DirectAR;

        // Determine Localization Mode
        string localizationModeString = PlayerPrefs.GetString("LocalizationMode", "GPS");
        currentLocalizationMode = localizationModeString == "Offline" ? LocalizationMode.Offline : LocalizationMode.GPS;

        Debug.Log($"🎯 AR Mode: {currentARMode} | Localization: {currentLocalizationMode}");
    }

    private void SetupBackButton()
    {
        if (backToMainButton == null)
        {
            GameObject backBtn = GameObject.Find("BackButton") ??
                                 GameObject.Find("Back Button") ??
                                 GameObject.Find("ExitARButton");
            if (backBtn != null)
                backToMainButton = backBtn.GetComponent<Button>();
        }

        if (backToMainButton != null)
        {
            backToMainButton.onClick.RemoveAllListeners();
            backToMainButton.onClick.AddListener(ExitARScene);
            Debug.Log("✅ AR exit button connected");
        }
    }

    public void ExitARScene()
    {
        if (isExitingAR) return;

        Debug.Log("Exiting AR scene...");
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
        yield return new WaitForSeconds(1f);

        // GPS Mode: Wait for GPS to initialize
        if (currentLocalizationMode == LocalizationMode.GPS)
        {
            UpdateLoadingUI("Waiting for GPS Manager...");
            while (GPSManager.Instance == null)
            {
                yield return new WaitForSeconds(0.1f);
            }
            UpdateLoadingUI("GPS Manager found, starting location services...");
            yield return new WaitForSeconds(1f);
        }
        // Offline Mode: Wait for ground plane
        else
        {
            UpdateLoadingUI("Detecting ground plane...");
            yield return StartCoroutine(WaitForGroundPlane());
        }

        UpdateLoadingUI("Loading map data...");
        string currentMapId = GetCurrentMapId();
        yield return StartCoroutine(LoadCurrentMapData(currentMapId));

        // MODE LOGIC: 4 combinations
        if (currentARMode == ARMode.DirectAR && currentLocalizationMode == LocalizationMode.Offline)
        {
            // Direct AR + Offline: Wait for QR scan (ARUIManager will show scan panel)
            UpdateLoadingUI("Ready - Scan QR code to begin");
            Debug.Log("🔒 Direct AR + Offline: Waiting for QR scan");
            HideLoadingUI();
        }
        else if (currentARMode == ARMode.DirectAR && currentLocalizationMode == LocalizationMode.GPS)
        {
            // Direct AR + GPS: Start immediately with GPS
            UpdateLoadingUI("Starting GPS tracking...");
            StartMarkerTracking();
            HideLoadingUI();
            Debug.Log("✅ Direct AR + GPS: Started");
        }
        else if (currentARMode == ARMode.Navigation && currentLocalizationMode == LocalizationMode.Offline)
        {
            // AR Navigation + Offline: Set reference point and start
            UpdateLoadingUI("Setting reference point...");
            SetReferencePoint(referenceNodeXY);
            UpdateLoadingUI("Starting AR tracking...");
            StartMarkerTracking();
            HideLoadingUI();
            Debug.Log("✅ AR Navigation + Offline: Started");
        }
        else if (currentARMode == ARMode.Navigation && currentLocalizationMode == LocalizationMode.GPS)
        {
            // AR Navigation + GPS: Start immediately with GPS
            UpdateLoadingUI("Starting GPS navigation...");
            StartMarkerTracking();
            HideLoadingUI();
            Debug.Log("✅ AR Navigation + GPS: Started");
        }

        Debug.Log($"✅ AR initialized - Nodes: {currentNodes.Count}, Infra: {currentInfrastructures.Count}");
    }

    private IEnumerator WaitForGroundPlane()
    {
        float timeout = 5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (arPlaneManager != null && arPlaneManager.trackables.count > 0)
            {
                foreach (var plane in arPlaneManager.trackables)
                {
                    if (plane.alignment == PlaneAlignment.HorizontalUp)
                    {
                        groundPlaneY = plane.transform.position.y;
                        Debug.Log($"✅ Ground plane detected at Y: {groundPlaneY:F2}");
                        yield break;
                    }
                }
            }

            elapsed += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        groundPlaneY = arCamera.transform.position.y - 1.6f;
        Debug.LogWarning($"⚠️ No ground plane detected, using estimated ground: Y={groundPlaneY:F2}");
    }

    /// <summary>
    /// Start the marker tracking/update loop
    /// </summary>
    private void StartMarkerTracking()
    {
        if (isTrackingStarted)
        {
            Debug.LogWarning("⚠️ Marker tracking already started");
            return;
        }

        isTrackingStarted = true;

        if (currentLocalizationMode == LocalizationMode.GPS)
        {
            InvokeRepeating(nameof(UpdateMarkersGPS), 2f, 1f);
        }
        else
        {
            InvokeRepeating(nameof(UpdateMarkersOffline), 0.5f, 0.15f);
        }

        Debug.Log("✅ Marker tracking started");
    }

    /// <summary>
    /// Set reference point for Offline mode (X,Y)
    /// </summary>
    private void SetReferencePoint(Vector2 nodeXY)
    {
        referenceNodeXY = nodeXY;
        referenceWorldPosition = arCamera.transform.position;
        lastARCameraPosition = referenceWorldPosition;
        smoothedARPosition = referenceWorldPosition;

        positionHistory.Clear();

        referencePointSet = true;
        userXY = referenceNodeXY;

        Debug.Log($"🎯 Reference point set at X:{nodeXY.x:F2}, Y:{nodeXY.y:F2}");
        Debug.Log($"🎯 Unity world position: {referenceWorldPosition}");
        Debug.Log($"🎯 User XY updated to: X:{userXY.x:F2}, Y:{userXY.y:F2}");
    }

    /// <summary>
    /// PUBLIC: Called by ARSceneQRRecalibration when QR is scanned
    /// Handles both GPS mode (gets lat/lng) and Offline mode (gets X,Y)
    /// </summary>
    public void OnQRCodeScanned(Node scannedNode)
    {
        Debug.Log($"📷 QR Scanned: {scannedNode.name}");

        if (currentLocalizationMode == LocalizationMode.GPS)
        {
            // GPS Mode: Update user location to scanned node's GPS coordinates
            userLocation = new Vector2(scannedNode.latitude, scannedNode.longitude);
            lastStableGPSLocation = userLocation;
            gpsLocationHistory.Clear();
            gpsLocationHistory.Enqueue(userLocation);
            gpsInitialized = true;

            Debug.Log($"✅ GPS recalibrated to: Lat:{scannedNode.latitude:F6}, Lng:{scannedNode.longitude:F6}");
        }
        else
        {
            // Offline Mode: Set reference point to scanned node's X,Y
            SetReferencePoint(new Vector2(scannedNode.x_coordinate, scannedNode.y_coordinate));

            // If Direct AR + Offline and not started yet, start tracking
            if (currentARMode == ARMode.DirectAR && !isTrackingStarted)
            {
                StartMarkerTracking();
                Debug.Log("✅ Direct AR + Offline: Tracking started after QR scan");
            }

            Debug.Log($"✅ Offline recalibrated to: X:{scannedNode.x_coordinate:F2}, Y:{scannedNode.y_coordinate:F2}");
        }

        UpdateTrackingStatusUI();
        UpdateDebugInfo();
    }

    private string GetCurrentMapId()
    {
        if (MapManager.Instance != null && MapManager.Instance.GetCurrentMap() != null)
        {
            return MapManager.Instance.GetCurrentMap().map_id;
        }

        if (PlayerPrefs.HasKey("CurrentMapId"))
        {
            return PlayerPrefs.GetString("CurrentMapId");
        }

        Debug.LogWarning("[UnifiedARManager] Using default MAP-01");
        return "MAP-01";
    }

    IEnumerator LoadCurrentMapData(string currentMapId)
    {
        bool nodesLoaded = false;
        bool infraLoaded = false;

        UpdateLoadingUI($"Loading nodes for map {currentMapId}...");

        yield return StartCoroutine(LoadNodesData(currentMapId, (success) =>
        {
            nodesLoaded = success;
            Debug.Log($"📦 Nodes: {currentNodes.Count} loaded");
        }));

        UpdateLoadingUI("Loading infrastructure data...");

        yield return StartCoroutine(LoadInfrastructureData((success) =>
        {
            infraLoaded = success;
            Debug.Log($"🏢 Infrastructure: {currentInfrastructures.Count} loaded");
        }));

        if (nodesLoaded && infraLoaded)
        {
            Debug.Log($"✅ DATA LOADED - Nodes: {currentNodes.Count}, Infra: {currentInfrastructures.Count}");
        }
        else
        {
            Debug.LogError($"❌ Load failed - Nodes: {nodesLoaded}, Infra: {infraLoaded}");
        }
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
                    currentNodes = nodes.Where(n => n.type == "infrastructure" && n.is_active).ToList();
                    Debug.Log($"✅ Found {currentNodes.Count} active infrastructure nodes");
                    loadSuccess = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ Error parsing nodes: {e.Message}");
                    loadSuccess = false;
                }
            },
            (error) =>
            {
                Debug.LogError($"❌ Failed to load nodes: {error}");
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
                    Debug.Log($"✅ Found {currentInfrastructures.Count} active infrastructures");
                    loadSuccess = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ Error loading infrastructure: {e.Message}");
                    loadSuccess = false;
                }
            },
            (error) =>
            {
                Debug.LogError($"❌ Failed to load infrastructure: {error}");
                loadSuccess = false;
            }
        ));

        onComplete?.Invoke(loadSuccess);
    }

    // ═══════════════════════════════════════════════════════════════
    // GPS MODE UPDATE LOGIC
    // ═══════════════════════════════════════════════════════════════
    void UpdateMarkersGPS()
    {
        if (isExitingAR) return;

        Vector2 rawGpsLocation = GPSManager.Instance.GetSmoothedCoordinates();

        if (rawGpsLocation.magnitude < 0.0001f)
        {
            return;
        }

        userLocation = StabilizeGPSLocation(rawGpsLocation);

        UpdateTrackingStatusUI();
        UpdateAnchoredMarkerPositionsGPS();
        ReconcileVisibleMarkersGPS();
        UpdateDebugInfo();
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

    private void UpdateAnchoredMarkerPositionsGPS()
    {
        foreach (var kvp in markerAnchors)
        {
            MarkerAnchor anchor = kvp.Value;

            if (anchor.markerGameObject == null) continue;

            Vector3 newWorldPos = GPSToWorldPosition(anchor.nodeLatitude, anchor.nodeLongitude);
            newWorldPos = GetGroundPosition(newWorldPos);

            anchor.markerGameObject.transform.position = Vector3.Lerp(
                anchor.markerGameObject.transform.position,
                newWorldPos,
                positionSmoothingFactor
            );
        }
    }

    private void ReconcileVisibleMarkersGPS()
    {
        if (currentNodes == null || currentNodes.Count == 0) return;

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

        int created = 0;

        foreach (Node node in currentNodes)
        {
            if (markerAnchors.ContainsKey(node.node_id))
            {
                continue;
            }

            if (ShouldShowMarkerGPS(node))
            {
                CreateMarkerForNodeGPS(node);
                created++;
            }
        }
    }

    bool ShouldShowMarkerGPS(Node node)
    {
        float distance = CalculateDistanceGPS(userLocation, new Vector2(node.latitude, node.longitude));

        if (currentARMode == ARMode.DirectAR)
        {
            return distance <= maxVisibleDistance;
        }

        return distance <= maxVisibleDistance && distance >= minMarkerDistance;
    }

    void CreateMarkerForNodeGPS(Node node)
    {
        if (buildingMarkerPrefab == null) return;

        Infrastructure infra = currentInfrastructures.FirstOrDefault(i => i.infra_id == node.related_infra_id);
        if (infra == null) return;

        Vector3 worldPosition = GPSToWorldPosition(node.latitude, node.longitude);
        worldPosition = GetGroundPosition(worldPosition);

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
    }

    Vector3 GPSToWorldPosition(float latitude, float longitude)
    {
        Vector2 userCoords = userLocation;
        float deltaLat = latitude - userCoords.x;
        float deltaLng = longitude - userCoords.y;

        float meterPerDegree = 111000f;
        float x = deltaLng * meterPerDegree * Mathf.Cos(userCoords.x * Mathf.Deg2Rad);
        float z = deltaLat * meterPerDegree;

        return new Vector3(x, 0, z);
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

    // ═══════════════════════════════════════════════════════════════
    // OFFLINE MODE (X,Y) UPDATE LOGIC
    // ═══════════════════════════════════════════════════════════════
    void UpdateMarkersOffline()
    {
        if (isExitingAR || !referencePointSet) return;

        Vector3 currentARPosition = arCamera.transform.position;

        if (requireGoodTracking && arCamera != null)
        {
            bool isTracking = Vector3.Distance(currentARPosition, lastARCameraPosition) > 0.0001f
                             || Time.frameCount < 100;

            if (!isTracking && lastARCameraPosition != Vector3.zero)
            {
                Debug.LogWarning($"⚠️ AR Tracking poor - Camera not moving");
                lastTrackingState = TrackingState.Limited;
                UpdateTrackingStatusUI();
                return;
            }

            lastTrackingState = TrackingState.Tracking;
        }

        smoothedARPosition = SmoothPosition(currentARPosition);

        Vector3 deltaPosition = smoothedARPosition - referenceWorldPosition;

        float movedX = deltaPosition.x;
        float movedY = deltaPosition.z;

        userXY = new Vector2(referenceNodeXY.x + movedX, referenceNodeXY.y + movedY);

        UpdateTrackingStatusUI();
        UpdateAnchoredMarkerPositionsOffline();
        ReconcileVisibleMarkersOffline();
        UpdateDebugInfo();

        lastARCameraPosition = currentARPosition;
    }

    private Vector3 SmoothPosition(Vector3 newPosition)
    {
        positionHistory.Enqueue(newPosition);
        if (positionHistory.Count > positionHistorySize)
            positionHistory.Dequeue();

        Vector3 sum = Vector3.zero;
        foreach (Vector3 pos in positionHistory)
            sum += pos;

        return sum / positionHistory.Count;
    }

    private void UpdateAnchoredMarkerPositionsOffline()
    {
        foreach (var kvp in markerAnchors)
        {
            MarkerAnchor anchor = kvp.Value;

            if (anchor.markerGameObject == null) continue;

            Vector3 newWorldPos = XYToWorldPosition(anchor.nodeX, anchor.nodeY);

            newWorldPos = GetGroundPosition(newWorldPos);

            anchor.markerGameObject.transform.position = Vector3.Lerp(
                anchor.markerGameObject.transform.position,
                newWorldPos,
                positionSmoothingFactor
            );

            bool isVisible = IsMarkerVisible(newWorldPos);
            anchor.markerGameObject.SetActive(isVisible);
        }
    }

    private bool IsMarkerVisible(Vector3 markerWorldPos)
    {
        Vector3 directionToMarker = markerWorldPos - arCamera.transform.position;
        float distance = directionToMarker.magnitude;

        float dotProduct = Vector3.Dot(arCamera.transform.forward, directionToMarker.normalized);

        return dotProduct > forwardDotThreshold && distance <= maxVisibleDistance;
    }

    private void ReconcileVisibleMarkersOffline()
    {
        if (currentNodes == null || currentNodes.Count == 0) return;

        List<string> nodesToRemove = new List<string>();

        foreach (var kvp in markerAnchors)
        {
            MarkerAnchor anchor = kvp.Value;

            if (!ShouldShowMarkerOffline(anchor.node))
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

        int created = 0;

        var nodesGroupedByInfra = currentNodes
            .Where(n => !string.IsNullOrEmpty(n.related_infra_id))
            .GroupBy(n => n.related_infra_id)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var infraGroup in nodesGroupedByInfra)
        {
            string infraId = infraGroup.Key;
            List<Node> infraNodes = infraGroup.Value;

            bool alreadyHasMarker = markerAnchors.Values.Any(a => a.node.related_infra_id == infraId);

            if (alreadyHasMarker) continue;

            Node closestNode = infraNodes
                .OrderBy(n => CalculateDistanceXY(userXY, new Vector2(n.x_coordinate, n.y_coordinate)))
                .FirstOrDefault();

            if (closestNode != null && ShouldShowMarkerOffline(closestNode))
            {
                CreateMarkerForNodeOffline(closestNode);
                created++;
            }
        }

        if (created > 0)
        {
            Debug.Log($"📊 Created {created} new markers. Total: {markerAnchors.Count}");
        }
    }

    bool ShouldShowMarkerOffline(Node node)
    {
        float distance = CalculateDistanceXY(userXY, new Vector2(node.x_coordinate, node.y_coordinate));

        if (currentARMode == ARMode.DirectAR)
        {
            return distance <= maxVisibleDistance;
        }

        return distance <= maxVisibleDistance && distance >= minMarkerDistance;
    }

    void CreateMarkerForNodeOffline(Node node)
    {
        if (buildingMarkerPrefab == null)
        {
            Debug.LogError("❌ Building marker prefab not assigned!");
            return;
        }

        Infrastructure infra = currentInfrastructures.FirstOrDefault(i => i.infra_id == node.related_infra_id);

        if (infra == null)
        {
            Debug.LogWarning($"❌ No infrastructure for node {node.node_id}");
            return;
        }

        Vector3 worldPosition = XYToWorldPosition(node.x_coordinate, node.y_coordinate);

        worldPosition = GetGroundPosition(worldPosition);

        GameObject marker = Instantiate(buildingMarkerPrefab);
        marker.transform.position = worldPosition;
        marker.transform.localScale = Vector3.one * markerScale;

        UpdateMarkerText(marker, infra);

        MarkerAnchor anchor = new MarkerAnchor
        {
            node = node,
            nodeX = node.x_coordinate,
            nodeY = node.y_coordinate,
            nodeLatitude = node.latitude,
            nodeLongitude = node.longitude,
            markerGameObject = marker
        };

        markerAnchors[node.node_id] = anchor;

        float distance = CalculateDistanceXY(userXY, new Vector2(node.x_coordinate, node.y_coordinate));
        Debug.Log($"✅ Created marker: {infra.name} at X:{node.x_coordinate:F1}, Y:{node.y_coordinate:F1}, Distance: {distance:F1}m");
    }

    Vector3 XYToWorldPosition(float nodeX, float nodeY)
    {
        float deltaX = nodeX - userXY.x;
        float deltaY = nodeY - userXY.y;

        Vector3 worldPos = smoothedARPosition;
        worldPos.x += deltaX;
        worldPos.z += deltaY;

        return worldPos;
    }

    float CalculateDistanceXY(Vector2 point1, Vector2 point2)
    {
        return Vector2.Distance(point1, point2);
    }

    // ═══════════════════════════════════════════════════════════════
    // SHARED UTILITIES
    // ═══════════════════════════════════════════════════════════════

    private Vector3 GetGroundPosition(Vector3 targetWorldPos)
    {
        if (arRaycastManager == null || arCamera == null)
        {
            if (currentLocalizationMode == LocalizationMode.Offline)
            {
                targetWorldPos.y = groundPlaneY + markerHeightOffset;
            }
            else
            {
                targetWorldPos.y = arCamera.transform.position.y + markerHeightOffset;
            }
            return targetWorldPos;
        }

        Vector3 screenPoint = arCamera.WorldToScreenPoint(targetWorldPos);

        if (screenPoint.z < 0)
        {
            targetWorldPos.y = (currentLocalizationMode == LocalizationMode.Offline ? groundPlaneY : arCamera.transform.position.y) + markerHeightOffset;
            return targetWorldPos;
        }

        arRaycastHits.Clear();
        if (arRaycastManager.Raycast(screenPoint, arRaycastHits, TrackableType.PlaneWithinPolygon))
        {
            Vector3 groundPos = arRaycastHits[0].pose.position;
            groundPos.y += markerHeightOffset;
            return groundPos;
        }

        targetWorldPos.y = (currentLocalizationMode == LocalizationMode.Offline ? groundPlaneY : arCamera.transform.position.y) + markerHeightOffset;
        return targetWorldPos;
    }

    void UpdateMarkerText(GameObject marker, Infrastructure infra)
    {
        TextMeshPro textMeshPro = marker.GetComponentInChildren<TextMeshPro>();
        if (textMeshPro != null)
        {
            textMeshPro.text = infra.name;
            textMeshPro.fontSize = 8;
            StartCoroutine(UpdateTextRotation(textMeshPro.transform));
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
        while (textTransform != null && !isExitingAR)
        {
            if (arCamera != null)
            {
                textTransform.LookAt(arCamera.transform);
                textTransform.Rotate(0, 180, 0);
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    void UpdateTrackingStatusUI()
    {
        if (trackingStatusText != null)
        {
            if (currentLocalizationMode == LocalizationMode.GPS)
            {
                Vector2 coords = GPSManager.Instance.GetCoordinates();
                if (coords.magnitude > 0)
                {
                    trackingStatusText.text = $"GPS: {coords.x:F5}, {coords.y:F5}";
                    trackingStatusText.color = Color.green;
                }
                else
                {
                    trackingStatusText.text = "GPS: No Signal";
                    trackingStatusText.color = Color.red;
                }
            }
            else
            {
                string trackingStatus = lastTrackingState == TrackingState.Tracking ? "Good" : lastTrackingState.ToString();
                Color statusColor = lastTrackingState == TrackingState.Tracking ? Color.green : Color.yellow;

                trackingStatusText.text = $"AR Tracking: {trackingStatus}\nPos: X:{userXY.x:F2}, Y:{userXY.y:F2}";
                trackingStatusText.color = statusColor;
            }
        }
    }

    void UpdateDebugInfo()
    {
        if (debugText != null)
        {
            string modeText = currentARMode == ARMode.DirectAR ? "🗺️ Direct AR" : "🧭 Navigation";
            string localizationText = currentLocalizationMode == LocalizationMode.GPS ? "GPS" : "Offline (X,Y)";

            if (currentLocalizationMode == LocalizationMode.GPS)
            {
                debugText.text = $"{modeText} + {localizationText}\n" +
                                 $"User: {userLocation.x:F5}, {userLocation.y:F5}\n" +
                                 $"Active Markers: {markerAnchors.Count}";
            }
            else
            {
                string trackingState = lastTrackingState.ToString();
                debugText.text = $"{modeText} + {localizationText} (AR: {trackingState})\n" +
                                 $"User XY: {userXY.x:F2}, {userXY.y:F2}\n" +
                                 $"Ground Y: {groundPlaneY:F2}\n" +
                                 $"Active Markers: {markerAnchors.Count}";
            }
        }
    }

    void UpdateLoadingUI(string message)
    {
        if (loadingText != null)
        {
            loadingText.text = message;
            loadingText.gameObject.SetActive(true);
        }
        Debug.Log($"[Loading] {message}");
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
        Debug.Log("🗑️ All markers cleared");
    }

    void OnDestroy()
    {
        isExitingAR = true;
        CancelInvoke();
        ClearMarkers();
        StopAllCoroutines();
    }
    public Vector2 GetUserXY()
    {
        if (currentLocalizationMode == LocalizationMode.GPS)
        {
            return userLocation; // Returns GPS lat/lng
        }
        else
        {
            return userXY; // Returns Offline X,Y
        }
    }

    private class MarkerAnchor
    {
        public Node node;
        public float nodeX;
        public float nodeY;
        public float nodeLatitude;
        public float nodeLongitude;
        public GameObject markerGameObject;
    }
}