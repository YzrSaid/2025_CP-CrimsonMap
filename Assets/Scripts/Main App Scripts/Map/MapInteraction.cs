using UnityEngine;
using UnityEngine.EventSystems;
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
    
    private Vector2 lastPointerPosition;
    private Vector2 initialPointerPosition;
    private bool isDragging = false;
    private bool hasStartedDragging = false;

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

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log("✓ OnPointerDown called!"); // This should appear when you click
        
        lastPointerPosition = eventData.position;
        initialPointerPosition = eventData.position;
        isDragging = true;
        hasStartedDragging = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log("✓ OnPointerUp called!"); // This should appear when you release click
        
        isDragging = false;
        hasStartedDragging = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Debug.Log("✓ OnDrag called!"); // This should appear when you drag
        
        if (mapboxMap == null || !isDragging) return;

        // Check if we've moved enough to start dragging
        if (!hasStartedDragging)
        {
            float distanceFromStart = Vector2.Distance(eventData.position, initialPointerPosition);
            if (distanceFromStart < dragThreshold)
            {
                return;
            }
            hasStartedDragging = true;
            Debug.Log("✓ Started actual dragging!");
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
        
        Debug.Log($"Coordinate change: Lat {currentCenter.x:F6} -> {newCenter.x:F6}, Lng {currentCenter.y:F6} -> {newCenter.y:F6}");

        Debug.Log($"Updating map - Current Zoom: {mapboxMap.Zoom}, Delta: {deltaPosition}");

        // Update map center (preserve current zoom level instead of forcing to 18)
        mapboxMap.UpdateMap(newCenter, mapboxMap.Zoom);
    }

    public void OnScroll(PointerEventData eventData)
    {
        Debug.Log("✓ OnScroll called!"); // This should appear when you scroll
        
        if (mapboxMap == null) return;

        // Calculate zoom change
        float zoomDelta = eventData.scrollDelta.y * zoomSensitivity;
        ZoomMap(zoomDelta);
    }

    // Public methods for your zoom buttons
    public void ZoomIn()
    {
        Debug.Log("✓ ZoomIn button pressed!");
        ZoomMap(2f);
    }

    public void ZoomOut()
    {
        Debug.Log("✓ ZoomOut button pressed!");
        ZoomMap(-2f);
    }

    // Private method to handle zoom logic
    private void ZoomMap(float zoomDelta)
    {
        if (mapboxMap == null) return;

        float newZoom = mapboxMap.Zoom + zoomDelta;
        
        // Clamp zoom level
        newZoom = Mathf.Clamp(newZoom, minZoom, maxZoom);

        Debug.Log($"Zoom: {mapboxMap.Zoom} -> {newZoom}");

        // Update map with new zoom level
        mapboxMap.UpdateMap(mapboxMap.CenterLatitudeLongitude, newZoom);
    }
}