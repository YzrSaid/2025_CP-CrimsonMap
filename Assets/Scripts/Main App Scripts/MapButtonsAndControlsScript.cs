using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MapButtonsAndControlsScript : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Header("Zoom Settings")]
    public float minScale = 0.1f;
    public float maxScale = 3f;
    public float zoomSpeedTouch = 0.01f;
    public float zoomSpeedMouse = 0.1f;

    [Header("Initial Settings")]
    public float initialScale = 0.5f;

    [Header("Dragging Settings")]
    public float baseOverscrollAmount = 500f;
    [Tooltip("Multiply overscroll by zoom level for consistent edge access")]
    public bool scaleOverscrollWithZoom = true;

    private RectTransform rectTransform;
    private Vector2 lastPointerPosition;
    private bool isDragging = false;
    private RectTransform userIndicator;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        rectTransform.localScale = new Vector3(initialScale, initialScale, 1f);

        MapManager mapManager = FindObjectOfType<MapManager>();
        if (mapManager != null)
        {
            mapManager.OnMapLoadingComplete += OnMapLoadingComplete;
        }
    }

    void OnDestroy()
    {
        MapManager mapManager = FindObjectOfType<MapManager>();
        if (mapManager != null)
        {
            mapManager.OnMapLoadingComplete -= OnMapLoadingComplete;
        }
    }

    void OnMapLoadingComplete()
    {
        ClampPosition();
    }

    void Update()
    {
        HandleZoom();

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
        if (rectTransform == null || rectTransform.parent == null) return;

        RectTransform parent = rectTransform.parent as RectTransform;

        Vector2 mapSize = MapCoordinateSystem.Instance.GetMapSizeInPixels();
        Vector2 parentSize = parent.rect.size;

        // Use half sizes for bounds calculation
        float mapHalfWidth = mapSize.x * rectTransform.localScale.x / 2f;
        float mapHalfHeight = mapSize.y * rectTransform.localScale.y / 2f;

        float parentHalfWidth = parentSize.x / 2f;
        float parentHalfHeight = parentSize.y / 2f;

        // Add overscroll so user can still drag beyond edges a little
        float overscroll = 200f;

        Vector2 pos = rectTransform.anchoredPosition;
        pos.x = Mathf.Clamp(pos.x, -(mapHalfWidth - parentHalfWidth) - overscroll, (mapHalfWidth - parentHalfWidth) + overscroll);
        pos.y = Mathf.Clamp(pos.y, -(mapHalfHeight - parentHalfHeight) - overscroll, (mapHalfHeight - parentHalfHeight) + overscroll);

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
        Vector2 scaledTargetPos = targetPosition * rectTransform.localScale.x;
        rectTransform.anchoredPosition = -scaledTargetPos;
        ClampPosition();
    }

    public void CenterOnUserPin()
    {
        if (userIndicator != null)
        {
            Vector2 userPos = userIndicator.anchoredPosition;
            CenterOnPosition(userPos);
        }
        else
        {
            Debug.Log("No user indicator found in scene!");
        }
    }

    public void ResetMapView()
    {
        rectTransform.localScale = new Vector3(initialScale, initialScale, 1f);
        rectTransform.anchoredPosition = Vector2.zero;
        ClampPosition();
    }

    public void SetInitialScale(float scale)
    {
        initialScale = scale;
        rectTransform.localScale = new Vector3(scale, scale, 1f);
        ClampPosition();
    }
    #endregion
}
