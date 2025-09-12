using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MapCameraFitter : MonoBehaviour
{
    [Header("UI Container for the Map")]
    public RectTransform mapContainer;

    private Camera mapCam;

    void Awake()
    {
        mapCam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (mapContainer == null || mapCam == null) return;

        // Convert container rect to screen space
        Vector3[] corners = new Vector3[4];
        mapContainer.GetWorldCorners(corners);

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        // bottom-left
        Vector3 bottomLeft = corners[0];
        // top-right
        Vector3 topRight = corners[2];

        // Normalize to viewport (0–1)
        float x = bottomLeft.x / screenWidth;
        float y = bottomLeft.y / screenHeight;
        float w = (topRight.x - bottomLeft.x) / screenWidth;
        float h = (topRight.y - bottomLeft.y) / screenHeight;

        mapCam.rect = new Rect(x, y, w, h);
    }
}
