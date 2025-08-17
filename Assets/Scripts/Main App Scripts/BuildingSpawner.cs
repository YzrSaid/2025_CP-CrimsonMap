using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildingSpawner : MonoBehaviour
{
    [Header("References")]
    public RectTransform mapContainer;
    public GameObject buildingPrefab;
    public RectTransform mapImage;

    [Header("Map Padding")]
    public float paddingLeft = 20f;
    public float paddingRight = 20f;
    public float paddingTop = 50f;
    public float paddingBottom = 20f;

    [System.Serializable]
    public class Building
    {
        public int building_id;
        public string name;
        public float latitude;
        public float longitude;
        public string image_url;
    }

    [System.Serializable]
    public class BuildingList
    {
        public List<Building> buildings;
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
        LoadAndSpawnBuildings();
    }

    void LoadAndSpawnBuildings()
    {
        // --- Load the categories.json --
        string categoryPath = Path.Combine(Application.streamingAssetsPath, "categories.json");
        if (!File.Exists(categoryPath)) { Debug.LogError("Categories JSON not found!"); return; }
        string categoryRaw = File.ReadAllText(categoryPath);
        CategoryList categoryList = JsonUtility.FromJson<CategoryList>("{\"categories\":" + categoryRaw + "}");


        // --- Load buildings.json ---
        string buildingPath = Path.Combine(Application.streamingAssetsPath, "buildings.json");
        if (!File.Exists(buildingPath)) { Debug.LogError("Buildings JSON not found!"); return; }

        string buildingRaw = File.ReadAllText(buildingPath);
        BuildingList buildingList = JsonUtility.FromJson<BuildingList>("{\"buildings\":" + buildingRaw + "}");

        // --- Load nodes.json for barrier nodes ---
        string nodePath = Path.Combine(Application.streamingAssetsPath, "nodes.json");
        if (!File.Exists(nodePath)) { Debug.LogError("Nodes JSON not found!"); return; }
        string nodeRaw = File.ReadAllText(nodePath);
        NodeList allNodes = JsonUtility.FromJson<NodeList>("{\"nodes\":" + nodeRaw + "}");

        // --- Filter only active barrier nodes ---
        List<Node> barrierNodes = allNodes.nodes.FindAll(n => n.is_barrier && n.is_active);
        if (barrierNodes.Count == 0) { Debug.LogError("No active barrier nodes!"); return; }

        // --- Convert barrier nodes to XY using BarrierSpawner projection ---
        List<Vector2> barrierXY = new List<Vector2>();
        foreach (var node in barrierNodes)
            barrierXY.Add(BarrierSpawner.LatLonToMapPositionStatic(node.latitude, node.longitude, mapImage, paddingLeft, paddingRight, paddingTop, paddingBottom));

        // --- Initialize polygon ---
        CampusBounds.InitializePolygon(barrierXY);

        // --- Spawn buildings ---
        foreach (var b in buildingList.buildings)
        {
            Vector2 pos = BarrierSpawner.LatLonToMapPositionStatic(b.latitude, b.longitude, mapImage, paddingLeft, paddingRight, paddingTop, paddingBottom);

            if (!CampusBounds.IsPointInPolygon(pos))
                pos = CampusBounds.ClampPointToPolygon(pos);

            // Apply non-overlapping adjustment
            // pos = GetNonOverlappingPosition(pos, minDistance: 25f);

            Category cat = categoryList.categories.Find(c => c.building_id.Contains(b.building_id));

            SpawnBuildingAtPosition(pos, b, cat);
        }
    }

    Vector2 GetNonOverlappingPosition(Vector2 originalPos, float minDistance = 20f)
    {
        // Keep a static list of used positions
        if (_usedPositions == null) _usedPositions = new List<Vector2>();

        Vector2 newPos = originalPos;

        // Try to find a position not too close to existing ones
        int tries = 0;
        while (_usedPositions.Exists(p => Vector2.Distance(p, newPos) < minDistance) && tries < 10)
        {
            newPos += new Vector2(Random.Range(-minDistance, minDistance), Random.Range(-minDistance, minDistance));
            tries++;
        }

        _usedPositions.Add(newPos);
        return newPos;
    }

    private static List<Vector2> _usedPositions;

    void SpawnBuildingAtPosition(Vector2 pos, Building b, Category cat)
    {
        // Instantiate building prefab
        GameObject buildingObj = Instantiate(buildingPrefab, mapContainer);
        RectTransform rt = buildingObj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;

        // Set label
        TextMeshProUGUI label = buildingObj.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.text = b.name;

        // Find Image_Icon child anywhere in prefab (including inactive)
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
            // Build path to icon in Assets/Images/icons
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
                Debug.LogWarning("Icon not found: " + iconPath);
            }
        }
        else
        {
            Debug.LogWarning("Image_Icon child not found in prefab or category icon missing.");
        }
    }


    // void SpawnBuildingAtPosition(Vector2 pos, Building b, Category cat)
    // {
    //     GameObject buildingObj = Instantiate(buildingPrefab, mapContainer);
    //     RectTransform rt = buildingObj.GetComponent<RectTransform>();
    //     rt.anchoredPosition = pos;

    //     // Set label
    //     TextMeshProUGUI label = buildingObj.GetComponentInChildren<TextMeshProUGUI>();
    //     if (label != null) label.text = b.name;

    //     // Find the Image_icon child explicitly
    //     Transform iconTransform = buildingObj.transform.Find("Image_Icon");
    //     if (iconTransform != null && cat != null && !string.IsNullOrEmpty(cat.icon))
    //     {
    //         Image icon = iconTransform.GetComponent<Image>();
    //         if (icon != null)
    //         {
    //             // Load icon from Assets/images/icons
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
    //                 Debug.LogWarning("Icon not found: " + iconPath);
    //             }
    //         }
    //     }
    //     else
    //     {
    //         Debug.LogWarning("Image_icon child not found in prefab or category icon missing.");
    //     }
    // }

}
