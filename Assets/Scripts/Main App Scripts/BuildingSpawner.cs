// using System.Collections.Generic;
// using System.IO;
// using UnityEngine;
// using UnityEngine.UI;
// using TMPro;
// using System.Linq;

// public class BuildingSpawner : MonoBehaviour
// {
//     [Header("References")]
//     public RectTransform mapContainer;
//     public GameObject buildingPrefab;
//     public RectTransform mapImage;
//     public BarrierSpawner barrierSpawner; // Reference to get bounds

//     [Header("Map Padding")]
//     public float paddingLeft = 20f;
//     public float paddingRight = 20f;
//     public float paddingTop = 50f;
//     public float paddingBottom = 20f;

//     [System.Serializable]
//     public class Infrastructure
//     {
//         public int infra_id;
//         public int category_id;
//         public string name;
//         public float latitude;
//         public float longitude;
//         public string image_url;
//     }

//     [System.Serializable]
//     public class InfrastructureList
//     {
//         public List<Infrastructure> infrastructures;
//     }

//     [System.Serializable]
//     public class NodeList
//     {
//         public List<Node> nodes;
//     }

//     [System.Serializable]
//     public class Category
//     {
//         public int category_id;
//         public string name;
//         public string icon;
//         public List<int> building_id;
//     }

//     [System.Serializable]
//     public class CategoryList
//     {
//         public List<Category> categories;
//     }

//     void Start()
//     {
//         // Wait for BarrierSpawner to finish first
//         Invoke(nameof(LoadAndSpawnBuildings), 0.1f);
//     }

//     void LoadAndSpawnBuildings()
//     {
//         // Load categories.json
//         string categoryPath = Path.Combine(Application.streamingAssetsPath, "categories.json");
//         if (!File.Exists(categoryPath)) { Debug.LogError("Categories JSON not found!"); return; }
//         string categoryRaw = File.ReadAllText(categoryPath);
//         CategoryList categoryList = JsonUtility.FromJson<CategoryList>("{\"categories\":" + categoryRaw + "}");

//         // Load infrastructures.json
//         string infraPath = Path.Combine(Application.streamingAssetsPath, "infrastructures.json");
//         if (!File.Exists(infraPath)) { Debug.LogError("Infrastructures JSON not found!"); return; }
//         string infraRaw = File.ReadAllText(infraPath);
//         InfrastructureList infraList = JsonUtility.FromJson<InfrastructureList>("{\"infrastructures\":" + infraRaw + "}");

//         // Load nodes.json for barrier nodes AND all nodes for bounds calculation
//         string nodePath = Path.Combine(Application.streamingAssetsPath, "nodes.json");
//         if (!File.Exists(nodePath)) { Debug.LogError("Nodes JSON not found!"); return; }
//         string nodeRaw = File.ReadAllText(nodePath);
//         NodeList allNodes = JsonUtility.FromJson<NodeList>("{\"nodes\":" + nodeRaw + "}");

//         // Calculate dynamic bounds from ALL data (nodes + infrastructures)
//         var allLatitudes = allNodes.nodes.Select(n => n.latitude).Concat(infraList.infrastructures.Select(i => i.latitude));
//         var allLongitudes = allNodes.nodes.Select(n => n.longitude).Concat(infraList.infrastructures.Select(i => i.longitude));

//         float minLatitude = allLatitudes.Min();
//         float maxLatitude = allLatitudes.Max();
//         float minLongitude = allLongitudes.Min();
//         float maxLongitude = allLongitudes.Max();

//         // Add small buffer
//         float latBuffer = (maxLatitude - minLatitude) * 0.05f;
//         float lonBuffer = (maxLongitude - minLongitude) * 0.05f;

//         minLatitude -= latBuffer;
//         maxLatitude += latBuffer;
//         minLongitude -= lonBuffer;
//         maxLongitude += lonBuffer;

