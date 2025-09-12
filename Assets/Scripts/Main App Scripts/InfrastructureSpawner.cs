using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using System.Linq;
using Mapbox.Utils;
using Mapbox.Unity.Map;
using TMPro;

public class InfrastructureSpawner : MonoBehaviour
{
    [Header("Mapbox")]
    public AbstractMap mapboxMap;

    [Header("Prefabs")]
    public GameObject infrastructurePrefab;

    [Header("JSON Files")]
    public string nodesFileName = "nodes.json";
    public string infrastructureFileName = "infrastructure.json";
    public string categoriesFileName = "categories.json";

    [Header("Settings")]
    public bool enableDebugLogs = true;
    public List<string> targetCampusIds = new List<string>();
    public float infrastructureSize = 3.0f;
    public float heightOffset = 15f;

    // Track spawned infrastructure with their location components
    private List<InfrastructureNode> spawnedInfrastructure = new List<InfrastructureNode>();
    private Dictionary<string, InfrastructureNode> infraIdToComponent = new Dictionary<string, InfrastructureNode>();

    private bool hasSpawned = false;
    private bool isSpawning = false;

    void Awake()
    {
        // Find map if not assigned
        if (mapboxMap == null)
        {
            mapboxMap = FindObjectOfType<AbstractMap>();
        }
    }

    void Start()
    {
        DebugLog("🏢 InfrastructureSpawner started");
        
        if (mapboxMap == null)
        {
            Debug.LogError("❌ No AbstractMap found! Please assign mapboxMap in inspector");
            return;
        }

        DebugLog("📍 Found AbstractMap, starting automatic spawn process");
        
        // Start the spawn process immediately
        StartCoroutine(WaitForMapAndSpawn());
    }

    private IEnumerator WaitForMapAndSpawn()
    {
        DebugLog("⏳ Waiting for map to be ready...");
        
        // Wait for map initialization
        float timeout = 30f;
        float elapsed = 0f;
        
        while (elapsed < timeout)
        {
            if (mapboxMap != null && mapboxMap.gameObject.activeInHierarchy)
            {
                DebugLog($"🗺️ Map seems ready after {elapsed:F1}s, attempting spawn...");
                break;
            }
            
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
            
            if (elapsed % 5f < 0.6f)
            {
                DebugLog($"⏳ Still waiting for map... ({elapsed:F1}s/{timeout}s)");
            }
        }

        if (elapsed >= timeout)
        {
            Debug.LogError("❌ Map initialization timeout!");
            yield break;
        }

        // Additional delay to ensure map is fully ready
        yield return new WaitForSeconds(2f);
        
        // Start spawning
        yield return StartCoroutine(LoadAndSpawnInfrastructure());
    }

