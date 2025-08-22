using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Centralized coordinate system manager for the entire map
/// Ensures all spawners use the same bounds and projection
/// </summary>
public class MapCoordinateSystem : MonoBehaviour
{
    [Header("Map References")]
    public RectTransform mapImage;
    
    [Header("Map Padding")]
    public float paddingLeft = 20f;
    public float paddingRight = 20f;
    public float paddingTop = 50f;
    public float paddingBottom = 20f;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    
    // Singleton instance
    public static MapCoordinateSystem Instance { get; private set; }
    
    // Bounds calculated from ALL data sources
    private float minLatitude, maxLatitude, minLongitude, maxLongitude;
    private float mapWidthMeters, mapHeightMeters;
    private bool boundsCalculated = false;
    
    // Data containers
    [System.Serializable]
    public class Infrastructure
    {
        public int infra_id;
        public int category_id;
        public string name;
        public float latitude;
        public float longitude;
        public string image_url;
    }
    
    [System.Serializable]
    public class InfrastructureList
    {
        public List<Infrastructure> infrastructures;
    }
    
    void Awake()
    {
        // Singleton pattern
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
    }
    
    void Start()
    {
        CalculateUniversalBounds();
    }
    
    /// <summary>
    /// Calculate bounds from ALL data sources (nodes + infrastructures)
    /// This ensures everything uses the same coordinate system
    /// </summary>
    void CalculateUniversalBounds()
    {
        List<float> allLatitudes = new List<float>();
        List<float> allLongitudes = new List<float>();
        
        // Load nodes.json
        string nodesPath = Path.Combine(Application.streamingAssetsPath, "nodes.json");
        if (File.Exists(nodesPath))
        {
            string nodesJson = File.ReadAllText(nodesPath);
            NodeList nodeList = JsonUtility.FromJson<NodeList>("{\"nodes\":" + nodesJson + "}");
            
            foreach (var node in nodeList.nodes)
            {
                allLatitudes.Add(node.latitude);
                allLongitudes.Add(node.longitude);
            }
            
            if (showDebugInfo)
                Debug.Log($"📍 Loaded {nodeList.nodes.Count} nodes for bounds calculation");
        }
        
        // Load infrastructures.json
        string infraPath = Path.Combine(Application.streamingAssetsPath, "infrastructures.json");
        if (File.Exists(infraPath))
        {
            string infraJson = File.ReadAllText(infraPath);
            InfrastructureList infraList = JsonUtility.FromJson<InfrastructureList>("{\"infrastructures\":" + infraJson + "}");
            
            foreach (var infra in infraList.infrastructures)
            {
                allLatitudes.Add(infra.latitude);
                allLongitudes.Add(infra.longitude);
            }
            
            if (showDebugInfo)
                Debug.Log($"🏢 Loaded {infraList.infrastructures.Count} infrastructures for bounds calculation");
        }
        
        if (allLatitudes.Count == 0)
        {
            Debug.LogError("❌ No coordinate data found! Check your JSON files.");
            return;
        }
        
        // Calculate actual bounds
        minLatitude = allLatitudes.Min();
        maxLatitude = allLatitudes.Max();
        minLongitude = allLongitudes.Min();
        maxLongitude = allLongitudes.Max();
        
        // Add buffer to prevent edge clipping
        float latBuffer = (maxLatitude - minLatitude) * 0.05f;
        float lonBuffer = (maxLongitude - minLongitude) * 0.05f;
        
        minLatitude -= latBuffer;
        maxLatitude += latBuffer;
        minLongitude -= lonBuffer;
        maxLongitude += lonBuffer;
        
        // Calculate map dimensions in meters
        float midLatitude = (minLatitude + maxLatitude) / 2f;
        mapWidthMeters = (maxLongitude - minLongitude) * 111320f * Mathf.Cos(midLatitude * Mathf.Deg2Rad);
        mapHeightMeters = (maxLatitude - minLatitude) * 111320f;
        
        boundsCalculated = true;
        
        if (showDebugInfo)
        {
            Debug.Log($"🗺️ UNIVERSAL BOUNDS CALCULATED:");
            Debug.Log($"   Latitude: [{minLatitude:F6}, {maxLatitude:F6}]");
            Debug.Log($"   Longitude: [{minLongitude:F6}, {maxLongitude:F6}]");
            Debug.Log($"   Dimensions: {mapWidthMeters:F1}m x {mapHeightMeters:F1}m");
            Debug.Log($"   Total data points: {allLatitudes.Count}");
        }
    }
    
    /// <summary>
    /// Convert latitude/longitude to UI position
    /// This is the ONLY conversion method all scripts should use
    /// </summary>
    public Vector2 LatLonToMapPosition(float lat, float lon)
    {
        if (!boundsCalculated)
        {
            Debug.LogError("Bounds not calculated yet! Call this after Start()");
            return Vector2.zero;
        }
        
        // Convert to meters from origin
        float midLatitude = (minLatitude + maxLatitude) / 2f;
        float xMeters = (lon - minLongitude) * 111320f * Mathf.Cos(midLatitude * Mathf.Deg2Rad);
        float yMeters = (lat - minLatitude) * 111320f;
        
        // Calculate usable area (excluding padding)
        float usableWidth = mapImage.rect.width - paddingLeft - paddingRight;
        float usableHeight = mapImage.rect.height - paddingTop - paddingBottom;
        
        // Scale meters to pixels
        float scaleX = usableWidth / mapWidthMeters;
        float scaleY = usableHeight / mapHeightMeters;
        
        // Convert to UI coordinates (centered at 0,0)
        float pixelX = xMeters * scaleX;
        float pixelY = yMeters * scaleY;
        
        // Adjust for padding and UI coordinate system
        float anchoredX = pixelX - usableWidth / 2f + paddingLeft;
        float anchoredY = pixelY - usableHeight / 2f + paddingBottom;
        
        return new Vector2(anchoredX, anchoredY);
    }
    
    /// <summary>
    /// Check if bounds are ready
    /// </summary>
    public bool AreBoundsReady()
    {
        return boundsCalculated;
    }
    
    /// <summary>
    /// Get bounds for external use
    /// </summary>
    public (float minLat, float maxLat, float minLon, float maxLon) GetBounds()
    {
        return (minLatitude, maxLatitude, minLongitude, maxLongitude);
    }
    
    /// <summary>
    /// Wait for bounds to be calculated (for use in coroutines)
    /// </summary>
    public System.Collections.IEnumerator WaitForBoundsReady()
    {
        while (!boundsCalculated)
        {
            yield return null;
        }
    }
}