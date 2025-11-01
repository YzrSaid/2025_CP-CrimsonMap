using UnityEngine;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using System.Collections;
using System;
using System.Collections.Generic;

public class MapboxOfflineManager : MonoBehaviour
{
    [Header("Map Reference")]
    public AbstractMap map;
    
    [Header("Current Map Being Cached")]
    public string currentMapId = "";
    public Vector2d currentMapCenter = new Vector2d(0, 0);
    
    [Header("Caching Settings")]
    public float radiusInKm = 2.0f; // Coverage radius
    public int minZoomLevel = 15;
    public int maxZoomLevel = 18;
    public int gridSize = 5; // 5x5 grid of cache points
    public float cachingSpeed = 0.5f; // Seconds between each cache point
    
    [Header("Status")]
    public bool isCaching = false;
    public float cachingProgress = 0f;
    
    // Events
    public System.Action<float> OnCacheProgress;
    public System.Action OnCacheComplete;
    public System.Action<string> OnCacheError;
    
    void Start()
    {
        // Don't auto-start caching - wait for manager to call it
    }
    
    /// <summary>
    /// Call this to start pre-caching tiles for a specific map
    /// </summary>
    public void StartCachingMap(string mapId, Vector2d mapCenter)
    {
        if (isCaching)
        {
            Debug.LogWarning("Already caching tiles!");
            return;
        }
        
        if (map == null)
        {
            Debug.LogError("Map reference is null!");
            OnCacheError?.Invoke("Map not initialized");
            return;
        }
        
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.LogWarning("No internet connection - cannot cache tiles");
            OnCacheError?.Invoke("No internet connection");
            return;
        }
        
        // Set current map info
        currentMapId = mapId;
        currentMapCenter = mapCenter;
        