    public IEnumerator LoadAndSpawnInfrastructure()
    {
        if (isSpawning)
        {
            DebugLog("⚠️ Already spawning infrastructure");
            yield break;
        }

        isSpawning = true;
        DebugLog("🏢 Starting LoadAndSpawnInfrastructure...");

        List<InfrastructureData> infrastructureToSpawn = null;
        try
        {
            // Get campus IDs to spawn
            List<string> campusIds = GetTargetCampusIds();
            if (campusIds.Count == 0)
            {
                Debug.LogError("❌ No campus IDs found in data");
                yield break;
            }

            DebugLog($"🏫 Target campus IDs: {string.Join(", ", campusIds)}");

            // Clear existing objects first
            ClearSpawnedInfrastructure();

            // Load all required JSON files
            Node[] nodes = LoadNodesFromJSON();
            Infrastructure[] infrastructures = LoadInfrastructureFromJSON();
            Category[] categories = LoadCategoriesFromJSON();

            if (nodes == null || infrastructures == null)
            {
                Debug.LogError("❌ Failed to load required JSON files");
                yield break;
            }

            // Build infrastructure data with location info
            infrastructureToSpawn = BuildInfrastructureData(nodes, infrastructures, categories, campusIds);
            DebugLog($"🏢 Found {infrastructureToSpawn.Count} infrastructure items to spawn");

            if (infrastructureToSpawn.Count == 0)
            {
                Debug.LogWarning("⚠️ No infrastructure found matching criteria");
                yield break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error in LoadAndSpawnInfrastructure: {e.Message}");
            yield break;
        }
        finally
        {
            isSpawning = false;
        }

        // Spawn the infrastructure outside the try-catch block
        yield return StartCoroutine(SpawnInfrastructureItems(infrastructureToSpawn));

        hasSpawned = true;
        Debug.Log($"✅ InfrastructureSpawner completed: {spawnedInfrastructure.Count} infrastructure items spawned");
    }

    private List<string> GetTargetCampusIds()
    {
        // If specific campus IDs are set in inspector, use those
        if (targetCampusIds != null && targetCampusIds.Count > 0)
        {
            return targetCampusIds.Where(id => !string.IsNullOrEmpty(id)).ToList();
        }

        // Otherwise, get all available campus IDs from the data
        return GetAllCampusIdsFromData();
    }

    private List<string> GetAllCampusIdsFromData()
    {
        string nodesPath = Path.Combine(Application.streamingAssetsPath, nodesFileName);
        DebugLog($"📂 Looking for nodes file at: {nodesPath}");
        
        if (!File.Exists(nodesPath))
        {
            Debug.LogError($"❌ Nodes file not found: {nodesPath}");
            return new List<string>();
        }

        try
        {
            string jsonContent = File.ReadAllText(nodesPath);
            Node[] nodes = JsonHelper.FromJson<Node>(jsonContent);
            
            if (nodes == null || nodes.Length == 0)
            {
                Debug.LogError("❌ No nodes found in JSON file");
                return new List<string>();
            }

            var campusIds = nodes
                .Where(n => n != null && n.type == "infrastructure" && n.is_active && !string.IsNullOrEmpty(n.campus_id))
                .Select(n => n.campus_id)
                .Distinct()
                .ToList();

            DebugLog($"🏫 Found {campusIds.Count} unique campus IDs with infrastructure");
            return campusIds;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error reading nodes file: {e.Message}");
            return new List<string>();
        }
    }

    private Node[] LoadNodesFromJSON()
    {
        string nodesPath = Path.Combine(Application.streamingAssetsPath, nodesFileName);
        DebugLog($"📂 Loading nodes from: {nodesPath}");

        if (!File.Exists(nodesPath))
        {
            Debug.LogError($"❌ Nodes file not found: {nodesPath}");
            return null;
        }

        try
        {
            string jsonContent = File.ReadAllText(nodesPath);
            DebugLog($"📄 Read {jsonContent.Length} characters from nodes file");
            
            Node[] nodes = JsonHelper.FromJson<Node>(jsonContent);
            DebugLog($"📊 Parsed {nodes?.Length ?? 0} nodes from JSON");
            
            return nodes;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error loading nodes JSON: {e.Message}");
            return null;
        }
    }

    private Infrastructure[] LoadInfrastructureFromJSON()
    {
        string infraPath = Path.Combine(Application.streamingAssetsPath, infrastructureFileName);
        DebugLog($"📂 Loading infrastructure from: {infraPath}");

        if (!File.Exists(infraPath))
        {
            Debug.LogError($"❌ Infrastructure file not found: {infraPath}");
            return null;
        }

        try
        {
            string jsonContent = File.ReadAllText(infraPath);
            DebugLog($"📄 Read {jsonContent.Length} characters from infrastructure file");
            
            Infrastructure[] infrastructures = JsonHelper.FromJson<Infrastructure>(jsonContent);
            DebugLog($"📊 Parsed {infrastructures?.Length ?? 0} infrastructures from JSON");
            
            return infrastructures;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error loading infrastructure JSON: {e.Message}");
            return null;
        }
    }

    private Category[] LoadCategoriesFromJSON()
    {
        string categoriesPath = Path.Combine(Application.streamingAssetsPath, categoriesFileName);
        DebugLog($"📂 Loading categories from: {categoriesPath}");

        if (!File.Exists(categoriesPath))
        {
            Debug.LogWarning($"⚠️ Categories file not found: {categoriesPath}");
            return null;
        }

        try
        {
            string jsonContent = File.ReadAllText(categoriesPath);
            DebugLog($"📄 Read {jsonContent.Length} characters from categories file");
            
            Category[] categories = JsonHelper.FromJson<Category>(jsonContent);
            DebugLog($"📊 Parsed {categories?.Length ?? 0} categories from JSON");
            
            return categories;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error loading categories JSON: {e.Message}");
            return null;
        }
    }

    private List<InfrastructureData> BuildInfrastructureData(Node[] nodes, Infrastructure[] infrastructures, 
                                                            Category[] categories, List<string> campusIds)
    {
        var infrastructureData = new List<InfrastructureData>();

        // Create dictionaries for quick lookup
        var infraDict = infrastructures.ToDictionary(i => i.infra_id, i => i);
        var categoryDict = categories?.ToDictionary(c => c.category_id.ToString(), c => c) ?? new Dictionary<string, Category>();

        DebugLog($"🔍 Created infrastructure dictionary with {infraDict.Count} entries");
        DebugLog($"🔍 Created category dictionary with {categoryDict.Count} entries");

        // Filter infrastructure nodes
        var infrastructureNodes = nodes.Where(n =>
            n != null &&
            n.type == "infrastructure" &&
            n.is_active &&
            campusIds.Contains(n.campus_id) &&
            !string.IsNullOrEmpty(n.related_infra_id) &&
            IsValidCoordinate(n.latitude, n.longitude)
        ).ToList();

        DebugLog($"🔍 Infrastructure node filtering:");
        DebugLog($"  - Total nodes: {nodes.Length}");
        DebugLog($"  - Infrastructure nodes: {nodes.Count(n => n?.type == "infrastructure")}");
        DebugLog($"  - Active infrastructure: {nodes.Count(n => n?.type == "infrastructure" && n.is_active)}");
        DebugLog($"  - Campus matched: {infrastructureNodes.Count}");

        // Build combined data
        foreach (var node in infrastructureNodes)
        {
            if (infraDict.TryGetValue(node.related_infra_id, out Infrastructure infrastructure))
            {
                categoryDict.TryGetValue(infrastructure.category_id.ToString(), out Category category);

                var data = new InfrastructureData
                {
                    Node = node,
                    Infrastructure = infrastructure,
                    Category = category
                };

                infrastructureData.Add(data);
                DebugLog($"✅ Matched: Node {node.node_id} -> Infrastructure {infrastructure.name}");
            }
            else
            {
                Debug.LogWarning($"⚠️ No infrastructure found for node {node.node_id} with related_infra_id: {node.related_infra_id}");
            }
        }

        return infrastructureData;
    }

    private IEnumerator SpawnInfrastructureItems(List<InfrastructureData> infrastructureData)
    {
        DebugLog($"🏢 Spawning {infrastructureData.Count} infrastructure items...");

        int spawnedCount = 0;
        foreach (var data in infrastructureData)
        {
            bool shouldYield = false;
            try
            {
                if (infrastructurePrefab == null)
                {
                    Debug.LogError("❌ Infrastructure prefab is null!");
                    break;
                }

                // Create the infrastructure GameObject
                GameObject infraObj = Instantiate(infrastructurePrefab, Vector3.zero, Quaternion.identity, mapboxMap.transform);
                infraObj.name = $"Infrastructure_{data.Infrastructure.name}_{data.Node.node_id}";
                infraObj.transform.localScale = Vector3.one * infrastructureSize;

                // Add the location-tracking component
                InfrastructureNode infraComponent = infraObj.AddComponent<InfrastructureNode>();
                infraComponent.Initialize(mapboxMap, data, heightOffset);
                
                spawnedInfrastructure.Add(infraComponent);
                
                // Add to lookup dictionary
                infraIdToComponent[data.Infrastructure.infra_id] = infraComponent;
                
                spawnedCount++;

                DebugLog($"🏢 Spawned infrastructure: {data.Infrastructure.name} (ID: {data.Infrastructure.infra_id})");
                DebugLog($"   Geo: ({data.Node.latitude}, {data.Node.longitude})");

                // Mark for yielding periodically to avoid frame drops
                if (spawnedCount % 5 == 0)
                {
                    shouldYield = true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Error spawning infrastructure {data.Infrastructure.infra_id}: {e.Message}");
            }
            
            if (shouldYield)
            {
                yield return null;
            }
        }

        DebugLog($"✅ Successfully spawned {spawnedCount} infrastructure items");
    }

    public void ClearSpawnedInfrastructure()
    {
        DebugLog($"🧹 Clearing {spawnedInfrastructure.Count} infrastructure items");

        foreach (var infrastructure in spawnedInfrastructure)
        {
            if (infrastructure != null && infrastructure.gameObject != null)
            {
                DestroyImmediate(infrastructure.gameObject);
            }
        }

        spawnedInfrastructure.Clear();
        infraIdToComponent.Clear();
        hasSpawned = false;
        
        DebugLog("✅ Cleared all infrastructure items");
    }

    // Manual spawn methods for testing
    public void ManualSpawn()
    {
        DebugLog("🔄 Manual spawn triggered");
        StartCoroutine(LoadAndSpawnInfrastructure());
    }

    [System.Obsolete("Debug method - remove in production")]
    public void ForceResetSpawning()
    {
        DebugLog("🔄 Force resetting spawning state");
        isSpawning = false;
        hasSpawned = false;
        StopAllCoroutines();
        DebugLog($"✅ Reset complete");
    }

    private bool IsValidCoordinate(float lat, float lon)
    {
        return !float.IsNaN(lat) && !float.IsNaN(lon) &&
               !float.IsInfinity(lat) && !float.IsInfinity(lon) &&
               lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[InfrastructureSpawner] {message}");
        }
    }

    void Update()
    {
        // Debug controls
        if (Application.isEditor && enableDebugLogs)
        {
            if (Input.GetKeyDown(KeyCode.I))
            {
                Debug.Log($"=== INFRASTRUCTURE SPAWNER STATUS ===");
                Debug.Log($"Has spawned: {hasSpawned}");
                Debug.Log($"Is spawning: {isSpawning}");
                Debug.Log($"Infrastructure spawned: {spawnedInfrastructure.Count}");
                Debug.Log($"Map assigned: {mapboxMap != null}");
            }

            if (Input.GetKeyDown(KeyCode.T))
            {
                ManualSpawn();
            }

            if (Input.GetKeyDown(KeyCode.Y))
            {
                ClearSpawnedInfrastructure();
            }
        }
    }
}

// Helper class to combine infrastructure data with location
[System.Serializable]
public class InfrastructureData
{
    public Node Node;
    public Infrastructure Infrastructure;
    public Category Category;
}

// Component that keeps an infrastructure item at its geographic location
public class InfrastructureNode : MonoBehaviour
{
    private AbstractMap map;
    private InfrastructureData infrastructureData;
    private Vector2d geoLocation;
    private float heightOffset;
    
