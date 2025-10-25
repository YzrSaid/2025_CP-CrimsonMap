using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;

/// <summary>
/// AR-based infrastructure marker manager using AR Camera tracking instead of GPS
/// Uses X,Y coordinate system relative to a reference point (QR scan location)
/// FIXED: Ground plane detection for proper marker placement
/// </summary>
public class ARTrackingManager : MonoBehaviour
{
    [Header("AR Exit Settings")]
    [SerializeField] private Button backToMainButton;
    [SerializeField] private string mainSceneName = "MainAppScene";
    private bool isExitingAR = false;

    [Header("AR Settings")]
    public GameObject buildingMarkerPrefab;
    public XROrigin xrOrigin;
    public ARRaycastManager arRaycastManager; 
    public ARPlaneManager arPlaneManager;
    public float maxVisibleDistance = 500f;
    public float markerScale = 0.3f;
    public float minMarkerDistance = 2f;
    public float markerHeightOffset = 0.1f; 

    [Header("Occlusion & Visibility Settings")]
    [Tooltip("Only show markers within this angle from camera forward")]
    public float fieldOfViewAngle = 90f;
    [Tooltip("Minimum dot product to consider marker 'in front'")]
    public float forwardDotThreshold = 0.3f;

    [Header("AR Tracking Quality")]
    public ARSession arSession;
    public ARCameraManager arCameraManager;
    private TrackingState lastTrackingState = TrackingState.None;
    [Tooltip("Only update markers when tracking is good")]
    public bool requireGoodTracking = true;

    [Header("UI References")]
    public TextMeshProUGUI trackingStatusText;
    public TextMeshProUGUI debugText;
    public TextMeshProUGUI loadingText;

    [Header("Reference Point Settings (QR Scan Location)")]
    [Tooltip("For testing - the node where user starts (0,0 reference point)")]
    public Vector2 referenceNodeXY = Vector2.zero;
    private Vector3 referenceWorldPosition;
    private bool referencePointSet = false;
    private float groundPlaneY = 0f; // Store detected ground height

    [Header("Tracking Smoothing")]
    public float positionSmoothingFactor = 0.3f;
    public int positionHistorySize = 5;
    private Queue<Vector3> positionHistory = new Queue<Vector3>();

    [Header("Data")]
    private List<Node> currentNodes = new List<Node>();
    private List<Infrastructure> currentInfrastructures = new List<Infrastructure>();
    private Dictionary<string, MarkerAnchor> markerAnchors = new Dictionary<string, MarkerAnchor>();

    [Header("User Position Tracking")]
    private Vector2 userXY;
    private Vector3 lastARCameraPosition;
    private Vector3 smoothedARPosition;
    private Camera arCamera;

    [Header("Feature Flags")]
    private ARFeatureMode currentFeatureMode = ARFeatureMode.DirectAR;
    private enum ARFeatureMode { DirectAR, ARNavigation }

    // For AR Raycasting
    private List<ARRaycastHit> arRaycastHits = new List<ARRaycastHit>();

    void Start()
    {
        if (arCamera == null)
            arCamera = Camera.main;
        if (xrOrigin == null)
            xrOrigin = FindObjectOfType<XROrigin>();
        if (arSession == null)
            arSession = FindObjectOfType<ARSession>();
        if (arCameraManager == null)
            arCameraManager = FindObjectOfType<ARCameraManager>();
        if (arRaycastManager == null)
            arRaycastManager = FindObjectOfType<ARRaycastManager>();
        if (arPlaneManager == null)
            arPlaneManager = FindObjectOfType<ARPlaneManager>();

        UpdateLoadingUI("Initializing AR Tracking...");

        SetupBackButton();
        DetermineARFeatureMode();

        StartCoroutine(InitializeARScene());
    }

    private void DetermineARFeatureMode()
    {
        string arMode = PlayerPrefs.GetString("ARMode", "DirectAR");

        if (arMode == "Navigation")
        {
            currentFeatureMode = ARFeatureMode.ARNavigation;
            Debug.Log("[ARTrackingManager] 🧭 AR NAVIGATION MODE");
        }
        else
        {
            currentFeatureMode = ARFeatureMode.DirectAR;
            Debug.Log("[ARTrackingManager] 🗺️ DIRECT AR MODE - Using AR Camera tracking");
        }
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

        UpdateLoadingUI("Detecting ground plane...");
        yield return StartCoroutine(WaitForGroundPlane());

        UpdateLoadingUI("Setting reference point...");
        SetReferencePoint(referenceNodeXY);

        UpdateLoadingUI("Loading map data...");
        string currentMapId = GetCurrentMapId();
        yield return StartCoroutine(LoadCurrentMapData(currentMapId));

        UpdateLoadingUI("Starting AR tracking...");
        InvokeRepeating(nameof(UpdateUserPositionAndMarkers), 0.5f, 0.15f);

        HideLoadingUI();

        Debug.Log($"✅ AR Tracking initialized - Nodes: {currentNodes.Count}, Infra: {currentInfrastructures.Count}");
    }

