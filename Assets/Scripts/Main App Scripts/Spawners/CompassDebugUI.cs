using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class CompassDebugUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI debugText;

    [Header("Settings")]
    public bool showDebug = true;
    public float updateInterval = 0.1f;

    private float lastUpdateTime = 0f;

    void Start()
    {
        if (debugText == null)
        {
            Debug.LogError("[CompassDebugUI] Debug text not assigned!");
            return;
        }

        Input.location.Start();
        Debug.Log("[CompassDebugUI] Debug UI initialized");
    }

    void Update()
    {
        if (!showDebug || debugText == null)
            return;

        if (Time.time - lastUpdateTime < updateInterval)
            return;

        lastUpdateTime = Time.time;

        // ✅ NEW: Get sensor data from new Input System
        string sensorData = "=== SENSORS (New Input System) ===\n";
        
        var magnetometer = UnityEngine.InputSystem.MagneticFieldSensor.current;
        var accelerometer = UnityEngine.InputSystem.Accelerometer.current;
        var gyroscope = UnityEngine.InputSystem.Gyroscope.current;

        if (magnetometer != null)
        {
            Vector3 magField = magnetometer.magneticField.ReadValue();
            sensorData += $"✅ Magnetometer: {magField}\n";
        }
        else
        {
            sensorData += "❌ Magnetometer: Not available\n";
        }

        if (accelerometer != null)
        {
            Vector3 accel = accelerometer.acceleration.ReadValue();
            sensorData += $"✅ Accelerometer: {accel}\n";
        }
        else
        {
            sensorData += "❌ Accelerometer: Not available\n";
        }

        if (gyroscope != null)
        {
            Vector3 gyro = gyroscope.angularVelocity.ReadValue();
            sensorData += $"✅ Gyroscope: {gyro}\n";
        }
        else
        {
            sensorData += "❌ Gyroscope: Not available\n";
        }

        // Get GPS data if available
        string gpsData = "\n=== GPS MANAGER ===\n";
        if (GPSManager.Instance != null)
        {
            float gpsHeading = GPSManager.Instance.GetHeading();
            // float smoothedHeading = GPSManager.Instance.GetSmoothedHeading();
            Vector2 coords = GPSManager.Instance.GetCoordinates();
            bool compassReady = GPSManager.Instance.IsCompassReady();
            
            gpsData += $"Compass Ready: {(compassReady ? "✅" : "❌")}\n";
            gpsData += $"Current Heading: {gpsHeading:F1}°\n";
            // gpsData += $"Smoothed Heading: {smoothedHeading:F1}°\n";
            gpsData += $"Coords: {coords.x:F5}, {coords.y:F5}\n";
            gpsData += $"\n{GPSManager.Instance.GetSensorStatus()}";
        }
        else
        {
            gpsData += "GPSManager not found!";
        }

        // Get map bearing if available
        string mapData = "\n\n=== MAP ===\n";
        MapInteraction mapInteraction = FindObjectOfType<MapInteraction>();
        if (mapInteraction != null)
        {
            // float mapBearing = mapInteraction.GetCurrentBearing();
            // mapData += $"Map Bearing: {mapBearing:F1}°";
        }
        else
        {
            mapData += "MapInteraction not found";
        }

        // ⚠️ OLD SYSTEM (for comparison)
        string oldCompassData = "\n\n=== OLD Input.compass (Deprecated) ===\n";
        oldCompassData += $"Enabled: {Input.compass.enabled}\n";
        oldCompassData += $"Magnetic: {Input.compass.magneticHeading:F1}°\n";
        oldCompassData += $"Accuracy: {Input.compass.headingAccuracy:F1}°";

        // Build debug text
        string debugInfo = sensorData + gpsData + mapData + oldCompassData;
        debugInfo += $"\n\nTime: {Time.time:F1}s";

#if UNITY_EDITOR
        debugInfo += "\n\n=== EDITOR CONTROLS ===\n";
        debugInfo += "Q/E: Rotate heading\n";
        debugInfo += "G: Print GPS\n";
        debugInfo += "R: Toggle QR override";
#endif

        debugText.text = debugInfo;
    }

    public void ToggleDebug()
    {
        showDebug = !showDebug;
        if (debugText != null)
        {
            debugText.gameObject.SetActive(showDebug);
        }
    }
}