    public InfrastructureData GetInfrastructureData() => infrastructureData;
    public Vector2d GetGeoLocation() => geoLocation;

    public void Initialize(AbstractMap mapReference, InfrastructureData data, float height)
    {
        map = mapReference;
        infrastructureData = data;
        geoLocation = new Vector2d(data.Node.latitude, data.Node.longitude);
        heightOffset = height;
        
        // Set up the UI components with infrastructure data
        SetupInfrastructureDisplay();
        
        // Set initial position
        UpdatePosition();
    }
    
    private void SetupInfrastructureDisplay()
    {
        // Set building name on TextMeshPro 3D component
        TextMeshPro label3D = GetComponentInChildren<TextMeshPro>();
        if (label3D != null) 
        {
            label3D.text = infrastructureData.Infrastructure.name;
            DebugLog($"✅ Set 3D label text: {infrastructureData.Infrastructure.name}");
        }

        // Also check for TextMeshProUGUI in case there's a world space canvas
        TextMeshProUGUI labelUI = GetComponentInChildren<TextMeshProUGUI>();
        if (labelUI != null) 
        {
            labelUI.text = infrastructureData.Infrastructure.name;
            DebugLog($"✅ Set UI label text: {infrastructureData.Infrastructure.name}");
        }

        // Set material/texture based on category
        if (infrastructureData.Category != null)
        {
            SetupInfrastructureMaterial();
        }

        // Set up any additional 3D components as needed
        SetupInfrastructureColor();
    }

