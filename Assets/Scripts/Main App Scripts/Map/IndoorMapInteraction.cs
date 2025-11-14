using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;

public class IndoorMapInteraction : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Map References")]
    public RectTransform indoorMapContainer;
    public RectTransform viewportRect;

    [Header("Drag Settings")]
    public float dragSensitivity = 1f;
    public bool clampDrag = true;

    [Header("Zoom Settings")]
    public float minZoom = 0.5f;
    public float maxZoom = 3f;
    public float zoomSensitivity = 0.01f;
    public float pinchZoomDeadzone = 5f;

    private Vector2 lastPointerPosition;
    private bool isDragging = false;

    private bool isPinching = false;
    private float lastPinchDistance = 0f;
    private float currentZoom = 1f;

    private MapModeController mapModeController;

    void Start()
    {
        mapModeController = FindObjectOfType<MapModeController>();

        if (indoorMapContainer != null)
        {
            indoorMapContainer.localScale = Vector3.one;
            currentZoom = 1f;
        }
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Update()
    {
        if (mapModeController != null && !mapModeController.IsIndoorMode())
            return;

        HandlePinchZoom();
    }

    private void HandlePinchZoom()
    {
        var activeTouches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;

        if (activeTouches.Count == 2)
        {
            var touch1 = activeTouches[0];
            var touch2 = activeTouches[1];

            Vector2 touch1Pos = touch1.screenPosition;
            Vector2 touch2Pos = touch2.screenPosition;

            float currentDistance = Vector2.Distance(touch1Pos, touch2Pos);

            if (!isPinching)
            {
                isPinching = true;
                lastPinchDistance = currentDistance;
                isDragging = false;
            }
            else
            {
                float distanceDelta = currentDistance - lastPinchDistance;

                if (Mathf.Abs(distanceDelta) > pinchZoomDeadzone)
                {
                    float zoomDelta = distanceDelta * zoomSensitivity;
                    ApplyZoom(zoomDelta);
                    lastPinchDistance = currentDistance;
                }
            }
        }
        else if (isPinching && activeTouches.Count < 2)
        {
            isPinching = false;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (mapModeController != null && !mapModeController.IsIndoorMode())
            return;

        var activeTouches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
        if (activeTouches.Count <= 1)
        {
            lastPointerPosition = eventData.position;
            isDragging = true;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (mapModeController != null && !mapModeController.IsIndoorMode())
            return;

        if (!isDragging || isPinching || indoorMapContainer == null)
            return;

        var activeTouches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
        if (activeTouches.Count > 1)
            return;

        Vector2 deltaPosition = eventData.position - lastPointerPosition;
        lastPointerPosition = eventData.position;

        Vector2 currentPos = indoorMapContainer.anchoredPosition;
        Vector2 newPos = currentPos + deltaPosition * dragSensitivity;

        if (clampDrag)
        {
            newPos = ClampToViewport(newPos);
        }

        indoorMapContainer.anchoredPosition = newPos;
    }

    private Vector2 ClampToViewport(Vector2 position)
    {
        if (viewportRect == null || indoorMapContainer == null)
            return position;

        Vector2 viewportSize = viewportRect.rect.size;
        Vector2 mapSize = indoorMapContainer.rect.size * currentZoom;

        float halfViewportWidth = viewportSize.x * 0.5f;
        float halfViewportHeight = viewportSize.y * 0.5f;

        float halfMapWidth = mapSize.x * 0.5f;
        float halfMapHeight = mapSize.y * 0.5f;

        float maxX = Mathf.Max(0, halfMapWidth - halfViewportWidth);
        float maxY = Mathf.Max(0, halfMapHeight - halfViewportHeight);

        position.x = Mathf.Clamp(position.x, -maxX, maxX);
        position.y = Mathf.Clamp(position.y, -maxY, maxY);

        return position;
    }

    private void ApplyZoom(float zoomDelta)
    {
        if (indoorMapContainer == null)
            return;

        currentZoom += zoomDelta;
        currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);

        indoorMapContainer.localScale = Vector3.one * currentZoom;

        if (clampDrag && viewportRect != null)
        {
            indoorMapContainer.anchoredPosition = ClampToViewport(indoorMapContainer.anchoredPosition);
        }
    }

    public void ZoomIn()
    {
        ApplyZoom(0.1f);
    }

    public void ZoomOut()
    {
        ApplyZoom(-0.1f);
    }

    public void ResetZoom()
    {
        currentZoom = 1f;
        if (indoorMapContainer != null)
        {
            indoorMapContainer.localScale = Vector3.one;
            indoorMapContainer.anchoredPosition = Vector2.zero;
        }
    }

    public float GetCurrentZoom()
    {
        return currentZoom;
    }
}