        StartCoroutine(CacheTilesCoroutine());
    }
    
    private IEnumerator CacheTilesCoroutine()
    {
        isCaching = true;
        cachingProgress = 0f;
        
        Debug.Log($"Starting Mapbox tile caching for map: {currentMapId}");
        Debug.Log($"Center: {currentMapCenter}, Coverage radius: {radiusInKm}km, Grid: {gridSize}x{gridSize}");
        
        // Store original camera position to restore later
        Vector2d originalCenter = map.CenterLatitudeLongitude;
        float originalZoom = map.Zoom;
        
        // Calculate grid points to visit
        Vector2d[] cachePoints = GenerateCacheGrid();
        int totalPoints = cachePoints.Length * (maxZoomLevel - minZoomLevel + 1);
        int currentPoint = 0;
        
        // Visit each zoom level
        for (int zoom = minZoomLevel; zoom <= maxZoomLevel; zoom++)
        {
            map.SetZoom(zoom);
            yield return new WaitForSeconds(0.3f); // Let zoom change settle
            
            // Visit each grid point at this zoom level
            foreach (Vector2d point in cachePoints)
            {
                // Pan camera to this location
                map.UpdateMap(point, map.Zoom);
                
                // Wait for tiles to load
                yield return new WaitForSeconds(cachingSpeed);
                
                // Update progress
                currentPoint++;
                cachingProgress = (float)currentPoint / totalPoints;
                OnCacheProgress?.Invoke(cachingProgress);
                
                Debug.Log($"Cached: {point} at zoom {zoom} ({cachingProgress * 100:F1}%)");
            }
        }
        
        // Restore original view
        map.UpdateMap(originalCenter, originalZoom);
        yield return new WaitForSeconds(0.3f);
        
        // Mark as complete FOR THIS SPECIFIC MAP
        PlayerPrefs.SetInt($"MapCache_{currentMapId}_Complete", 1);
        PlayerPrefs.SetString($"MapCache_{currentMapId}_Date", DateTime.UtcNow.ToString("o"));
        PlayerPrefs.SetString($"MapCache_{currentMapId}_Center", $"{currentMapCenter.x},{currentMapCenter.y}");
        PlayerPrefs.SetFloat($"MapCache_{currentMapId}_Radius", radiusInKm);
        PlayerPrefs.Save();
        
        isCaching = false;
        cachingProgress = 1f;
        
        Debug.Log($"Mapbox tile caching complete for map: {currentMapId}!");
        OnCacheComplete?.Invoke();
    }
    
    /// <summary>
    /// Generates a grid of lat/lng points around the current map center
    /// </summary>
    private Vector2d[] GenerateCacheGrid()
    {
        Vector2d[] points = new Vector2d[gridSize * gridSize];
        int index = 0;
        
        // Calculate step size in degrees (approximately)
        // 1 degree latitude ≈ 111 km
        float stepKm = (radiusInKm * 2) / (gridSize - 1);
        float stepDegrees = stepKm / 111.32f;
        
        // Start from top-left corner of the grid
        double startLat = currentMapCenter.x + (radiusInKm / 111.32f);
        double startLng = currentMapCenter.y - (radiusInKm / 111.32f);
        
        for (int row = 0; row < gridSize; row++)
        {
            for (int col = 0; col < gridSize; col++)
            {
                double lat = startLat - (row * stepDegrees);
                double lng = startLng + (col * stepDegrees);
                
                points[index] = new Vector2d(lat, lng);
                index++;
            }
        }
        
        return points;
    }
    
    /// <summary>
    /// Check if tiles are already cached for a specific map
    /// </summary>
    public bool HasCachedTiles(string mapId)
    {
        return PlayerPrefs.GetInt($"MapCache_{mapId}_Complete", 0) == 1;
    }
    
    /// <summary>
    /// Check if any map has cached tiles
    /// </summary>
    public bool HasAnyCachedTiles()
    {
        // This is less efficient but checks all possible map caches
        // You could also maintain a list of cached map IDs
        string[] possibleMapIds = { "MAP-01", "MAP-02", "MAP-03" }; // Add more as needed
        foreach (string mapId in possibleMapIds)
        {
            if (HasCachedTiles(mapId))
                return true;
        }
        return false;
    }
    
    /// <summary>
    /// Check if cache is stale for a specific map (older than specified days)
    /// </summary>
    public bool IsCacheStale(string mapId, int maxAgeDays = 30)
    {
        if (!HasCachedTiles(mapId))
            return true;
        
        string cacheDateStr = PlayerPrefs.GetString($"MapCache_{mapId}_Date", "");
        if (string.IsNullOrEmpty(cacheDateStr))
            return true;
        
        try
        {
            DateTime cacheDate = DateTime.Parse(cacheDateStr);
            TimeSpan age = DateTime.UtcNow - cacheDate;
            return age.TotalDays > maxAgeDays;
        }
        catch
        {
            return true;
        }
    }
    
    /// <summary>
    /// Get cache info for a specific map as string
    /// </summary>
    public string GetCacheInfo(string mapId)
    {
        if (!HasCachedTiles(mapId))
            return "Not downloaded";
        
        string dateStr = PlayerPrefs.GetString($"MapCache_{mapId}_Date", "Unknown");
        try
        {
            DateTime cacheDate = DateTime.Parse(dateStr);
            int daysOld = (DateTime.UtcNow - cacheDate).Days;
            
            if (daysOld == 0)
                return "Downloaded today";
            else if (daysOld == 1)
                return "Downloaded yesterday";
            else
                return $"Downloaded {daysOld} days ago";
        }
        catch
        {
            return "Downloaded (date unknown)";
        }
    }
    
    /// <summary>
    /// Get all cached map IDs
    /// </summary>
    public List<string> GetAllCachedMapIds()
    {
        List<string> cachedMaps = new List<string>();
        
        // Check all possible map IDs (you might want to get this from FirestoreManager instead)
        string[] possibleMapIds = { "MAP-01", "MAP-02", "MAP-03", "MAP-04", "MAP-05" };
        
        foreach (string mapId in possibleMapIds)
        {
            if (HasCachedTiles(mapId))
            {
                cachedMaps.Add(mapId);
            }
        }
        
        return cachedMaps;
    }
    
    /// <summary>
    /// Clear the cache metadata for a specific map
    /// </summary>
    public void ClearCacheMetadata(string mapId)
    {
        PlayerPrefs.DeleteKey($"MapCache_{mapId}_Complete");
        PlayerPrefs.DeleteKey($"MapCache_{mapId}_Date");
        PlayerPrefs.DeleteKey($"MapCache_{mapId}_Center");
        PlayerPrefs.DeleteKey($"MapCache_{mapId}_Radius");
        PlayerPrefs.Save();
        
        Debug.Log($"Cache metadata cleared for map: {mapId}");
    }
    
    /// <summary>
    /// Clear ALL cache metadata for all maps
    /// </summary>
    public void ClearAllCacheMetadata()
    {
        List<string> cachedMaps = GetAllCachedMapIds();
        
        foreach (string mapId in cachedMaps)
        {
            ClearCacheMetadata(mapId);
        }
        
        Debug.Log("All cache metadata cleared");
    }
    
    /// <summary>
    /// Calculate distance between two lat/lng points in kilometers
    /// </summary>
    private double CalculateDistance(Vector2d point1, Vector2d point2)
    {
        double R = 6371; // Earth's radius in km
        
        double lat1 = point1.x * Math.PI / 180;
        double lat2 = point2.x * Math.PI / 180;
        double dLat = (point2.x - point1.x) * Math.PI / 180;
        double dLon = (point2.y - point1.y) * Math.PI / 180;
        
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1) * Math.Cos(lat2) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        
        return R * c;
    }
}