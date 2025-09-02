using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Linq;

public class InfrastructureSpawner : MonoBehaviour
{
    [Header("References")]
    public RectTransform mapContainer;
    public GameObject buildingPrefab;

    // Track spawned buildings for cleanup
    private List<GameObject> spawnedBuildings = new List<GameObject>();

    void Start()
    {
        Debug.Log("🏢 InfrastructureSpawner ready - waiting for map assignment");
    }

    /// <summary>
    /// Load and spawn infrastructure for specific campuses
    /// Called by MapManager when switching maps
    /// </summary>
    public IEnumerator LoadAndSpawnForCampuses(List<string> campusIds)
    {
        Debug.Log($"🏢 Loading infrastructure for campuses: {string.Join(", ", campusIds)}");

        // Wait for MapCoordinateSystem to be ready
        if (MapCoordinateSystem.Instance == null)
        {
            Debug.LogError("❌ MapCoordinateSystem not found!");
            yield break;
        }

        yield return StartCoroutine(MapCoordinateSystem.Instance.WaitForBoundsReady());

        // Load categories
        string categoryPath = Path.Combine(Application.streamingAssetsPath, "categories.json");
        if (!File.Exists(categoryPath))
        {
            Debug.LogError("Categories JSON not found!");
            yield break;
        }

        string categoryRaw = File.ReadAllText(categoryPath);
        Debug.Log($"🔍 DEBUG: Raw categories JSON: {categoryRaw}");
        CategoryList categoryList = JsonUtility.FromJson<CategoryList>("{\"categories\":" + categoryRaw + "}");

        // Load infrastructures
        string infraPath = Path.Combine(Application.streamingAssetsPath, "infrastructures.json");
        if (!File.Exists(infraPath))
        {
            Debug.LogError("Infrastructures JSON not found!");
            yield break;
        }

        string infraRaw = File.ReadAllText(infraPath);
        Debug.Log($"🔍 DEBUG: Raw infrastructures JSON: {infraRaw}");
        InfrastructureList infraList = JsonUtility.FromJson<InfrastructureList>("{\"infrastructures\":" + infraRaw + "}");

        // DEBUG: Print loaded infrastructures
        if (infraList?.infrastructures != null)
        {
            Debug.Log($"🔍 DEBUG: Loaded {infraList.infrastructures.Length} infrastructures:");
            foreach (var infra in infraList.infrastructures)
            {
                Debug.Log($"   - ID: {infra.infra_id}, Name: {infra.name}");
            }
        }
        else
        {
            Debug.LogError("❌ Failed to parse infrastructures JSON!");
            yield break;
        }

        // Load nodes to get location data
        string nodesPath = Path.Combine(Application.streamingAssetsPath, "nodes.json");
        if (!File.Exists(nodesPath))
        {
            Debug.LogError("Nodes JSON not found!");
            yield break;
        }

        string nodesRaw = File.ReadAllText(nodesPath);
        Debug.Log($"🔍 DEBUG: Raw nodes JSON length: {nodesRaw.Length} characters");
        NodeList nodeList = JsonUtility.FromJson<NodeList>("{\"nodes\":" + nodesRaw + "}");

        // DEBUG: Print all nodes first
        Debug.Log($"🔍 DEBUG: Loaded {nodeList.nodes.Count} total nodes");
        
        // DEBUG: Print infrastructure nodes specifically
        var allInfraNodes = nodeList.nodes.Where(n => n.type == "infrastructure").ToList();
        Debug.Log($"🔍 DEBUG: Found {allInfraNodes.Count} nodes with type 'infrastructure':");
        foreach (var node in allInfraNodes)
        {
            Debug.Log($"   - Node: {node.node_id}, Name: {node.name}, Campus: {node.campus_id}, Active: {node.is_active}, Related Infra: {node.related_infra_id}");
        }

        // DEBUG: Check campus filtering
        var campusFilteredNodes = nodeList.nodes.Where(n => campusIds.Contains(n.campus_id)).ToList();
        Debug.Log($"🔍 DEBUG: Found {campusFilteredNodes.Count} nodes for campuses {string.Join(", ", campusIds)}:");
        foreach (var node in campusFilteredNodes)
        {
            Debug.Log($"   - Node: {node.node_id}, Type: {node.type}, Campus: {node.campus_id}");
        }

        // Filter infrastructure nodes by campus - nodes with type "infrastructure"
        var infrastructureNodes = nodeList.nodes.Where(n =>
            campusIds.Contains(n.campus_id) &&
            n.type == "infrastructure" &&
            n.is_active &&
            n.HasRelatedInfraId  // Use the new helper property
        ).ToList();

        Debug.Log($"🏢 Found {infrastructureNodes.Count} infrastructure nodes for campuses: {string.Join(", ", campusIds)}");

        // DEBUG: Print final filtered nodes
        if (infrastructureNodes.Count == 0)
        {
            Debug.LogWarning("❌ NO INFRASTRUCTURE NODES FOUND! Checking each filter condition:");
            
            // Check each condition separately
            var step1 = nodeList.nodes.Where(n => campusIds.Contains(n.campus_id)).ToList();
            Debug.Log($"   Step 1 - Campus filter: {step1.Count} nodes");
            
            var step2 = step1.Where(n => n.type == "infrastructure").ToList();
            Debug.Log($"   Step 2 - Type filter: {step2.Count} nodes");
            
            var step3 = step2.Where(n => n.is_active).ToList();
            Debug.Log($"   Step 3 - Active filter: {step3.Count} nodes");
            
            var step4 = step3.Where(n => n.HasRelatedInfraId).ToList();
            Debug.Log($"   Step 4 - Related infra filter: {step4.Count} nodes");
            
            foreach (var node in step3)
            {
                Debug.Log($"   Node {node.node_id}: related_infra_id = {node.related_infra_id}, HasRelatedInfraId = {node.HasRelatedInfraId}");
            }
        }
        else
        {
            Debug.Log($"✅ Final infrastructure nodes to spawn:");
            foreach (var node in infrastructureNodes)
            {
                Debug.Log($"   - {node.node_id}: {node.name} (Infra ID: {node.related_infra_id})");
            }
        }

        // Get barrier nodes for current campuses from BarrierSpawner
        var barrierSpawner = FindObjectOfType<BarrierSpawner>();
        List<Node> barrierNodes = new List<Node>();
        
        if (barrierSpawner != null)
        {
            barrierNodes = barrierSpawner.GetFilteredBarrierNodes();
        }

        if (barrierNodes.Count == 0)
        {
            Debug.LogWarning("⚠️ No barrier nodes found for campus bounds - buildings will spawn without boundary checking");
        }
        else
        {
            // Convert barrier nodes to XY for polygon bounds
            List<Vector2> barrierXY = new List<Vector2>();
            foreach (var node in barrierNodes)
            {
                Vector2 pos = MapCoordinateSystem.Instance.LatLonToMapPosition(node.latitude, node.longitude);
                barrierXY.Add(pos);
            }
            
            // Initialize polygon bounds for each campus
            CampusBounds.InitializePolygon(barrierXY);
            Debug.Log($"🗺️ Initialized barrier polygon with {barrierXY.Count} points for campuses");
        }

        // Create dictionary for quick infrastructure lookup
        Dictionary<int, Infrastructure> infraDict = new Dictionary<int, Infrastructure>();
        foreach (var infra in infraList.infrastructures)
        {
            infraDict[infra.infra_id] = infra;
        }

        // Spawn infrastructure based on nodes (lat/lng comes from nodes now)
        foreach (var node in infrastructureNodes)
        {
            int infraId = node.related_infra_id;
            if (!infraDict.ContainsKey(infraId))
            {
                Debug.LogWarning($"⚠️ Infrastructure ID {infraId} not found for node {node.node_id}");
                continue;
            }

            Infrastructure infrastructure = infraDict[infraId];
            // Position comes from node data (not infrastructure data anymore)
            Vector2 pos = MapCoordinateSystem.Instance.LatLonToMapPosition(node.latitude, node.longitude);
            
            Vector2 finalPos = pos;
            bool isInside = true;
            
            if (barrierNodes.Count > 0)
            {
                isInside = CampusBounds.IsPointInPolygon(pos);
                if (!isInside)
                {
                    finalPos = CampusBounds.ClampPointToPolygon(pos);
                    Debug.Log($"🏢 {infrastructure.name}: OUTSIDE boundary -> Clamped to {finalPos}");
                }
            }

            Category cat = categoryList.categories.Find(c => c.category_id == infrastructure.category_id);
            SpawnInfrastructureAtPosition(finalPos, infrastructure, cat, node);
        }
    }

