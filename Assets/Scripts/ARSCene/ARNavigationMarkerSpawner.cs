using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARNavigationMarkerSpawner : MonoBehaviour
{
    [Header("AR Marker Prefabs")]
    public GameObject circleMarkerPrefab; 
    public GameObject nodeMarkerPrefab;
    public GameObject destinationMarkerPrefab;

    [Header("AR Camera & Plane Detection")]
    public Camera arCamera;
    public ARRaycastManager arRaycastManager; // ADD THIS
    public ARPlaneManager arPlaneManager; // ADD THIS

    [Header("Marker Settings")]
    public float markerScale = 1.5f;
    public float circleMarkerScale = 0.5f; 
    public float markerHeightOffset = 0.05f; // REDUCED - just slightly above ground
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

    // For AR Raycasting
    private List<ARRaycastHit> arRaycastHits = new List<ARRaycastHit>();

    void Start()
    {
        if (arCamera == null)
            arCamera = Camera.main;

        // Find AR components
        if (arRaycastManager == null)
            arRaycastManager = FindObjectOfType<ARRaycastManager>();
        
        if (arPlaneManager == null)
            arPlaneManager = FindObjectOfType<ARPlaneManager>();

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

        InvokeRepeating(nameof(UpdateNavigationMarkers), 0.5f, 1f);
    }

    private void UpdateNavigationMarkers()
    {
        if (!isARNavigationMode || pathNodes.Count == 0)
            return;

        if (GPSManager.Instance == null)
            return;

        userLocation = GPSManager.Instance.GetSmoothedCoordinates();

        UpdateNodeMarkers();
        UpdatePathCircles();
    }

    private void UpdateNodeMarkers()
    {
        if (nodeMarkerPrefab == null && destinationMarkerPrefab == null)
            return;

        for (int i = 0; i < pathNodes.Count; i++)
        {
            Node node = pathNodes[i];
            float distance = CalculateDistance(userLocation, new Vector2(node.latitude, node.longitude));

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
                    Vector3 worldPos = GPSToWorldPosition(node.latitude, node.longitude);
                    
                    // FIXED: Find ground plane at this position
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

        float segmentDistance = CalculateDistance(
            new Vector2(currentNode.latitude, currentNode.longitude),
            new Vector2(nextNode.latitude, nextNode.longitude)
        );

        int circleCount = Mathf.CeilToInt(segmentDistance / circleSpacing);
        circleCount = Mathf.Min(circleCount, 20); 

        for (int i = 1; i <= circleCount; i++)
        {
            float t = i / (float)(circleCount + 1);

            float lat = Mathf.Lerp(currentNode.latitude, nextNode.latitude, t);
            float lng = Mathf.Lerp(currentNode.longitude, nextNode.longitude, t);

            float distanceFromUser = CalculateDistance(userLocation, new Vector2(lat, lng));

            if (distanceFromUser <= circleVisibilityDistance)
            {
                Vector3 worldPos = GPSToWorldPosition(lat, lng);
                
                // FIXED: Find ground plane for circle
                worldPos = GetGroundPosition(worldPos);

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
        }
    }

    /// <summary>
    /// NEW METHOD: Find the actual ground plane position using AR Raycast
    /// </summary>
    private Vector3 GetGroundPosition(Vector3 targetWorldPos)
    {
        if (arRaycastManager == null || arCamera == null)
        {
            // Fallback: Use camera height as ground reference
            targetWorldPos.y = arCamera.transform.position.y + markerHeightOffset;
            return targetWorldPos;
        }

        // Raycast from above the target position downward to find ground
        Vector3 rayOrigin = targetWorldPos;
        rayOrigin.y = arCamera.transform.position.y + 2f; // Start from above

        Vector3 rayDirection = Vector3.down;

        // Try to hit an AR plane
        Ray ray = new Ray(rayOrigin, rayDirection);
        
        // Alternative: Try screen-space raycast if available
        Vector3 screenPoint = arCamera.WorldToScreenPoint(targetWorldPos);
        
        arRaycastHits.Clear();
        if (arRaycastManager.Raycast(screenPoint, arRaycastHits, TrackableType.PlaneWithinPolygon))
        {
            // Found a plane! Use its position
            Vector3 groundPos = arRaycastHits[0].pose.position;
            groundPos.y += markerHeightOffset; // Slight offset above ground
            return groundPos;
        }

        // Fallback: Use camera's Y position as reference (assuming user is standing)
        targetWorldPos.y = arCamera.transform.position.y - 1.5f + markerHeightOffset; // Ground is ~1.5m below camera
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

    private float CalculateDistance(Vector2 coord1, Vector2 coord2)
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

    void OnDestroy()
    {
        CancelInvoke();
        ClearAllMarkers();
    }
}