using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MapModeController : MonoBehaviour
{
    [Header("Map References")]
    public GameObject outdoorMapContainer;
    public GameObject indoorMapContainer;

    [Header("Outdoor Buttons")]
    public Button centerOnMyLocationButton;
    public Button zoomInButton;
    public Button zoomOutButton;
    public Button goInsideButton;

    [Header("Indoor Buttons")]
    public Button goOutsideButton;
    public Button floorUpButton;
    public Button floorDownButton;

    [Header("Managers")]
    public IndoorMapManager indoorMapManager;
    public MapManager mapManager;
    public PathfindingController pathfindingController;

    private bool isIndoorMode = false;
    private string currentInfraId;
    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>();
    private Dictionary<string, IndoorInfrastructure> indoorInfrastructures = new Dictionary<string, IndoorInfrastructure>();

    public static MapModeController Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (goInsideButton != null)
            goInsideButton.onClick.AddListener(OnGoInsideClicked);

        if (goOutsideButton != null)
            goOutsideButton.onClick.AddListener(OnGoOutsideClicked);

        if (floorUpButton != null)
            floorUpButton.onClick.AddListener(OnFloorUpClicked);

        if (floorDownButton != null)
            floorDownButton.onClick.AddListener(OnFloorDownClicked);

        SetOutdoorMode();

        StartCoroutine(LoadAllData());
    }

    private IEnumerator LoadAllData()
    {
        if (mapManager != null && mapManager.GetCurrentMap() != null)
        {
            string mapId = mapManager.GetCurrentMap().map_id;
            yield return StartCoroutine(LoadNodes(mapId));
        }

        yield return StartCoroutine(LoadIndoorData());

        UpdateGoInsideButtonVisibility();
    }

    private IEnumerator LoadNodes(string mapId)
    {
        string fileName = $"nodes_{mapId}.json";
        bool loadComplete = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonContent) =>
            {
                try
                {
                    Node[] nodes = JsonHelper.FromJson<Node>(jsonContent);
                    allNodes.Clear();

                    foreach (var node in nodes)
                    {
                        allNodes[node.node_id] = node;
                    }

                    loadComplete = true;
                }
                catch (System.Exception)
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

    private IEnumerator LoadIndoorData()
    {
        bool loadComplete = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "indoor.json",
            (jsonContent) =>
            {
                try
                {
                    IndoorInfrastructure[] indoorArray = JsonHelper.FromJson<IndoorInfrastructure>(jsonContent);
                    indoorInfrastructures.Clear();

                    foreach (var indoor in indoorArray)
                    {
                        if (!indoor.is_deleted)
                        {
                            indoorInfrastructures[indoor.room_id] = indoor;
                        }
                    }

                    loadComplete = true;
                }
                catch (System.Exception)
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

    void Update()
    {
        if (!isIndoorMode)
        {
            UpdateGoInsideButtonVisibility();
        }
    }

    private void UpdateGoInsideButtonVisibility()
    {
        if (goInsideButton == null || pathfindingController == null)
            return;

        string currentLocationName = pathfindingController.GetCurrentFromLocationName();

        Node currentNode = allNodes.Values.FirstOrDefault(n => n.name == currentLocationName);

        if (currentNode != null && currentNode.type == "infrastructure" && !string.IsNullOrEmpty(currentNode.related_infra_id))
        {
            string infraId = currentNode.related_infra_id;
            bool hasIndoor = indoorInfrastructures.Values.Any(ind => ind.infra_id == infraId);

            goInsideButton.gameObject.SetActive(hasIndoor);

            if (hasIndoor)
            {
                currentInfraId = infraId;
            }
        }
        else
        {
            goInsideButton.gameObject.SetActive(false);
        }
    }

    private void OnGoInsideClicked()
    {
        if (string.IsNullOrEmpty(currentInfraId))
            return;

        Node infraNode = allNodes.Values.FirstOrDefault(n => 
            n.type == "infrastructure" && n.related_infra_id == currentInfraId);

        if (infraNode == null)
            return;

        SetIndoorMode(currentInfraId, infraNode);
    }

    private void OnGoOutsideClicked()
    {
        SetOutdoorMode();
    }

    private void OnFloorUpClicked()
    {
        if (indoorMapManager != null)
        {
            indoorMapManager.ChangeFloor(1);
        }
    }

    private void OnFloorDownClicked()
    {
        if (indoorMapManager != null)
        {
            indoorMapManager.ChangeFloor(-1);
        }
    }

    private void SetOutdoorMode()
    {
        isIndoorMode = false;

        if (outdoorMapContainer != null)
            outdoorMapContainer.SetActive(true);

        if (indoorMapContainer != null)
            indoorMapContainer.SetActive(false);

        if (centerOnMyLocationButton != null)
            centerOnMyLocationButton.gameObject.SetActive(true);

        if (zoomInButton != null)
            zoomInButton.gameObject.SetActive(true);

        if (zoomOutButton != null)
            zoomOutButton.gameObject.SetActive(true);

        if (goInsideButton != null)
            goInsideButton.gameObject.SetActive(false);

        if (goOutsideButton != null)
            goOutsideButton.gameObject.SetActive(false);

        if (floorUpButton != null)
            floorUpButton.gameObject.SetActive(false);

        if (floorDownButton != null)
            floorDownButton.gameObject.SetActive(false);

        UpdateGoInsideButtonVisibility();
    }

    private void SetIndoorMode(string infraId, Node infraNode)
    {
        isIndoorMode = true;

        if (outdoorMapContainer != null)
            outdoorMapContainer.SetActive(false);

        if (indoorMapContainer != null)
            indoorMapContainer.SetActive(true);

        if (centerOnMyLocationButton != null)
            centerOnMyLocationButton.gameObject.SetActive(false);

        if (zoomInButton != null)
            zoomInButton.gameObject.SetActive(false);

        if (zoomOutButton != null)
            zoomOutButton.gameObject.SetActive(false);

        if (goInsideButton != null)
            goInsideButton.gameObject.SetActive(false);

        if (goOutsideButton != null)
            goOutsideButton.gameObject.SetActive(true);

        if (floorUpButton != null)
            floorUpButton.gameObject.SetActive(true);

        if (floorDownButton != null)
            floorDownButton.gameObject.SetActive(true);

        if (indoorMapManager != null)
        {
            indoorMapManager.LoadIndoorMap(infraId, infraNode);
        }
    }

    public bool IsIndoorMode()
    {
        return isIndoorMode;
    }

    public string GetCurrentInfraId()
    {
        return currentInfraId;
    }

    void OnDestroy()
    {
        if (goInsideButton != null)
            goInsideButton.onClick.RemoveListener(OnGoInsideClicked);

        if (goOutsideButton != null)
            goOutsideButton.onClick.RemoveListener(OnGoOutsideClicked);

        if (floorUpButton != null)
            floorUpButton.onClick.RemoveListener(OnFloorUpClicked);

        if (floorDownButton != null)
            floorDownButton.onClick.RemoveListener(OnFloorDownClicked);
    }
}