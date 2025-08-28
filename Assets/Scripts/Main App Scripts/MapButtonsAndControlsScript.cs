using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MapButtonsAndControlsScript : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Header("Zoom Settings")]
    public float minScale = 1f;
    public float maxScale = 3f;
    public float zoomSpeedTouch = 0.01f;
    public float zoomSpeedMouse = 0.1f;

    private RectTransform rectTransform;
    private Vector2 lastPointerPosition;
    private bool isDragging = false;

    // Reference to the user indicator in the scene
    private RectTransform userIndicator;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        RectTransform parentRect = rectTransform.parent as RectTransform;
        Vector2 containerSize = parentRect.rect.size;
        Vector2 mapSize = rectTransform.rect.size;

        // Calculate scale to make map fill the container fully
        float scaleX = containerSize.x / mapSize.x;
        float scaleY = containerSize.y / mapSize.y;

        minScale = Mathf.Max(scaleX, scaleY);

        // Start scale at minScale
        rectTransform.localScale = new Vector3(minScale, minScale, 1f);

        ClampPosition();
    }

    void Update()
    {
        HandleZoom();

        // Cache the user indicator (first time only)
        if (userIndicator == null)
        {
            GameObject pin = GameObject.Find("UserIndicatorInstance");
            if (pin != null)
                userIndicator = pin.GetComponent<RectTransform>();
        }

    }

    #region Dragging
    public void OnPointerDown(PointerEventData eventData)
    {
        lastPointerPosition = eventData.position;
        isDragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        Vector2 currentPointerPosition = eventData.position;
        Vector2 diff = currentPointerPosition - lastPointerPosition;

        rectTransform.anchoredPosition += diff;
        lastPointerPosition = currentPointerPosition;

        ClampPosition();
    }

    private void ClampPosition()
    {
        RectTransform parentRect = rectTransform.parent as RectTransform;
        Rect parentRectBounds = parentRect.rect;
        Rect mapRect = rectTransform.rect;

        Vector2 scaledMapSize = new Vector2(
            mapRect.width * rectTransform.localScale.x,
            mapRect.height * rectTransform.localScale.y
        );

        Vector2 pos = rectTransform.anchoredPosition;

        float halfParentWidth = parentRectBounds.width / 2f;
        float halfParentHeight = parentRectBounds.height / 2f;

        float halfMapWidth = scaledMapSize.x / 2f;
        float halfMapHeight = scaledMapSize.y / 2f;

        float baseMargin = 200f;
        float zoomLevel = rectTransform.localScale.x;
        float overscrollX = Mathf.Max(baseMargin * zoomLevel, (scaledMapSize.x - parentRectBounds.width) / 2f);
        float overscrollY = Mathf.Max(baseMargin * zoomLevel, (scaledMapSize.y - parentRectBounds.height) / 2f);

        float minX = halfParentWidth - halfMapWidth - overscrollX;
        float maxX = halfMapWidth - halfParentWidth + overscrollX;

        float minY = halfParentHeight - halfMapHeight - overscrollY;
        float maxY = halfMapHeight - halfParentHeight + overscrollY;

        if (scaledMapSize.x <= parentRectBounds.width)
            minX = maxX = 0;
        if (scaledMapSize.y <= parentRectBounds.height)
            minY = maxY = 0;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        rectTransform.anchoredPosition = pos;
    }
    #endregion

    #region Zooming
    private void HandleZoom()
    {
        if (Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            Vector2 t0Prev = t0.position - t0.deltaPosition;
            Vector2 t1Prev = t1.position - t1.deltaPosition;

            float prevDist = Vector2.Distance(t0Prev, t1Prev);
            float currentDist = Vector2.Distance(t0.position, t1.position);

            float delta = currentDist - prevDist;
            Zoom(delta * zoomSpeedTouch);
        }
        else
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            Zoom(scroll * zoomSpeedMouse * 100f);
        }
    }

    private void Zoom(float increment)
    {
        float newScale = Mathf.Clamp(rectTransform.localScale.x + increment, minScale, maxScale);
        rectTransform.localScale = new Vector3(newScale, newScale, 1f);

        ClampPosition();
    }

    public void ZoomInButton() => Zoom(0.1f);
    public void ZoomOutButton() => Zoom(-0.1f);
    #endregion

    #region Centering
    public void CenterOnPosition(Vector2 targetPosition)
    {
        // The target position is already in the map's local coordinate space
        // We need to calculate what the map's anchoredPosition should be
        // to center this target position in the parent container

        RectTransform parentRect = rectTransform.parent as RectTransform;

        // Calculate the offset needed to center the target position
        // Since targetPosition is relative to the map, we need to scale it by the current map scale
        Vector2 scaledTargetPos = targetPosition * rectTransform.localScale.x;

        // The map's anchoredPosition should be the negative of the scaled target position
        // This will place the target at (0,0) in the parent container (which is the center)
        rectTransform.anchoredPosition = -scaledTargetPos;

        // Ensure we stay within bounds
        ClampPosition();
    }

    public void CenterOnUserPin()
    {
        if (userIndicator != null)
        {
            // Use anchoredPosition since that's what UserIndicator uses
            Vector2 userPos = userIndicator.anchoredPosition;
            CenterOnPosition(userPos);
        }
        else
        {
            Debug.Log("No user indicator found in scene!");
        }
    }

    #endregion
}
