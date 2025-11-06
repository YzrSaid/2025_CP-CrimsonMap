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

    [Header("Keyboard Controls (Editor Testing)")]
    public bool enableKeyboardControls = true;
    public float keyboardRotationSpeed = 30f;

    [Header("My Location Settings")]
    public float myLocationZoomLevel = 20f;
    public bool useSmoothedCoordinates = true;
    public bool enableLocationDebugging = true;

    private Vector2 lastPointerPosition;
    private Vector2 initialPointerPosition;
    private bool isDragging = false;
    private bool hasStartedDragging = false;

    private bool isPinching = false;
    private float lastPinchDistance = 0f;
    private Vector2 lastPinchCenter;

    private bool isRotating = false;
    private float lastRotationAngle = 0f;
    private float currentMapBearing = 0f;

    private InputAction touchPositionAction;
    private InputAction touchContactAction;
    
    private InputAction rotateLeftAction;
    private InputAction rotateRightAction;

    private UserIndicator userIndicator;

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();

        touchPositionAction = new InputAction(type: InputActionType.PassThrough, binding: "<Touchscreen>/position");
        touchContactAction = new InputAction(type: InputActionType.PassThrough, binding: "<Touchscreen>/touch*/press");

        touchPositionAction.Enable();
        touchContactAction.Enable();

        if (enableKeyboardControls)
        {
            rotateLeftAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/q");
            rotateRightAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/e");
            
            rotateLeftAction.Enable();
            rotateRightAction.Enable();
        }

        Input.compass.enabled = true;
    }

    private void OnDisable()
    {
        touchPositionAction?.Disable();
        touchContactAction?.Disable();
        
        rotateLeftAction?.Disable();
        rotateRightAction?.Disable();
        
        EnhancedTouchSupport.Disable();
    }

    private void Start()
    {
        var rawImage = GetComponent<UnityEngine.UI.RawImage>();
        if (rawImage == null)
        {
        }

        if (mapboxMap != null)
        {
            currentMapBearing = 0f;
        }
        else
        {
        }

        if (FindObjectOfType<EventSystem>() == null)
        {
        }

        userIndicator = FindObjectOfType<UserIndicator>();
    }

    private void Update()
    {
        HandleMultiTouch();
        HandleKeyboardControls();
    }

    private void HandleKeyboardControls()
    {
        if (!enableKeyboardControls || mapboxMap == null) return;

        if (rotateLeftAction != null && rotateLeftAction.IsPressed())
        {
            RotateMap(-keyboardRotationSpeed * Time.deltaTime);
        }
        
        if (rotateRightAction != null && rotateRightAction.IsPressed())
        {
            RotateMap(keyboardRotationSpeed * Time.deltaTime);
        }
    }

    private void RotateMap(float angleDelta)
    {
        currentMapBearing += angleDelta;

        while (currentMapBearing > 180f) currentMapBearing -= 360f;
        while (currentMapBearing < -180f) currentMapBearing += 360f;

        if (mapboxMap != null)
        {
            mapboxMap.transform.rotation = Quaternion.Euler(0, currentMapBearing, 0);
        }
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

            Vector2 currentDirection = (touch2Pos - touch1Pos).normalized;
            float currentAngle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;

            if (!isPinching && !isRotating)
            {
                isPinching = true;
                isRotating = enableTwoFingerRotation;
                lastPinchDistance = currentDistance;
                lastPinchCenter = currentCenter;
                lastRotationAngle = currentAngle;

                isDragging = false;
                hasStartedDragging = false;

                NotifyUserIndicatorDragStart();
            }
            else
            {
                float distanceDelta = currentDistance - lastPinchDistance;
                if (Mathf.Abs(distanceDelta) > pinchZoomDeadzone)
                {
                    float zoomDelta = (distanceDelta / Screen.dpi) * pinchZoomSensitivity;
                    ZoomMap(zoomDelta);
                    lastPinchDistance = currentDistance;
                }

                if (isRotating && enableTwoFingerRotation)
                {
                    float angleDelta = Mathf.DeltaAngle(lastRotationAngle, currentAngle);

                    if (Mathf.Abs(angleDelta) > rotationDeadzone)
                    {
                        RotateMap(-angleDelta * rotationSensitivity);
                        lastRotationAngle = currentAngle;
                    }
                }

                Vector2 centerDelta = currentCenter - lastPinchCenter;
                if (centerDelta.magnitude > 1f)
                {
                    Vector2 rotatedDelta = RotateVector2(centerDelta, currentMapBearing);

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
            isPinching = false;
            isRotating = false;
            NotifyUserIndicatorDragEnd();
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
        if (isDragging)
        {
            isDragging = false;
            hasStartedDragging = false;
            NotifyUserIndicatorDragEnd();
        }
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
            NotifyUserIndicatorDragStart();
        }

        Vector2 deltaPosition = eventData.position - lastPointerPosition;
        lastPointerPosition = eventData.position;

        Vector2 rotatedDelta = RotateVector2(deltaPosition, currentMapBearing);

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

    private void NotifyUserIndicatorDragStart()
    {
        if (userIndicator != null)
        {
            userIndicator.SetMapDragging(true);
        }
    }

    private void NotifyUserIndicatorDragEnd()
    {
        if (userIndicator != null)
        {
            userIndicator.SetMapDragging(false);
        }
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
            return;
        }

        if (GPSManager.Instance == null)
        {
            return;
        }

        Vector2 coords = useSmoothedCoordinates ?
            GPSManager.Instance.GetSmoothedCoordinates() :
            GPSManager.Instance.GetCoordinates();

        var myLocation = new Mapbox.Utils.Vector2d(coords.x, coords.y);

        mapboxMap.UpdateMap(myLocation, myLocationZoomLevel);
        mapboxMap.transform.rotation = Quaternion.Euler(0, currentMapBearing, 0);
    }

    public void ResetMapBearing()
    {
        currentMapBearing = 0f;

        if (mapboxMap != null)
        {
            mapboxMap.transform.rotation = Quaternion.identity;
        }
    }

    public float GetCurrentBearing()
    {
        return currentMapBearing;
    }
}