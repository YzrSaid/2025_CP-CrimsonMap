using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Mapbox.Map;
using Mapbox.Unity.Map;

public class MapInteraction : MonoBehaviour, IDragHandler, IScrollHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Map References")]
    public AbstractMap mapboxMap;

    [Header("Interaction Settings")]
    public float dragSensitivity = 0.000002f;
    public float zoomSensitivity = 0.5f;
    public int minZoom = 18;
    public int maxZoom = 21;

    [Header("Drag Threshold")]
    public float dragThreshold = 0.5f;

    [Header("Pinch Zoom Settings")]
    public float pinchZoomSensitivity = 2.0f;
    public float pinchZoomDeadzone = 5.0f;
    public bool enablePinchZoomDebugging = false;

    [Header("Rotation Settings")]
    public bool enableTwoFingerRotation = true;
    public float rotationSensitivity = 0.5f;
    public float rotationDeadzone = 2.0f;
    public bool enableRotationDebugging = false;

    [Header("My Location Settings")]
    public float myLocationZoomLevel = 20f;
    public bool useSmoothedCoordinates = true;
    public bool enableLocationDebugging = true;

    // Single touch drag variables
    private Vector2 lastPointerPosition;
    private Vector2 initialPointerPosition;
    private bool isDragging = false;
    private bool hasStartedDragging = false;

    // Multi-touch pinch zoom variables
    private bool isPinching = false;
    private float lastPinchDistance = 0f;
    private Vector2 lastPinchCenter;

    // Rotation variables
    private bool isRotating = false;
    private float lastRotationAngle = 0f;
    private float currentMapBearing = 0f;

    // Input System references
    private InputAction touchPositionAction;
    private InputAction touchContactAction;

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();

        touchPositionAction = new InputAction(type: InputActionType.PassThrough, binding: "<Touchscreen>/position");
        touchContactAction = new InputAction(type: InputActionType.PassThrough, binding: "<Touchscreen>/touch*/press");

        touchPositionAction.Enable();
        touchContactAction.Enable();

        // Enable compass hardware
        Input.compass.enabled = true;
    }

    private void OnDisable()
    {
        touchPositionAction?.Disable();
        touchContactAction?.Disable();
        EnhancedTouchSupport.Disable();
    }

    private void Start()
    {
        var rawImage = GetComponent<UnityEngine.UI.RawImage>();
        if (rawImage == null)
        {
            Debug.LogError("MapInteraction script must be on a RawImage component!");
        }

        if (mapboxMap != null)
        {
            Debug.Log($"Map initialized with zoom: {mapboxMap.Zoom}");
            currentMapBearing = 0f;
        }
        else
        {
            Debug.LogError("MapboxMap reference is null!");
        }

        if (FindObjectOfType<EventSystem>() == null)
        {
            Debug.LogError("No EventSystem found in scene!");
        }
    }

    private void Update()
    {
        HandleMultiTouch();
    }

    private void HandleMultiTouch()
    {
        if (mapboxMap == null) return;

        var activeTouches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;

        if (activeTouches.Count == 2)
        {
            var touch1 = activeTouches[0];
            var touch2 = activeTouches[1];

            Vector2 touch1Pos = touch1.screenPosition;
            Vector2 touch2Pos = touch2.screenPosition;

            float currentDistance = Vector2.Distance(touch1Pos, touch2Pos);
            Vector2 currentCenter = (touch1Pos + touch2Pos) / 2f;

            // Calculate rotation angle
            Vector2 currentDirection = (touch2Pos - touch1Pos).normalized;
            float currentAngle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;

            if (!isPinching && !isRotating)
            {
                // Start pinching/rotating
                isPinching = true;
                isRotating = enableTwoFingerRotation;
                lastPinchDistance = currentDistance;
                lastPinchCenter = currentCenter;
                lastRotationAngle = currentAngle;

                // Stop single finger dragging
                isDragging = false;
                hasStartedDragging = false;

                if (enablePinchZoomDebugging)
                {
                    Debug.Log($"[Pinch] Started - Distance: {currentDistance}");
                }
                if (enableRotationDebugging && isRotating)
                {
                    Debug.Log($"[Rotation] Started - Angle: {currentAngle:F1}°");
                }
            }
            else
            {
                // Handle Zoom
                float distanceDelta = currentDistance - lastPinchDistance;
                if (Mathf.Abs(distanceDelta) > pinchZoomDeadzone)
                {
                    float zoomDelta = (distanceDelta / Screen.dpi) * pinchZoomSensitivity;
                    ZoomMap(zoomDelta);
                    lastPinchDistance = currentDistance;
                }

                // Handle Rotation
                if (isRotating && enableTwoFingerRotation)
                {
                    float angleDelta = Mathf.DeltaAngle(lastRotationAngle, currentAngle);

                    if (Mathf.Abs(angleDelta) > rotationDeadzone)
                    {
                        // Apply rotation - negative because screen rotation is opposite
                        currentMapBearing -= angleDelta * rotationSensitivity;

                        // Normalize bearing to -180 to 180
                        while (currentMapBearing > 180f) currentMapBearing -= 360f;
                        while (currentMapBearing < -180f) currentMapBearing += 360f;

                        // ✅ Y-AXIS ROTATION for 3D map
                        mapboxMap.transform.rotation = Quaternion.Euler(0, currentMapBearing, 0);

                        if (enableRotationDebugging)
                        {
                            Debug.Log($"[Rotation] Angle Delta: {angleDelta:F1}°, Map Bearing: {currentMapBearing:F1}°");
                        }

                        lastRotationAngle = currentAngle;
                    }
                }

                // Handle Panning during pinch
                Vector2 centerDelta = currentCenter - lastPinchCenter;
                if (centerDelta.magnitude > 1f)
                {
                    // Transform the screen delta based on current map rotation
                    Vector2 rotatedDelta = RotateVector2(centerDelta, -currentMapBearing);

                    float latOffset = -rotatedDelta.y * dragSensitivity;
                    float lngOffset = -rotatedDelta.x * dragSensitivity;

                    var currentMapCenter = mapboxMap.CenterLatitudeLongitude;
                    var newCenter = new Mapbox.Utils.Vector2d(
                        Mathf.Clamp((float)(currentMapCenter.x + latOffset), -85f, 85f),
                        currentMapCenter.y + lngOffset
                    );

                    mapboxMap.UpdateMap(newCenter, mapboxMap.Zoom);
                    lastPinchCenter = currentCenter;
                }
            }
        }
        else if ((isPinching || isRotating) && activeTouches.Count < 2)
        {
            // End pinching/rotating
            isPinching = false;
            isRotating = false;

            if (enablePinchZoomDebugging)
            {
                Debug.Log("[Pinch] Ended");
            }
            if (enableRotationDebugging)
            {
                Debug.Log($"[Rotation] Ended - Final Bearing: {currentMapBearing:F1}°");
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        var activeTouches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
        if (activeTouches.Count <= 1)
        {
            lastPointerPosition = eventData.position;
            initialPointerPosition = eventData.position;
            isDragging = true;
            hasStartedDragging = false;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        hasStartedDragging = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (mapboxMap == null || !isDragging || isPinching) return;

        var activeTouches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
        if (activeTouches.Count > 1) return;

        if (!hasStartedDragging)
        {
            float distanceFromStart = Vector2.Distance(eventData.position, initialPointerPosition);
            if (distanceFromStart < dragThreshold)
            {
                return;
            }
            hasStartedDragging = true;
        }



        Vector2 deltaPosition = eventData.position - lastPointerPosition;
        lastPointerPosition = eventData.position;

        // ✅ FIX: Account for map rotation when dragging
        Vector2 rotatedDelta = RotateVector2(deltaPosition, -currentMapBearing);

        float latOffset = -rotatedDelta.y * dragSensitivity;
        float lngOffset = -rotatedDelta.x * dragSensitivity;

        var currentCenter = mapboxMap.CenterLatitudeLongitude;
        var newCenter = new Mapbox.Utils.Vector2d(
            Mathf.Clamp((float)(currentCenter.x + latOffset), -85f, 85f),
            currentCenter.y + lngOffset
        );

        mapboxMap.UpdateMap(newCenter, mapboxMap.Zoom);
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (mapboxMap == null) return;
        float zoomDelta = eventData.scrollDelta.y * zoomSensitivity;
        ZoomMap(zoomDelta);
    }
    private Vector2 RotateVector2(Vector2 v, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        return new Vector2(
            v.x * cos - v.y * sin,
            v.x * sin + v.y * cos
        );
    }

    public void ZoomIn()
    {
        ZoomMap(0.5f);
    }

    public void ZoomOut()
    {
        ZoomMap(-0.5f);
    }

    private void ZoomMap(float zoomDelta)
    {
        if (mapboxMap == null) return;

        float newZoom = mapboxMap.Zoom + zoomDelta;
        newZoom = Mathf.Clamp(newZoom, minZoom, maxZoom);

        mapboxMap.UpdateMap(mapboxMap.CenterLatitudeLongitude, newZoom);
    }

    public void CenterOnMyLocation()
    {
        if (mapboxMap == null)
        {
            Debug.LogError("MapboxMap is null!");
            return;
        }

        if (GPSManager.Instance == null)
        {
            Debug.LogError("GPSManager not found!");
            return;
        }

        Vector2 coords = useSmoothedCoordinates ?
            GPSManager.Instance.GetSmoothedCoordinates() :
            GPSManager.Instance.GetCoordinates();

        var myLocation = new Mapbox.Utils.Vector2d(coords.x, coords.y);

        // Center map and preserve rotation
        mapboxMap.UpdateMap(myLocation, myLocationZoomLevel);
        mapboxMap.transform.rotation = Quaternion.Euler(0, currentMapBearing, 0);

        if (enableLocationDebugging)
        {
            Debug.Log($"[My Location] Centered on: {coords.x}, {coords.y} at zoom {myLocationZoomLevel}");
        }
    }

    public void ResetMapBearing()
    {
        currentMapBearing = 0f;

        if (mapboxMap != null)
        {
            mapboxMap.transform.rotation = Quaternion.identity;
        }

        Debug.Log("[Map] Bearing reset to North (0°)");
    }

    public float GetCurrentBearing()
    {
        return currentMapBearing;
    }
}