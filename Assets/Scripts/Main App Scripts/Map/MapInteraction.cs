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

    [Header("My Location Settings")]
    public float myLocationZoomLevel = 20f;
    public bool useSmoothedCoordinates = true;

    private Vector2 lastPointerPosition;
    private Vector2 initialPointerPosition;
    private bool isDragging = false;
    private bool hasStartedDragging = false;

    private bool isPinching = false;
    private float lastPinchDistance = 0f;
    private Vector2 lastPinchCenter;

    private InputAction touchPositionAction;
    private InputAction touchContactAction;

    private UserIndicator userIndicator;

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();

        touchPositionAction = new InputAction(type: InputActionType.PassThrough, binding: "<Touchscreen>/position");
        touchContactAction = new InputAction(type: InputActionType.PassThrough, binding: "<Touchscreen>/touch*/press");

        touchPositionAction.Enable();
        touchContactAction.Enable();
    }

    private void OnDisable()
    {
        touchPositionAction?.Disable();
        touchContactAction?.Disable();
        
        EnhancedTouchSupport.Disable();
    }

    private void Start()
    {
        userIndicator = FindObjectOfType<UserIndicator>();
        
        if (mapboxMap != null)
        {
            mapboxMap.transform.rotation = Quaternion.identity;
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

            if (!isPinching)
            {
                isPinching = true;
                lastPinchDistance = currentDistance;
                lastPinchCenter = currentCenter;

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
                Vector2 centerDelta = currentCenter - lastPinchCenter;
                if (centerDelta.magnitude > 1f)
                {
                    float latOffset = -centerDelta.y * dragSensitivity;
                    float lngOffset = -centerDelta.x * dragSensitivity;

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
        else if (isPinching && activeTouches.Count < 2)
        {
            isPinching = false;
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

        float latOffset = -deltaPosition.y * dragSensitivity;
        float lngOffset = -deltaPosition.x * dragSensitivity;

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
        if (mapboxMap == null || GPSManager.Instance == null)
        {
            return;
        }

        Vector2 coords = useSmoothedCoordinates ?
            GPSManager.Instance.GetSmoothedCoordinates() :
            GPSManager.Instance.GetCoordinates();

        var myLocation = new Mapbox.Utils.Vector2d(coords.x, coords.y);

        mapboxMap.UpdateMap(myLocation, myLocationZoomLevel);
    }
}