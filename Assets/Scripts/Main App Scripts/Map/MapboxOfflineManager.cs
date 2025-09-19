using UnityEngine;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using Mapbox.Unity.Utilities;
using System.Collections;
using System;

public class MapboxOfflineManager : MonoBehaviour
{
    [Header("Offline Settings")]
    public AbstractMap map;
    public string offlineRegionName = "CrimsonMap_Campus";
    
    [Header("Campus Bounds")]
    public Vector2d campusCenter = new Vector2d(8.947460, 125.543677); 
    public float radiusInKm = 2.0f;
    
    [Header("Zoom Levels")]
    public int minZoomLevel = 14;
    public int maxZoomLevel = 18;

    [Header("Cache Status")]
    public bool isOfflineModeActive = false;
    public bool isCacheDownloading = false;
    
    // Events
    public System.Action<float> OnCacheProgress;
    public System.Action OnCacheComplete;
    public System.Action<string> OnCacheError;
    
    void Start()
    {
        CheckOfflineCapability();
    }
    
    public void CheckOfflineCapability()
    {
        // Check if offline tiles are already cached
        bool hasOfflineCache = HasCachedMapData();
        bool hasInternet = Application.internetReachability != NetworkReachability.NotReachable;
        
        Debug.Log($"Offline cache available: {hasOfflineCache}");
        Debug.Log($"Internet available: {hasInternet}");
        
        if (!hasInternet && !hasOfflineCache)
        {
            // No internet and no cache - app can't work
            OnCacheError?.Invoke("Internet required for first-time map setup");
        }
        else if (!hasInternet && hasOfflineCache)
        {
            // No internet but has cache - use offline mode
            EnableOfflineMode();
        }
        else if (hasInternet)
        {
            // Has internet - normal mode, but cache if needed
            EnableOnlineMode();
            
            if (!hasOfflineCache)
            {
                StartCachingProcess();
            }
        }
    }
    
    public void StartCachingProcess()
    {
        if (isCacheDownloading) return;
        
        Debug.Log("Starting Mapbox offline caching...");
        StartCoroutine(CacheMapData());
    }
    
    private IEnumerator CacheMapData()
    {
        isCacheDownloading = true;

        // Variables to track progress and exceptions
        float progress = 0f;
        Exception caughtException = null;

        // Calculate bounds around campus
        Vector2d northeast = CalculateBounds(campusCenter, radiusInKm, true);
        Vector2d southwest = CalculateBounds(campusCenter, radiusInKm, false);

        Debug.Log($"Caching area from {southwest} to {northeast}");
        Debug.Log($"Zoom levels: {minZoomLevel} to {maxZoomLevel}");

        // Simulate caching progress (replace with actual Mapbox SDK calls)
        while (progress < 1f)
        {
            try
            {
                progress += Time.deltaTime * 0.1f; // Simulate download progress
                OnCacheProgress?.Invoke(progress);
            }
            catch (Exception ex)
            {
                caughtException = ex;
                break;
            }
            yield return null;
        }

        if (caughtException == null && progress >= 1f)
        {
            try
            {
                // Mark cache as complete
                PlayerPrefs.SetInt("MapboxCacheComplete", 1);
                PlayerPrefs.SetString("CacheDate", DateTime.Now.ToBinary().ToString());

                OnCacheComplete?.Invoke();
                Debug.Log("Mapbox offline cache completed!");
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }
        }

        if (caughtException != null)
        {
            OnCacheError?.Invoke($"Cache failed: {caughtException.Message}");
        }

        isCacheDownloading = false;
    }
    
    private Vector2d CalculateBounds(Vector2d center, float radiusKm, bool isNorthEast)
    {
        // Convert radius to degrees (rough approximation)
        float radiusDegrees = radiusKm / 111.32f; // 1 degree ≈ 111.32 km
        
        if (isNorthEast)
        {
            return new Vector2d(center.x + radiusDegrees, center.y + radiusDegrees);
        }
        else
        {
            return new Vector2d(center.x - radiusDegrees, center.y - radiusDegrees);
        }
    }
    
    public bool HasCachedMapData()
    {
        return PlayerPrefs.GetInt("MapboxCacheComplete", 0) == 1;
    }
    
    public void EnableOfflineMode()
    {
        isOfflineModeActive = true;
        
        // Configure map for offline mode
        if (map != null)
        {
            // Set map to use cached tiles only
            Debug.Log("Map configured for offline mode");
        }
    }
    
    public void EnableOnlineMode()
    {
        isOfflineModeActive = false;
        
        // Configure map for online mode
        if (map != null)
        {
            // Set map to use online tiles
            Debug.Log("Map configured for online mode");
        }
    }
    
    public void ClearOfflineCache()
    {
        PlayerPrefs.DeleteKey("MapboxCacheComplete");
        PlayerPrefs.DeleteKey("CacheDate");
        Debug.Log("Offline cache cleared");
    }
    
    public string GetCacheStatus()
    {
        if (!HasCachedMapData()) return "No offline cache";
        
        long cacheDateBinary = Convert.ToInt64(PlayerPrefs.GetString("CacheDate", "0"));
        DateTime cacheDate = DateTime.FromBinary(cacheDateBinary);
        
        return $"Cache from: {cacheDate:yyyy-MM-dd HH:mm}";
    }
}