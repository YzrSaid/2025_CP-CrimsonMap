using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.IO;
using UnityEngine.Networking;

public class ARInfrastructureManager : MonoBehaviour
{
    [Header("AR Settings")]
    public GameObject buildingMarkerPrefab; // Your infrastructure prefab
    public Camera arCamera;
    public float maxVisibleDistance = 500f; // meters
    public float markerScale = 1f;
    public float minMarkerDistance = 2f; // Minimum distance to show marker in AR
    public float markerHeightOffset = 0f; // Height above ground

    [Header("UI References")]
    public TextMeshProUGUI gpsStrengthText;
    public TextMeshProUGUI debugText; // For debugging info

    [Header("Demo Settings - Set Static Map ID")]
    [SerializeField] private string demoMapId = "MAP-01"; // Set this to your test map ID

    [Header("Data")]
    private List<Node> currentNodes = new List<Node>();
    private List<Infrastructure> currentInfrastructures = new List<Infrastructure>();
    private List<GameObject> activeMarkers = new List<GameObject>();

    [Header("GPS")]
    private Vector2 userLocation;

    void Start()
    {
        if (arCamera == null)
            arCamera = Camera.main;

        StartCoroutine(InitializeARScene());
    }

    IEnumerator InitializeARScene()
    {
        while (GPSManager.Instance == null)
            yield return new WaitForSeconds(0.1f);

        yield return new WaitForSeconds(1f); // Give GPS time to start

        // Load data based on demo map
        yield return StartCoroutine(LoadCurrentMapData());

        // Start updating markers
        InvokeRepeating(nameof(UpdateMarkers), 2f, 2f); // Update every 2 seconds
    }

    IEnumerator LoadCurrentMapData()
    {
        string currentMapId = demoMapId;

        // Load nodes for current map
        yield return StartCoroutine(LoadNodesData(currentMapId));

        // Load infrastructure data
        yield return StartCoroutine(LoadInfrastructureData());

        Debug.Log($"Loaded {currentNodes.Count} nodes and {currentInfrastructures.Count} infrastructures for map: {currentMapId}");

        if (debugText != null)
            debugText.text = $"Loaded: {currentNodes.Count} nodes, {currentInfrastructures.Count} infra";
    }

