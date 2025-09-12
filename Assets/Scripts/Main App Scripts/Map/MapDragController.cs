using UnityEngine;
using UnityEngine.EventSystems;

public class MapDragController : MonoBehaviour, IDragHandler, IScrollHandler
{
    public Camera mapCamera;
    public float panSpeed = 0.1f;
    public float zoomSpeed = 10f;

    public void OnDrag(PointerEventData eventData)
    {
        if (mapCamera == null) return;

        // Move camera opposite to drag
        Vector3 move = new Vector3(-eventData.delta.x * panSpeed, 0, -eventData.delta.y * panSpeed);
        mapCamera.transform.Translate(move, Space.World);
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (mapCamera == null) return;

        // Zoom camera
        if (mapCamera.orthographic)
        {
            mapCamera.orthographicSize -= eventData.scrollDelta.y * zoomSpeed * Time.deltaTime;
            mapCamera.orthographicSize = Mathf.Clamp(mapCamera.orthographicSize, 10f, 500f);
        }
    }
}
