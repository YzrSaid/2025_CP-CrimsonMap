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

    [Header("Compass Arrow")]
    public CompassNavigationArrow compassArrow;

    [Header("Debug UI")]
    public GameObject debugPanel;
    public TextMeshProUGUI debugAllDirectionsText;
    public Button toggleDebugButton;

    [Header("Settings")]
    public bool enableKeyboardTesting = true;
    public float autoProgressDistance = 5f;
    public bool enableDebugLogs = true;

    private List<NavigationDirection> allDirections = new List<NavigationDirection>();
    private int currentDirectionIndex = 0;
    private bool isNavigationActive = false;
    private bool debugPanelVisible = false;

    private Vector2 userLocation; // GPS: lat/lng, Offline: x/y
    private Node currentTargetNode;
    private float distanceToTarget = 0f;
    private bool hasAutoProgressed = false;

    // Localization mode
    private enum LocalizationMode { GPS, Offline }
    private LocalizationMode currentLocalizationMode = LocalizationMode.GPS;

    void Start()
    {
        // Determine localization mode
        string localizationModeString = PlayerPrefs.GetString("LocalizationMode", "GPS");
        currentLocalizationMode = localizationModeString == "Offline" 
            ? LocalizationMode.Offline 
            : LocalizationMode.GPS;

        if (enableDebugLogs)
            Debug.Log($"[DirectionDisplay] Localization Mode: {currentLocalizationMode}");

        if (directionPanel != null)
            directionPanel.SetActive(false);

        if (debugPanel != null)
            debugPanel.SetActive(false);

        if (toggleDebugButton != null)
            toggleDebugButton.onClick.AddListener(ToggleDebugPanel);

        HideAllTurnIcons();

        // Find compass arrow if not assigned
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

        // Keyboard testing
        if (enableKeyboardTesting && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            MoveToNextDirection();
        }

        // Update user location based on localization mode
        UpdateUserLocation();
        
        // Update distance to target
        UpdateDistanceToTarget();
        
        // Check if we should auto-progress
        CheckAutoProgress();
    }

    private void UpdateUserLocation()
    {
        if (currentLocalizationMode == LocalizationMode.GPS)
        {
            // GPS Mode: Get from GPSManager
            if (GPSManager.Instance != null)
            {
                userLocation = GPSManager.Instance.GetSmoothedCoordinates();
            }
        }
        else
        {
            // Offline Mode: Get from UnifiedARManager
            UnifiedARManager arManager = FindObjectOfType<UnifiedARManager>();
            if (arManager != null)
            {
                userLocation = arManager.GetUserXY();
            }
        }
    }

    private void LoadDirectionsFromPlayerPrefs()
    {
        int directionCount = PlayerPrefs.GetInt("ARNavigation_DirectionCount", 0);

        // ✅ DETAILED DEBUG
        Debug.Log($"[DirectionDisplay] ========== LOADING DIRECTIONS ==========");
        Debug.Log($"[DirectionDisplay] Direction count in PlayerPrefs: {directionCount}");

        if (directionCount == 0)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[DirectionDisplay] ❌ No directions found in PlayerPrefs");
            return;
        }

        // ✅ Log first direction as test
        string firstInstruction = PlayerPrefs.GetString("ARNavigation_Direction_0_Instruction", "NOT FOUND");
        Debug.Log($"[DirectionDisplay] First direction instruction: {firstInstruction}");

        allDirections.Clear();

        // Load map data to get full node info
        string mapId = PlayerPrefs.GetString("ARScene_MapId", "MAP-01");
        Debug.Log($"[DirectionDisplay] Loading map: {mapId}");

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

                    if (enableDebugLogs)
                        Debug.Log($"[DirectionDisplay] Loaded {allNodes.Length} nodes for directions");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DirectionDisplay] Error loading nodes: {e.Message}");
                    loadComplete = true;
                }
            },
            (error) =>
            {
                Debug.LogError($"[DirectionDisplay] Failed to load nodes: {error}");
                loadComplete = true;
            }
        ));

        yield return new WaitUntil(() => loadComplete);

        // Now load directions with proper node references
        for (int i = 0; i < directionCount; i++)
        {
            NavigationDirection dir = new NavigationDirection
            {
                instruction = PlayerPrefs.GetString($"ARNavigation_Direction_{i}_Instruction", ""),
                turn = (TurnDirection)System.Enum.Parse(typeof(TurnDirection),
                       PlayerPrefs.GetString($"ARNavigation_Direction_{i}_Turn", "Straight")),
                distanceInMeters = PlayerPrefs.GetFloat($"ARNavigation_Direction_{i}_Distance", 0f),
            };

            // ✅ Load full node data using node ID
            string destNodeId = PlayerPrefs.GetString($"ARNavigation_Direction_{i}_DestNodeId", "");

            if (allNodes != null && !string.IsNullOrEmpty(destNodeId))
            {
                // Find the full node data
                dir.destinationNode = System.Array.Find(allNodes, n => n.node_id == destNodeId);

                if (dir.destinationNode == null)
                {
                    // Fallback: create minimal node with just name
                    dir.destinationNode = new Node
                    {
                        name = PlayerPrefs.GetString($"ARNavigation_Direction_{i}_DestNode", "Unknown"),
                        node_id = destNodeId
                    };
                }
            }
            else
            {
                // Fallback if no node ID
                dir.destinationNode = new Node
                {
                    name = PlayerPrefs.GetString($"ARNavigation_Direction_{i}_DestNode", "Unknown")
                };
            }

            allDirections.Add(dir);
        }

        if (enableDebugLogs)
            Debug.Log($"[DirectionDisplay] ✅ Loaded {allDirections.Count} directions with full node data");

        UpdateDebugAllDirections();

        // Start navigation after loading
        if (allDirections.Count > 0)
        {
            StartNavigation();
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

        if (directionText != null)
            directionText.text = currentDir.instruction;

        ShowTurnIcon(currentDir.turn);

        currentTargetNode = currentDir.destinationNode;

        // ✅ Update compass arrow to point to current target
        if (compassArrow != null)
        {
            compassArrow.SetTargetNode(currentTargetNode);
            compassArrow.SetActive(true);
        }

        hasAutoProgressed = false;

        if (enableDebugLogs)
        {
            Debug.Log($"[DirectionDisplay] 🎯 Direction {currentDirectionIndex + 1}/{allDirections.Count}");
            Debug.Log($"[DirectionDisplay] Target: {currentTargetNode?.name ?? "Unknown"}");
            if (currentTargetNode != null)
            {
                if (currentLocalizationMode == LocalizationMode.GPS)
                {
                    Debug.Log($"[DirectionDisplay] Target GPS: {currentTargetNode.latitude:F6}, {currentTargetNode.longitude:F6}");
                }
                else
                {
                    Debug.Log($"[DirectionDisplay] Target XY: {currentTargetNode.x_coordinate:F2}, {currentTargetNode.y_coordinate:F2}");
                }
            }
        }
    }

    private void HideAllTurnIcons()
    {
        if (turnRightImage != null) turnRightImage.SetActive(false);
        if (turnLeftImage != null) turnLeftImage.SetActive(false);
        if (walkStraightImage != null) walkStraightImage.SetActive(false);
        if (enterImage != null) enterImage.SetActive(false);
    }

    private void ShowTurnIcon(TurnDirection turn)
    {
        HideAllTurnIcons();

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

        if (debugPanelVisible)
            UpdateDebugAllDirections();
    }

    private void UpdateDistanceToTarget()
    {
        if (currentTargetNode == null)
            return;

        if (currentLocalizationMode == LocalizationMode.GPS)
        {
            // GPS Mode
            Vector2 targetLocation = new Vector2(currentTargetNode.latitude, currentTargetNode.longitude);
            distanceToTarget = CalculateDistanceGPS(userLocation, targetLocation);
        }
        else
        {
            // Offline Mode
            Vector2 targetLocation = new Vector2(currentTargetNode.x_coordinate, currentTargetNode.y_coordinate);
            distanceToTarget = CalculateDistanceXY(userLocation, targetLocation);
        }

        if (Time.frameCount % 60 == 0 && enableDebugLogs)
        {
            DebugLog($"📏 Distance to target ({currentTargetNode.name}): {distanceToTarget:F1}m");
        }
    }

    private void CheckAutoProgress()
    {
        if (hasAutoProgressed)
            return;

        if (distanceToTarget <= autoProgressDistance && distanceToTarget > 0)
        {
            DebugLog($"✅ Auto-progressing - within {autoProgressDistance}m of {currentTargetNode.name}");
            hasAutoProgressed = true;
            MoveToNextDirection();
        }
    }

    private void CompleteNavigation()
    {
        isNavigationActive = false;

        if (directionText != null)
            directionText.text = "🎉 You have arrived at your destination!";

        HideAllTurnIcons();
        if (enterImage != null)
            enterImage.SetActive(true);

        // Hide compass arrow
        if (compassArrow != null)
            compassArrow.SetActive(false);

        DebugLog("🎯 Navigation completed!");
    }

    private void ToggleDebugPanel()
    {
        debugPanelVisible = !debugPanelVisible;

        if (debugPanel != null)
            debugPanel.SetActive(debugPanelVisible);

        if (debugPanelVisible)
            UpdateDebugAllDirections();
    }

    private void UpdateDebugAllDirections()
    {
        if (debugAllDirectionsText == null)
            return;

        if (allDirections.Count == 0)
        {
            debugAllDirectionsText.text = "No directions loaded";
            return;
        }

        string debugText = $"<b>🧭 ALL DIRECTIONS ({allDirections.Count} total)</b>\n\n";

        for (int i = 0; i < allDirections.Count; i++)
        {
            var dir = allDirections[i];
            string highlight = (i == currentDirectionIndex) ? "<color=yellow>→ </color>" : "  ";
            string iconEmoji = GetIconEmoji(dir.turn);

            debugText += $"{highlight}<b>{i + 1}.</b> {iconEmoji} {dir.turn}\n";
            debugText += $"     {dir.instruction}\n";
            debugText += $"     Distance: {dir.distanceInMeters:F0}m | To: {dir.destinationNode?.name ?? "Unknown"}\n\n";
        }

        debugAllDirectionsText.text = debugText;
    }

    private string GetIconEmoji(TurnDirection turn)
    {
        switch (turn)
        {
            case TurnDirection.Right:
            case TurnDirection.SlightRight:
                return "➡️";
            case TurnDirection.Left:
            case TurnDirection.SlightLeft:
                return "⬅️";
            case TurnDirection.Straight:
                return "⬆️";
            case TurnDirection.Enter:
            case TurnDirection.Arrive:
                return "🚪";
            default:
                return "⬆️";
        }
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

        DebugLog("🔄 Navigation reset");
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

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[DirectionDisplay] {message}");
    }

    void OnDestroy()
    {
        if (toggleDebugButton != null)
            toggleDebugButton.onClick.RemoveListener(ToggleDebugPanel);
    }
}