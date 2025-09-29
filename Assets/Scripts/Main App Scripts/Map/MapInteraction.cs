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
    public float dragSensitivity = 0.000001f;
    public float zoomSensitivity = 0.5f;
    public int minZoom = 18;
    public int maxZoom = 21;

    [Header("Drag Threshold")]
    public float dragThreshold = 0.5f;
    
    [Header("Pinch Zoom Settings")]
    public float pinchZoomSensitivity = 2.0f;
    public float pinchZoomDeadzone = 5.0f; // Minimum distance change to register as zoom
    public bool enablePinchZoomDebugging = false;
    
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

    // Input System references
    private InputAction touchPositionAction;
    private InputAction touchContactAction;

    private void OnEnable()
    {
        // Enable enhanced touch support
        EnhancedTouchSupport.Enable();
        
        // Set up input actions for touch
        touchPositionAction = new InputAction(type: InputActionType.PassThrough, binding: "<Touchscreen>/position");
        touchContactAction = new InputAction(type: InputActionType.PassThrough, binding: "<Touchscreen>/touch*/press");
        
        touchPositionAction.Enable();
        touchContactAction.Enable();
    }

    private void OnDisable()
    {
        // Clean up input actions
        touchPositionAction?.Disable();
        touchContactAction?.Disable();
        
        // Disable enhanced touch support
        EnhancedTouchSupport.Disable();
    }

    private void Start()
    {
        // Debug: Check if this is on the right component
        var rawImage = GetComponent<UnityEngine.UI.RawImage>();
        if (rawImage == null)
        {
            Debug.LogError("MapInteraction script must be on a RawImage component!");
        }
        else
        {
            Debug.Log("MapInteraction script correctly attached to RawImage");
        }

        // Make sure map is initialized
        if (mapboxMap != null)
        {
            Debug.Log($"Map initialized with zoom: {mapboxMap.Zoom}");
        }
        else
        {
            Debug.LogError("MapboxMap reference is null! Please assign it in the inspector.");
        }

        // Check for EventSystem
        if (FindObjectOfType<EventSystem>() == null)
        {
            Debug.LogError("No EventSystem found in scene! Create one: GameObject > UI > Event System");
        }
    }

    private void Update()
    {
        HandleMultiTouch();
    }

    private void HandleMultiTouch()
    {
        if (mapboxMap == null) return;

        // Use Enhanced Touch API for multi-touch
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
                // Start pinching
                isPinching = true;
                lastPinchDistance = currentDistance;
                lastPinchCenter = currentCenter;
                
                // Stop single finger dragging when pinch starts
                isDragging = false;
                hasStartedDragging = false;

                if (enablePinchZoomDebugging)
                {
                    Debug.Log($"[Pinch] Started - Initial distance: {currentDistance}");
                }
            }
            else
            {
                // Continue pinching
                float distanceDelta = currentDistance - lastPinchDistance;

                // Only process zoom if the distance change is significant enough
                if (Mathf.Abs(distanceDelta) > pinchZoomDeadzone)
                {
                    // Calculate zoom delta (positive = zoom in, negative = zoom out)
                    float zoomDelta = (distanceDelta / Screen.dpi) * pinchZoomSensitivity;
                    
                    // Apply zoom
                    ZoomMap(zoomDelta);

                    if (enablePinchZoomDebugging)
                    {
                        Debug.Log($"[Pinch] Distance: {currentDistance:F1}, Delta: {distanceDelta:F1}, Zoom Delta: {zoomDelta:F3}");
                    }

                    lastPinchDistance = currentDistance;
                }

                // Handle map panning during pinch (optional - you can remove this if you only want zoom)
                Vector2 centerDelta = currentCenter - lastPinchCenter;
                if (centerDelta.magnitude > 1f) // Only pan if center moved significantly
                {
                    // Convert screen movement to lat/lng offset
                    float latOffset = -centerDelta.y * dragSensitivity;
                    float lngOffset = -centerDelta.x * dragSensitivity;

                    // Get current center coordinates
                    var currentMapCenter = mapboxMap.CenterLatitudeLongitude;

                    // Validate the new coordinates
                    var newCenter = new Mapbox.Utils.Vector2d(
                        Mathf.Clamp((float)(currentMapCenter.x + latOffset), -85f, 85f),
                        currentMapCenter.y + lngOffset
                    );
                    
                    // Update map center
                    mapboxMap.UpdateMap(newCenter, mapboxMap.Zoom);
                    
                    lastPinchCenter = currentCenter;
                }
            }
        }
        else if (isPinching && activeTouches.Count < 2)
        {
            // End pinching
            isPinching = false;
            
            if (enablePinchZoomDebugging)
            {
                Debug.Log("[Pinch] Ended");
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Only handle single touch for dragging
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

        // Don't drag during pinch zoom
        var activeTouches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
        if (activeTouches.Count > 1) return;

        // Check if we've moved enough to start dragging
        if (!hasStartedDragging)
        {
            float distanceFromStart = Vector2.Distance(eventData.position, initialPointerPosition);
            if (distanceFromStart < dragThreshold)
            {
                return;
            }
            hasStartedDragging = true;
        }

        // Calculate delta movement
        Vector2 deltaPosition = eventData.position - lastPointerPosition;
        lastPointerPosition = eventData.position;

        // Convert screen movement to lat/lng offset
        float latOffset = -deltaPosition.y * dragSensitivity; // UP drag = go south
        float lngOffset = -deltaPosition.x * dragSensitivity; // LEFT drag = go west

        // Get current center coordinates
        var currentCenter = mapboxMap.CenterLatitudeLongitude;

        // Validate the new coordinates to prevent extreme values
        var newCenter = new Mapbox.Utils.Vector2d(
            Mathf.Clamp((float)(currentCenter.x + latOffset), -85f, 85f), // Clamp latitude
            currentCenter.y + lngOffset // Don't clamp longitude, it wraps around
        );
        
        // Update map center (preserve current zoom level instead of forcing to 18)
        mapboxMap.UpdateMap(newCenter, mapboxMap.Zoom);
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (mapboxMap == null) return;

        // Calculate zoom change
        float zoomDelta = eventData.scrollDelta.y * zoomSensitivity;
        ZoomMap(zoomDelta);
    }

    // Public methods for your zoom buttons
    public void ZoomIn()
    {
        ZoomMap(0.5f);
    }

    public void ZoomOut()
    {
        ZoomMap(-0.5f);
    }

    // Private method to handle zoom logic
    private void ZoomMap(float zoomDelta)
    {
        if (mapboxMap == null) return;

        float newZoom = mapboxMap.Zoom + zoomDelta;

        // Clamp zoom level
        newZoom = Mathf.Clamp(newZoom, minZoom, maxZoom);
        
        // Update map with new zoom level
        mapboxMap.UpdateMap(mapboxMap.CenterLatitudeLongitude, newZoom);
    }
    
    // MY LOCATION FEATURE 
    public void CenterOnMyLocation()
    {
        if (mapboxMap == null)
        {
            Debug.LogError("MapboxMap is null! Cannot center on location.");
            return;
        }

        if (GPSManager.Instance == null)
        {
            Debug.LogError("GPSManager not found!");
            return;
        }

        // Get coordinates from GPSManager
        Vector2 coords = useSmoothedCoordinates ? 
            GPSManager.Instance.GetSmoothedCoordinates() : 
            GPSManager.Instance.GetCoordinates();

        // Center the map
        var myLocation = new Mapbox.Utils.Vector2d(coords.x, coords.y);
        mapboxMap.UpdateMap(myLocation, myLocationZoomLevel);

        if (enableLocationDebugging)
        {
            Debug.Log($"[My Location] Centered on: {coords.x}, {coords.y} at zoom {myLocationZoomLevel}");
        }
    }
}