    IEnumerator LoadNodesData(string mapId)
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, $"nodes_{mapId}.json");
        string jsonData = "";

        if (filePath.Contains("://"))
        {
            UnityWebRequest request = UnityWebRequest.Get(filePath);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
                jsonData = request.downloadHandler.text;
            else
                Debug.LogError("Failed to load: " + filePath);
        }
        else
        {
            jsonData = File.ReadAllText(filePath);
        }

        if (!string.IsNullOrEmpty(jsonData))
        {
            try
            {
                Node[] nodes = JsonHelper.FromJson<Node>(jsonData);
                currentNodes = nodes.Where(n => n.type == "infrastructure" && n.is_active).ToList();
                Debug.Log($"Found {currentNodes.Count} active infrastructure nodes");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error parsing nodes JSON: {e.Message}");
            }
        }
    }

    IEnumerator LoadInfrastructureData()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "infrastructure.json");
        string jsonData = "";

        if (filePath.Contains("://"))
        {
            UnityWebRequest request = UnityWebRequest.Get(filePath);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
                jsonData = request.downloadHandler.text;
            else
                Debug.LogError("Failed to load: " + filePath);
        }
        else
        {
            jsonData = File.ReadAllText(filePath);
        }

        if (!string.IsNullOrEmpty(jsonData))
        {
            try
            {
                Infrastructure[] infrastructures = JsonHelper.FromJson<Infrastructure>(jsonData);
                currentInfrastructures = infrastructures.Where(i => !i.is_deleted).ToList();
                Debug.Log($"Found {currentInfrastructures.Count} active infrastructures");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error parsing infrastructure JSON: {e.Message}");
            }
        }
    }
    IEnumerator LoadJsonFile(string path, System.Action<string> onLoaded)
    {
        string jsonText = null;

        if (path.Contains("://") || path.Contains("jar:")) // Android / WebGL
        {
            UnityWebRequest request = UnityWebRequest.Get(path);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
                jsonText = request.downloadHandler.text;
            else
                Debug.LogError($"Failed to load JSON from {path}: {request.error}");
        }
        else // Desktop
        {
            if (File.Exists(path))
                jsonText = File.ReadAllText(path);
            else
                Debug.LogError($"File not found at {path}");
        }

        if (!string.IsNullOrEmpty(jsonText))
            onLoaded?.Invoke(jsonText);
    }

    void UpdateMarkers()
    {
        userLocation = GPSManager.Instance.GetSmoothedCoordinates();
        UpdateGPSStrengthUI();

        ClearMarkers();
        CreateVisibleMarkers();
        UpdateDebugInfo();
    }

    void UpdateGPSStrengthUI()
    {
        if (gpsStrengthText != null)
        {
            Vector2 coords = GPSManager.Instance.GetCoordinates();
            if (coords.magnitude > 0)
            {
                gpsStrengthText.text = $"GPS: {coords.x:F5}, {coords.y:F5}";
                gpsStrengthText.color = Color.green;
            }
            else
            {
                gpsStrengthText.text = "GPS: No Signal";
                gpsStrengthText.color = Color.red;
            }
        }
    }

    void UpdateDebugInfo()
    {
        if (debugText != null)
        {
            debugText.text = $"User: {userLocation.x:F5}, {userLocation.y:F5}\n" +
                           $"Nodes: {currentNodes.Count} | Active Markers: {activeMarkers.Count}";
        }
    }

    void ClearMarkers()
    {
        foreach (GameObject marker in activeMarkers)
        {
            if (marker != null)
                Destroy(marker);
        }
        activeMarkers.Clear();
    }

    void CreateVisibleMarkers()
    {
        foreach (Node node in currentNodes)
        {
            if (ShouldShowMarker(node))
            {
                CreateMarkerForNode(node);
            }
        }

        Debug.Log($"Created {activeMarkers.Count} markers");
    }

    bool ShouldShowMarker(Node node)
    {
        float distance = CalculateDistance(userLocation, new Vector2(node.latitude, node.longitude));
        bool inRange = distance <= maxVisibleDistance && distance >= minMarkerDistance;

        if (!inRange)
            Debug.Log($"Node {node.name} distance: {distance:F1}m - Out of range");

        return inRange;
    }

    void CreateMarkerForNode(Node node)
    {
        if (buildingMarkerPrefab == null)
        {
            Debug.LogError("Building marker prefab is not assigned!");
            return;
        }

        Infrastructure infra = currentInfrastructures.FirstOrDefault(i => i.infra_id == node.related_infra_id);

        if (infra == null)
        {
            Debug.LogWarning($"No infrastructure found for node {node.node_id} with infra_id {node.related_infra_id}");
            return;
        }

        Vector3 worldPosition = GPSToWorldPosition(node.latitude, node.longitude);
        worldPosition.y += markerHeightOffset;

        GameObject marker = Instantiate(buildingMarkerPrefab);
        marker.transform.position = worldPosition;
        marker.transform.localScale = Vector3.one * markerScale;

        UpdateMarkerText(marker, infra, node);
        activeMarkers.Add(marker);

        Debug.Log($"Created marker for {infra.name} at {worldPosition}");
    }

    void UpdateMarkerText(GameObject marker, Infrastructure infra, Node node)
    {
        TextMeshPro textMeshPro = marker.GetComponentInChildren<TextMeshPro>();
        if (textMeshPro != null)
        {
            textMeshPro.text = infra.name;
            StartCoroutine(UpdateTextRotation(textMeshPro.transform));
        }

        Text nameText = marker.GetComponentInChildren<Text>();
        if (nameText != null && textMeshPro == null)
        {
            nameText.text = infra.name;
        }
    }

    IEnumerator UpdateTextRotation(Transform textTransform)
    {
        while (textTransform != null)
        {
            if (arCamera != null)
            {
                textTransform.LookAt(arCamera.transform);
                textTransform.Rotate(0, 180, 0);
            }
            yield return new WaitForSeconds(0.1f);
        }
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

    float CalculateDistance(Vector2 coord1, Vector2 coord2)
    {
        float lat1Rad = coord1.x * Mathf.Deg2Rad;
        float lat2Rad = coord2.x * Mathf.Deg2Rad;
        float deltaLatRad = (coord2.x - coord1.x) * Mathf.Deg2Rad;
        float deltaLngRad = (coord2.y - coord1.y) * Mathf.Deg2Rad;

        float a = Mathf.Sin(deltaLatRad / 2) * Mathf.Sin(deltaLatRad / 2) +
                  Mathf.Cos(lat1Rad) * Mathf.Cos(lat2Rad) *
                  Mathf.Sin(deltaLngRad / 2) * Mathf.Sin(deltaLngRad / 2);

        float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));

        return 6371000 * c; // Earth radius in meters
    }

    void OnDestroy()
    {
        CancelInvoke();
        ClearMarkers();
        StopAllCoroutines();
    }
}
