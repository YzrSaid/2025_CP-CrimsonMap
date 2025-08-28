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

    void Update()
    {
        if (GPSManager.Instance == null || userIndicatorInstance == null) return;

        // Get GPS Data from the GPS Manager
        UnityEngine.Vector2 gpsCoords = GPSManager.Instance.GetCoordinates();

        // Convert lat/lon to map position
        UnityEngine.Vector2 mapPos = MapCoordinateSystem.Instance.LatLonToMapPosition(gpsCoords.x, gpsCoords.y);

        userIndicatorInstance.GetComponent<RectTransform>().anchoredPosition = mapPos;

        // Ensure the user indicator stays on top (optional - only if needed)
        // You can comment this out if performance is a concern
        if (userIndicatorInstance.transform.GetSiblingIndex() != userIndicatorInstance.transform.parent.childCount - 1)
        {
            userIndicatorInstance.transform.SetAsLastSibling();
        }
    }

}
