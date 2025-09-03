using UnityEngine;
using UnityEngine.UI;

public class GPSDebug : MonoBehaviour
{
    public Text gpsText; // Assign in inspector

    private void Update()
    {
        if (GPSManager.Instance != null)
        {
            Vector2 coords = GPSManager.Instance.GetCoordinates();
            gpsText.text = $"Lat: {coords.x:F6}, Lon: {coords.y:F6}";
        }
    }
}
