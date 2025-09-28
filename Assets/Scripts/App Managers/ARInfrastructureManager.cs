using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using UnityEngine.XR.ARSubsystems;

public class ARInfrastructureManager : MonoBehaviour
{
    [Header("AR Exit Settings")]
    [SerializeField] private Button backToMainButton;
    [SerializeField] private string mainSceneName = "MainAppScene";
    private bool isExitingAR = false;

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
    public TextMeshProUGUI loadingText; // Add this for loading feedback

    [Header("Demo Settings - Set Static Map ID")]
    [SerializeField] private string demoMapId = "MAP-01"; // Set this to your test map ID

    [Header("Data")]
    private List<Node> currentNodes = new List<Node>();
    private List<Infrastructure> currentInfrastructures = new List<Infrastructure>();
    private List<GameObject> activeMarkers = new List<GameObject>();

    [Header("GPS")]
    private Vector2 userLocation;

    [Header("Loading States")]
    private bool isDataLoaded = false;
    private bool isInitializing = true;

    void Start()
    {
        if (arCamera == null)
            arCamera = Camera.main;

        UpdateLoadingUI("Initializing AR Scene...");

        // Setup the back button first
        SetupBackButton();

        StartCoroutine(InitializeARScene());
    }

    private void SetupBackButton()
    {
        // Try to find back button if not assigned
        if (backToMainButton == null)
        {
            GameObject backBtn = GameObject.Find("BackButton") ??
                                GameObject.Find("Back Button") ??
                                GameObject.Find("ExitARButton") ??
                                GameObject.Find("ARBackButton");
            if (backBtn != null)
                backToMainButton = backBtn.GetComponent<Button>();
        }

        if (backToMainButton != null)
        {
            backToMainButton.onClick.RemoveAllListeners();
            backToMainButton.onClick.AddListener(ExitARScene);
            Debug.Log("AR exit button connected successfully");
        }
        else
        {
            Debug.LogWarning("AR exit button not found - make sure you have a button named 'BackButton', 'ExitARButton', or assign it in inspector");
        }
    }

    // Public method that can be called from UI buttons or other scripts
    public void ExitARScene()
    {
        if (isExitingAR) return;

        Debug.Log("Exiting AR scene...");
        isExitingAR = true;

        // Clear markers
        ClearMarkers();
        CancelInvoke();

        // Simple scene transition - SceneUtility handles XR cleanup automatically
        if (GlobalManager.Instance != null)
        {
            GlobalManager.Instance.StartCoroutine(GlobalManager.Instance.SafeARCleanupAndExit(mainSceneName));
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(mainSceneName);
        }
    }
    // Alternative method that matches your existing GoToTargetSceneSimple pattern
    public void GoToTargetSceneSimple(string sceneName)
    {
        if (isExitingAR) return;

        Debug.Log($"AR Back Button clicked - returning to {sceneName}");
        mainSceneName = sceneName; // Update the target scene
        StartCoroutine(SafeExitAR());
    }

    private IEnumerator SafeExitAR()
    {
        isExitingAR = true;
        UpdateLoadingUI("Exiting AR...");

        // Stop our marker updates and coroutines
        CancelInvoke();
        StopAllCoroutines();

        // This coroutine continues after StopAllCoroutines, so restart it
        StartCoroutine(FinishARExitAfterStop());
        yield break;
    }

    private IEnumerator FinishARExitAfterStop()
    {
        // Clear markers to free up resources
        ClearMarkers();

        yield return new WaitForEndOfFrame();

        // Disable AR session properly
        var arSession = FindObjectOfType<UnityEngine.XR.ARFoundation.ARSession>();
        if (arSession != null)
        {
            arSession.enabled = false;
            Debug.Log("AR Session disabled");
        }

        yield return new WaitForEndOfFrame();

        // Stop XR subsystems to prevent camera reference errors
        yield return StartCoroutine(StopXRSubsystems());

        // Let GlobalManager handle the rest of the cleanup
        if (GlobalManager.Instance != null)
        {
            yield return StartCoroutine(GlobalManager.Instance.SafeARCleanupAndExit(mainSceneName));
        }
        else
        {
            // Fallback if GlobalManager is not available
            Debug.LogWarning("GlobalManager not found, using direct scene transition");
            UnityEngine.SceneManagement.SceneManager.LoadScene(mainSceneName);
        }
    }

