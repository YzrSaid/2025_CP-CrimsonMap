using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Linq;

public class BuildingSpawner : MonoBehaviour
{
    [Header("References")]
    public RectTransform mapContainer;
    public GameObject buildingPrefab;

    // Track spawned buildings for cleanup
    private List<GameObject> spawnedBuildings = new List<GameObject>();

    void Start()
    {
        Debug.Log("🏢 BuildingSpawner ready - waiting for map assignment");
    }

    /// <summary>
    /// Load and spawn buildings for specific campuses
    /// Called by MapManager when switching maps
    /// </summary>
    public IEnumerator LoadAndSpawnForCampuses(List<string> campusIds)
    {
        Debug.Log($"🏢 Loading buildings for campuses: {string.Join(", ", campusIds)}");

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
        CategoryList categoryList = JsonUtility.FromJson<CategoryList>("{\"categories\":" + categoryRaw + "}");

        // Load infrastructures
        string infraPath = Path.Combine(Application.streamingAssetsPath, "infrastructures.json");
        if (!File.Exists(infraPath))
        {
            Debug.LogError("Infrastructures JSON not found!");
            yield break;
        }

        string infraRaw = File.ReadAllText(infraPath);
        InfrastructureList infraList = JsonUtility.FromJson<InfrastructureList>("{\"infrastructures\":" + infraRaw + "}");

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

        // Filter and spawn buildings
        // Note: You may want to add campus_id to Infrastructure class for better filtering
        foreach (var building in infraList.infrastructures)
        {
            Vector2 pos = MapCoordinateSystem.Instance.LatLonToMapPosition(building.latitude, building.longitude);
            
            Vector2 finalPos = pos;
            bool isInside = true;
            
            if (barrierNodes.Count > 0)
            {
                isInside = CampusBounds.IsPointInPolygon(pos);
                if (!isInside)
                {
                    finalPos = CampusBounds.ClampPointToPolygon(pos);
                    Debug.Log($"🏢 {building.name}: OUTSIDE boundary -> Clamped to {finalPos}");
                }
            }

            Category cat = categoryList.categories.Find(c => c.category_id == building.category_id);
            SpawnBuildingAtPosition(finalPos, building, cat);
        }
    }

    void SpawnBuildingAtPosition(Vector2 pos, Infrastructure building, Category cat)
    {
        GameObject buildingObj = Instantiate(buildingPrefab, mapContainer);
        buildingObj.name = $"Building_{building.name}";
        
        RectTransform rt = buildingObj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;

        // Set building name
        TextMeshProUGUI label = buildingObj.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = building.name;

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
            canvas.sortingOrder = 1; // Above paths
        }
        else
        {
            SpriteRenderer sr = buildingObj.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 1;
        }

        spawnedBuildings.Add(buildingObj);
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
        Debug.Log("🧹 BuildingSpawner: Cleared all spawned buildings");
    }
}