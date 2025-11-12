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
    private float mockLatitude = 6.91261f;
    private float mockLongitude = 122.06359f;
    private float mockHeading = 0f;

    [Header("GPS Smoothing")]
    private List<Vector2> recentCoordinates = new List<Vector2>();
    private int maxHistorySize = 3;

    [Header("Compass Debug")]
    public bool enableCompassDebug = true;

    private MagneticFieldSensor magnetometer;
    private Accelerometer accelerometer;
    private UnityEngine.InputSystem.Gyroscope gyroscope;

    private bool sensorsInitialized = false;
    private float currentHeading = 0f;

    // ✅ PlayerPrefs Keys for persistence
    private const string PREF_LOCATION_LOCKED = "GPS_LocationLocked";
    private const string PREF_LOCKED_LAT = "GPS_LockedLatitude";
    private const string PREF_LOCKED_LNG = "GPS_LockedLongitude";
    private const string PREF_QR_OVERRIDE = "GPS_QROverride";
    private const string PREF_QR_LAT = "GPS_QRLatitude";
    private const string PREF_QR_LNG = "GPS_QRLongitude";

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
        LoadLockStateFromPlayerPrefs(); // ✅ Load saved lock state on startup
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
                Debug.LogWarning("[GPSManager] Gyroscope not found (optional)");
            }

            sensorsInitialized = (magnetometer != null && accelerometer != null);

            if (sensorsInitialized)
            {
                Debug.Log("[GPSManager] Sensors initialized successfully!");
            }
            else
            {
                Debug.LogError("[GPSManager] Failed to initialize required sensors");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GPSManager] Error initializing sensors: {e.Message}");
        }
    }

    // ✅ Load lock state from PlayerPrefs
    private void LoadLockStateFromPlayerPrefs()
    {
        bool isLocked = PlayerPrefs.GetInt(PREF_LOCATION_LOCKED, 0) == 1;
        bool hasQROverride = PlayerPrefs.GetInt(PREF_QR_OVERRIDE, 0) == 1;

        if (isLocked)
        {
            float lat = PlayerPrefs.GetFloat(PREF_LOCKED_LAT, 0f);
            float lng = PlayerPrefs.GetFloat(PREF_LOCKED_LNG, 0f);
            Debug.Log($"[GPSManager] Loaded locked location from PlayerPrefs: {lat}, {lng}");
        }

        if (hasQROverride)
        {
            float lat = PlayerPrefs.GetFloat(PREF_QR_LAT, 0f);
            float lng = PlayerPrefs.GetFloat(PREF_QR_LNG, 0f);
            Debug.Log($"[GPSManager] Loaded QR override from PlayerPrefs: {lat}, {lng}");
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
        // ✅ PRIORITY 1: Pathfinding locked location (from PlayerPrefs)
        if (PlayerPrefs.GetInt(PREF_LOCATION_LOCKED, 0) == 1)
        {
            float lat = PlayerPrefs.GetFloat(PREF_LOCKED_LAT, 0f);
            float lng = PlayerPrefs.GetFloat(PREF_LOCKED_LNG, 0f);
            return new Vector2(lat, lng);
        }

        // ✅ PRIORITY 2: QR override location (from PlayerPrefs)
        if (PlayerPrefs.GetInt(PREF_QR_OVERRIDE, 0) == 1)
        {
            float lat = PlayerPrefs.GetFloat(PREF_QR_LAT, 0f);
            float lng = PlayerPrefs.GetFloat(PREF_QR_LNG, 0f);
            return new Vector2(lat, lng);
        }

#if UNITY_EDITOR
        // ✅ PRIORITY 3: Mock location in editor
        if (useMockLocationInEditor)
        {
            return new Vector2(mockLatitude, mockLongitude);
        }
#endif

        // ✅ PRIORITY 4: Real GPS
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

            float magX = magnetic.x * Mathf.Cos(pitch) + magnetic.z * Mathf.Sin(pitch);
            float magY = -magnetic.x * Mathf.Sin(roll) * Mathf.Sin(pitch)  
                         + magnetic.y * Mathf.Cos(roll)
                         + magnetic.z * Mathf.Sin(roll) * Mathf.Cos(pitch);

            float heading = Mathf.Atan2(-magY, -magX) * Mathf.Rad2Deg + 90f; 
            heading = (heading + 360f) % 360f;

            if (enableCompassDebug && Time.frameCount % 120 == 0)
                Debug.Log($"[GPSManager] Tilt-compensated heading: {heading:F1}°");

            return heading;
        }
        catch (System.Exception e)
        {
            if (enableCompassDebug && Time.frameCount % 120 == 0)
                Debug.LogError($"[GPSManager] Error calculating heading: {e.Message}");
            return currentHeading;
        }
    }

    // ✅ Lock location for pathfinding (saves to PlayerPrefs)
    public void LockLocationForPathfinding(float latitude, float longitude)
    {
        PlayerPrefs.SetInt(PREF_LOCATION_LOCKED, 1);
        PlayerPrefs.SetFloat(PREF_LOCKED_LAT, latitude);
        PlayerPrefs.SetFloat(PREF_LOCKED_LNG, longitude);
        PlayerPrefs.Save();
        Debug.Log($"[GPSManager] Location locked and saved: {latitude}, {longitude}");
    }

    // ✅ Unlock pathfinding location (clears PlayerPrefs)
    public void UnlockLocationForPathfinding()
    {
        PlayerPrefs.DeleteKey(PREF_LOCATION_LOCKED);
        PlayerPrefs.DeleteKey(PREF_LOCKED_LAT);
        PlayerPrefs.DeleteKey(PREF_LOCKED_LNG);
        PlayerPrefs.Save();
        Debug.Log("[GPSManager] Location unlocked and cleared from PlayerPrefs");
    }

    // ✅ Check if locked (reads from PlayerPrefs)
    public bool IsLocationLocked()
    {
        bool isLocked = PlayerPrefs.GetInt(PREF_LOCATION_LOCKED, 0) == 1;
        bool hasQROverride = PlayerPrefs.GetInt(PREF_QR_OVERRIDE, 0) == 1;
        return isLocked || hasQROverride;
    }

    // ✅ Set QR override (saves to PlayerPrefs)
    public void SetQRLocationOverride(Vector2 location, float heading = 0f)
    {
        PlayerPrefs.SetInt(PREF_QR_OVERRIDE, 1);
        PlayerPrefs.SetFloat(PREF_QR_LAT, location.x);
        PlayerPrefs.SetFloat(PREF_QR_LNG, location.y);
        PlayerPrefs.Save();
        Debug.Log($"[GPSManager] QR Override set and saved: {location}, heading: {heading}°");
    }

    public void SetQRLocationOverride(float latitude, float longitude, float heading = 0f)
    {
        SetQRLocationOverride(new Vector2(latitude, longitude), heading);
    }

    // ✅ Clear QR override (clears PlayerPrefs)
    public void ClearQRLocationOverride()
    {
        PlayerPrefs.DeleteKey(PREF_QR_OVERRIDE);
        PlayerPrefs.DeleteKey(PREF_QR_LAT);
        PlayerPrefs.DeleteKey(PREF_QR_LNG);
        PlayerPrefs.Save();
        Debug.Log("[GPSManager] QR Override cleared from PlayerPrefs");
    }

    public bool IsUsingQROverride()
    {
        return PlayerPrefs.GetInt(PREF_QR_OVERRIDE, 0) == 1;
    }

    public Vector2 GetSmoothedCoordinates()
    {
        Vector2 rawCoords = GetCoordinates();

        // ✅ Don't smooth locked/QR coordinates
        if (IsLocationLocked())
        {
            return rawCoords;
        }

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
            if (IsUsingQROverride())
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