    private IEnumerator StopXRSubsystems()
    {
        Debug.Log("Stopping XR subsystems to prevent camera reference errors...");

        bool errorOccurred = false;
        try
        {
            // Get all subsystem instances
            var sessionSubsystems = new List<XRSessionSubsystem>();
            var planeSubsystems = new List<XRPlaneSubsystem>();
            var raycastSubsystems = new List<XRRaycastSubsystem>();

            SubsystemManager.GetInstances(sessionSubsystems);
            SubsystemManager.GetInstances(planeSubsystems);
            SubsystemManager.GetInstances(raycastSubsystems);

            // Stop session subsystem first (most important)
            foreach (var subsystem in sessionSubsystems)
            {
                if (subsystem.running)
                {
                    Debug.Log("Stopping XR Session Subsystem...");
                    subsystem.Stop();
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Error stopping XR session subsystems: {ex.Message}");
            errorOccurred = true;
        }

        yield return new WaitForSeconds(0.1f);

        try
        {
            // Stop other subsystems
            var planeSubsystems = new List<XRPlaneSubsystem>();
            var raycastSubsystems = new List<XRRaycastSubsystem>();
            SubsystemManager.GetInstances(planeSubsystems);
            SubsystemManager.GetInstances(raycastSubsystems);

            foreach (var subsystem in planeSubsystems)
            {
                if (subsystem.running)
                {
                    Debug.Log("Stopping XR Plane Subsystem...");
                    subsystem.Stop();
                }
            }

            foreach (var subsystem in raycastSubsystems)
            {
                if (subsystem.running)
                {
                    Debug.Log("Stopping XR Raycast Subsystem...");
                    subsystem.Stop();
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Error stopping XR plane/raycast subsystems: {ex.Message}");
            errorOccurred = true;
        }

        yield return new WaitForSeconds(0.1f);
        Debug.Log("XR subsystems stopped successfully");
    }

    IEnumerator InitializeARScene()
    {
        UpdateLoadingUI("Waiting for GPS Manager...");

        // Wait for GPS Manager
        while (GPSManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        UpdateLoadingUI("GPS Manager found, starting location services...");
        yield return new WaitForSeconds(1f); // Give GPS time to start

        UpdateLoadingUI("Loading map data...");

        // Load data based on demo map
        yield return StartCoroutine(LoadCurrentMapData());

        if (isDataLoaded)
        {
            UpdateLoadingUI("Data loaded, starting AR tracking...");
            // Start updating markers
            InvokeRepeating(nameof(UpdateMarkers), 2f, 2f); // Update every 2 seconds
            isInitializing = false;
            HideLoadingUI();
        }
        else
        {
            UpdateLoadingUI("Failed to load data. Check console for errors.");
        }
    }

    IEnumerator LoadCurrentMapData()
    {
        string currentMapId = demoMapId;
        bool nodesLoaded = false;
        bool infraLoaded = false;

        UpdateLoadingUI($"Loading nodes for map {currentMapId}...");

        // Load nodes for current map
        yield return StartCoroutine(LoadNodesData(currentMapId, (success) =>
        {
            nodesLoaded = success;
        }));

        UpdateLoadingUI("Loading infrastructure data...");

        // Load infrastructure data
        yield return StartCoroutine(LoadInfrastructureData((success) =>
        {
            infraLoaded = success;
        }));

        isDataLoaded = nodesLoaded && infraLoaded;

        if (isDataLoaded)
        {
            Debug.Log($"✅ Successfully loaded {currentNodes.Count} nodes and {currentInfrastructures.Count} infrastructures for map: {currentMapId}");

            if (debugText != null)
                debugText.text = $"Loaded: {currentNodes.Count} nodes, {currentInfrastructures.Count} infra";
        }
        else
        {
            Debug.LogError($"❌ Failed to load data - Nodes: {nodesLoaded}, Infrastructure: {infraLoaded}");
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
                    Debug.LogError($"❌ Error parsing nodes JSON: {e.Message}");
                    loadSuccess = false;
                }
            },
            (error) =>
            {
                Debug.LogError($"❌ Failed to load nodes file: {error}");
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
                    Debug.LogError($"❌ Error parsing infrastructure JSON: {e.Message}");
                    loadSuccess = false;
                }
            },
            (error) =>
            {
                Debug.LogError($"❌ Failed to load infrastructure file: {error}");
                loadSuccess = false;
            }
        ));

        onComplete?.Invoke(loadSuccess);
    }

    void UpdateMarkers()
    {
        // Don't update markers if we're exiting AR, still initializing, or data isn't loaded
        if (isExitingAR || isInitializing || !isDataLoaded)
            return;

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
            string dataStatus = isDataLoaded ? "✅" : "❌";
            debugText.text = $"{dataStatus} User: {userLocation.x:F5}, {userLocation.y:F5}\n" +
                           $"Nodes: {currentNodes.Count} | Active Markers: {activeMarkers.Count}";
        }
    }

    void UpdateLoadingUI(string message)
    {
        if (loadingText != null)
        {
            loadingText.text = message;
            loadingText.gameObject.SetActive(true);
        }
        Debug.Log($"[AR Loading] {message}");
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
        foreach (GameObject marker in activeMarkers)
        {
            if (marker != null)
                Destroy(marker);
        }
        activeMarkers.Clear();
    }

    void CreateVisibleMarkers()
    {
        if (!isDataLoaded || currentNodes == null || currentNodes.Count == 0)
        {
            Debug.LogWarning("Cannot create markers - data not loaded or no nodes available");
            return;
        }

        foreach (Node node in currentNodes)
        {
            if (ShouldShowMarker(node))
            {
                CreateMarkerForNode(node);
            }
        }

        Debug.Log($"Created {activeMarkers.Count} markers from {currentNodes.Count} nodes");
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
            Debug.LogError("❌ Building marker prefab is not assigned!");
            return;
        }

        Infrastructure infra = currentInfrastructures.FirstOrDefault(i => i.infra_id == node.related_infra_id);

        if (infra == null)
        {
            Debug.LogWarning($"⚠️ No infrastructure found for node {node.node_id} with infra_id {node.related_infra_id}");
            return;
        }

        Vector3 worldPosition = GPSToWorldPosition(node.latitude, node.longitude);
        worldPosition.y += markerHeightOffset;

        GameObject marker = Instantiate(buildingMarkerPrefab);
        marker.transform.position = worldPosition;
        marker.transform.localScale = Vector3.one * markerScale;

        UpdateMarkerText(marker, infra, node);
        activeMarkers.Add(marker);

        Debug.Log($"✅ Created marker for {infra.name} at {worldPosition}");
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
        isExitingAR = true;
        CancelInvoke();
        ClearMarkers();
        StopAllCoroutines();
        Debug.Log("ARInfrastructureManager destroyed");
    }
}