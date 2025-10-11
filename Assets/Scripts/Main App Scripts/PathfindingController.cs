using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public class PathfindingController : MonoBehaviour
{
    [Header("References")]
    public AStarPathfinding pathfinding;
    public InfrastructurePopulator infrastructurePopulator;
    public GPSManager gpsManager;

    [Header("UI Elements")]
    public TMP_Dropdown toDropdown;
    public Button findPathButton;

    [Header("Result Display")]
    public GameObject resultPanel;
    public GameObject destinationPanel;
    public TextMeshProUGUI fromText;
    public TextMeshProUGUI toText;
    public TextMeshProUGUI distanceText;
    public TextMeshProUGUI walkingTimeText;
    public TextMeshProUGUI pathInfoText;

    [Header("Static Test Settings")]
    public bool useStaticTesting = false;
    public string staticFromNodeId = "ND-025";
    public string staticToNodeId = "ND-017";

    [Header("GPS Settings")]
    public bool useGPSForFromLocation = true;
    public float nearestNodeSearchRadius = 500f;
    public bool autoUpdateGPSLocation = true;
    public float gpsUpdateInterval = 5f;

    [Header("Settings")]
    public bool enableDebugLogs = true;

    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>();
    private Dictionary<string, string> infraIdToNodeId = new Dictionary<string, string>();
    private string selectedFromNodeId;
    private string selectedToNodeId;
    private Node currentNearestNode;
    private float lastGPSUpdateTime;
    private bool nodesLoaded = false;

    private string currentMapId;
    private List<string> currentCampusIds;

    void Start()
    {
        if (findPathButton != null)
        {
            findPathButton.onClick.AddListener(OnFindPathClicked);
        }
        if (toDropdown != null)
        {
            toDropdown.onValueChanged.AddListener(OnToDropdownChanged);
            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
                destinationPanel.SetActive(false);
            }
        }
        if (MapManager.Instance != null)
        {
            MapManager.Instance.OnMapChanged += OnMapChanged;
        }
        if (gpsManager == null)
        {
            gpsManager = GPSManager.Instance;
        }
    }

    void OnDestroy()
    {
        if (findPathButton != null)
        {
            findPathButton.onClick.RemoveListener(OnFindPathClicked);
        }

        if (toDropdown != null)
        {
            toDropdown.onValueChanged.RemoveListener(OnToDropdownChanged);
        }

        if (MapManager.Instance != null)
        {
            MapManager.Instance.OnMapChanged -= OnMapChanged;
        }
    }

    void Update()
    {
        if (useGPSForFromLocation && autoUpdateGPSLocation && !useStaticTesting && nodesLoaded)
        {
            if (Time.time - lastGPSUpdateTime >= gpsUpdateInterval)
            {
                UpdateFromLocationByGPS();
                lastGPSUpdateTime = Time.time;
            }
        }
    }

    #region MapManager Integration

    private void OnMapChanged(MapInfo mapInfo)
    {
        ClearCurrentPath();
    }

    public IEnumerator InitializeForMap(string mapId, List<string> campusIds)
    {
        currentMapId = mapId;
        currentCampusIds = campusIds;

        yield return StartCoroutine(LoadNodesFromJSON(mapId));

        yield return StartCoroutine(BuildInfrastructureNodeMapping());

        if (useGPSForFromLocation && !useStaticTesting)
        {
            UpdateFromLocationByGPS();
        }

        if (pathfinding != null)
        {
            yield return StartCoroutine(pathfinding.LoadGraphDataForMap(mapId, campusIds));
        }
    }

    #endregion

    #region Node Loading from JSON

    private IEnumerator LoadNodesFromJSON(string mapId)
    {
        string fileName = $"nodes_{mapId}.json";
        bool loadSuccess = false;
        string errorMsg = "";

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonContent) =>
            {
                try
                {
                    Node[] nodesArray = JsonHelper.FromJson<Node>(jsonContent);

                    allNodes.Clear();
                    foreach (Node node in nodesArray)
                    {
                        allNodes[node.node_id] = node;
                    }

                    nodesLoaded = true;
                    loadSuccess = true;
                }
                catch (System.Exception e)
                {
                    errorMsg = $"Error parsing nodes JSON: {e.Message}";
                }
            },
            (error) =>
            {
                errorMsg = $"Failed to load {fileName}: {error}";
            }
        ));

        yield return null;
    }

    #endregion

    #region Infrastructure to Node Mapping

    private IEnumerator BuildInfrastructureNodeMapping()
    {
        if (allNodes == null || allNodes.Count == 0)
        {
            yield break;
        }

        infraIdToNodeId.Clear();

        foreach (var kvp in allNodes)
        {
            Node node = kvp.Value;

            if (node.type == "infrastructure" && !string.IsNullOrEmpty(node.related_infra_id))
            {
                infraIdToNodeId[node.related_infra_id] = node.node_id;
            }
        }

        yield return null;
    }

    #endregion

    #region GPS Location Handling

    private void UpdateFromLocationByGPS()
    {
        if (gpsManager == null)
        {
            return;
        }

        if (!nodesLoaded || allNodes == null || allNodes.Count == 0)
        {
            return;
        }

        Vector2 coords = gpsManager.GetSmoothedCoordinates();

        Node nearestNode = FindNearestNode(coords.x, coords.y);

        if (nearestNode != null)
        {
            selectedFromNodeId = nearestNode.node_id;
            currentNearestNode = nearestNode;
        }
    }

    private Node FindNearestNode(float latitude, float longitude)
    {
        if (allNodes == null || allNodes.Count == 0)
        {
            return null;
        }

        Node nearestNode = null;
        float nearestDistance = float.MaxValue;

        foreach (var kvp in allNodes)
        {
            Node node = kvp.Value;

            float distance = CalculateDistance(latitude, longitude, node.latitude, node.longitude);

            if (distance < nearestDistance && distance <= nearestNodeSearchRadius)
            {
                nearestDistance = distance;
                nearestNode = node;
            }
        }

        return nearestNode;
    }

    private float CalculateDistance(float lat1, float lon1, float lat2, float lon2)
    {
        const float R = 6371000f;

        float dLat = (lat2 - lat1) * Mathf.Deg2Rad;
        float dLon = (lon2 - lon1) * Mathf.Deg2Rad;

        float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                  Mathf.Cos(lat1 * Mathf.Deg2Rad) * Mathf.Cos(lat2 * Mathf.Deg2Rad) *
                  Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);

        float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));

        return R * c;
    }

    #endregion

    #region Dropdown Handlers

    private void OnToDropdownChanged(int index)
    {
        if (infrastructurePopulator == null || toDropdown == null)
        {
            return;
        }

        Infrastructure selectedInfra = infrastructurePopulator.GetSelectedInfrastructure(toDropdown);

        if (selectedInfra == null)
        {
            selectedToNodeId = null;
            return;
        }

        if (infraIdToNodeId.TryGetValue(selectedInfra.infra_id, out string nodeId))
        {
            selectedToNodeId = nodeId;
        }
        else
        {
            selectedToNodeId = null;
        }
    }

    #endregion

    #region Pathfinding Trigger

    private void OnFindPathClicked()
    {
        string fromNodeId;
        string toNodeId;

        if (useStaticTesting)
        {
            fromNodeId = staticFromNodeId;
            toNodeId = staticToNodeId;
        }
        else
        {
            UpdateFromLocationByGPS();
            fromNodeId = selectedFromNodeId;

            if (string.IsNullOrEmpty(fromNodeId))
            {
                ShowError("Cannot determine your location. Please check GPS.");
                return;
            }

            toNodeId = selectedToNodeId;

            if (string.IsNullOrEmpty(toNodeId))
            {
                ShowError("Please select a destination");
                return;
            }
        }

        if (fromNodeId == toNodeId)
        {
            ShowError("You are already at this location!");
            return;
        }

        StartCoroutine(FindAndDisplayPath(fromNodeId, toNodeId));
    }

    private IEnumerator FindAndDisplayPath(string fromNodeId, string toNodeId)
    {
        if (pathfinding == null)
        {
            yield break;
        }

        if (findPathButton != null)
        {
            findPathButton.interactable = false;
        }

        yield return StartCoroutine(pathfinding.FindPath(fromNodeId, toNodeId));

        if (findPathButton != null)
        {
            findPathButton.interactable = true;
        }

        if (!pathfinding.HasPath())
        {
            ShowError("No path found between these locations");
            yield break;
        }

        DisplayPathResults();
    }

    #endregion

    #region Result Display

    private void DisplayPathResults()
    {
        if (resultPanel != null && destinationPanel != null)
        {
            resultPanel.SetActive(true);
            destinationPanel.SetActive(true);
        }

        var path = pathfinding.GetCurrentPath();
        float distance = pathfinding.GetTotalDistance();
        string formattedDistance = pathfinding.GetFormattedDistance();
        string walkingTime = pathfinding.GetEstimatedWalkingTime();

        var fromNode = pathfinding.GetStartNode();
        var toNode = pathfinding.GetEndNode();

        if (fromText != null)
        {
            fromText.text = $"<b>From:</b> {fromNode.name}";
        }

        if (toText != null)
        {
            toText.text = $"<b>To:</b> {toNode.name}";
        }

        if (distanceText != null)
        {
            distanceText.text = $"<b>Distance:</b> {formattedDistance}";
        }

        if (walkingTimeText != null)
        {
            walkingTimeText.text = $"<b>Time:</b> ~{walkingTime}";
        }

        if (pathInfoText != null)
        {
            string pathInfo = $"<b>Route ({path.Count} stops):</b>\n";

            for (int i = 0; i < path.Count; i++)
            {
                var node = path[i].node;
                pathInfo += $"{i + 1}. {node.name}\n";
            }

            pathInfoText.text = pathInfo;
        }
    }

    private void ShowError(string message)
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        if (fromText != null)
        {
            fromText.text = "";
        }

        if (toText != null)
        {
            toText.text = "";
        }

        if (distanceText != null)
        {
            distanceText.text = "❌ Error";
        }

        if (walkingTimeText != null)
        {
            walkingTimeText.text = "";
        }

        if (pathInfoText != null)
        {
            pathInfoText.text = message;
        }
    }

    public void HideResults()
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }
    }

    public void ClearCurrentPath()
    {
        selectedToNodeId = null;
        if (pathfinding != null)
        {
            pathfinding.ClearCurrentPath();
        }
        HideResults();
    }

    #endregion

    #region Public Methods for External Integration

    public void RefreshGPSLocation()
    {
        if (useGPSForFromLocation && !useStaticTesting && nodesLoaded)
        {
            UpdateFromLocationByGPS();
        }
    }

    public void SetFromLocationByQR(string nodeId)
    {
        if (allNodes.ContainsKey(nodeId))
        {
            selectedFromNodeId = nodeId;
            currentNearestNode = allNodes[nodeId];
        }
    }

    public void ToggleGPSMode(bool useGPS)
    {
        useGPSForFromLocation = useGPS;

        if (useGPS && nodesLoaded)
        {
            UpdateFromLocationByGPS();
        }
    }

    #endregion

    #region Utility Methods

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
        }
    }

    public string GetCurrentFromLocationName()
    {
        if (currentNearestNode != null)
        {
            return currentNearestNode.name;
        }
        return "Unknown";
    }

    public bool IsReadyForPathfinding()
    {
        return nodesLoaded &&
               !string.IsNullOrEmpty(selectedFromNodeId) &&
               !string.IsNullOrEmpty(selectedToNodeId);
    }

    #endregion
}