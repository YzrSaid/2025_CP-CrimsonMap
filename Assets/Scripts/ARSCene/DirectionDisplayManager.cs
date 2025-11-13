using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class DirectionDisplayManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject directionPanel;
    public TextMeshProUGUI directionText;

    [Header("Turn Icon Images (GameObjects)")]
    public GameObject turnRightImage;
    public GameObject turnLeftImage;
    public GameObject walkStraightImage;
    public GameObject enterImage;
    public GameObject turnIconsContainer;

    [Header("Compass Arrow")]
    public CompassNavigationArrow compassArrow;

    [Header("All Directions Panel")]
    public Transform directionsScrollContent;
    public GameObject directionItemPrefab;

    [Header("Settings")]
    public bool enableKeyboardTesting = true;
    public float autoProgressDistance = 5f;
    public bool enableDebugLogs = true;

    private List<NavigationDirection> allDirections = new List<NavigationDirection>();
    private List<DirectionItemUI> directionItemInstances = new List<DirectionItemUI>();
    private int currentDirectionIndex = 0;
    private bool isNavigationActive = false;

    private Vector2 userLocation;
    private Node currentTargetNode;
    private float distanceToTarget = 0f;
    private bool hasAutoProgressed = false;

    private UnifiedARManager arManager;

    public enum ARMode { DirectAR, Navigation }

    private ARModeHelper.ARMode currentARMode;

    void Start()
    {
        arManager = FindObjectOfType<UnifiedARManager>();

        if (directionPanel != null)
            directionPanel.SetActive(false);

        HideAllTurnIcons();

        if (compassArrow == null)
            compassArrow = FindObjectOfType<CompassNavigationArrow>();

        LoadDirectionsFromPlayerPrefs();

        if (allDirections.Count > 0)
        {
            StartNavigation();
        }
    }

    void Update()
    {
        if (!isNavigationActive)
            return;

        if (enableKeyboardTesting && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            MoveToNextDirection();
        }

        UpdateUserLocation();
        UpdateDistanceToTarget();
        CheckAutoProgress();
    }

    private void UpdateUserLocation()
    {
        if (arManager != null)
        {
            userLocation = arManager.GetUserXY();
        }
        else if (GPSManager.Instance != null)
        {
            userLocation = GPSManager.Instance.GetSmoothedCoordinates();
        }
    }

    private void LoadDirectionsFromPlayerPrefs()
    {
        int directionCount = PlayerPrefs.GetInt("ARNavigation_DirectionCount", 0);

        if (directionCount == 0)
        {
            return;
        }

        allDirections.Clear();

        string mapId = PlayerPrefs.GetString("ARScene_MapId", "MAP-01");
        StartCoroutine(LoadNodesAndDirections(mapId, directionCount));
    }

    private IEnumerator LoadNodesAndDirections(string mapId, int directionCount)
    {
        string fileName = $"nodes_{mapId}.json";
        Node[] allNodes = null;
        bool loadComplete = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonContent) =>
            {
                try
                {
                    allNodes = JsonHelper.FromJson<Node>(jsonContent);
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

        for (int i = 0; i < directionCount; i++)
        {
            NavigationDirection dir = new NavigationDirection
            {
                instruction = PlayerPrefs.GetString($"ARNavigation_Direction_{i}_Instruction", ""),
                turn = (TurnDirection)System.Enum.Parse(typeof(TurnDirection),
                       PlayerPrefs.GetString($"ARNavigation_Direction_{i}_Turn", "Straight")),
                distanceInMeters = PlayerPrefs.GetFloat($"ARNavigation_Direction_{i}_Distance", 0f),
                isIndoorGrouped = PlayerPrefs.GetInt($"ARNavigation_Direction_{i}_IsIndoorGrouped", 0) == 1,
                isIndoorDirection = PlayerPrefs.GetInt($"ARNavigation_Direction_{i}_IsIndoorDirection", 0) == 1
            };

            string destNodeId = PlayerPrefs.GetString($"ARNavigation_Direction_{i}_DestNodeId", "");

            if (allNodes != null && !string.IsNullOrEmpty(destNodeId))
            {
                dir.destinationNode = System.Array.Find(allNodes, n => n.node_id == destNodeId);

                if (dir.destinationNode == null)
                {
                    dir.destinationNode = new Node
                    {
                        name = PlayerPrefs.GetString($"ARNavigation_Direction_{i}_DestNode", "Unknown"),
                        node_id = destNodeId
                    };
                }
            }
            else
            {
                dir.destinationNode = new Node
                {
                    name = PlayerPrefs.GetString($"ARNavigation_Direction_{i}_DestNode", "Unknown")
                };
            }

            allDirections.Add(dir);
        }

        PopulateDirectionItems();

        if (allDirections.Count > 0 && ARModeHelper.IsNavigationMode())
        {
            StartNavigation();
        }
    }

    private void PopulateDirectionItems()
    {
        ClearDirectionItems();

        if (directionItemPrefab == null || directionsScrollContent == null)
        {
            return;
        }

        for (int i = 0; i < allDirections.Count; i++)
        {
            GameObject itemObj = Instantiate(directionItemPrefab, directionsScrollContent);
            DirectionItemUI itemUI = itemObj.GetComponent<DirectionItemUI>();

            if (itemUI != null)
            {
                itemUI.Initialize(i, allDirections[i]);
                directionItemInstances.Add(itemUI);
            }
        }
    }

    private void ClearDirectionItems()
    {
        directionItemInstances.Clear();

        if (directionsScrollContent != null)
        {
            foreach (Transform child in directionsScrollContent)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void StartNavigation()
    {
        if (allDirections.Count == 0)
        {
            return;
        }

        isNavigationActive = true;
        currentDirectionIndex = 0;

        if (directionPanel != null)
            directionPanel.SetActive(true);

        DisplayCurrentDirection();
    }

    private void DisplayCurrentDirection()
    {
        if (currentDirectionIndex >= allDirections.Count)
        {
            CompleteNavigation();
            return;
        }

        NavigationDirection currentDir = allDirections[currentDirectionIndex];

        if (currentDir.isIndoorGrouped)
        {
            string groupedInstructions = "";
            List<Node> groupedTargets = new List<Node>();

            int startIndex = currentDirectionIndex;
            while (currentDirectionIndex < allDirections.Count &&
                   allDirections[currentDirectionIndex].isIndoorGrouped)
            {
                var indoorDir = allDirections[currentDirectionIndex];
                groupedInstructions += indoorDir.instruction + "\n\n";
                groupedTargets.Add(indoorDir.destinationNode);
                currentDirectionIndex++;
            }

            if (directionText != null)
            {
                directionText.text = groupedInstructions.Trim();
            }

            HideAllTurnIcons();

            if (groupedTargets.Count > 0)
            {
                currentTargetNode = groupedTargets[groupedTargets.Count - 1];

                if (compassArrow != null)
                {
                    compassArrow.SetTargetNode(currentTargetNode);
                    compassArrow.SetActive(true);
                }
            }

            hasAutoProgressed = false;
            return;
        }

        if (directionText != null)
            directionText.text = currentDir.instruction;

        ShowTurnIcon(currentDir.turn);

        currentTargetNode = currentDir.destinationNode;

        if (compassArrow != null)
        {
            compassArrow.SetTargetNode(currentTargetNode);
            compassArrow.SetActive(true);
        }

        hasAutoProgressed = false;
        UpdateDirectionItemsStatus();
    }

    public void ShowTurnIconContainer()
    {
        if (turnIconsContainer != null)
        {
            turnIconsContainer.SetActive(true);
        }
    }

    private void HideAllTurnIcons()
    {
        if (turnIconsContainer != null) turnIconsContainer.SetActive(false);
    }

    private void ShowTurnIcon(TurnDirection turn)
    {
        HideAllTurnIcons();
        ShowTurnIconContainer();

        switch (turn)
        {
            case TurnDirection.Right:
            case TurnDirection.SlightRight:
                if (turnRightImage != null)
                {
                    turnRightImage.SetActive(true);
                }
                break;

            case TurnDirection.Left:
            case TurnDirection.SlightLeft:
                if (turnLeftImage != null)
                {
                    turnLeftImage.SetActive(true);
                }
                break;

            case TurnDirection.Straight:
                if (walkStraightImage != null)
                {
                    walkStraightImage.SetActive(true);
                }
                break;

            case TurnDirection.Enter:
            case TurnDirection.Arrive:
                if (enterImage != null)
                {
                    enterImage.SetActive(true);
                }
                break;

            default:
                if (walkStraightImage != null)
                {
                    walkStraightImage.SetActive(true);
                }
                break;
        }
    }

    private void MoveToNextDirection()
    {
        if (!isNavigationActive)
            return;

        if (currentDirectionIndex >= allDirections.Count - 1)
        {
            CompleteNavigation();
            return;
        }

        currentDirectionIndex++;
        DisplayCurrentDirection();
    }

    private void UpdateDistanceToTarget()
    {
        if (currentTargetNode == null)
            return;

        bool isIndoor = (arManager != null && arManager.IsIndoorMode()) ||
                        currentTargetNode.type == "indoorinfra";

        if (isIndoor)
        {
            Vector2 targetXY;
            if (currentTargetNode.indoor != null)
            {
                targetXY = new Vector2(currentTargetNode.indoor.x, currentTargetNode.indoor.y);
            }
            else
            {
                targetXY = new Vector2(currentTargetNode.x_coordinate, currentTargetNode.y_coordinate);
            }
            distanceToTarget = CalculateDistanceXY(userLocation, targetXY);
        }
        else
        {
            Vector2 targetGPS = new Vector2(currentTargetNode.latitude, currentTargetNode.longitude);
            distanceToTarget = CalculateDistanceGPS(userLocation, targetGPS);
        }
    }

    private void CheckAutoProgress()
    {
        if (hasAutoProgressed)
            return;

        if (currentDirectionIndex < allDirections.Count)
        {
            var currentDir = allDirections[currentDirectionIndex];
            if (currentDir.isIndoorGrouped)
            {
                return;
            }
        }

        if (distanceToTarget <= autoProgressDistance && distanceToTarget > 0)
        {
            hasAutoProgressed = true;
            MoveToNextDirection();
        }
    }

    private void UpdateDirectionItemsStatus()
    {
        for (int i = 0; i < directionItemInstances.Count; i++)
        {
            bool isCompleted = i < currentDirectionIndex;
            directionItemInstances[i].SetCompleted(isCompleted);
        }
    }

    private void CompleteNavigation()
    {
        isNavigationActive = false;

        if (directionText != null)
            directionText.text = "You have arrived at your destination!";

        HideAllTurnIcons();
        if (enterImage != null)
            enterImage.SetActive(true);

        if (compassArrow != null)
            compassArrow.SetActive(false);

        UpdateDirectionItemsStatus();
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

    private float CalculateDistanceXY(Vector2 point1, Vector2 point2)
    {
        return Vector2.Distance(point1, point2);
    }

    public void ResetNavigation()
    {
        currentDirectionIndex = 0;
        isNavigationActive = false;

        if (directionPanel != null)
            directionPanel.SetActive(false);

        HideAllTurnIcons();

        if (compassArrow != null)
            compassArrow.SetActive(false);

        UpdateDirectionItemsStatus();
    }

    public int GetCurrentDirectionIndex()
    {
        return currentDirectionIndex;
    }

    public NavigationDirection GetCurrentDirection()
    {
        if (currentDirectionIndex >= 0 && currentDirectionIndex < allDirections.Count)
            return allDirections[currentDirectionIndex];
        return null;
    }

    public List<NavigationDirection> GetAllDirections()
    {
        return new List<NavigationDirection>(allDirections);
    }
}