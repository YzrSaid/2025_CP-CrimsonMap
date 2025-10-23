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

    private Vector2 userLocation;
    private Node currentTargetNode;
    private float distanceToTarget = 0f;
    private bool hasAutoProgressed = false;

    void Start()
    {
        if (directionPanel != null)
            directionPanel.SetActive(false);

        if (debugPanel != null)
            debugPanel.SetActive(false);

        if (toggleDebugButton != null)
            toggleDebugButton.onClick.AddListener(ToggleDebugPanel);

        HideAllTurnIcons();

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

        if (GPSManager.Instance != null)
        {
            userLocation = GPSManager.Instance.GetSmoothedCoordinates();
            UpdateDistanceToTarget();
            CheckAutoProgress();
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

        for (int i = 0; i < directionCount; i++)
        {
            NavigationDirection dir = new NavigationDirection
            {
                instruction = PlayerPrefs.GetString($"ARNavigation_Direction_{i}_Instruction", ""),
                turn = (TurnDirection)System.Enum.Parse(typeof(TurnDirection), 
                       PlayerPrefs.GetString($"ARNavigation_Direction_{i}_Turn", "Straight")),
                distanceInMeters = PlayerPrefs.GetFloat($"ARNavigation_Direction_{i}_Distance", 0f),
            };

            string destNodeName = PlayerPrefs.GetString($"ARNavigation_Direction_{i}_DestNode", "");
            
            dir.destinationNode = new Node { name = destNodeName };

            allDirections.Add(dir);
        }

        UpdateDebugAllDirections();
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

        hasAutoProgressed = false;

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

        Vector2 targetLocation = new Vector2(currentTargetNode.latitude, currentTargetNode.longitude);
        distanceToTarget = CalculateDistance(userLocation, targetLocation);

        if (Time.frameCount % 60 == 0) 
        {
            DebugLog($"📏 Distance to target: {distanceToTarget:F1}m");
        }
    }

    private void CheckAutoProgress()
    {
        if (hasAutoProgressed)
            return;

        if (distanceToTarget <= autoProgressDistance && distanceToTarget > 0)
        {
            DebugLog($"✅ Auto-progressing - within {autoProgressDistance}m of target");
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

    public void ResetNavigation()
    {
        currentDirectionIndex = 0;
        isNavigationActive = false;

        if (directionPanel != null)
            directionPanel.SetActive(false);

        HideAllTurnIcons();

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