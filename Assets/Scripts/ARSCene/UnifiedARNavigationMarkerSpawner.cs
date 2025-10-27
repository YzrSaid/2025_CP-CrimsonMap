using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class UnifiedARNavigationMarkerSpawner : MonoBehaviour
{
    [Header("AR Marker Prefabs")]
    public GameObject nodeMarkerPrefab;
    public GameObject destinationMarkerPrefab;

    [Header("AR Components")]
    public Camera arCamera;
    public ARRaycastManager arRaycastManager;
    public ARPlaneManager arPlaneManager;
    public UnifiedARManager unifiedARManager;

    [Header("Compass Arrow")]
    public CompassNavigationArrow compassArrow;

    [Header("Marker Settings")]
    public float markerScale = 1.5f;
    public float markerHeightOffset = 0.05f;
    public float nodeMarkerDistance = 10f; // Show markers when close to waypoints

    [Header("Colors")]
    public Color navigationNodeColor = new Color(0.74f, 0.06f, 0.18f, 1f);
    public Color destinationColor = new Color(0.2f, 0.8f, 0.2f, 1f);

    [Header("Settings")]
    public bool enableDebugLogs = true;
    public bool showWaypointMarkers = true; // Toggle to show/hide 3D markers

    private List<Node> pathNodes = new List<Node>();
    private Dictionary<string, GameObject> spawnedNodeMarkers = new Dictionary<string, GameObject>();

    private Vector2 userLocation;
    private DirectionDisplayManager directionManager;
    private bool isARNavigationMode = false;

    private enum LocalizationMode { GPS, Offline }
    private LocalizationMode currentLocalizationMode = LocalizationMode.GPS;

    private Vector2 userXY;
    private float groundPlaneY = 0f;
    private bool markersInitialized = false;

    private List<ARRaycastHit> arRaycastHits = new List<ARRaycastHit>();

    void Start()
    {
        if (arCamera == null)
            arCamera = Camera.main;

        if (arRaycastManager == null)
            arRaycastManager = FindObjectOfType<ARRaycastManager>();

        if (arPlaneManager == null)
            arPlaneManager = FindObjectOfType<ARPlaneManager>();

        if (unifiedARManager == null)
            unifiedARManager = FindObjectOfType<UnifiedARManager>();

        directionManager = GetComponent<DirectionDisplayManager>();
        if (directionManager == null)
            directionManager = FindObjectOfType<DirectionDisplayManager>();

        // Find compass arrow if not assigned
        if (compassArrow == null)
            compassArrow = FindObjectOfType<CompassNavigationArrow>();

        DetermineNavigationMode();

        if (isARNavigationMode)
        {
            LoadPathNodesFromPlayerPrefs();
            
            // For offline mode, estimate ground plane height
            if (currentLocalizationMode == LocalizationMode.Offline)
            {
                groundPlaneY = arCamera.transform.position.y - 1.6f;
                if (enableDebugLogs)
                    Debug.Log($"[ARMarkerSpawner] Estimated ground Y: {groundPlaneY:F2}");
            }

            StartCoroutine(InitializeNavigationMarkers());
        }
    }

    private void DetermineNavigationMode()
    {
        string arMode = PlayerPrefs.GetString("ARMode", "DirectAR");
        isARNavigationMode = (arMode == "Navigation");

        string localizationModeString = PlayerPrefs.GetString("LocalizationMode", "GPS");
        currentLocalizationMode = localizationModeString == "Offline" 
            ? LocalizationMode.Offline 
            : LocalizationMode.GPS;

        if (enableDebugLogs)
            Debug.Log($"[ARMarkerSpawner] Mode: {(isARNavigationMode ? "Navigation" : "DirectAR")} + {currentLocalizationMode}");
    }

    private void LoadPathNodesFromPlayerPrefs()
    {
        int pathNodeCount = PlayerPrefs.GetInt("ARNavigation_PathNodeCount", 0);

        if (pathNodeCount == 0)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[ARMarkerSpawner] No path nodes found");
            return;
        }

        pathNodes.Clear();

        List<string> pathNodeIds = new List<string>();
        for (int i = 0; i < pathNodeCount; i++)
        {
            string nodeId = PlayerPrefs.GetString($"ARNavigation_PathNode_{i}", "");
            if (!string.IsNullOrEmpty(nodeId))
                pathNodeIds.Add(nodeId);
        }

        if (enableDebugLogs)
            Debug.Log($"[ARMarkerSpawner] Loading {pathNodeIds.Count} path nodes...");

        StartCoroutine(LoadNodesData(pathNodeIds));
    }

    private IEnumerator LoadNodesData(List<string> nodeIds)
    {
        string mapId = PlayerPrefs.GetString("ARScene_MapId", "MAP-01");
        string fileName = $"nodes_{mapId}.json";

        bool loadComplete = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonContent) =>
            {
                try
                {
                    Node[] allNodes = JsonHelper.FromJson<Node>(jsonContent);

                    foreach (string nodeId in nodeIds)
                    {
                        Node node = System.Array.Find(allNodes, n => n.node_id == nodeId);
                        if (node != null)
                            pathNodes.Add(node);
                    }

                    if (enableDebugLogs)
                        Debug.Log($"[ARMarkerSpawner] ✅ Loaded {pathNodes.Count} path nodes");

                    loadComplete = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ARMarkerSpawner] ❌ Error: {e.Message}");
                    loadComplete = true;
                }
            },
            (error) =>
            {
                Debug.LogError($"[ARMarkerSpawner] ❌ Failed to load: {error}");
                loadComplete = true;
            }
        ));

        yield return new WaitUntil(() => loadComplete);
    }

    private IEnumerator InitializeNavigationMarkers()
    {
        yield return new WaitForSeconds(0.5f);

        if (pathNodes.Count < 2)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[ARMarkerSpawner] Not enough path nodes (min 2)");
            yield break;
        }

        markersInitialized = true;

        // Start updating compass arrow and optional waypoint markers
        InvokeRepeating(nameof(UpdateNavigationSystem), 0.5f, 0.2f);

        if (enableDebugLogs)
            Debug.Log("[ARMarkerSpawner] ✅ Navigation system initialized");
    }

    private void UpdateNavigationSystem()
    {
        if (!isARNavigationMode || pathNodes.Count == 0 || !markersInitialized)
            return;

        // Get user location
        if (currentLocalizationMode == LocalizationMode.GPS)
        {
            if (GPSManager.Instance == null)
            {
                if (enableDebugLogs && Time.frameCount % 120 == 0)
                    Debug.LogWarning("[ARMarkerSpawner] GPS Manager not found");
                return;
            }

            userLocation = GPSManager.Instance.GetSmoothedCoordinates();

            if (userLocation.magnitude < 0.0001f)
            {
                if (enableDebugLogs && Time.frameCount % 120 == 0)
                    Debug.LogWarning("[ARMarkerSpawner] Waiting for GPS signal...");
                return;
            }
        }
        else
        {
            if (unifiedARManager == null)
            {
                if (enableDebugLogs && Time.frameCount % 120 == 0)
                    Debug.LogWarning("[ARMarkerSpawner] UnifiedARManager not found");
                return;
            }

            userXY = unifiedARManager.GetUserXY();
            userLocation = userXY;

            if (userXY.magnitude < 0.0001f)
            {
                if (enableDebugLogs && Time.frameCount % 120 == 0)
                    Debug.LogWarning("[ARMarkerSpawner] Waiting for user XY position...");
                return;
            }
        }

        // Update compass arrow to point to current target
        UpdateCompassArrow();

        // Update optional 3D waypoint markers
        if (showWaypointMarkers)
        {
            UpdateNodeMarkers();
        }

        if (enableDebugLogs && Time.frameCount % 120 == 0)
        {
            Debug.Log($"[ARMarkerSpawner] User location: {userLocation}");
        }
    }

    private void UpdateCompassArrow()
    {
        if (compassArrow == null || directionManager == null)
            return;

        // Get current target node from direction manager
        NavigationDirection currentDir = directionManager.GetCurrentDirection();
        if (currentDir == null || currentDir.destinationNode == null)
            return;

        Node targetNode = currentDir.destinationNode;

        // Update compass arrow to point to target
        compassArrow.SetTargetNode(targetNode);
        compassArrow.SetActive(true);

        if (enableDebugLogs && Time.frameCount % 120 == 0)
        {
            Debug.Log($"[ARMarkerSpawner] Compass pointing to: {targetNode.name}");
        }
    }

    private void UpdateNodeMarkers()
    {
        if (nodeMarkerPrefab == null && destinationMarkerPrefab == null)
            return;

        for (int i = 0; i < pathNodes.Count; i++)
        {
            Node node = pathNodes[i];

            float distance = 0f;

            if (currentLocalizationMode == LocalizationMode.GPS)
            {
                distance = CalculateDistanceGPS(userLocation, new Vector2(node.latitude, node.longitude));
            }
            else
            {
                distance = CalculateDistanceXY(userLocation, new Vector2(node.x_coordinate, node.y_coordinate));
            }

            bool shouldShow = distance <= nodeMarkerDistance;
            bool isDestination = (i == pathNodes.Count - 1);
            string markerId = $"node_{node.node_id}";

            if (shouldShow && !spawnedNodeMarkers.ContainsKey(markerId))
            {
                GameObject prefab = isDestination ? destinationMarkerPrefab : nodeMarkerPrefab;
                if (prefab == null)
                    prefab = nodeMarkerPrefab;

                if (prefab != null)
                {
                    Vector3 worldPos;

                    if (currentLocalizationMode == LocalizationMode.GPS)
                    {
                        worldPos = GPSToWorldPosition(node.latitude, node.longitude);
                    }
                    else
                    {
                        worldPos = XYToWorldPosition(node.x_coordinate, node.y_coordinate);
                    }

                    worldPos = GetGroundPosition(worldPos);

                    GameObject marker = Instantiate(prefab, worldPos, Quaternion.identity);
                    marker.transform.localScale = Vector3.one * markerScale;

                    Renderer[] renderers = marker.GetComponentsInChildren<Renderer>();
                    Color targetColor = isDestination ? destinationColor : navigationNodeColor;
                    foreach (var rend in renderers)
                    {
                        if (rend.material != null)
                            rend.material.color = targetColor;
                    }

                    spawnedNodeMarkers[markerId] = marker;
                }
            }
            else if (!shouldShow && spawnedNodeMarkers.ContainsKey(markerId))
            {
                Destroy(spawnedNodeMarkers[markerId]);
                spawnedNodeMarkers.Remove(markerId);
            }
        }

        // Make markers face camera
        foreach (var marker in spawnedNodeMarkers.Values)
        {
            if (marker != null && arCamera != null)
            {
                marker.transform.LookAt(arCamera.transform);
                marker.transform.Rotate(0, 180, 0);
            }
        }
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
            foreach (var hit in arRaycastHits)
            {
                ARPlane plane = arPlaneManager.GetPlane(hit.trackableId);
                if (plane != null && plane.alignment == PlaneAlignment.HorizontalUp)
                {
                    Vector3 groundPos = hit.pose.position;
                    groundPos.y += markerHeightOffset;
                    return groundPos;
                }
            }
        }

        targetWorldPos.y = groundPlaneY + markerHeightOffset;
        return targetWorldPos;
    }

    private void ClearAllMarkers()
    {
        foreach (var marker in spawnedNodeMarkers.Values)
        {
            if (marker != null)
                Destroy(marker);
        }
        spawnedNodeMarkers.Clear();
    }

    private Vector3 GPSToWorldPosition(float latitude, float longitude)
    {
        Vector2 userCoords = userLocation;
        float deltaLat = latitude - userCoords.x;
        float deltaLng = longitude - userCoords.y;

        float meterPerDegree = 111000f;
        float x = deltaLng * meterPerDegree * Mathf.Cos(userCoords.x * Mathf.Deg2Rad);
        float z = deltaLat * meterPerDegree;

        return new Vector3(x, 0, z);
    }

    private float CalculateDistanceGPS(Vector2 coord1, Vector2 coord2)
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

    private Vector3 XYToWorldPosition(float nodeX, float nodeY)
    {
        float deltaX = nodeX - userXY.x;
        float deltaY = nodeY - userXY.y;

        Vector3 worldPos = arCamera.transform.position;
        worldPos.x += deltaX;
        worldPos.z += deltaY;

        return worldPos;
    }

    private float CalculateDistanceXY(Vector2 point1, Vector2 point2)
    {
        return Vector2.Distance(point1, point2);
    }

    public void UpdateUserXY(Vector2 newUserXY)
    {
        userXY = newUserXY;
    }

    public Vector2 GetUserLocation()
    {
        return userLocation;
    }

    /// <summary>
    /// Toggle waypoint markers on/off (compass arrow remains)
    /// </summary>
    public void ToggleWaypointMarkers(bool show)
    {
        showWaypointMarkers = show;
        
        if (!show)
        {
            ClearAllMarkers();
        }
    }

    void OnDestroy()
    {
        CancelInvoke();
        ClearAllMarkers();

        if (compassArrow != null)
        {
            compassArrow.SetActive(false);
        }
    }
}