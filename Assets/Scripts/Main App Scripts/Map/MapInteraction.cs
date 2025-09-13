// using UnityEngine;
// using UnityEngine.EventSystems;
// using Mapbox.Map;
// using Mapbox.Unity.Map;

// public class MapInteraction : MonoBehaviour, IDragHandler, IScrollHandler, IPointerDownHandler, IPointerUpHandler
// {
//     [Header("Map References")]
//     public AbstractMap mapboxMap;

//     [Header("Interaction Settings")]
//     public float dragSensitivity = 0.000001f;
//     public float zoomSensitivity = 0.5f;
//     public int minZoom = 18;
//     public int maxZoom = 21;

//     [Header("Drag Threshold")]
//     public float dragThreshold = 0.5f;

//     private Vector2 lastPointerPosition;
//     private Vector2 initialPointerPosition;
//     private bool isDragging = false;
//     private bool hasStartedDragging = false;

//     private void Start()
//     {
//         // Debug: Check if this is on the right component
//         var rawImage = GetComponent<UnityEngine.UI.RawImage>();
//         if (rawImage == null)
//         {
//             Debug.LogError("MapInteraction script must be on a RawImage component!");
//         }
//         else
//         {
//             Debug.Log("MapInteraction script correctly attached to RawImage");
//         }

//         // Make sure map is initialized
//         if (mapboxMap != null)
//         {
//             Debug.Log($"Map initialized with zoom: {mapboxMap.Zoom}");
//         }
//         else
//         {
//             Debug.LogError("MapboxMap reference is null! Please assign it in the inspector.");
//         }

//         // Check for EventSystem
//         if (FindObjectOfType<EventSystem>() == null)
//         {
//             Debug.LogError("No EventSystem found in scene! Create one: GameObject > UI > Event System");
//         }
//     }

//     public void OnPointerDown(PointerEventData eventData)
//     {
//         lastPointerPosition = eventData.position;
//         initialPointerPosition = eventData.position;
//         isDragging = true;
//         hasStartedDragging = false;
//     }

//     public void OnPointerUp(PointerEventData eventData)
//     {
//         isDragging = false;
//         hasStartedDragging = false;
//     }

//     public void OnDrag(PointerEventData eventData)
//     {
//         if (mapboxMap == null || !isDragging) return;

//         // Check if we've moved enough to start dragging
//         if (!hasStartedDragging)
//         {
//             float distanceFromStart = Vector2.Distance(eventData.position, initialPointerPosition);
//             if (distanceFromStart < dragThreshold)
//             {
//                 return;
//             }
//             hasStartedDragging = true;
//         }

//         // Calculate delta movement
//         Vector2 deltaPosition = eventData.position - lastPointerPosition;
//         lastPointerPosition = eventData.position;

//         // Convert screen movement to lat/lng offset
//         float latOffset = -deltaPosition.y * dragSensitivity; // UP drag = go south
//         float lngOffset = -deltaPosition.x * dragSensitivity; // LEFT drag = go west

//         // Get current center coordinates
//         var currentCenter = mapboxMap.CenterLatitudeLongitude;

//         // Validate the new coordinates to prevent extreme values
//         var newCenter = new Mapbox.Utils.Vector2d(
//             Mathf.Clamp((float)(currentCenter.x + latOffset), -85f, 85f), // Clamp latitude
//             currentCenter.y + lngOffset // Don't clamp longitude, it wraps around
//         );
//         // Update map center (preserve current zoom level instead of forcing to 18)
//         mapboxMap.UpdateMap(newCenter, mapboxMap.Zoom);
//     }

//     public void OnScroll(PointerEventData eventData)
//     {
//         if (mapboxMap == null) return;

//         // Calculate zoom change
//         float zoomDelta = eventData.scrollDelta.y * zoomSensitivity;
//         ZoomMap(zoomDelta);
//     }

//     // Public methods for your zoom buttons
//     public void ZoomIn()
//     {
//         ZoomMap(1f);
//     }

//     public void ZoomOut()
//     {
//         ZoomMap(-1f);
//     }

//     // Private method to handle zoom logic
//     private void ZoomMap(float zoomDelta)
//     {
//         if (mapboxMap == null) return;

//         float newZoom = mapboxMap.Zoom + zoomDelta;

//         // Clamp zoom level
//         newZoom = Mathf.Clamp(newZoom, minZoom, maxZoom);
        
//         // Update map with new zoom level
//         mapboxMap.UpdateMap(mapboxMap.CenterLatitudeLongitude, newZoom);
//     }
// }

using UnityEngine;
using UnityEngine.EventSystems;
using Mapbox.Map;
using Mapbox.Unity.Map;

public class MapInteraction : MonoBehaviour, IDragHandler, IScrollHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Map References")]
    public AbstractMap mapboxMap;
    
    [Header("Path Renderer Reference")]
    public PathRenderer pathRenderer; // Add reference to PathRenderer

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
        
        // Auto-find PathRenderer if not assigned
        if (pathRenderer == null)
        {
            pathRenderer = FindObjectOfType<PathRenderer>();
            if (pathRenderer != null)
            {
                Debug.Log("PathRenderer found automatically");
            }
            else
            {
                Debug.LogWarning("PathRenderer not found. Path updates on zoom won't work!");
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        lastPointerPosition = eventData.position;
        initialPointerPosition = eventData.position;
        isDragging = true;
        hasStartedDragging = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        hasStartedDragging = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
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
        ZoomMap(1f);
    }

    public void ZoomOut()
    {
        ZoomMap(-1f);
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
        
        // Force update all paths after zoom change
        if (pathRenderer != null)
        {
            // Small delay to ensure map has updated first
            StartCoroutine(UpdatePathsAfterDelay());
        }
    }
    
    // Coroutine to update paths with a small delay
    private System.Collections.IEnumerator UpdatePathsAfterDelay()
    {
        // Wait for the next frame to ensure map has updated
        yield return null;
        
        // Now update all paths
        if (pathRenderer != null)
        {
            pathRenderer.ForceUpdateAllPaths();
            Debug.Log($"Updated paths after zoom to level: {mapboxMap.Zoom}");
        }
    }
}