    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.EventSystems;

    public class MapButtonsAndControlsScript : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [Header("Zoom Settings")]
        public float minScale = 1f;           // will be calculated to fill container fully on Awake
        public float maxScale = 3f;
        public float zoomSpeedTouch = 0.01f;
        public float zoomSpeedMouse = 0.1f;

        [Header("User Pin")]
        public GameObject userPinPrefab; // Assign your user pin prefab here

        private RectTransform rectTransform;
        private Vector2 lastPointerPosition;
        private bool isDragging = false;

        private GameObject currentUserPin;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();

            RectTransform parentRect = rectTransform.parent as RectTransform;
            Vector2 containerSize = parentRect.rect.size;
            Vector2 mapSize = rectTransform.rect.size;

            // Calculate scale to make map fill the container fully (like "cover")
            float scaleX = containerSize.x / mapSize.x;
            float scaleY = containerSize.y / mapSize.y;

            minScale = Mathf.Max(scaleX, scaleY);  // Max to cover whole container

            // Start scale at minScale to fill container fully
            rectTransform.localScale = new Vector3(minScale, minScale, 1f);

            ClampPosition();
        }

        void Update()
        {
            HandleZoom();
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

            // overscroll depends on zoom: larger maps = larger margin
            // float overscrollX = Mathf.Max(200f, (scaledMapSize.x - parentRectBounds.width) / 2f);
            // float overscrollY = Mathf.Max(200f, (scaledMapSize.y - parentRectBounds.height) / 2f);

            // base margin in pixels (at scale = 1)
            float baseMargin = 200f;

            // scale it with zoom level so it grows naturally
            float zoomLevel = rectTransform.localScale.x;
            float overscrollX = Mathf.Max(baseMargin * zoomLevel, (scaledMapSize.x - parentRectBounds.width) / 2f);
            float overscrollY = Mathf.Max(baseMargin * zoomLevel, (scaledMapSize.y - parentRectBounds.height) / 2f);


            float minX = halfParentWidth - halfMapWidth - overscrollX;
            float maxX = halfMapWidth - halfParentWidth + overscrollX;

            float minY = halfParentHeight - halfMapHeight - overscrollY;
            float maxY = halfMapHeight - halfParentHeight + overscrollY;

            // If the map is smaller than container, keep centered
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
            // Pinch zoom (touch)
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
                // Mouse scroll wheel zoom
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

        public void ZoomInButton()
        {
            Zoom(0.1f);
        }

        public void ZoomOutButton()
        {
            Zoom(-0.1f);
        }
        #endregion

        #region User Pin
        public void PlaceUserPin(Vector2 localPosition)
        {
            if (userPinPrefab == null)
            {
                Debug.LogWarning("User pin prefab not assigned!");
                return;
            }

            if (currentUserPin == null)
            {
                currentUserPin = Instantiate(userPinPrefab, transform);
            }

            currentUserPin.GetComponent<RectTransform>().localPosition = localPosition;
        }
        #endregion

        #region Centering
        public void CenterOnPosition(Vector2 localPosition)
        {
            RectTransform parentRect = rectTransform.parent as RectTransform;
            Vector2 parentCenter = new Vector2(parentRect.rect.width / 2, parentRect.rect.height / 2);

            // Adjust anchoredPosition so the localPosition point is centered in the parent RectTransform
            Vector2 offset = parentCenter - localPosition * rectTransform.localScale.x;
            rectTransform.anchoredPosition = offset;

            // Also move user pin if it exists
            if (currentUserPin != null)
            {
                currentUserPin.GetComponent<RectTransform>().localPosition = localPosition;
            }

            ClampPosition();
        }
        #endregion
    }
