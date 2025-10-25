using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class UnifiedARNavigationMarkerSpawner : MonoBehaviour
{
    [Header("AR Marker Prefabs")]
    public GameObject circleMarkerPrefab;
    public GameObject nodeMarkerPrefab;
    public GameObject destinationMarkerPrefab;

    [Header("AR Components")]
    public Camera arCamera;
    public ARRaycastManager arRaycastManager;
    public ARPlaneManager arPlaneManager;
    public UnifiedARManager unifiedARManager;

    [Header("Marker Settings")]
    public float markerScale = 1.5f;
    public float circleMarkerScale = 0.5f;
    public float markerHeightOffset = 0.05f;
    public float circleSpacing = 5f;
    public float nodeMarkerDistance = 3f;
    public float circleVisibilityDistance = 50f;

    [Header("Colors")]
    public Color pathCircleColor = new Color(0.74f, 0.06f, 0.18f, 0.9f);
    public Color navigationNodeColor = new Color(0.74f, 0.06f, 0.18f, 1f);
    public Color destinationColor = new Color(0.2f, 0.8f, 0.2f, 1f);

    [Header("Settings")]
    public bool enableDebugLogs = true;

    private List<Node> pathNodes = new List<Node>();
    private Dictionary<string, GameObject> spawnedNodeMarkers = new Dictionary<string, GameObject>();
    private List<GameObject> spawnedCircleMarkers = new List<GameObject>();

    private Vector2 userLocation;
    private DirectionDisplayManager directionManager;
    private bool isARNavigationMode = false;

    private enum LocalizationMode { GPS, Offline }
    private LocalizationMode currentLocalizationMode = LocalizationMode.GPS;

    private Vector2 userXY;
    private float groundPlaneY = 0f;

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

        DetermineNavigationMode();

        if (isARNavigationMode)
        {
            LoadPathNodesFromPlayerPrefs();
            StartCoroutine(InitializeNavigationMarkers());
        }
    }

    private void DetermineNavigationMode()
    {
        string arMode = PlayerPrefs.GetString("ARMode", "DirectAR");
        isARNavigationMode = (arMode == "Navigation");

        string localizationModeString = PlayerPrefs.GetString("LocalizationMode", "GPS");
        currentLocalizationMode = localizationModeString == "Offline" ? LocalizationMode.Offline : LocalizationMode.GPS;
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
        yield return new WaitForSeconds(1f);

        if (pathNodes.Count < 2)
        {
            yield break;
        }

        if (currentLocalizationMode == LocalizationMode.Offline)
        {
            yield return StartCoroutine(WaitForGroundPlane());
        }

        InvokeRepeating(nameof(UpdateNavigationMarkers), 0.5f, 1f);
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

    private void UpdateNavigationMarkers()
    {
        if (!isARNavigationMode || pathNodes.Count == 0)
            return;

        if (currentLocalizationMode == LocalizationMode.GPS)
        {
            if (GPSManager.Instance == null)
                return;

            userLocation = GPSManager.Instance.GetSmoothedCoordinates();

            if (userLocation.magnitude < 0.0001f)
                return;
        }
        else
        {
            if (unifiedARManager != null)
            {
                userXY = GetCurrentUserXY();
                userLocation = userXY;
            }
        }

        UpdateNodeMarkers();
        UpdatePathCircles();
    }

    private Vector2 GetCurrentUserXY()
    {
        return userXY;
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

        foreach (var marker in spawnedNodeMarkers.Values)
        {
            if (marker != null && arCamera != null)
            {
                marker.transform.LookAt(arCamera.transform);
                marker.transform.Rotate(0, 180, 0);
            }
        }
    }

    private void UpdatePathCircles()
    {
        if (circleMarkerPrefab == null || directionManager == null)
            return;

        NavigationDirection currentDir = directionManager.GetCurrentDirection();
        if (currentDir == null)
            return;

        int currentIndex = directionManager.GetCurrentDirectionIndex();
        if (currentIndex >= pathNodes.Count - 1)
            return;

        ClearCircleMarkers();

        Node currentNode = pathNodes[currentIndex];
        Node nextNode = pathNodes[currentIndex + 1];

        float segmentDistance = 0f;
        
        if (currentLocalizationMode == LocalizationMode.GPS)
        {
            segmentDistance = CalculateDistanceGPS(
                new Vector2(currentNode.latitude, currentNode.longitude),
                new Vector2(nextNode.latitude, nextNode.longitude)
            );
        }
        else
        {
            segmentDistance = CalculateDistanceXY(
                new Vector2(currentNode.x_coordinate, currentNode.y_coordinate),
                new Vector2(nextNode.x_coordinate, nextNode.y_coordinate)
            );
        }

        int circleCount = Mathf.CeilToInt(segmentDistance / circleSpacing);
        circleCount = Mathf.Min(circleCount, 20);

        for (int i = 1; i <= circleCount; i++)
        {
            float t = i / (float)(circleCount + 1);

            float distanceFromUser = 0f;
            Vector3 worldPos;

            if (currentLocalizationMode == LocalizationMode.GPS)
            {
                float lat = Mathf.Lerp(currentNode.latitude, nextNode.latitude, t);
                float lng = Mathf.Lerp(currentNode.longitude, nextNode.longitude, t);

                distanceFromUser = CalculateDistanceGPS(userLocation, new Vector2(lat, lng));

                if (distanceFromUser <= circleVisibilityDistance)
                {
                    worldPos = GPSToWorldPosition(lat, lng);
                    worldPos = GetGroundPosition(worldPos);

                    CreateCircleMarker(worldPos);
                }
            }
            else
            {
                float x = Mathf.Lerp(currentNode.x_coordinate, nextNode.x_coordinate, t);
                float y = Mathf.Lerp(currentNode.y_coordinate, nextNode.y_coordinate, t);

                distanceFromUser = CalculateDistanceXY(userLocation, new Vector2(x, y));

                if (distanceFromUser <= circleVisibilityDistance)
                {
                    worldPos = XYToWorldPosition(x, y);
                    worldPos = GetGroundPosition(worldPos);

                    CreateCircleMarker(worldPos);
                }
            }
        }
    }

    private void CreateCircleMarker(Vector3 worldPos)
    {
        GameObject circle = Instantiate(circleMarkerPrefab, worldPos, Quaternion.identity);
        circle.transform.localScale = Vector3.one * circleMarkerScale;

        Renderer[] renderers = circle.GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
        {
            if (rend.material != null)
                rend.material.color = pathCircleColor;
        }

        spawnedCircleMarkers.Add(circle);
    }

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

    private void ClearCircleMarkers()
    {
        foreach (var circle in spawnedCircleMarkers)
        {
            if (circle != null)
                Destroy(circle);
        }
        spawnedCircleMarkers.Clear();
    }

    private void ClearAllMarkers()
    {
        ClearCircleMarkers();

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

    void OnDestroy()
    {
        CancelInvoke();
        ClearAllMarkers();
    }
}