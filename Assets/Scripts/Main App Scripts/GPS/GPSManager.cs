using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPSManager : MonoBehaviour
{
    public static GPSManager Instance;
    public bool useMockLocationInEditor = true;

    // Mock coordinates (example: WMSU Main Campus)
    private float mockLatitude = 6.9077f;
    private float mockLongitude = 122.0761f;

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
}