//         Debug.Log($"🏢 Building bounds: Lat [{minLatitude:F6}, {maxLatitude:F6}], Lon [{minLongitude:F6}, {maxLongitude:F6}]");

//         // Filter only active barrier nodes
//         List<Node> barrierNodes = allNodes.nodes.FindAll(n => n.is_barrier && n.is_active);
//         if (barrierNodes.Count == 0) { Debug.LogError("No active barrier nodes!"); return; }

//         // Convert barrier nodes to XY using consistent projection
//         List<Vector2> barrierXY = new List<Vector2>();
//         foreach (var node in barrierNodes)
//         {
//             Vector2 pos = BarrierSpawner.LatLonToMapPositionStatic(
//                 node.latitude, node.longitude, mapImage,
//                 paddingLeft, paddingRight, paddingTop, paddingBottom,
//                 minLatitude, maxLatitude, minLongitude, maxLongitude);
//             barrierXY.Add(pos);
//             Debug.Log($"🚧 Barrier node {node.name}: ({node.latitude:F6}, {node.longitude:F6}) -> {pos}");
//         }

//         // Initialize polygon
//         CampusBounds.InitializePolygon(barrierXY);
//         Debug.Log($"🗺️ Barrier polygon initialized with {barrierXY.Count} points.");

//         // Spawn buildings using the SAME bounds
//         foreach (var building in infraList.infrastructures)
//         {
//             Vector2 pos = BarrierSpawner.LatLonToMapPositionStatic(
//                 building.latitude, building.longitude, mapImage,
//                 paddingLeft, paddingRight, paddingTop, paddingBottom,
//                 minLatitude, maxLatitude, minLongitude, maxLongitude);

//             bool isInside = CampusBounds.IsPointInPolygon(pos);
//             Vector2 finalPos = pos;

//             if (!isInside)
//             {
//                 finalPos = CampusBounds.ClampPointToPolygon(pos);
//                 Debug.Log($"🏢 {building.name}: ({building.latitude:F6}, {building.longitude:F6}) -> {pos} ❌ OUTSIDE -> Clamped to {finalPos}");
//             }
//             else
//             {
//                 Debug.Log($"🏢 {building.name}: ({building.latitude:F6}, {building.longitude:F6}) -> {pos} ✅ INSIDE");
//             }

//             Category cat = categoryList.categories.Find(c => c.category_id == building.category_id);
//             SpawnBuildingAtPosition(finalPos, building, cat);
//         }

//         // Debug: Test the specific cafeteria case
//         var cafeteria = infraList.infrastructures.Find(b => b.name == "Cafeteria");
//         if (cafeteria != null)
//         {
//             Vector2 cafeteriaPos = BarrierSpawner.LatLonToMapPositionStatic(
//                 cafeteria.latitude, cafeteria.longitude, mapImage,
//                 paddingLeft, paddingRight, paddingTop, paddingBottom,
//                 minLatitude, maxLatitude, minLongitude, maxLongitude);

//             Debug.Log($"🍽️ CAFETERIA DEBUG:");
//             Debug.Log($"   Coordinates: ({cafeteria.latitude:F6}, {cafeteria.longitude:F6})");
//             Debug.Log($"   UI Position: {cafeteriaPos}");
//             Debug.Log($"   Inside polygon: {CampusBounds.IsPointInPolygon(cafeteriaPos)}");
//         }
//     }

//     void SpawnBuildingAtPosition(Vector2 pos, Infrastructure building, Category cat)
//     {
//         GameObject buildingObj = Instantiate(buildingPrefab, mapContainer);
//         RectTransform rt = buildingObj.GetComponent<RectTransform>();
//         rt.anchoredPosition = pos;

//         // Set building name
//         TextMeshProUGUI label = buildingObj.GetComponentInChildren<TextMeshProUGUI>();
//         if (label != null)
//             label.text = building.name;

