using UnityEngine;
using UnityEngine.UI;

public class CompassNavigationArrow : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform arrowTransform; // The inner arrow image

    [Header("Settings")]
    public float rotationSmoothSpeed = 5f;
    public bool enableDebugLogs = false;

    private Vector2 userLocation; // GPS: lat/lng, Offline: x/y
    private Node targetNode;
    private bool isActive = false;

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
            Debug.Log($"[CompassArrow] Localization Mode: {currentLocalizationMode}");

        // Start with compass enabled if using GPS
        if (currentLocalizationMode == LocalizationMode.GPS)
        {
            Input.compass.enabled = true;
            Input.location.Start();
        }
    }

    void Update()
    {
        if (!isActive || targetNode == null)
            return;

        UpdateUserLocation();
        UpdateArrowRotation();
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

    private void UpdateArrowRotation()
    {
        if (arrowTransform == null || targetNode == null)
            return;

        float targetAngle = 0f;

        if (currentLocalizationMode == LocalizationMode.GPS)
        {
            // Get GPS position and target bearing
            Vector2 targetLocation = new Vector2(targetNode.latitude, targetNode.longitude);
            float bearingToTarget = CalculateBearingGPS(userLocation, targetLocation);

            // ✅ Use tilt-compensated heading from GPSManager (not Input.compass)
            float deviceHeading = GPSManager.Instance != null ? GPSManager.Instance.GetHeading() : 0f;

            // Calculate angle difference (bearing - heading)
            targetAngle = bearingToTarget - deviceHeading;

            // Normalize to 0–360
            targetAngle = (targetAngle + 360f) % 360f;
        }
        else
        {
            // Offline mode (XY coordinates)
            Vector2 targetLocation = new Vector2(targetNode.x_coordinate, targetNode.y_coordinate);
            Vector2 direction = targetLocation - userLocation;

            // Calculate bearing relative to north (up = 0°)
            float bearingToTarget = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            targetAngle = (bearingToTarget + 360f) % 360f;
        }

        // Apply to arrow (rotate clockwise, so invert Z)
        Quaternion targetRotation = Quaternion.Euler(0, 0, -targetAngle);
        arrowTransform.rotation = Quaternion.Lerp(
            arrowTransform.rotation,
            targetRotation,
            Time.deltaTime * rotationSmoothSpeed
        );

        if (enableDebugLogs && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[CompassArrow] TargetAngle: {targetAngle:F1}°");
        }
    }


    /// <summary>
    /// Calculate bearing between two GPS coordinates (0° = North, clockwise)
    /// </summary>
    private float CalculateBearingGPS(Vector2 from, Vector2 to)
    {
        float lat1 = from.x * Mathf.Deg2Rad;
        float lat2 = to.x * Mathf.Deg2Rad;
        float deltaLng = (to.y - from.y) * Mathf.Deg2Rad;

        float y = Mathf.Sin(deltaLng) * Mathf.Cos(lat2);
        float x = Mathf.Cos(lat1) * Mathf.Sin(lat2) -
                  Mathf.Sin(lat1) * Mathf.Cos(lat2) * Mathf.Cos(deltaLng);

        float bearing = Mathf.Atan2(y, x) * Mathf.Rad2Deg;

        // Normalize to 0-360
        bearing = (bearing + 360f) % 360f;

        return bearing;
    }

    /// <summary>
    /// Set the target node to point towards
    /// </summary>
    public void SetTargetNode(Node node)
    {
        targetNode = node;
        isActive = (node != null);

        if (enableDebugLogs)
            Debug.Log($"[CompassArrow] Target set to: {node?.name ?? "None"}");
    }

    /// <summary>
    /// Show/hide the compass arrow
    /// </summary>
    public void SetActive(bool active)
    {
        isActive = active;
        gameObject.SetActive(active);
    }

    void OnDestroy()
    {
        // Disable compass when destroyed
        if (currentLocalizationMode == LocalizationMode.GPS)
        {
            Input.compass.enabled = false;
        }
    }
}