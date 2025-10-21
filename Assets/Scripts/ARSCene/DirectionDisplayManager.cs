using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem; // NEW: Import for new Input System
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
    public float autoProgressDistance = 5f; // meters to node before auto-progress
    public bool enableDebugLogs = true;

    private List<NavigationDirection> allDirections = new List<NavigationDirection>();
    private int currentDirectionIndex = 0;
    private bool isNavigationActive = false;
    private bool debugPanelVisible = false;

    // GPS tracking
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

        // Hide all turn icons initially
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

        // NEW: Keyboard testing using new Input System
        if (enableKeyboardTesting && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            DebugLog("⌨️ Space pressed - Moving to next direction");
            MoveToNextDirection();
        }

        // GPS-based auto progression
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
            DebugLog("⚠️ No directions found in PlayerPrefs");
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

            // Load destination node name
            string destNodeName = PlayerPrefs.GetString($"ARNavigation_Direction_{i}_DestNode", "");
            
            // Create a minimal node with just the name
            dir.destinationNode = new Node { name = destNodeName };

            allDirections.Add(dir);
        }

        DebugLog($"✅ Loaded {allDirections.Count} directions from PlayerPrefs");
        UpdateDebugAllDirections();
    }

    private void StartNavigation()
    {
        if (allDirections.Count == 0)
        {
            DebugLog("❌ Cannot start navigation - no directions loaded");
            return;
        }

        isNavigationActive = true;
        currentDirectionIndex = 0;

        if (directionPanel != null)
            directionPanel.SetActive(true);

        DisplayCurrentDirection();
        DebugLog($"🧭 Navigation started with {allDirections.Count} directions");
    }

    private void DisplayCurrentDirection()
    {
        if (currentDirectionIndex >= allDirections.Count)
        {
            CompleteNavigation();
            return;
        }

        NavigationDirection currentDir = allDirections[currentDirectionIndex];

        // Update instruction text
        if (directionText != null)
            directionText.text = currentDir.instruction;

        // Show appropriate turn icon
        ShowTurnIcon(currentDir.turn);

        // Set target node for GPS tracking
        currentTargetNode = currentDir.destinationNode;

        // Reset auto-progress flag
        hasAutoProgressed = false;

        DebugLog($"📍 Displaying direction {currentDirectionIndex + 1}/{allDirections.Count}: {currentDir.instruction}");
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
        // Hide all first
        HideAllTurnIcons();

        // Show the appropriate one
        switch (turn)
        {
            case TurnDirection.Right:
            case TurnDirection.SlightRight:
                if (turnRightImage != null)
                {
                    turnRightImage.SetActive(true);
                    DebugLog("➡️ Showing Turn Right icon");
                }
                break;

            case TurnDirection.Left:
            case TurnDirection.SlightLeft:
                if (turnLeftImage != null)
                {
                    turnLeftImage.SetActive(true);
                    DebugLog("⬅️ Showing Turn Left icon");
                }
                break;

            case TurnDirection.Straight:
                if (walkStraightImage != null)
                {
                    walkStraightImage.SetActive(true);
                    DebugLog("⬆️ Showing Walk Straight icon");
                }
                break;

            case TurnDirection.Enter:
            case TurnDirection.Arrive:
                if (enterImage != null)
                {
                    enterImage.SetActive(true);
                    DebugLog("🚪 Showing Enter icon");
                }
                break;

            default:
                if (walkStraightImage != null)
                {
                    walkStraightImage.SetActive(true);
                    DebugLog("⬆️ Showing Walk Straight icon (default)");
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
        
        // Update debug panel if visible
        if (debugPanelVisible)
            UpdateDebugAllDirections();
    }

    private void UpdateDistanceToTarget()
    {
        if (currentTargetNode == null)
            return;

        Vector2 targetLocation = new Vector2(currentTargetNode.latitude, currentTargetNode.longitude);
        distanceToTarget = CalculateDistance(userLocation, targetLocation);

        // Update debug log less frequently to avoid spam
        if (Time.frameCount % 60 == 0) // Every 60 frames (~1 second at 60fps)
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

        // Show enter icon for completion
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

        return 6371000 * c; // Earth radius in meters
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