    private void SetupInfrastructureMaterial()
    {
        // Try to load a material based on category
        if (infrastructureData.Category != null && !string.IsNullOrEmpty(infrastructureData.Category.icon))
        {
            // First try to load as material
            string materialPath = infrastructureData.Category.icon.Replace(".png", "").Replace(".jpg", "");
            Material categoryMaterial = Resources.Load<Material>(materialPath);
            
            if (categoryMaterial != null)
            {
                ApplyMaterialToRenderers(categoryMaterial);
                DebugLog($"✅ Applied material: {materialPath}");
                return;
            }

            // If no material, try to load as texture and create material
            Texture2D categoryTexture = Resources.Load<Texture2D>(materialPath);
            if (categoryTexture != null)
            {
                Material newMaterial = new Material(Shader.Find("Standard"));
                newMaterial.mainTexture = categoryTexture;
                ApplyMaterialToRenderers(newMaterial);
                DebugLog($"✅ Created and applied material from texture: {materialPath}");
                return;
            }

            DebugLog($"⚠️ Could not load material or texture: {materialPath}");
        }
    }

    private void SetupInfrastructureColor()
    {
        // Apply color based on category or use default
        Color infrastructureColor = Color.white;
        
        if (infrastructureData.Category != null)
        {
            // You could define colors per category ID or use a hash-based color
            infrastructureColor = GetColorForCategory(infrastructureData.Category.category_id);
        }
        
        // Apply color to all renderers
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (renderer.material != null)
            {
                renderer.material.color = infrastructureColor;
            }
        }
        