    void SpawnInfrastructureAtPosition(Vector2 pos, Infrastructure infrastructure, Category cat, Node node)
    {
        GameObject buildingObj = Instantiate(buildingPrefab, mapContainer);
        buildingObj.name = $"Infrastructure_{infrastructure.name}_{node.node_id}";
        
        RectTransform rt = buildingObj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;

        // Set building name
        TextMeshProUGUI label = buildingObj.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = infrastructure.name;

        // Set icon if available
        Image icon = null;
        foreach (var img in buildingObj.GetComponentsInChildren<Image>(true))
        {
            if (img.name == "Image_Icon")
            {
                icon = img;
                break;
            }
        }

        if (icon != null && cat != null && !string.IsNullOrEmpty(cat.icon))
        {
            string iconPath = Path.Combine(Application.dataPath, "Images", "icons", Path.GetFileName(cat.icon));
            if (File.Exists(iconPath))
            {
                byte[] imgData = File.ReadAllBytes(iconPath);
                Texture2D tex = new Texture2D(2, 2);
                tex.LoadImage(imgData);
                icon.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            else
            {
                Debug.LogWarning($"⚠️ Icon not found: {iconPath}");
            }
        }

        // Set sorting order
        Canvas canvas = buildingObj.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.sortingOrder = 1;
        }
        else
        {
            SpriteRenderer sr = buildingObj.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 1;
        }

        spawnedBuildings.Add(buildingObj);
        Debug.Log($"✅ Spawned infrastructure: {infrastructure.name} at position {pos}");
    }

    /// <summary>
    /// Clear all spawned buildings when switching maps
    /// </summary>
    public void ClearSpawnedObjects()
    {
        foreach (var building in spawnedBuildings)
        {
            if (building != null) Destroy(building);
        }
        spawnedBuildings.Clear();
        Debug.Log("🧹 InfrastructureSpawner: Cleared all spawned infrastructure");
    }
}