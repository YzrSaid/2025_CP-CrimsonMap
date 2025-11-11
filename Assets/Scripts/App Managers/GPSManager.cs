using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mapbox.Utils;
using Mapbox.Unity.Map;
using UnityEngine.InputSystem;

public class GPSManager : MonoBehaviour
{
    public static GPSManager Instance;
    public bool useMockLocationInEditor = true;

    [Header("Mock GPS Settings (Editor Only)")]
    // private float mockLatitude = 6.91261f;
    // private float mockLongitude = 122.06359f;

    private float mockLatitude = 6.91261f;
    private float mockLongitude = 122.06359f;
    private float mockHeading = 0f;

    [Header("QR Override Settings")]
    public bool useQROverride = false;
    private Vector2 qrOverrideLocation;
    private float qrOverrideHeading = 0f;

    [Header("GPS Smoothing")]
    private List<Vector2> recentCoordinates = new List<Vector2>();
    private int maxHistorySize = 3; // Reduced for snappier response

    [Header("Compass Debug")]
    public bool enableCompassDebug = true;

    private MagneticFieldSensor magnetometer;
    private Accelerometer accelerometer;
    private UnityEngine.InputSystem.Gyroscope gyroscope;

    private bool sensorsInitialized = false;
    private float currentHeading = 0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeSensors();
    }

    private void InitializeSensors()
    {
        Debug.Log("[GPSManager] Initializing sensors with new Input System...");

        try
        {
            magnetometer = MagneticFieldSensor.current;
            if (magnetometer != null)
            {
                InputSystem.EnableDevice(magnetometer);
                Debug.Log("[GPSManager] Magnetometer enabled");
            }
            else
            {
                Debug.LogWarning("[GPSManager] Magnetometer not found!");
            }

            accelerometer = Accelerometer.current;
            if (accelerometer != null)
            {
                InputSystem.EnableDevice(accelerometer);
                Debug.Log("[GPSManager] Accelerometer enabled");
            }
            else
            {
                Debug.LogWarning("[GPSManager] Accelerometer not found!");
            }

            gyroscope = UnityEngine.InputSystem.Gyroscope.current;
            if (gyroscope != null)
            {
                InputSystem.EnableDevice(gyroscope);
                Debug.Log("[GPSManager] Gyroscope enabled");
            }
            else
            {
                Debug.LogWarning("[GPSManager]  Gyroscope not found (optional)");
            }

            sensorsInitialized = (magnetometer != null && accelerometer != null);

            if (sensorsInitialized)
            {
                Debug.Log("[GPSManager] Sensors initialized successfully!");
            }
            else
            {
                Debug.LogError("[GPSManager]  Failed to initialize required sensors");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GPSManager] Error initializing sensors: {e.Message}");
        }
    }

    public void Start()
    {
        StartCoroutine(StartLocationService());
    }

    public IEnumerator StartLocationService()
    {
#if UNITY_EDITOR
        if (useMockLocationInEditor)
        {
            Debug.Log("[GPSManager] Using mock GPS in Editor");
            sensorsInitialized = true;
            yield break;
        }
#endif

        if (!Input.location.isEnabledByUser)
        {
            Debug.LogWarning("[GPSManager] Location service not enabled by user");
            yield break;
        }

        Debug.Log("[GPSManager] Starting location services...");
        Input.location.Start();

        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            Debug.Log($"[GPSManager] Waiting for location... {maxWait}s remaining");
            yield return new WaitForSeconds(1);
            maxWait--;
        }

        if (Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.LogError("[GPSManager] Unable to determine device location");
        }
        else
        {
            Debug.Log($"[GPSManager] GPS started: {Input.location.lastData.latitude}, {Input.location.lastData.longitude}");
        }
    }

    public Vector2 GetCoordinates()
    {
        if (useQROverride)
        {
            return qrOverrideLocation;
        }

#if UNITY_EDITOR
        if (useMockLocationInEditor)
        {
            return new Vector2(mockLatitude, mockLongitude);
        }
#endif

        if (Input.location.status == LocationServiceStatus.Running)
        {
            return new Vector2(Input.location.lastData.latitude, Input.location.lastData.longitude);
        }
        else
        {
            return new Vector2(mockLatitude, mockLongitude);
        }
    }

    public float GetHeading()
    {
#if UNITY_EDITOR
        if (useMockLocationInEditor)
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.qKey.isPressed)
                    mockHeading -= 90f * Time.deltaTime;
                if (Keyboard.current.eKey.isPressed)
                    mockHeading += 90f * Time.deltaTime;
            }

            mockHeading = mockHeading % 360f;
            if (mockHeading < 0) mockHeading += 360f;

            return mockHeading;
        }