        DebugLog($"✅ Applied color: {infrastructureColor} to {renderers.Length} renderers");
    }

    private Color GetColorForCategory(long categoryId)
    {
        // Generate a consistent color based on category ID
        UnityEngine.Random.State oldState = UnityEngine.Random.state;
        UnityEngine.Random.InitState((int)categoryId);
        
        Color color = new Color(
            UnityEngine.Random.Range(0.3f, 1f),
            UnityEngine.Random.Range(0.3f, 1f),
            UnityEngine.Random.Range(0.3f, 1f),
            1f
        );
        
        UnityEngine.Random.state = oldState;
        return color;
    }

    private void ApplyMaterialToRenderers(Material material)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.material = material;
        }
    }
    
    void Update()
    {
        if (map != null)
        {
            UpdatePosition();
        }
    }
    
    void UpdatePosition()
    {
        // Convert geo coordinate to current world position
        Vector3 worldPos = map.GeoToWorldPosition(geoLocation, true);
        worldPos.y += heightOffset;
        
        // Update our position to stay locked to geographic location
        transform.position = worldPos;
    }

    private void DebugLog(string message)
    {
        // Only log if the spawner has debug logs enabled
        var spawner = FindObjectOfType<InfrastructureSpawner>();
        if (spawner != null && spawner.enableDebugLogs)
        {
            Debug.Log($"[InfrastructureNode] {message}");
        }
    }
}