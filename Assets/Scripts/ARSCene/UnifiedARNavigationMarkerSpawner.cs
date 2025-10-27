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
    public float nodeMarkerDistance = 10f;

    [Header("Colors")]
    public Color navigationNodeColor = new Color(0.74f, 0.06f, 0.18f, 1f);
    public Color destinationColor = new Color(0.2f, 0.8f, 0.2f, 1f);

    [Header("Settings")]
    public bool enableDebugLogs = true;
    public bool showWaypointMarkers = true;

    private List<Node> pathNodes = new List<Node>();
    private Dictionary<string, GameObject> spawnedNodeMarkers = new Dictionary<string, GameObject>();

    private Vector2 userLocation;
    private DirectionDisplayManager directionManager;
    private bool isARNavigationMode = false;

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

        if (compassArrow == null)
            compassArrow = FindObjectOfType<CompassNavigationArrow>();

        DetermineNavigationMode();

        if (isARNavigationMode)
        {
            LoadPathNodesFromPlayerPrefs();
            groundPlaneY = arCamera.transform.position.y - 1.6f;
            StartCoroutine(InitializeNavigationMarkers());
        }
    }

    private void DetermineNavigationMode()
    {
        string arMode = PlayerPrefs.GetString("ARMode", "DirectAR");
        isARNavigationMode = (arMode == "Navigation");
    }

    private void LoadPathNodesFromPlayerPrefs()
    {
        int pathNodeCount = PlayerPrefs.GetInt("ARNavigation_PathNodeCount", 0);

        if (pathNodeCount == 0)
        {
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

                    loadComplete = true;
                }
                catch (System.Exception e)
                {
                    loadComplete = true;
                }
            },
            (error) =>
            {
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
            yield break;
        }

        markersInitialized = true;

        InvokeRepeating(nameof(UpdateNavigationSystem), 0.5f, 0.2f);
    }

    private void UpdateNavigationSystem()
    {
        if (!isARNavigationMode || pathNodes.Count == 0 || !markersInitialized)
            return;

        if (unifiedARManager != null)
        {
            userLocation = unifiedARManager.GetUserXY();
            userXY = userLocation;
        }
        else if (GPSManager.Instance != null)
        {
            userLocation = GPSManager.Instance.GetSmoothedCoordinates();
        }

        if (userLocation.magnitude < 0.0001f)
        {
            return;
        }

        UpdateCompassArrow();

        if (showWaypointMarkers)
        {
            UpdateNodeMarkers();
        }
    }

    private void UpdateCompassArrow()
    {
        if (compassArrow == null || directionManager == null)
            return;

        NavigationDirection currentDir = directionManager.GetCurrentDirection();
        if (currentDir == null || currentDir.destinationNode == null)
            return;

        Node targetNode = currentDir.destinationNode;

        compassArrow.SetTargetNode(targetNode);
        compassArrow.SetActive(true);
    }

    private void UpdateNodeMarkers()
    {
        if (nodeMarkerPrefab == null && destinationMarkerPrefab == null)
            return;

        bool isIndoor = (unifiedARManager != null && unifiedARManager.IsIndoorMode());

        for (int i = 0; i < pathNodes.Count; i++)
        {
            Node node = pathNodes[i];

            float distance = CalculateDistance(node, isIndoor);

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
                    Vector3 worldPos = GetNodeWorldPosition(node, isIndoor);
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

        foreach (var marker in spawnedNodeMarkers.Values)
        {
            if (marker != null && arCamera != null)
            {
                marker.transform.LookAt(arCamera.transform);
                marker.transform.Rotate(0, 180, 0);
            }
        }
    }

    private float CalculateDistance(Node node, bool isIndoor)
    {
        if (isIndoor)
        {
            // Indoor navigation uses X,Y coordinates
            Vector2 nodeXY;
            if (node.indoor != null)
            {
                nodeXY = new Vector2(node.indoor.x, node.indoor.y);
            }
            else
            {
                nodeXY = new Vector2(node.x_coordinate, node.y_coordinate);
            }
            return CalculateDistanceXY(userLocation, nodeXY);
        }
        else
        {
            // Outdoor navigation uses GPS
            Vector2 nodeGPS = new Vector2(node.latitude, node.longitude);
            return CalculateDistanceGPS(userLocation, nodeGPS);
        }
    }

    private Vector3 GetNodeWorldPosition(Node node, bool isIndoor)
    {
        if (isIndoor)
        {
            // Indoor uses X,Y coordinates
            float nodeX = node.indoor != null ? node.indoor.x : node.x_coordinate;
            float nodeY = node.indoor != null ? node.indoor.y : node.y_coordinate;
            return XYToWorldPosition(nodeX, nodeY);
        }
        else
        {
            // Outdoor uses GPS
            return GPSToWorldPosition(node.latitude, node.longitude);
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