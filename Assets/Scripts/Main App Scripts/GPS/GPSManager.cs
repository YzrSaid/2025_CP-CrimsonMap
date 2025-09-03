using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPSManager : MonoBehaviour
{
    public static GPSManager Instance;
    public bool useMockLocationInEditor = true;

    // In GPSManager - Fixed coordinates
private float mockLatitude = 6.9215f;   // Middle of campus  
private float mockLongitude = 122.0790f; // Middle of campus

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
            yield break;
        }
#endif

        if (!Input.location.isEnabledByUser)
        {
            Debug.LogError("Location service not enabled");
            yield break;
        }
        Input.location.Start();

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

    // Add this to GPSManager to smooth GPS readings
private List<Vector2> recentCoordinates = new List<Vector2>();
private int maxHistorySize = 5;

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
}
