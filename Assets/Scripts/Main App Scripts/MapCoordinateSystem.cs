using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections;

/// <summary>
/// Centralized coordinate system manager for the entire map
/// Enhanced to handle multi-campus maps with proper content positioning
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

    [Header("Scale Settings")]
    [Tooltip("Fixed scale: pixels per meter. Higher = more zoomed in")]
    public float fixedScale = 0.2f;
    
    [Header("Content Margins")]
    [Tooltip("Extra space around actual content to ensure everything is visible")]
    public float contentMargin = 300f;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool showContentBounds = true;

    // Singleton instance
    public static MapCoordinateSystem Instance { get; private set; }

    // Bounds calculated from filtered campus data
    private float minLatitude, maxLatitude, minLongitude, maxLongitude;
    private float contentMinLat, contentMaxLat, contentMinLon, contentMaxLon; // Actual content bounds
    private float mapWidthMeters, mapHeightMeters;
    private bool boundsCalculated = false;

    // Current campus filter
    private List<string> currentCampusIds = new List<string>();

    void Awake()
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
    }

    void Start()
    {
        Debug.Log("MapCoordinateSystem ready - waiting for campus assignment");
    }

    /// <summary>
    /// Recalculate bounds for specific campuses with enhanced content positioning
    /// </summary>
    public IEnumerator RecalculateBoundsForCampuses(List<string> campusIds)
    {
        currentCampusIds.Clear();
        currentCampusIds.AddRange(campusIds);
        boundsCalculated = false;

        Debug.Log($"Recalculating bounds for campuses: {string.Join(", ", campusIds)}");

        List<float> allLatitudes = new List<float>();
        List<float> allLongitudes = new List<float>();

        // Load and filter nodes by campus
        string nodesPath = Path.Combine(Application.streamingAssetsPath, "nodes.json");
        if (File.Exists(nodesPath))
        {
            string nodesJson = File.ReadAllText(nodesPath);
            NodeList nodeList = JsonUtility.FromJson<NodeList>("{\"nodes\":" + nodesJson + "}");

            var filteredNodes = nodeList.nodes.Where(n =>
                campusIds.Contains(n.campus_id) &&
                n.is_barrier &&
                n.is_active
            ).ToList();

            foreach (var node in filteredNodes)
            {
                allLatitudes.Add(node.latitude);
                allLongitudes.Add(node.longitude);
            }

            if (showDebugInfo)
                Debug.Log($"Loaded {filteredNodes.Count} nodes for campuses: {string.Join(", ", campusIds)}");
        }

        // Load and filter infrastructures by campus
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
        }

        if (allLatitudes.Count == 0)
        {
            Debug.LogError("No coordinate data found for selected campuses!");
            yield break;
        }

        // Store actual content bounds (without buffer)
        contentMinLat = allLatitudes.Min();
        contentMaxLat = allLatitudes.Max();
        contentMinLon = allLongitudes.Min();
        contentMaxLon = allLongitudes.Max();

        // Calculate content dimensions in meters
        float midLatitude = (contentMinLat + contentMaxLat) / 2f;
        float contentWidthMeters = (contentMaxLon - contentMinLon) * 111320f * Mathf.Cos(midLatitude * Mathf.Deg2Rad);
        float contentHeightMeters = (contentMaxLat - contentMinLat) * 111320f;

        // Convert content margin from pixels to meters
        float marginWidthMeters = contentMargin / fixedScale;
        float marginHeightMeters = contentMargin / fixedScale;

        // Expand bounds to include margins
        float lonMarginDegrees = marginWidthMeters / (111320f * Mathf.Cos(midLatitude * Mathf.Deg2Rad));
        float latMarginDegrees = marginHeightMeters / 111320f;

        minLatitude = contentMinLat - latMarginDegrees;
        maxLatitude = contentMaxLat + latMarginDegrees;
        minLongitude = contentMinLon - lonMarginDegrees;
        maxLongitude = contentMaxLon + lonMarginDegrees;

        // Calculate final map dimensions
        mapWidthMeters = (maxLongitude - minLongitude) * 111320f * Mathf.Cos(midLatitude * Mathf.Deg2Rad);
        mapHeightMeters = (maxLatitude - minLatitude) * 111320f;

        // Resize the mapImage RectTransform
        ResizeMapImageForContent();

        boundsCalculated = true;

        if (showDebugInfo)
        {
            Debug.Log($"BOUNDS CALCULATED FOR CAMPUSES: {string.Join(", ", campusIds)}");
            Debug.Log($" Content Bounds: Lat[{contentMinLat:F6}, {contentMaxLat:F6}] Lon[{contentMinLon:F6}, {contentMaxLon:F6}]");
            Debug.Log($" Final Bounds: Lat[{minLatitude:F6}, {maxLatitude:F6}] Lon[{minLongitude:F6}, {maxLongitude:F6}]");
            Debug.Log($" Content Size: {contentWidthMeters:F1}m x {contentHeightMeters:F1}m");
            Debug.Log($" Total Map Size: {mapWidthMeters:F1}m x {mapHeightMeters:F1}m");
            Debug.Log($" UI Size: {mapWidthMeters * fixedScale:F1}px x {mapHeightMeters * fixedScale:F1}px");
            Debug.Log($" Margin: {contentMargin}px ({marginWidthMeters:F1}m x {marginHeightMeters:F1}m)");
        }

        if (showContentBounds)
        {
            // Show where each campus content is positioned
            DebugCampusPositions(campusIds);
        }
    }

    /// <summary>
    /// Debug method to show where campus content is positioned
    /// </summary>
    private void DebugCampusPositions(List<string> campusIds)
    {
        Debug.Log("=== CAMPUS CONTENT POSITIONS ===");
        
        // Sample a few points from each campus to show their UI positions
        string nodesPath = Path.Combine(Application.streamingAssetsPath, "nodes.json");
        if (File.Exists(nodesPath))
        {
            string nodesJson = File.ReadAllText(nodesPath);
            NodeList nodeList = JsonUtility.FromJson<NodeList>("{\"nodes\":" + nodesJson + "}");

            foreach (string campusId in campusIds)
            {
                var campusNodes = nodeList.nodes.Where(n =>
                    n.campus_id == campusId &&
                    n.is_barrier &&
                    n.is_active
                ).Take(3).ToList(); // Take first 3 nodes as samples

                Debug.Log($"Campus {campusId}:");
                foreach (var node in campusNodes)
                {
                    Vector2 uiPos = LatLonToMapPosition(node.latitude, node.longitude);
                    Debug.Log($"  Node at ({node.latitude:F6}, {node.longitude:F6}) -> UI({uiPos.x:F1}, {uiPos.y:F1})");
                }
            }
        }
        
        // Show map bounds in UI coordinates
        Vector2 bottomLeft = LatLonToMapPosition(minLatitude, minLongitude);
        Vector2 topRight = LatLonToMapPosition(maxLatitude, maxLongitude);
        Vector2 contentBottomLeft = LatLonToMapPosition(contentMinLat, contentMinLon);
        Vector2 contentTopRight = LatLonToMapPosition(contentMaxLat, contentMaxLon);
        
        Debug.Log($"Map UI Bounds: BottomLeft({bottomLeft.x:F1}, {bottomLeft.y:F1}) to TopRight({topRight.x:F1}, {topRight.y:F1})");
        Debug.Log($"Content UI Bounds: BottomLeft({contentBottomLeft.x:F1}, {contentBottomLeft.y:F1}) to TopRight({contentTopRight.x:F1}, {contentTopRight.y:F1})");
        Debug.Log($"Map Image Size: {mapImage.sizeDelta.x:F1}px x {mapImage.sizeDelta.y:F1}px");
    }

    /// <summary>
    /// Resize the map image to fit all content with margins
    /// </summary>
    private void ResizeMapImageForContent()
    {
        if (mapImage != null)
        {
            // Calculate total required size: content + margins + padding
            float totalWidth = mapWidthMeters * fixedScale + paddingLeft + paddingRight;
            float totalHeight = mapHeightMeters * fixedScale + paddingTop + paddingBottom;
            
            mapImage.sizeDelta = new Vector2(totalWidth, totalHeight);
            
            if (showDebugInfo)
            {
                Debug.Log($"Resized mapImage to: {totalWidth:F1}px x {totalHeight:F1}px");
                Debug.Log($"  Content area: {mapWidthMeters * fixedScale:F1}px x {mapHeightMeters * fixedScale:F1}px");
                Debug.Log($"  Margins: {contentMargin}px per side");
            }
        }
    }

    /// <summary>
    /// Convert latitude/longitude to UI position
    /// </summary>
    public Vector2 LatLonToMapPosition(float lat, float lon)
    {
        if (!boundsCalculated)
        {
            Debug.LogError("Bounds not calculated yet! Wait for RecalculateBoundsForCampuses()");
            return Vector2.zero;
        }

        // Convert lat/lon to meters from the minimum bounds (including margins)
        float midLatitude = (minLatitude + maxLatitude) / 2f;
        float xMeters = (lon - minLongitude) * 111320f * Mathf.Cos(midLatitude * Mathf.Deg2Rad);
        float yMeters = (lat - minLatitude) * 111320f;

        // Convert meters to pixels using fixed scale
        float pixelX = xMeters * fixedScale;
        float pixelY = yMeters * fixedScale;

        // Adjust for padding and anchoring (map is centered at 0,0)
        float anchoredX = pixelX - (mapWidthMeters * fixedScale) / 2f + paddingLeft;
        float anchoredY = pixelY - (mapHeightMeters * fixedScale) / 2f + paddingBottom;

        return new Vector2(anchoredX, anchoredY);
    }

    /// <summary>
    /// Convert UI position back to latitude/longitude
    /// </summary>
    public Vector2 MapPositionToLatLon(Vector2 mapPosition)
    {
        if (!boundsCalculated)
        {
            Debug.LogError("Bounds not calculated yet!");
            return Vector2.zero;
        }

        // Remove padding and anchoring offset
        float pixelX = mapPosition.x + (mapWidthMeters * fixedScale) / 2f - paddingLeft;
        float pixelY = mapPosition.y + (mapHeightMeters * fixedScale) / 2f - paddingBottom;

        // Convert pixels to meters
        float xMeters = pixelX / fixedScale;
        float yMeters = pixelY / fixedScale;

        // Convert meters to lat/lon
        float midLatitude = (minLatitude + maxLatitude) / 2f;
        float longitude = minLongitude + xMeters / (111320f * Mathf.Cos(midLatitude * Mathf.Deg2Rad));
        float latitude = minLatitude + yMeters / 111320f;

        return new Vector2(latitude, longitude);
    }

    /// <summary>
    /// Get the actual content bounds (without margins) for debugging
    /// </summary>
    public (float minLat, float maxLat, float minLon, float maxLon) GetContentBounds()
    {
        return (contentMinLat, contentMaxLat, contentMinLon, contentMaxLon);
    }

    /// <summary>
    /// Check if all content fits within the current map image
    /// </summary>
    public void ValidateContentFit()
    {
        if (!boundsCalculated) return;

        Vector2 contentBottomLeft = LatLonToMapPosition(contentMinLat, contentMinLon);
        Vector2 contentTopRight = LatLonToMapPosition(contentMaxLat, contentMaxLon);
        
        Vector2 mapSize = mapImage.sizeDelta;
        Vector2 mapCenter = Vector2.zero; // Map is anchored at center

        // Check if content extends beyond map bounds
        float mapLeft = mapCenter.x - mapSize.x / 2f;
        float mapRight = mapCenter.x + mapSize.x / 2f;
        float mapBottom = mapCenter.y - mapSize.y / 2f;
        float mapTop = mapCenter.y + mapSize.y / 2f;

        bool contentFits = contentBottomLeft.x >= mapLeft && 
                          contentTopRight.x <= mapRight &&
                          contentBottomLeft.y >= mapBottom && 
                          contentTopRight.y <= mapTop;

        Debug.Log($"=== CONTENT FIT VALIDATION ===");
        Debug.Log($"Map UI Bounds: Left={mapLeft:F1}, Right={mapRight:F1}, Bottom={mapBottom:F1}, Top={mapTop:F1}");
        Debug.Log($"Content UI Bounds: Left={contentBottomLeft.x:F1}, Right={contentTopRight.x:F1}, Bottom={contentBottomLeft.y:F1}, Top={contentTopRight.y:F1}");
        Debug.Log($"Content Fits: {contentFits}");
        
        if (!contentFits)
        {
            Debug.LogWarning("Content extends beyond map bounds! Some campus content may be cut off.");
            
            if (contentTopRight.x > mapRight)
                Debug.LogWarning($"Content extends {contentTopRight.x - mapRight:F1}px beyond RIGHT edge");
            if (contentBottomLeft.x < mapLeft)
                Debug.LogWarning($"Content extends {mapLeft - contentBottomLeft.x:F1}px beyond LEFT edge");
            if (contentTopRight.y > mapTop)
                Debug.LogWarning($"Content extends {contentTopRight.y - mapTop:F1}px beyond TOP edge");
            if (contentBottomLeft.y < mapBottom)
                Debug.LogWarning($"Content extends {mapBottom - contentBottomLeft.y:F1}px beyond BOTTOM edge");
        }
    }

    /// <summary>
    /// Set content margin and recalculate if needed
    /// </summary>
    public void SetContentMargin(float newMargin)
    {
        contentMargin = newMargin;
        if (boundsCalculated)
        {
            // Recalculate bounds with new margin
            StartCoroutine(RecalculateBoundsForCampuses(currentCampusIds));
        }
    }

    public void SetFixedScale(float newScale)
    {
        fixedScale = newScale;
        if (boundsCalculated)
        {
            StartCoroutine(RecalculateBoundsForCampuses(currentCampusIds));
            if (showDebugInfo)
                Debug.Log($"Updated scale to {fixedScale:F2} pixels/meter");
        }
    }

    public float GetFixedScale()
    {
        return fixedScale;
    }

    public Vector2 GetMapSizeInPixels()
    {
        return new Vector2(mapWidthMeters * fixedScale, mapHeightMeters * fixedScale);
    }

    public bool IsNodeInCurrentCampuses(Node node)
    {
        string nodeCampusId = "C-" + node.campus_id.ToString().PadLeft(3, '0');
        return currentCampusIds.Contains(nodeCampusId);
    }

    public List<string> GetCurrentCampusIds()
    {
        return new List<string>(currentCampusIds);
    }

    public bool AreBoundsReady()
    {
        return boundsCalculated;
    }

    public (float minLat, float maxLat, float minLon, float maxLon) GetBounds()
    {
        return (minLatitude, maxLatitude, minLongitude, maxLongitude);
    }

    public System.Collections.IEnumerator WaitForBoundsReady()
    {
        while (!boundsCalculated)
        {
            yield return null;
        }
    }

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
}