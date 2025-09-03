using System.Collections;
using UnityEngine;

public class UserIndicator : MonoBehaviour
{
    [Header("References")]
    public GameObject userIndicatorPrefab;
    public RectTransform mapParent;

    private GameObject userIndicatorInstance;

    private IEnumerator Start()
    {
        // Wait until the end of frame so all spawners finish
        yield return new WaitForEndOfFrame();

        // Spawn the indicator/prefab on the map
        userIndicatorInstance = Instantiate(userIndicatorPrefab, mapParent);
        userIndicatorInstance.name = "UserIndicatorInstance";

        // Wait a bit more to ensure all other spawners have finished creating their children
        yield return new WaitForSeconds(0.1f);

        // Move to last sibling after all spawning is complete
        userIndicatorInstance.transform.SetAsLastSibling();
    }

    // In UserIndicator.cs, replace the Update() method with this:
    private Vector2 lastMapPos = Vector2.zero;
    private float updateInterval = 0.5f; 
    private float lastUpdateTime = 0f;

    void Update()
    {
        if (GPSManager.Instance == null || userIndicatorInstance == null) return;

        if (!MapCoordinateSystem.Instance.AreBoundsReady()) return;
        // Only update at intervals, not every frame
        if (Time.time - lastUpdateTime < updateInterval) return;
        lastUpdateTime = Time.time;

        Vector2 gpsCoords = GPSManager.Instance.GetSmoothedCoordinates();
        Vector2 mapPos = MapCoordinateSystem.Instance.LatLonToMapPosition(gpsCoords.x, gpsCoords.y);

        // Only update if position changed significantly (reduce flickering)
        if (Vector2.Distance(mapPos, lastMapPos) > 1f) // 1 unit threshold
        {
            // userIndicatorInstance.GetComponent<RectTransform>().anchoredPosition = mapPos;
            // lastMapPos = mapPos;
            userIndicatorInstance.GetComponent<RectTransform>().anchoredPosition =
        Vector2.Lerp(lastMapPos, mapPos, 0.3f); // 0.3 = smoothness factor
            lastMapPos = userIndicatorInstance.GetComponent<RectTransform>().anchoredPosition;

        }
    }

}