    /// <summary>
    /// Wait for AR to detect at least one ground plane
    /// </summary>
    private IEnumerator WaitForGroundPlane()
    {
        float timeout = 5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (arPlaneManager != null && arPlaneManager.trackables.count > 0)
            {
                // Found a plane! Get its Y position
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

        // Fallback: Use camera position minus typical user height
        groundPlaneY = arCamera.transform.position.y - 1.6f;
        Debug.LogWarning($"⚠️ No ground plane detected, using estimated ground: Y={groundPlaneY:F2}");
    }

    private void SetReferencePoint(Vector2 nodeXY)
    {
        referenceNodeXY = nodeXY;
        referenceWorldPosition = arCamera.transform.position;
        lastARCameraPosition = referenceWorldPosition;
        smoothedARPosition = referenceWorldPosition;
        referencePointSet = true;

        Debug.Log($"🎯 Reference point set at X:{nodeXY.x:F2}, Y:{nodeXY.y:F2}");
        Debug.Log($"🎯 Unity world position: {referenceWorldPosition}");
    }

    public void OnQRCodeScanned(Node scannedNode)
    {
        SetReferencePoint(new Vector2(scannedNode.x_coordinate, scannedNode.y_coordinate));
        Debug.Log($"📷 QR Scanned: {scannedNode.name} at X:{scannedNode.x_coordinate:F2}, Y:{scannedNode.y_coordinate:F2}");
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

        Debug.LogWarning("[ARTrackingManager] Using default MAP-01");
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

    void UpdateUserPositionAndMarkers()
    {
        if (isExitingAR || !referencePointSet) return;

        Vector3 currentARPosition = arCamera.transform.position;

        // CHECK AR TRACKING QUALITY
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
        UpdateAnchoredMarkerPositions();
        ReconcileVisibleMarkers();
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

    private void UpdateAnchoredMarkerPositions()
    {
        foreach (var kvp in markerAnchors)
        {
            MarkerAnchor anchor = kvp.Value;

            if (anchor.markerGameObject == null) continue;

            Vector3 newWorldPos = XYToWorldPosition(anchor.nodeX, anchor.nodeY);
            
            // FIXED: Use detected ground plane
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

    private void ReconcileVisibleMarkers()
    {
        if (currentNodes == null || currentNodes.Count == 0) return;

        List<string> nodesToRemove = new List<string>();

        foreach (var kvp in markerAnchors)
        {
            MarkerAnchor anchor = kvp.Value;

            if (!ShouldShowMarker(anchor.node))
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

            if (closestNode != null && ShouldShowMarker(closestNode))
            {
                CreateMarkerForNode(closestNode);
                created++;
            }
        }

        if (created > 0)
        {
            Debug.Log($"📊 Created {created} new markers. Total: {markerAnchors.Count}");
        }
    }

    bool ShouldShowMarker(Node node)
    {
        float distance = CalculateDistanceXY(userXY, new Vector2(node.x_coordinate, node.y_coordinate));

        if (currentFeatureMode == ARFeatureMode.DirectAR)
        {
            return distance <= maxVisibleDistance;
        }

        return distance <= maxVisibleDistance && distance >= minMarkerDistance;
    }

    void CreateMarkerForNode(Node node)
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
            markerGameObject = marker
        };

        markerAnchors[node.node_id] = anchor;

        float distance = CalculateDistanceXY(userXY, new Vector2(node.x_coordinate, node.y_coordinate));
        Debug.Log($"✅ Created marker: {infra.name} at X:{node.x_coordinate:F1}, Y:{node.y_coordinate:F1}, Distance: {distance:F1}m");
    }
    private Vector3 GetGroundPosition(Vector3 targetWorldPos)
    {
        if (arRaycastManager == null || arCamera == null)
        {
            // Fallback: Use detected ground plane or camera-relative height
            targetWorldPos.y = groundPlaneY + markerHeightOffset;
            return targetWorldPos;
        }

        // Convert world position to screen space for raycasting
        Vector3 screenPoint = arCamera.WorldToScreenPoint(targetWorldPos);
        
        // Make sure it's in front of camera
        if (screenPoint.z < 0)
        {
            targetWorldPos.y = groundPlaneY + markerHeightOffset;
            return targetWorldPos;
        }

        arRaycastHits.Clear();
        if (arRaycastManager.Raycast(screenPoint, arRaycastHits, TrackableType.PlaneWithinPolygon))
        {
            // Found a plane! Use its position
            Vector3 groundPos = arRaycastHits[0].pose.position;
            groundPos.y += markerHeightOffset;
            return groundPos;
        }

        // Fallback: Use stored ground plane Y
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

    void UpdateTrackingStatusUI()
    {
        if (trackingStatusText != null)
        {
            string trackingStatus = lastTrackingState == TrackingState.Tracking ? "Good" : lastTrackingState.ToString();
            Color statusColor = lastTrackingState == TrackingState.Tracking ? Color.green : Color.yellow;

            trackingStatusText.text = $"AR Tracking: {trackingStatus}\nPos: X:{userXY.x:F2}, Y:{userXY.y:F2}";
            trackingStatusText.color = statusColor;
        }
    }

    void UpdateDebugInfo()
    {
        if (debugText != null)
        {
            string modeText = currentFeatureMode == ARFeatureMode.DirectAR ? "🗺️ Direct AR" : "🧭 Navigation";
            string trackingState = lastTrackingState.ToString();

            debugText.text = $"{modeText} (AR Tracking: {trackingState})\n" +
                             $"User XY: {userXY.x:F2}, {userXY.y:F2}\n" +
                             $"Ground Y: {groundPlaneY:F2}\n" +
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

    private class MarkerAnchor
    {
        public Node node;
        public float nodeX;
        public float nodeY;
        public GameObject markerGameObject;
    }
}