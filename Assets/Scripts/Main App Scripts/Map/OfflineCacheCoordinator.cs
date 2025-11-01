using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Mapbox.Utils;

public class OfflineCacheCoordinator : MonoBehaviour
{
    [Header("References")]
    public MapboxOfflineManager offlineManager;
    
    [Header("Caching Settings")]
    public int cacheMaxAgeDays = 30;
    
    [Header("Status")]
    public bool isInitialized = false;
    public string statusMessage = "Initializing...";
    public List<MapInfo> availableMaps = new List<MapInfo>();
    public MapInfo currentSelectedMap = null;
    
    void Start()
    {
        StartCoroutine(InitializeOfflineCapability());
    }
    
    private IEnumerator InitializeOfflineCapability()
    {
        statusMessage = "Waiting for Firebase...";
        
        yield return new WaitUntil(() => FirestoreManager.Instance != null && FirestoreManager.Instance.IsReady);
        
        statusMessage = "Loading map data...";
        
        yield return new WaitUntil(() => FirestoreManager.Instance.AvailableMaps != null && 
                                         FirestoreManager.Instance.AvailableMaps.Count > 0);
        
        availableMaps = FirestoreManager.Instance.AvailableMaps;
        
        if (availableMaps.Count > 0)
        {
            currentSelectedMap = availableMaps[0];
        }
        
        statusMessage = "Ready";
        isInitialized = true;
    }
    
    private void StartCachingProcess(MapInfo mapInfo)
    {
        if (offlineManager == null) return;
        
        offlineManager.OnCacheProgress += OnCacheProgress;
        offlineManager.OnCacheComplete += OnCacheComplete;
        offlineManager.OnCacheError += OnCacheError;
        
        Vector2d mapCenter = new Mapbox.Utils.Vector2d(mapInfo.center_lat, mapInfo.center_lng);
        offlineManager.StartCachingMap(mapInfo.map_id, mapCenter);
    }
    
    private void OnCacheProgress(float progress)
    {
        statusMessage = $"Caching tiles: {progress * 100:F0}%";
    }
    
    private void OnCacheComplete()
    {
        statusMessage = "Map tiles cached successfully!";
        
        offlineManager.OnCacheProgress -= OnCacheProgress;
        offlineManager.OnCacheComplete -= OnCacheComplete;
        offlineManager.OnCacheError -= OnCacheError;
        
        isInitialized = true;
    }
    
    private void OnCacheError(string error)
    {
        statusMessage = $"Cache error: {error}";
        
        offlineManager.OnCacheProgress -= OnCacheProgress;
        offlineManager.OnCacheComplete -= OnCacheComplete;
        offlineManager.OnCacheError -= OnCacheError;
        
        isInitialized = true;
    }
    
    public void DownloadMapForOffline(string mapId = null)
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            statusMessage = "No internet connection";
            return;
        }
        
        if (offlineManager == null)
        {
            statusMessage = "Error: Offline manager not found";
            return;
        }
        
        if (offlineManager.isCaching)
        {
            statusMessage = "Already downloading...";
            return;
        }
        
        MapInfo mapToDownload = null;
        
        if (!string.IsNullOrEmpty(mapId))
        {
            mapToDownload = availableMaps.Find(m => m.map_id == mapId);
        }
        else if (currentSelectedMap != null)
        {
            mapToDownload = currentSelectedMap;
        }
        
        if (mapToDownload == null)
        {
            statusMessage = "Error: No map selected";
            return;
        }
        
        statusMessage = $"Downloading {mapToDownload.map_name}...";
        
        StartCachingProcess(mapToDownload);
    }
    
    public void SetCurrentMap(string mapId)
    {
        currentSelectedMap = availableMaps.Find(m => m.map_id == mapId);
    }
    
    public bool IsMapDownloaded(string mapId)
    {
        if (offlineManager == null) return false;
        return offlineManager.HasCachedTiles(mapId);
    }
    
    public string GetMapCacheStatus(string mapId)
    {
        if (offlineManager == null) return "Unknown";
        return offlineManager.GetCacheInfo(mapId);
    }
    
    public void ClearCache(string mapId = null)
    {
        if (offlineManager != null)
        {
            if (string.IsNullOrEmpty(mapId))
            {
                offlineManager.ClearAllCacheMetadata();
                statusMessage = "All caches cleared";
            }
            else
            {
                offlineManager.ClearCacheMetadata(mapId);
                statusMessage = $"Cache cleared for {mapId}";
            }
        }
    }
    
    void OnDestroy()
    {
        if (offlineManager != null)
        {
            offlineManager.OnCacheProgress -= OnCacheProgress;
            offlineManager.OnCacheComplete -= OnCacheComplete;
            offlineManager.OnCacheError -= OnCacheError;
        }
    }
}