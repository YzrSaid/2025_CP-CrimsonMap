using UnityEngine;
using UnityEngine.UI;

public class CompassNavigationArrow : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform arrowTransform;

    [Header("Settings")]
    public float rotationSmoothSpeed = 5f;
    public bool enableDebugLogs = false;

    private Vector2 userLocation;
    private Node targetNode;
    private bool isActive = false;
    private UnifiedARManager arManager;

    void Start()
    {
        arManager = FindObjectOfType<UnifiedARManager>();
        
        // Always enable compass for outdoor GPS mode
        Input.compass.enabled = true;
        Input.location.Start();
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
        if (arManager != null)
        {
            userLocation = arManager.GetUserXY();
        }
        else if (GPSManager.Instance != null)
        {
            userLocation = GPSManager.Instance.GetSmoothedCoordinates();
        }
    }

    private void UpdateArrowRotation()
    {
        if (arrowTransform == null || targetNode == null)
            return;

        bool isIndoor = (arManager != null && arManager.IsIndoorMode()) || 
                        targetNode.type == "indoorinfra";

        float targetAngle = 0f;

        if (isIndoor)
        {
            // Indoor mode - use X,Y coordinates
            Vector2 targetXY;
            if (targetNode.indoor != null)
            {
                targetXY = new Vector2(targetNode.indoor.x, targetNode.indoor.y);
            }
            else
            {
                targetXY = new Vector2(targetNode.x_coordinate, targetNode.y_coordinate);
            }

            Vector2 direction = targetXY - userLocation;

            // Calculate bearing in X,Y space
            // Assuming Y is forward (north) and X is right (east)
            float bearingToTarget = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            
            // Get device heading from AR camera if available
            float deviceHeading = 0f;
            if (arManager != null && Camera.main != null)
            {
                // Use AR camera's Y rotation as heading
                deviceHeading = Camera.main.transform.eulerAngles.y;
            }

            targetAngle = bearingToTarget - deviceHeading;
            targetAngle = (targetAngle + 360f) % 360f;
        }
        else
        {
            // Outdoor mode - use GPS coordinates
            Vector2 targetGPS = new Vector2(targetNode.latitude, targetNode.longitude);
            float bearingToTarget = CalculateBearingGPS(userLocation, targetGPS);

            float deviceHeading = GPSManager.Instance != null ? GPSManager.Instance.GetHeading() : 0f;

            targetAngle = bearingToTarget - deviceHeading;
            targetAngle = (targetAngle + 360f) % 360f;
        }

        Quaternion targetRotation = Quaternion.Euler(0, 0, -targetAngle);
        arrowTransform.rotation = Quaternion.Lerp(
            arrowTransform.rotation,
            targetRotation,
            Time.deltaTime * rotationSmoothSpeed
        );

        if (enableDebugLogs)
        {
            string mode = isIndoor ? "Indoor" : "Outdoor";
            Debug.Log($"CompassArrow ({mode}): Target angle = {targetAngle:F1}°");
        }
    }

    private float CalculateBearingGPS(Vector2 from, Vector2 to)
    {
        float lat1 = from.x * Mathf.Deg2Rad;
        float lat2 = to.x * Mathf.Deg2Rad;
        float deltaLng = (to.y - from.y) * Mathf.Deg2Rad;

        float y = Mathf.Sin(deltaLng) * Mathf.Cos(lat2);
        float x = Mathf.Cos(lat1) * Mathf.Sin(lat2) -
                  Mathf.Sin(lat1) * Mathf.Cos(lat2) * Mathf.Cos(deltaLng);

        float bearing = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
        bearing = (bearing + 360f) % 360f;

        return bearing;
    }

    public void SetTargetNode(Node node)
    {
        targetNode = node;
        isActive = (node != null);
        
        if (enableDebugLogs && node != null)
        {
            string nodeType = node.type == "indoorinfra" ? "Indoor" : "Outdoor";
            Debug.Log($"CompassArrow: Target set to {node.name} ({nodeType})");
        }
    }

    public void SetActive(bool active)
    {
        isActive = active;
        gameObject.SetActive(active);
    }

    void OnDestroy()
    {
        Input.compass.enabled = false;
    }
}