//         // Set icon if available
//         Image icon = null;
//         foreach (var img in buildingObj.GetComponentsInChildren<Image>(true))
//         {
//             if (img.name == "Image_Icon")
//             {
//                 icon = img;
//                 break;
//             }
//         }

//         if (icon != null && cat != null && !string.IsNullOrEmpty(cat.icon))
//         {
//             string iconPath = Path.Combine(Application.dataPath, "Images", "icons", Path.GetFileName(cat.icon));
//             if (File.Exists(iconPath))
//             {
//                 byte[] imgData = File.ReadAllBytes(iconPath);
//                 Texture2D tex = new Texture2D(2, 2);
//                 tex.LoadImage(imgData);
//                 icon.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
//             }
//             else
//             {
//                 Debug.LogWarning($"⚠️ Icon not found: {iconPath}");
//             }
//         }
//     }
// }


using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class BuildingSpawner : MonoBehaviour
{
    [Header("References")]
    public RectTransform mapContainer;
    public GameObject buildingPrefab;

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

    [System.Serializable]
    public class NodeList
    {
        public List<Node> nodes;
    }

    [System.Serializable]
    public class Category
    {
        public int category_id;
        public string name;
        public string icon;
        public List<int> building_id;
    }

    [System.Serializable]
    public class CategoryList
    {
        public List<Category> categories;
    }

    void Start()
    {
        StartCoroutine(LoadAndSpawnBuildingsCoroutine());
    }

    IEnumerator LoadAndSpawnBuildingsCoroutine()
    {
        // Wait for MapCoordinateSystem to be ready
        if (MapCoordinateSystem.Instance == null)
        {
            Debug.LogError("❌ MapCoordinateSystem not found! Please add it to the scene first.");
            yield break;
        }

        yield return StartCoroutine(MapCoordinateSystem.Instance.WaitForBoundsReady());

        LoadAndSpawnBuildings();
    }

    void LoadAndSpawnBuildings()
    {
        // Load categories.json
        string categoryPath = Path.Combine(Application.streamingAssetsPath, "categories.json");
        if (!File.Exists(categoryPath)) { Debug.LogError("Categories JSON not found!"); return; }
        string categoryRaw = File.ReadAllText(categoryPath);
        CategoryList categoryList = JsonUtility.FromJson<CategoryList>("{\"categories\":" + categoryRaw + "}");

        // Load infrastructures.json
        string infraPath = Path.Combine(Application.streamingAssetsPath, "infrastructures.json");
        if (!File.Exists(infraPath)) { Debug.LogError("Infrastructures JSON not found!"); return; }
        string infraRaw = File.ReadAllText(infraPath);
        InfrastructureList infraList = JsonUtility.FromJson<InfrastructureList>("{\"infrastructures\":" + infraRaw + "}");

        // Load nodes.json for barrier polygon
        string nodePath = Path.Combine(Application.streamingAssetsPath, "nodes.json");
        if (!File.Exists(nodePath)) { Debug.LogError("Nodes JSON not found!"); return; }
        string nodeRaw = File.ReadAllText(nodePath);
        NodeList allNodes = JsonUtility.FromJson<NodeList>("{\"nodes\":" + nodeRaw + "}");

        // Filter only active barrier nodes
        List<Node> barrierNodes = allNodes.nodes.FindAll(n => n.is_barrier && n.is_active);
        if (barrierNodes.Count == 0) { Debug.LogError("No active barrier nodes!"); return; }

        // Convert barrier nodes to XY using centralized coordinate system
        List<Vector2> barrierXY = new List<Vector2>();
        foreach (var node in barrierNodes)
        {
            Vector2 pos = MapCoordinateSystem.Instance.LatLonToMapPosition(node.latitude, node.longitude);
            barrierXY.Add(pos);
            Debug.Log($"🚧 Barrier node {node.name}: ({node.latitude:F6}, {node.longitude:F6}) -> {pos}");
        }

        // Initialize polygon
        CampusBounds.InitializePolygon(barrierXY);
        Debug.Log($"🗺️ Barrier polygon initialized with {barrierXY.Count} points.");

        // Spawn buildings using the centralized coordinate system
        foreach (var building in infraList.infrastructures)
        {
            Vector2 pos = MapCoordinateSystem.Instance.LatLonToMapPosition(building.latitude, building.longitude);

            bool isInside = CampusBounds.IsPointInPolygon(pos);
            Vector2 finalPos = pos;

            if (!isInside)
            {
                finalPos = CampusBounds.ClampPointToPolygon(pos);
                Debug.Log($"🏢 {building.name}: ({building.latitude:F6}, {building.longitude:F6}) -> {pos} ❌ OUTSIDE -> Clamped to {finalPos}");
            }
            else
            {
                Debug.Log($"🏢 {building.name}: ({building.latitude:F6}, {building.longitude:F6}) -> {pos} ✅ INSIDE");
            }

            Category cat = categoryList.categories.Find(c => c.category_id == building.category_id);
            SpawnBuildingAtPosition(finalPos, building, cat);
        }

        // Debug: Test the specific cafeteria case
        var cafeteria = infraList.infrastructures.Find(b => b.name == "Cafeteria");
        if (cafeteria != null)
        {
            Vector2 cafeteriaPos = MapCoordinateSystem.Instance.LatLonToMapPosition(cafeteria.latitude, cafeteria.longitude);

            Debug.Log($"🍽️ CAFETERIA DEBUG:");
            Debug.Log($"   Coordinates: ({cafeteria.latitude:F6}, {cafeteria.longitude:F6})");
            Debug.Log($"   UI Position: {cafeteriaPos}");
            Debug.Log($"   Inside polygon: {CampusBounds.IsPointInPolygon(cafeteriaPos)}");
        }

        Debug.Log($"✅ BuildingSpawner completed: {infraList.infrastructures.Count} buildings processed");
    }

    // void SpawnBuildingAtPosition(Vector2 pos, Infrastructure building, Category cat)
    // {
    //     GameObject buildingObj = Instantiate(buildingPrefab, mapContainer);
    //     RectTransform rt = buildingObj.GetComponent<RectTransform>();
    //     rt.anchoredPosition = pos;

    //     // Set building name
    //     TextMeshProUGUI label = buildingObj.GetComponentInChildren<TextMeshProUGUI>();
    //     if (label != null)
    //         label.text = building.name;

    //     // Set icon if available
    //     Image icon = null;
    //     foreach (var img in buildingObj.GetComponentsInChildren<Image>(true))
    //     {
    //         if (img.name == "Image_Icon")
    //         {
    //             icon = img;
    //             break;
    //         }
    //     }

    //     if (icon != null && cat != null && !string.IsNullOrEmpty(cat.icon))
    //     {
    //         string iconPath = Path.Combine(Application.dataPath, "Images", "icons", Path.GetFileName(cat.icon));
    //         if (File.Exists(iconPath))
    //         {
    //             byte[] imgData = File.ReadAllBytes(iconPath);
    //             Texture2D tex = new Texture2D(2, 2);
    //             tex.LoadImage(imgData);
    //             icon.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    //         }
    //         else
    //         {
    //             Debug.LogWarning($"⚠️ Icon not found: {iconPath}");
    //         }
    //     }
    // }

    void SpawnBuildingAtPosition(Vector2 pos, Infrastructure building, Category cat)
    {
        GameObject buildingObj = Instantiate(buildingPrefab, mapContainer);
        RectTransform rt = buildingObj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;

        // Set building name
        TextMeshProUGUI label = buildingObj.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.text = building.name;

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

        // Set sorting order to ensure buildings are in front
        Canvas canvas = buildingObj.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.sortingOrder = 0; // Default or higher than paths (-2)
        }
        else
        {
            // If using SpriteRenderer or other renderers
            SpriteRenderer sr = buildingObj.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sortingOrder = 0;
        }
    }
}
