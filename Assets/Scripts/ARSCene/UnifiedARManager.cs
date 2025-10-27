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

    [Header("GPS Settings")]
    public int gpsHistorySize = 5;
    public float positionUpdateThreshold = 1f;
    public float positionSmoothingFactor = 0.3f;
    private Queue<Vector2> gpsLocationHistory = new Queue<Vector2>();
    private Vector2 lastStableGPSLocation;
    private bool gpsInitialized = false;

    [Header("Indoor (X,Y) Settings")]
    public Vector2 referenceNodeXY = Vector2.zero;
    private Vector3 referenceWorldPosition;
    private bool isIndoorMode = false;
    private float groundPlaneY = 0f;
    public int positionHistorySize = 5;
    private Queue<Vector3> positionHistory = new Queue<Vector3>();
    private string currentIndoorInfraId = "";

    [Header("Data")]
    private List<Node> currentNodes = new List<Node>();
    private List<Infrastructure> currentInfrastructures = new List<Infrastructure>();
    private Dictionary<string, MarkerAnchor> markerAnchors = new Dictionary<string, MarkerAnchor>();

    [Header("Position Tracking")]
    private Vector2 userLocation;
    private Vector2 userXY;
    private Vector3 lastARCameraPosition;
    private Vector3 smoothedARPosition;

    [Header("Mode Configuration")]
    private ARMode currentARMode = ARMode.DirectAR;
    private bool isTrackingStarted = false;

    private enum ARMode { DirectAR, Navigation }

    private List<ARRaycastHit> arRaycastHits = new List<ARRaycastHit>();

    void Start()
    {
        InitializeComponents();
        DetermineARMode();
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

    private void DetermineARMode()
    {
        string arModeString = PlayerPrefs.GetString("ARMode", "DirectAR");
        currentARMode = arModeString == "Navigation" ? ARMode.Navigation : ARMode.DirectAR;
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
        }
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
        yield return new WaitForSeconds(1f);

        UpdateLoadingUI("Waiting for GPS Manager...");
        while (GPSManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }
        UpdateLoadingUI("GPS Manager found, starting location services...");
        yield return new WaitForSeconds(1f);

        UpdateLoadingUI("Detecting ground plane...");
        yield return StartCoroutine(WaitForGroundPlane());

        UpdateLoadingUI("Loading map data...");
        string currentMapId = GetCurrentMapId();
        yield return StartCoroutine(LoadCurrentMapData(currentMapId));

        UpdateLoadingUI("Starting tracking...");
        StartMarkerTracking();
        HideLoadingUI();
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
                        yield break;
                    }
                }
            }

            elapsed += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        groundPlaneY = arCamera.transform.position.y - 1.6f;
    }

    private void StartMarkerTracking()
    {
        if (isTrackingStarted)
        {
            return;
        }

        isTrackingStarted = true;
        InvokeRepeating(nameof(UpdateMarkers), 2f, 0.2f);
    }

    private void SetIndoorReference(Vector2 nodeXY, string infraId)
    {
        referenceNodeXY = nodeXY;
        referenceWorldPosition = arCamera.transform.position;
        lastARCameraPosition = referenceWorldPosition;
        smoothedARPosition = referenceWorldPosition;

        positionHistory.Clear();

        isIndoorMode = true;
        currentIndoorInfraId = infraId;
        userXY = referenceNodeXY;

        Debug.Log($"Indoor mode activated - Infra: {infraId}, Reference XY: {referenceNodeXY}");
    }

    public void OnQRCodeScanned(Node scannedNode)
    {
        bool isIndoor = scannedNode.type == "indoorinfra";

        if (isIndoor)
        {
            // Switch to indoor mode with X,Y coordinates
            if (scannedNode.indoor != null)
            {
                Vector2 indoorXY = new Vector2(scannedNode.indoor.x, scannedNode.indoor.y);
                SetIndoorReference(indoorXY, scannedNode.related_infra_id);

                Debug.Log($"Switched to INDOOR mode - {scannedNode.name} at X:{indoorXY.x}, Y:{indoorXY.y}");
            }
        }
        else
        {
            // Update GPS position for outdoor
            userLocation = new Vector2(scannedNode.latitude, scannedNode.longitude);
            lastStableGPSLocation = userLocation;
            gpsLocationHistory.Clear();
            gpsLocationHistory.Enqueue(userLocation);
            gpsInitialized = true;

            // Exit indoor mode if we were in it
            if (isIndoorMode)
            {
                isIndoorMode = false;
                currentIndoorInfraId = "";
                Debug.Log($"Switched back to OUTDOOR GPS mode - {scannedNode.name}");
            }
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
                    // Load both infrastructure and indoorinfra types
                    currentNodes = nodes.Where(n =>
                        (n.type == "infrastructure" || n.type == "indoorinfra") && n.is_active
                    ).ToList();
                    loadSuccess = true;
                }
                catch (System.Exception e)
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
                catch (System.Exception e)
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

        if (isIndoorMode)
        {
            UpdateMarkersIndoor();
        }
        else
        {
            UpdateMarkersGPS();
        }
    }

    void UpdateMarkersGPS()
    {
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

        foreach (Node node in currentNodes)
        {
            // Skip indoor nodes when in GPS mode
            if (node.type == "indoorinfra")
                continue;

            if (markerAnchors.ContainsKey(node.node_id))
            {
                continue;
            }

            if (ShouldShowMarkerGPS(node))
            {
                CreateMarkerForNodeGPS(node);
            }
        }
    }

    bool ShouldShowMarkerGPS(Node node)
    {
        if (node.type == "indoorinfra")
            return false;

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

    void UpdateMarkersIndoor()
    {
        if (!isIndoorMode) return;

        Vector3 currentARPosition = arCamera.transform.position;

        if (requireGoodTracking && arCamera != null)
        {
            bool isTracking = Vector3.Distance(currentARPosition, lastARCameraPosition) > 0.0001f
                             || Time.frameCount < 100;

            if (!isTracking && lastARCameraPosition != Vector3.zero)
            {
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
        UpdateAnchoredMarkerPositionsIndoor();
        ReconcileVisibleMarkersIndoor();
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

    private void UpdateAnchoredMarkerPositionsIndoor()
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

    private void ReconcileVisibleMarkersIndoor()
    {
        if (currentNodes == null || currentNodes.Count == 0) return;

        List<string> nodesToRemove = new List<string>();

        foreach (var kvp in markerAnchors)
        {
            MarkerAnchor anchor = kvp.Value;

            if (!ShouldShowMarkerIndoor(anchor.node))
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

        // Group nodes by infrastructure for indoor
        var nodesInCurrentBuilding = currentNodes
            .Where(n => n.type == "indoorinfra" &&
                       n.related_infra_id == currentIndoorInfraId &&
                       n.indoor != null)
            .GroupBy(n => n.related_infra_id)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var infraGroup in nodesInCurrentBuilding)
        {
            string infraId = infraGroup.Key;
            List<Node> infraNodes = infraGroup.Value;

            bool alreadyHasMarker = markerAnchors.Values.Any(a => a.node.related_infra_id == infraId);

            if (alreadyHasMarker) continue;

            Node closestNode = infraNodes
                .OrderBy(n => CalculateDistanceXY(userXY, new Vector2(n.indoor.x, n.indoor.y)))
                .FirstOrDefault();

            if (closestNode != null && ShouldShowMarkerIndoor(closestNode))
            {
                CreateMarkerForNodeIndoor(closestNode);
            }
        }
    }

    bool ShouldShowMarkerIndoor(Node node)
    {
        if (node.type != "indoorinfra" || node.indoor == null)
            return false;

        if (node.related_infra_id != currentIndoorInfraId)
            return false;

        float distance = CalculateDistanceXY(userXY, new Vector2(node.indoor.x, node.indoor.y));

        if (currentARMode == ARMode.DirectAR)
        {
            return distance <= maxVisibleDistance;
        }

        return distance <= maxVisibleDistance && distance >= minMarkerDistance;
    }

    void CreateMarkerForNodeIndoor(Node node)
    {
        if (buildingMarkerPrefab == null || node.indoor == null)
        {
            return;
        }

        Infrastructure infra = currentInfrastructures.FirstOrDefault(i => i.infra_id == node.related_infra_id);

        if (infra == null)
        {
            return;
        }

        Vector3 worldPosition = XYToWorldPosition(node.indoor.x, node.indoor.y);
        worldPosition = GetGroundPosition(worldPosition);

        GameObject marker = Instantiate(buildingMarkerPrefab);
        marker.transform.position = worldPosition;
        marker.transform.localScale = Vector3.one * markerScale;

        UpdateMarkerText(marker, infra);

        MarkerAnchor anchor = new MarkerAnchor
        {
            node = node,
            nodeX = node.indoor.x,
            nodeY = node.indoor.y,
            nodeLatitude = node.latitude,
            nodeLongitude = node.longitude,
            markerGameObject = marker
        };

        markerAnchors[node.node_id] = anchor;
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

    private Vector3 GetGroundPosition(Vector3 targetWorldPos)
    {
        if (arRaycastManager == null || arCamera == null)
        {
            targetWorldPos.y = groundPlaneY + markerHeightOffset;
            return targetWorldPos;
        }

        Vector3 screenPoint = arCamera.WorldToScreenPoint(targetWorldPos);

        if (screenPoint.z < 0)
        {
            targetWorldPos.y = groundPlaneY + markerHeightOffset;
            return targetWorldPos;
        }

        arRaycastHits.Clear();
        if (arRaycastManager.Raycast(screenPoint, arRaycastHits, TrackableType.PlaneWithinPolygon))
        {
            Vector3 groundPos = arRaycastHits[0].pose.position;
            groundPos.y += markerHeightOffset;
            return groundPos;
        }

        targetWorldPos.y = groundPlaneY + markerHeightOffset;
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
            if (isIndoorMode)
            {
                string trackingStatus = lastTrackingState == TrackingState.Tracking ? "Good" : lastTrackingState.ToString();
                Color statusColor = lastTrackingState == TrackingState.Tracking ? Color.green : Color.yellow;

                trackingStatusText.text = $"🏢 Indoor Mode\nAR Tracking: {trackingStatus}\nPos: X:{userXY.x:F2}m, Y:{userXY.y:F2}m";
                trackingStatusText.color = statusColor;
            }
            else
            {
                Vector2 coords = GPSManager.Instance.GetCoordinates();
                if (coords.magnitude > 0)
                {
                    trackingStatusText.text = $"🌍 Outdoor (GPS)\n{coords.x:F5}, {coords.y:F5}";
                    trackingStatusText.color = Color.green;
                }
                else
                {
                    trackingStatusText.text = "🌍 Outdoor\nGPS: No Signal";
                    trackingStatusText.color = Color.red;
                }
            }
        }
    }

    void UpdateDebugInfo()
    {
        if (debugText != null)
        {
            string modeText = currentARMode == ARMode.DirectAR ? "🗺️ Direct AR" : "🧭 Navigation";

            if (isIndoorMode)
            {
                string trackingState = lastTrackingState.ToString();
                debugText.text = $"{modeText} + 🏢 Indoor (Building: {currentIndoorInfraId})\n" +
                                 $"AR Tracking: {trackingState}\n" +
                                 $"User XY: {userXY.x:F2}m, {userXY.y:F2}m\n" +
                                 $"Ground Y: {groundPlaneY:F2}m\n" +
                                 $"Active Markers: {markerAnchors.Count}";
            }
            else
            {
                debugText.text = $"{modeText} + 🌍 Outdoor (GPS)\n" +
                                 $"User: {userLocation.x:F5}, {userLocation.y:F5}\n" +
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
    }

    public Vector2 GetUserXY()
    {
        if (isIndoorMode)
        {
            return userXY;
        }
        else
        {
            return userLocation;
        }
    }

    public bool IsIndoorMode()
    {
        return isIndoorMode;
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