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

    [Header("JSON Files - Static Files")]
    public string infrastructureFileName = "infrastructure.json";
    public string categoriesFileName = "categories.json";

    [Header("Settings")]
    public bool enableDebugLogs = true;
    public float infrastructureSize = 3.0f;
    public float heightOffset = 1f;

    // Current map data - set by MapManager
    private string currentMapId;
    private List<string> currentCampusIds = new List<string>();

    // Track spawned infrastructure with their location components
    private List<InfrastructureNode> spawnedInfrastructure = new List<InfrastructureNode>();
    private Dictionary<string, InfrastructureNode> infraIdToComponent = new Dictionary<string, InfrastructureNode>();

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
        DebugLog("🏢 InfrastructureSpawner started - waiting for MapManager");

        if (mapboxMap == null)
        {
            Debug.LogError("❌ No AbstractMap found! Please assign mapboxMap in inspector");
            return;
        }

        // Subscribe to MapManager events
        if (MapManager.Instance != null)
        {
            MapManager.Instance.OnMapChanged += OnMapChanged;
            MapManager.Instance.OnMapLoadingStarted += OnMapLoadingStarted;
        }
        else
        {
            Debug.LogWarning("⚠️ MapManager.Instance is null - InfrastructureSpawner will not receive map change events");
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from MapManager events
        if (MapManager.Instance != null)
        {
            MapManager.Instance.OnMapChanged -= OnMapChanged;
            MapManager.Instance.OnMapLoadingStarted -= OnMapLoadingStarted;
        }
    }

    #region MapManager Integration

    /// <summary>
    /// Called by MapManager to set current map data
    /// </summary>
    public void SetTargetCampusIds(List<string> campusIds)
    {
        DebugLog($"🗺️ Setting campus IDs: {string.Join(", ", campusIds)}");
        
        currentCampusIds.Clear();
        if (campusIds != null)
        {
            currentCampusIds.AddRange(campusIds);
        }
    }

    /// <summary>
    /// Called by MapManager to set current map data (extended version)
    /// </summary>
    public void SetCurrentMapData(string mapId, List<string> campusIds)
    {
        DebugLog($"🗺️ Setting map data - Map ID: {mapId}, Campuses: {string.Join(", ", campusIds)}");
        
        currentMapId = mapId;
        currentCampusIds.Clear();
        if (campusIds != null)
        {
            currentCampusIds.AddRange(campusIds);
        }
    }

    /// <summary>
    /// Called when MapManager changes the map
    /// </summary>
    private void OnMapChanged(MapInfo mapInfo)
    {
        DebugLog($"🔄 Map changed to: {mapInfo.map_name} (ID: {mapInfo.map_id})");
        SetCurrentMapData(mapInfo.map_id, mapInfo.campus_included);
    }

    /// <summary>
    /// Called when MapManager starts loading a new map
    /// </summary>
    private void OnMapLoadingStarted()
    {
        DebugLog("🧹 Map loading started - clearing existing infrastructure");
        ClearSpawnedInfrastructure();
    }

    /// <summary>
    /// Main method called by MapManager to load infrastructure for specific campuses
    /// </summary>
    public IEnumerator LoadAndSpawnForCampuses(List<string> campusIds)
    {
        if (isSpawning)
        {
            DebugLog("⚠️ Already spawning infrastructure, skipping");
            yield break;
        }

        DebugLog($"🏢 LoadAndSpawnForCampuses called - Campuses: {string.Join(", ", campusIds ?? new List<string>())}");

        // Update current campus data
        SetTargetCampusIds(campusIds);

        // Wait for map to be ready
        yield return StartCoroutine(WaitForMapReady());

        // Start spawning
        yield return StartCoroutine(LoadAndSpawnInfrastructure());

        DebugLog($"✅ Infrastructure spawning completed for campuses: {string.Join(", ", campusIds ?? new List<string>())}");
    }

    #endregion

    #region Map Readiness Check

    private IEnumerator WaitForMapReady()
    {
        DebugLog("⏳ Waiting for map to be ready...");

        float timeout = 30f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (mapboxMap != null && mapboxMap.gameObject.activeInHierarchy)
            {
                DebugLog($"🗺️ Map ready after {elapsed:F1}s");
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

        yield return new WaitForSeconds(1f); // Extra buffer time
    }

    #endregion

    #region File Name Generation

    /// <summary>
    /// Get the appropriate nodes file name based on current map
    /// </summary>
    private string GetNodesFileName()
    {
        if (string.IsNullOrEmpty(currentMapId))
        {
            Debug.LogWarning("⚠️ No current map ID set, using default nodes.json");
            return "nodes.json";
        }
        
        string fileName = $"nodes_{currentMapId}.json";
        DebugLog($"📁 Using nodes file: {fileName}");
        return fileName;
    }

    #endregion

    #region Main Loading and Spawning

    public IEnumerator LoadAndSpawnInfrastructure()
    {
        if (isSpawning)
        {
            DebugLog("⚠️ Already spawning infrastructure");
            yield break;
        }

        isSpawning = true;
        DebugLog($"🏢 Starting LoadAndSpawnInfrastructure for campuses: {string.Join(", ", currentCampusIds)}");

        List<InfrastructureData> infrastructureToSpawn = null;
        bool errorOccurred = false;

        // Declare variables to hold loaded data
        Node[] nodes = null;
        Infrastructure[] infrastructures = null;
        Category[] categories = null;

        // Clear existing objects first
        ClearSpawnedInfrastructure();

        // Load all required JSON files (yielding outside try-catch)
        yield return StartCoroutine(LoadNodesFromJSONAsync((loadedNodes) => {
            nodes = loadedNodes;
        }));

        yield return StartCoroutine(LoadInfrastructureFromJSONAsync((loadedInfra) => {
            infrastructures = loadedInfra;
        }));

        yield return StartCoroutine(LoadCategoriesFromJSONAsync((loadedCategories) => {
            categories = loadedCategories;
        }));

        try
        {
            if (currentCampusIds == null || currentCampusIds.Count == 0)
            {
                Debug.LogWarning("⚠️ No campus IDs available for infrastructure loading");
            }

            if (nodes == null || infrastructures == null)
            {
                Debug.LogError("❌ Failed to load required JSON files");
                errorOccurred = true;
            }
            else
            {
                // Build infrastructure data with location info, filtering by campus
                infrastructureToSpawn = BuildInfrastructureData(nodes, infrastructures, categories, currentCampusIds);
                DebugLog($"🏢 Found {infrastructureToSpawn.Count} infrastructure items to spawn");

                if (infrastructureToSpawn.Count == 0)
                {
                    Debug.LogWarning("⚠️ No infrastructure found matching criteria");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error in LoadAndSpawnInfrastructure: {e.Message}");
            errorOccurred = true;
        }
        finally
        {
            isSpawning = false;
        }

        if (errorOccurred || infrastructureToSpawn == null)
        {
            yield break;
        }

        // Spawn the infrastructure outside the try-catch block
        yield return StartCoroutine(SpawnInfrastructureItems(infrastructureToSpawn));

        Debug.Log($"✅ InfrastructureSpawner completed: {spawnedInfrastructure.Count} infrastructure items spawned");
    }

    #endregion

    #region JSON Loading Methods

    private IEnumerator LoadNodesFromJSONAsync(System.Action<Node[]> onComplete)
    {
        DebugLog($"📂 Loading nodes from: {GetNodesFileName()}");

        bool loadCompleted = false;
        Node[] nodes = null;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            GetNodesFileName(),
            // onSuccess
            (jsonContent) => {
                try
                {
                    DebugLog($"📄 Read {jsonContent.Length} characters from nodes file");
                    nodes = JsonHelper.FromJson<Node>(jsonContent);
                    DebugLog($"📊 Parsed {nodes?.Length ?? 0} nodes from JSON");
                    loadCompleted = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ Error parsing nodes JSON: {e.Message}");
                    loadCompleted = true;
                }
            },
            // onError
            (error) => {
                Debug.LogError($"❌ Error loading nodes file: {error}");
                loadCompleted = true;
            }
        ));

        yield return new WaitUntil(() => loadCompleted);
        onComplete?.Invoke(nodes);
    }

    private IEnumerator LoadInfrastructureFromJSONAsync(System.Action<Infrastructure[]> onComplete)
    {
        DebugLog($"📂 Loading infrastructure from: {infrastructureFileName}");

        bool loadCompleted = false;
        Infrastructure[] infrastructures = null;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            infrastructureFileName,
            // onSuccess
            (jsonContent) => {
                try
                {
                    DebugLog($"📄 Read {jsonContent.Length} characters from infrastructure file");
                    infrastructures = JsonHelper.FromJson<Infrastructure>(jsonContent);
                    DebugLog($"📊 Parsed {infrastructures?.Length ?? 0} infrastructures from JSON");
                    loadCompleted = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ Error parsing infrastructure JSON: {e.Message}");
                    loadCompleted = true;
                }
            },
            // onError
            (error) => {
                Debug.LogError($"❌ Error loading infrastructure file: {error}");
                loadCompleted = true;
            }
        ));

        yield return new WaitUntil(() => loadCompleted);
        onComplete?.Invoke(infrastructures);
    }

    private IEnumerator LoadCategoriesFromJSONAsync(System.Action<Category[]> onComplete)
    {
        DebugLog($"📂 Loading categories from: {categoriesFileName}");

        bool loadCompleted = false;
        Category[] categories = null;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            categoriesFileName,
            // onSuccess
            (jsonContent) => {
                try
                {
                    DebugLog($"📄 Read {jsonContent.Length} characters from categories file");
                    categories = JsonHelper.FromJson<Category>(jsonContent);
                    DebugLog($"📊 Parsed {categories?.Length ?? 0} categories from JSON");
                    loadCompleted = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"⚠️ Error parsing categories JSON: {e.Message}");
                    loadCompleted = true;
                }
            },
            // onError
            (error) => {
                Debug.LogWarning($"⚠️ Categories file not found (optional): {error}");
                loadCompleted = true;
            }
        ));

        yield return new WaitUntil(() => loadCompleted);
        onComplete?.Invoke(categories);
    }

    #endregion

    #region Data Processing

    private List<InfrastructureData> BuildInfrastructureData(Node[] nodes, Infrastructure[] infrastructures,
                                                            Category[] categories, List<string> campusIds)
    {
        var infrastructureData = new List<InfrastructureData>();

        // Create dictionaries for quick lookup
        var infraDict = infrastructures.ToDictionary(i => i.infra_id, i => i);
        var categoryDict = categories?.ToDictionary(c => c.category_id.ToString(), c => c) ?? new Dictionary<string, Category>();

        DebugLog($"🔍 Created infrastructure dictionary with {infraDict.Count} entries");
        DebugLog($"🔍 Created category dictionary with {categoryDict.Count} entries");

        // Filter infrastructure nodes by campus IDs
        var infrastructureNodes = nodes.Where(n =>
            n != null &&
            n.type == "infrastructure" &&
            n.is_active &&
            (campusIds == null || campusIds.Count == 0 || campusIds.Contains(n.campus_id)) &&
            !string.IsNullOrEmpty(n.related_infra_id) &&
            IsValidCoordinate(n.latitude, n.longitude)
        ).ToList();

        DebugLog($"🔍 Infrastructure node filtering for campuses ({string.Join(", ", campusIds ?? new List<string>())}):");
        DebugLog($"  - Total nodes: {nodes.Length}");
        DebugLog($"  - Infrastructure nodes: {nodes.Count(n => n?.type == "infrastructure")}");
        DebugLog($"  - Active infrastructure nodes: {nodes.Count(n => n?.type == "infrastructure" && n.is_active)}");
        DebugLog($"  - Campus-filtered infrastructure nodes: {infrastructureNodes.Count}");

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
                DebugLog($"✅ Matched: Node {node.node_id} (Campus: {node.campus_id}) -> Infrastructure {infrastructure.name}");
            }
            else
            {
                Debug.LogWarning($"⚠️ No infrastructure found for node {node.node_id} with related_infra_id: {node.related_infra_id}");
            }
        }

        return infrastructureData;
    }

    #endregion

    #region Infrastructure Spawning

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

                DebugLog($"🏢 Spawned infrastructure: {data.Infrastructure.name} (ID: {data.Infrastructure.infra_id}, Campus: {data.Node.campus_id})");
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

    #endregion

    #region Public Utility Methods

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

        DebugLog("✅ Cleared all infrastructure items");
    }

    #endregion

    #region Legacy/Debug Methods

    // Manual spawn methods for testing
    public void ManualSpawn()
    {
        DebugLog("🔄 Manual spawn triggered");
        if (currentCampusIds != null && currentCampusIds.Count > 0)
        {
            StartCoroutine(LoadAndSpawnInfrastructure());
        }
        else
        {
            Debug.LogWarning("⚠️ No current campus IDs set - cannot manually spawn");
        }
    }

    [System.Obsolete("Debug method - remove in production")]
    public void ForceResetSpawning()
    {
        DebugLog("🔄 Force resetting spawning state");
        isSpawning = false;
        StopAllCoroutines();
        DebugLog($"✅ Reset complete");
    }

    #endregion

    #region Utility Methods

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

    #endregion

    #region Debug Input Handling

    void Update()
    {
        // Debug controls
        if (Application.isEditor && enableDebugLogs)
        {
            if (Input.GetKeyDown(KeyCode.I))
            {
                Debug.Log($"=== INFRASTRUCTURE SPAWNER STATUS ===");
                Debug.Log($"Current Map ID: {currentMapId ?? "None"}");
                Debug.Log($"Current Campus IDs: {string.Join(", ", currentCampusIds)}");
                Debug.Log($"Is spawning: {isSpawning}");
                Debug.Log($"Infrastructure spawned: {spawnedInfrastructure.Count}");
                Debug.Log($"Map assigned: {mapboxMap != null}");
                Debug.Log($"Nodes file: {GetNodesFileName()}");
                Debug.Log($"Infrastructure file: {infrastructureFileName}");
                Debug.Log($"Categories file: {categoriesFileName}");
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

    #endregion
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

        // Set up the circle background color
        SetupCircleBackground();

        // Prefer infrastructure image_url over category icon
        if (!string.IsNullOrEmpty(infrastructureData.Infrastructure.image_url))
        {
            SetupIconFromImageUrl(infrastructureData.Infrastructure.image_url);
        }
        else if (infrastructureData.Category != null && !string.IsNullOrEmpty(infrastructureData.Category.icon))
        {
            SetupCategoryIcon();
        }
        else
        {
            DebugLog($"⚠️ No icon found for {infrastructureData.Infrastructure.name}");
        }
    }

    private void SetupIconFromImageUrl(string imageUrl)
    {
        Transform iconPlane = transform.Find("Icon_Plane") ?? transform.Find("Icon");
        if (iconPlane == null)
        {
            foreach (Transform child in transform)
            {
                if (child.GetComponent<MeshFilter>()?.sharedMesh?.name.Contains("Quad") == true)
                {
                    iconPlane = child;
                    break;
                }
            }
        }

        if (iconPlane == null)
        {
            Debug.LogWarning($"⚠️ No Icon plane found for {infrastructureData.Infrastructure.name}");
            return;
        }

        Renderer iconRenderer = iconPlane.GetComponent<Renderer>();
        if (iconRenderer == null) return;

        // Build path relative to Resources folder
        string resourcePath = $"Images/icons/{Path.GetFileNameWithoutExtension(imageUrl)}";
        DebugLog($"🔍 Loading infra icon from: {resourcePath}");

        Texture2D iconTexture = Resources.Load<Texture2D>(resourcePath);
        if (iconTexture != null)
        {
            Material iconMaterial = new Material(Shader.Find("Unlit/Texture"));
            iconMaterial.mainTexture = iconTexture;
            iconRenderer.material = iconMaterial;

            DebugLog($"✅ Applied infra-specific icon: {imageUrl}");
        }
        else
        {
            Debug.LogWarning($"⚠️ Could not load infra-specific icon: {resourcePath}");
        }
    }

    private void SetupCircleBackground()
    {
        // Find the base circle (cylinder) and apply background color/material
        Transform baseCircle = transform.Find("InfrastructurePrefab_3D");
        if (baseCircle == null)
        {
            // Try to find cylinder by component if naming is different
            foreach (Transform child in transform)
            {
                if (child.GetComponent<MeshFilter>()?.sharedMesh?.name.Contains("Cylinder") == true)
                {
                    baseCircle = child;
                    break;
                }
            }
        }

        if (baseCircle != null)
        {
            Renderer circleRenderer = baseCircle.GetComponent<Renderer>();
            if (circleRenderer != null)
            {
                // Create or modify material for the circle background
                Material circleMaterial = new Material(Shader.Find("Default-Material"));

                // Set background color based on category
                if (infrastructureData.Category != null)
                {
                    circleMaterial.color = GetColorForCategory(infrastructureData.Category.category_id);
                }
                else
                {
                    circleMaterial.color = Color.gray; // Default color
                }

                circleRenderer.material = circleMaterial;
                DebugLog($"✅ Applied circle background color: {circleMaterial.color}");
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ Could not find Base_Circle for infrastructure: {infrastructureData.Infrastructure.name}");
        }
    }

    private void SetupCategoryIcon()
    {
        // Find the icon plane (quad)
        Transform iconPlane = transform.Find("Icon");
        if (iconPlane == null)
        {
            iconPlane = transform.Find("Icon");
            if (iconPlane == null)
            {
                // Try to find quad by component if naming is different
                foreach (Transform child in transform)
                {
                    if (child.GetComponent<MeshFilter>()?.sharedMesh?.name.Contains("Quad") == true)
                    {
                        iconPlane = child;
                        break;
                    }
                }
            }
        }

        if (iconPlane == null)
        {
            Debug.LogWarning($"⚠️ Could not find Icon_Plane for infrastructure: {infrastructureData.Infrastructure.name}");
            return;
        }

        Renderer iconRenderer = iconPlane.GetComponent<Renderer>();
        if (iconRenderer == null)
        {
            Debug.LogWarning($"⚠️ Icon plane has no renderer for infrastructure: {infrastructureData.Infrastructure.name}");
            return;
        }

        // Load the icon texture
        Texture2D iconTexture = LoadIconTexture(infrastructureData.Category.icon);
        if (iconTexture != null)
        {
            // Create material with the icon texture
            Material iconMaterial = new Material(Shader.Find("Default-Material"));
            iconMaterial.mainTexture = iconTexture;
            iconMaterial.color = Color.white; // Keep icon at full brightness

            // Make it unlit so it's always visible
            iconMaterial.shader = Shader.Find("Unlit/Texture");

            iconRenderer.material = iconMaterial;
            DebugLog($"✅ Applied icon texture to plane: {infrastructureData.Category.icon}");
        }
        else
        {
            // Fallback: create a simple colored material if no texture found
            Material fallbackMaterial = new Material(Shader.Find("Default-Material"));
            fallbackMaterial.color = GetColorForCategory(infrastructureData.Category.category_id);
            iconRenderer.material = fallbackMaterial;
            DebugLog($"⚠️ Used fallback color for icon: {infrastructureData.Category.icon}");
        }
    }

    private Texture2D LoadIconTexture(string iconPath)
    {
        string resourcePath = iconPath;

        // Remove file extension for Resources.Load
        if (resourcePath.EndsWith(".png") || resourcePath.EndsWith(".jpg") || resourcePath.EndsWith(".jpeg"))
        {
            resourcePath = Path.GetFileNameWithoutExtension(resourcePath);
            string directory = Path.GetDirectoryName(iconPath).Replace("\\", "/");
            if (!string.IsNullOrEmpty(directory))
            {
                resourcePath = directory + "/" + resourcePath;
            }
        }

        DebugLog($"🔍 Trying to load texture from Resources: '{resourcePath}' for category: {infrastructureData.Category.name}");

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);

        if (texture != null)
        {
            DebugLog($"✅ Loaded texture successfully from Resources: {resourcePath}");
            return texture;
        }

        // Try alternative paths
        string[] tryPaths = {
            resourcePath,
            iconPath.Replace(".png", "").Replace(".jpg", ""),
            "Images/icons/" + Path.GetFileNameWithoutExtension(iconPath),
            Path.GetFileNameWithoutExtension(iconPath),
            "icons/" + Path.GetFileNameWithoutExtension(iconPath)
        };

        DebugLog("🔍 Trying alternative texture paths:");
        foreach (string tryPath in tryPaths)
        {
            Texture2D testTexture = Resources.Load<Texture2D>(tryPath);
            DebugLog($"   {(testTexture != null ? "✅" : "❌")} '{tryPath}'");
            if (testTexture != null)
            {
                DebugLog($"✅ Success with alternative path: {tryPath}");
                return testTexture;
            }
        }

        Debug.LogWarning($"⚠️ Could not load texture from Resources: {resourcePath}");
        return null;
    }

    private Color GetColorForCategory(long categoryId)
    {
        // Generate a consistent color based on category ID
        UnityEngine.Random.State oldState = UnityEngine.Random.state;
        UnityEngine.Random.InitState((int)categoryId);

        Color color = new Color(
            UnityEngine.Random.Range(0.4f, 0.9f), // Avoid too dark or too bright
            UnityEngine.Random.Range(0.4f, 0.9f),
            UnityEngine.Random.Range(0.4f, 0.9f),
            1f
        );

        UnityEngine.Random.state = oldState;
        return color;
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