#endif

        if (sensorsInitialized && magnetometer != null && accelerometer != null)
        {
            return CalculateHeadingFromSensors();
        }
        else
        {
            if (enableCompassDebug && Time.frameCount % 120 == 0)
            {
                Debug.LogWarning("[GPSManager] Sensors not initialized!");
            }
            return currentHeading;
        }
    }
    private float CalculateHeadingFromSensors()
    {
        try
        {
            Vector3 magnetic = magnetometer.magneticField.ReadValue();
            Vector3 accel = accelerometer.acceleration.ReadValue();

            if (magnetic.sqrMagnitude < 0.01f || accel.sqrMagnitude < 0.01f)
                return currentHeading;

            magnetic.Normalize();
            accel.Normalize();

            float pitch = Mathf.Asin(-accel.x);
            float roll = Mathf.Asin(accel.y / Mathf.Cos(pitch));

            // Compensate the magnetometer readings based on tilt
            float magX = magnetic.x * Mathf.Cos(pitch) + magnetic.z * Mathf.Sin(pitch);
            float magY = -magnetic.x * Mathf.Sin(roll) * Mathf.Sin(pitch)  
                         + magnetic.y * Mathf.Cos(roll)
                         + magnetic.z * Mathf.Sin(roll) * Mathf.Cos(pitch);

            // Compute heading in degrees
            float heading = Mathf.Atan2(-magY, -magX) * Mathf.Rad2Deg + 90f; 


            // Apply axis correction
            heading = (heading + 360f) % 360f;

            if (enableCompassDebug && Time.frameCount % 120 == 0)
                Debug.Log($"[GPSManager] Tilt-compensated heading (fixed v2): {heading:F1}° (pitch={pitch * Mathf.Rad2Deg:F1}, roll={roll * Mathf.Rad2Deg:F1})");

            return heading;


        }
        catch (System.Exception e)
        {
            if (enableCompassDebug && Time.frameCount % 120 == 0)
                Debug.LogError($"[GPSManager] Error calculating heading: {e.Message}");
            return currentHeading;
        }
    }

    public void SetQRLocationOverride(Vector2 location, float heading = 0f)
    {
        qrOverrideLocation = location;
        qrOverrideHeading = heading;
        useQROverride = true;
        Debug.Log($"[GPSManager] QR Override set: {location}, heading: {heading}°");
    }

    public void SetQRLocationOverride(float latitude, float longitude, float heading = 0f)
    {
        SetQRLocationOverride(new Vector2(latitude, longitude), heading);
    }

    public void ClearQRLocationOverride()
    {
        useQROverride = false;
        Debug.Log("[GPSManager] QR Override cleared");
    }

    public bool IsUsingQROverride()
    {
        return useQROverride;
    }

    public Vector2 GetSmoothedCoordinates()
    {
        Vector2 rawCoords = GetCoordinates();

        recentCoordinates.Add(rawCoords);
        if (recentCoordinates.Count > maxHistorySize)
            recentCoordinates.RemoveAt(0);

        Vector2 sum = Vector2.zero;
        foreach (var coord in recentCoordinates)
            sum += coord;

        return sum / recentCoordinates.Count;
    }

    public bool IsCompassReady()
    {
        return sensorsInitialized && magnetometer != null;
    }

    public string GetSensorStatus()
    {
        string status = "";
        status += $"Magnetometer: {(magnetometer != null ? "✅" : "❌")}\n";
        status += $"Accelerometer: {(accelerometer != null ? "✅" : "❌")}\n";
        status += $"Gyroscope: {(gyroscope != null ? "✅" : "❌")}\n";
        status += $"Initialized: {sensorsInitialized}";
        return status;
    }

    void Update()
    {
#if UNITY_EDITOR
        if (useMockLocationInEditor && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
        {
            Debug.Log($"[GPSManager] Mock GPS: {mockLatitude}, {mockLongitude}, Heading: {mockHeading:F1}°");
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            if (useQROverride)
                ClearQRLocationOverride();
            else
                SetQRLocationOverride(mockLatitude + 0.001f, mockLongitude + 0.001f, mockHeading + 45f);
        }
#endif
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
        {
            Debug.Log("[GPSManager] App resumed - re-enabling sensors");
            InitializeSensors();
        }
    }

    void OnDestroy()
    {
        if (magnetometer != null)
            InputSystem.DisableDevice(magnetometer);
        if (accelerometer != null)
            InputSystem.DisableDevice(accelerometer);
        if (gyroscope != null)
            InputSystem.DisableDevice(gyroscope);
    }
}