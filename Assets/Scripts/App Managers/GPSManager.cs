using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mapbox.Utils;
using Mapbox.Unity.Map;

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
    private List<float> recentHeadings = new List<float>();
    private int maxHistorySize = 5;

    public void Start()
    {
        StartCoroutine(StartLocationService());
    }

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
        }
    }

    public IEnumerator StartLocationService()
    {
#if UNITY_EDITOR
        if (useMockLocationInEditor)
        {
            Debug.Log("Using mock GPS in Editor");
            // Start compass for heading even in editor (won't work but prevents errors)
            Input.compass.enabled = true;
            yield break;
        }
#endif

        if (!Input.location.isEnabledByUser)
        {
            Debug.LogError("Location service not enabled");
            yield break;
        }

        // Start location service
        Input.location.Start();

        // Start compass for heading
        Input.compass.enabled = true;

        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }

        if (Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.LogError("Unable to determine device location");
        }
        else
        {
            Debug.Log("GPS started: " +
            Input.location.lastData.latitude + ", " + Input.location.lastData.longitude);
        }
    }

    public Vector2 GetCoordinates()
    {
#if UNITY_EDITOR
        if (useMockLocationInEditor)
        {
            return new Vector2(mockLatitude, mockLongitude);
        }
#endif

        return new Vector2(Input.location.lastData.latitude, Input.location.lastData.longitude);
    }

    public float GetHeading()
    {
#if UNITY_EDITOR
        if (useMockLocationInEditor)
        {
            // In editor, simulate rotation with input keys for testing
            if (Input.GetKey(KeyCode.Q))
                mockHeading -= 90f * Time.deltaTime;
            if (Input.GetKey(KeyCode.E))
                mockHeading += 90f * Time.deltaTime;
            
            mockHeading = mockHeading % 360f;
            if (mockHeading < 0) mockHeading += 360f;
            
            return mockHeading;
        }
#endif

        // Return compass heading (0 = North, clockwise)
        return Input.compass.magneticHeading;
    }

    public Vector2 GetSmoothedCoordinates()
    {
        Vector2 rawCoords = GetCoordinates();
        
        recentCoordinates.Add(rawCoords);
        if (recentCoordinates.Count > maxHistorySize)
            recentCoordinates.RemoveAt(0);
        
        // Return average of recent coordinates
        Vector2 sum = Vector2.zero;
        foreach (var coord in recentCoordinates)
            sum += coord;
        
        return sum / recentCoordinates.Count;
    }

    public float GetSmoothedHeading()
    {
        float rawHeading = GetHeading();
        
        recentHeadings.Add(rawHeading);
        if (recentHeadings.Count > maxHistorySize)
            recentHeadings.RemoveAt(0);
        
        // Handle circular averaging for angles
        float avgHeading = 0f;
        if (recentHeadings.Count > 0)
        {
            float sinSum = 0f, cosSum = 0f;
            foreach (var heading in recentHeadings)
            {
                sinSum += Mathf.Sin(heading * Mathf.Deg2Rad);
                cosSum += Mathf.Cos(heading * Mathf.Deg2Rad);
            }
            avgHeading = Mathf.Atan2(sinSum / recentHeadings.Count, cosSum / recentHeadings.Count) * Mathf.Rad2Deg;
            if (avgHeading < 0) avgHeading += 360f;
        }
        
        return avgHeading;
    }

    void Update()
    {
        // Debug info in editor
#if UNITY_EDITOR
        if (useMockLocationInEditor && Input.GetKeyDown(KeyCode.G))
        {
            Debug.Log($"Mock GPS: {mockLatitude}, {mockLongitude}, Heading: {mockHeading:F1}°");
            Debug.Log("Use Q/E keys to rotate the mock heading");
        }
#endif
    }
}