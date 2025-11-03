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
    public GameObject buildingMarkerPrefab;
    public Camera arCamera;
    public float maxVisibleDistance = 500f;
    public float markerScale = 1f;
    public float minMarkerDistance = 2f;
    public float markerHeightOffset = 0f;

    [Header("UI References")]
    public TextMeshProUGUI gpsStrengthText;
    public TextMeshProUGUI debugText;
    public TextMeshProUGUI loadingText;

    [Header("GPS Stability Settings")]
    public int gpsHistorySize = 5;
    public float positionUpdateThreshold = 1f;
    public float positionSmoothingFactor = 0.3f;

    [Header("Data")]
    private List<Node> currentNodes = new List<Node>();
    private List<Infrastructure> currentInfrastructures = new List<Infrastructure>();
    private List<GameObject> activeMarkers = new List<GameObject>();
    private Dictionary<string, MarkerAnchor> markerAnchors = new Dictionary<string, MarkerAnchor>();

    [Header("GPS")]
    private Vector2 userLocation;
    private Vector2 lastStableLocation;
    private Queue<Vector2> gpsLocationHistory = new Queue<Vector2>();
    private bool gpsInitialized = false;

    [Header("Feature Flags")]
    private ARFeatureMode currentFeatureMode = ARFeatureMode.None;
    private enum ARFeatureMode { None, DirectAR, ARNavigation }

    void Start()
    {
        if (arCamera == null)
            arCamera = Camera.main;

        UpdateLoadingUI("Initializing AR Scene...");

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
        }
        else
        {
            currentFeatureMode = ARFeatureMode.DirectAR;
        }
    }

    private void SetupBackButton()
    {
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

    public void GoToTargetSceneSimple(string sceneName)
    {
        if (isExitingAR) return;

        mainSceneName = sceneName;
        StartCoroutine(SafeExitAR());
    }

    private IEnumerator SafeExitAR()
    {
        isExitingAR = true;
        UpdateLoadingUI("Exiting AR...");

        CancelInvoke();
        StopAllCoroutines();

        StartCoroutine(FinishARExitAfterStop());
        yield break;
    }

    private IEnumerator FinishARExitAfterStop()
    {
        ClearMarkers();

        yield return new WaitForEndOfFrame();

        var arSession = FindObjectOfType<UnityEngine.XR.ARFoundation.ARSession>();
        if (arSession != null)
        {
            arSession.enabled = false;
        }

        yield return new WaitForEndOfFrame();

        yield return StartCoroutine(StopXRSubsystems());

        if (GlobalManager.Instance != null)
        {
            yield return StartCoroutine(GlobalManager.Instance.SafeARCleanupAndExit(mainSceneName));
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(mainSceneName);
        }
    }

    private IEnumerator StopXRSubsystems()
    {
        try
        {
            var sessionSubsystems = new List<XRSessionSubsystem>();
            var planeSubsystems = new List<XRPlaneSubsystem>();
            var raycastSubsystems = new List<XRRaycastSubsystem>();

            SubsystemManager.GetInstances(sessionSubsystems);
            SubsystemManager.GetInstances(planeSubsystems);
            SubsystemManager.GetInstances(raycastSubsystems);

            foreach (var subsystem in sessionSubsystems)
            {
                if (subsystem.running)
                {
                    subsystem.Stop();
                }
            }
        }
        catch (System.Exception)
        {
        }

        yield return new WaitForSeconds(0.1f);

        try
        {
            var planeSubsystems = new List<XRPlaneSubsystem>();
            var raycastSubsystems = new List<XRRaycastSubsystem>();
            SubsystemManager.GetInstances(planeSubsystems);
            SubsystemManager.GetInstances(raycastSubsystems);

            foreach (var subsystem in planeSubsystems)
            {
                if (subsystem.running)
                {
                    subsystem.Stop();
                }
            }

            foreach (var subsystem in raycastSubsystems)
            {
                if (subsystem.running)
                {
                    subsystem.Stop();
                }
            }
        }
        catch (System.Exception)
        {
        }

        yield return new WaitForSeconds(0.1f);
    }

    IEnumerator InitializeARScene()
    {
        UpdateLoadingUI("Waiting for GPS Manager...");

        while (GPSManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        UpdateLoadingUI("GPS Manager found, starting location services...");
        yield return new WaitForSeconds(1f);

        UpdateLoadingUI("Loading map data...");

        string currentMapId = GetCurrentMapId();
        yield return StartCoroutine(LoadCurrentMapData(currentMapId));

        UpdateLoadingUI("Starting AR tracking...");
        InvokeRepeating(nameof(UpdateMarkers), 2f, 1f);

        HideLoadingUI();
    }

    private string GetCurrentMapId()
    {
        if (MapManager.Instance != null && MapManager.Instance.GetCurrentMap() != null)
        {
            string mapId = MapManager.Instance.GetCurrentMap().map_id;
            return mapId;
        }

        if (PlayerPrefs.HasKey("CurrentMapId"))
        {
            string mapId = PlayerPrefs.GetString("CurrentMapId");
            return mapId;
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
                    currentNodes = nodes.Where(n => n.type == "infrastructure" && n.is_active).ToList();
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
        if (isExitingAR)
        {
            return;
        }

        Vector2 rawGpsLocation = GPSManager.Instance.GetSmoothedCoordinates();

        if (rawGpsLocation.magnitude < 0.0001f)
        {
            return;
        }

        userLocation = StabilizeGPSLocation(rawGpsLocation);

        UpdateGPSStrengthUI();

        UpdateAnchoredMarkerPositions();

        ReconcileVisibleMarkers();

        UpdateDebugInfo();
    }

    private Vector2 StabilizeGPSLocation(Vector2 rawLocation)
    {
        if (!gpsInitialized && rawLocation.magnitude > 0.0001f)
        {
            lastStableLocation = rawLocation;
            gpsLocationHistory.Enqueue(rawLocation);
            gpsInitialized = true;
            return lastStableLocation;
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

        float distanceFromLast = Vector2.Distance(averagedLocation, lastStableLocation);

        if (distanceFromLast >= positionUpdateThreshold)
        {
            lastStableLocation = Vector2.Lerp(lastStableLocation, averagedLocation, positionSmoothingFactor);
        }

        return lastStableLocation;
    }

    private void UpdateAnchoredMarkerPositions()
    {
        foreach (var kvp in markerAnchors)
        {
            MarkerAnchor anchor = kvp.Value;

            if (anchor.markerGameObject == null)
            {
                continue;
            }

            Vector3 newWorldPos = GPSToWorldPosition(anchor.nodeLatitude, anchor.nodeLongitude);
            newWorldPos.y += markerHeightOffset;

            anchor.markerGameObject.transform.position = Vector3.Lerp(
                        anchor.markerGameObject.transform.position,
                        newWorldPos,
                        positionSmoothingFactor
                    );
        }
    }

    private void ReconcileVisibleMarkers()
    {
        if (currentNodes == null || currentNodes.Count == 0)
        {
            return;
        }

        List<string> nodesToRemove = new List<string>();

        foreach (var kvp in markerAnchors)
        {
            MarkerAnchor anchor = kvp.Value;
            Node node = anchor.node;

            if (!ShouldShowMarker(node))
            {
                if (anchor.markerGameObject != null)
                {
                    Destroy(anchor.markerGameObject);
                }
                nodesToRemove.Add(kvp.Key);
            }
        }

        foreach (string nodeId in nodesToRemove)
        {
            markerAnchors.Remove(nodeId);
            activeMarkers.RemoveAll(m => m == null);
        }

        int created = 0;

        foreach (Node node in currentNodes)
        {
            if (markerAnchors.ContainsKey(node.node_id))
            {
                continue;
            }

            if (ShouldShowMarker(node))
            {
                CreateMarkerForNode(node);
                created++;
            }
        }
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
            string modeText = currentFeatureMode == ARFeatureMode.DirectAR ? "🗺️ Direct AR" : "🧭 AR Navigation";

            debugText.text = $"{modeText}\n" +
                             $"User: {userLocation.x:F5}, {userLocation.y:F5}\n" +
                             $"Active Markers: {markerAnchors.Count} | History: {gpsLocationHistory.Count}";
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
            {
                Destroy(kvp.Value.markerGameObject);
            }
        }
        markerAnchors.Clear();
        activeMarkers.Clear();
    }

    bool ShouldShowMarker(Node node)
    {
        float distance = CalculateDistance(userLocation, new Vector2(node.latitude, node.longitude));

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
            return;
        }

        Infrastructure infra = currentInfrastructures.FirstOrDefault(i => i.infra_id == node.related_infra_id);

        if (infra == null)
        {
            return;
        }

        Vector3 worldPosition = GPSToWorldPosition(node.latitude, node.longitude);
        worldPosition.y += markerHeightOffset;

        GameObject marker = Instantiate(buildingMarkerPrefab);
        marker.transform.position = worldPosition;
        marker.transform.localScale = Vector3.one * markerScale;

        UpdateMarkerText(marker, infra, node);

        MarkerAnchor anchor = new MarkerAnchor
        {
            node = node,
            nodeLatitude = node.latitude,
            nodeLongitude = node.longitude,
            markerGameObject = marker
        };

        markerAnchors[node.node_id] = anchor;
        activeMarkers.Add(marker);
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

        return 6371000 * c;
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
        public float nodeLatitude;
        public float nodeLongitude;
        public GameObject markerGameObject;
    }
}