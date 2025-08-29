// using UnityEngine;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;

// /// <summary>
// /// Manages multiple campus maps based on map.json configuration
// /// </summary>
// public class MapManager : MonoBehaviour
// {
//     [Header("Map Configuration")]
//     public string currentMapId = "M-001"; // Default to Main Map
    
//     [Header("References")]
//     public RectTransform mapContainer;
//     public BarrierSpawner barrierSpawner;
//     public BuildingSpawner buildingSpawner;
//     public PathRenderer pathRenderer;
    
//     [Header("JSON Files")]
//     public string mapFileName = "map.json";
//     public string campusFileName = "campus.json";
    
//     // Map data structures
//     [System.Serializable]
//     public class Map
//     {
//         public string map_id;
//         public string map_name;
//         public string[] campus_included;
//     }
    
//     [System.Serializable]
//     public class MapList
//     {
//         public List<Map> maps;
//     }
    
//     [System.Serializable]
//     public class Campus
//     {
//         public string campus_id;
//         public string campus_name;
//     }
    
//     [System.Serializable]
//     public class CampusList
//     {
//         public List<Campus> campuses;
//     }
    
//     // Current map data
//     private Map currentMap;
//     private List<Campus> activeCampuses;
    
//     public static MapManager Instance { get; private set; }
    
//     void Awake()
//     {
//         if (Instance == null)
//         {
//             Instance = this;
//             DontDestroyOnLoad(gameObject);
//         }
//         else
//         {
//             Destroy(gameObject);
//         }
//     }
    
//     void Start()
//     {
//         LoadMapConfiguration();
//         SetActiveMap(currentMapId);
//     }
    
//     void LoadMapConfiguration()
//     {
//         // Load map.json
//         string mapPath = Path.Combine(Application.streamingAssetsPath, mapFileName);
//         if (!File.Exists(mapPath))
//         {
//             Debug.LogError($"Map configuration file not found: {mapFileName}");
//             return;
//         }
        
//         string mapJson = File.ReadAllText(mapPath);
//         MapList mapList = JsonUtility.FromJson<MapList>("{\"maps\":" + mapJson + "}");
        
//         // Load campus.json  
//         string campusPath = Path.Combine(Application.streamingAssetsPath, campusFileName);
//         if (!File.Exists(campusPath))
//         {
//             Debug.LogError($"Campus configuration file not found: {campusFileName}");
//             return;
//         }
        
//         string campusJson = File.ReadAllText(campusPath);
//         CampusList campusList = JsonUtility.FromJson<CampusList>("{\"campuses\":" + campusJson + "}");
        
//         Debug.Log($"Loaded {mapList.maps.Count} maps and {campusList.campuses.Count} campuses");
//     }
    
//     /// <summary>
//     /// Switch to a different map (Main Map or Campus C Map)
//     /// </summary>
//     public void SetActiveMap(string mapId)
//     {
//         currentMapId = mapId;
        
//         // Load map configuration
//         string mapPath = Path.Combine(Application.streamingAssetsPath, mapFileName);
//         string mapJson = File.ReadAllText(mapPath);
//         MapList mapList = JsonUtility.FromJson<MapList>("{\"maps\":" + mapJson + "}");
        
//         currentMap = mapList.maps.FirstOrDefault(m => m.map_id == mapId);
//         if (currentMap == null)
//         {
//             Debug.LogError($"Map not found: {mapId}");
//             return;
//         }
        
//         // Load campus data to get campus names
//         string campusPath = Path.Combine(Application.streamingAssetsPath, campusFileName);
//         string campusJson = File.ReadAllText(campusPath);
//         CampusList campusList = JsonUtility.FromJson<CampusList>("{\"campuses\":" + campusJson + "}");
        
//         // Get active campuses for this map
//         activeCampuses = new List<Campus>();
//         foreach (string campusId in currentMap.campus_included)
//         {
//             Campus campus = campusList.campuses.FirstOrDefault(c => c.campus_id == campusId);
//             if (campus != null)
//             {
//                 activeCampuses.Add(campus);
//             }
//         }
        
//         Debug.Log($"Switched to {currentMap.map_name} with campuses: {string.Join(", ", activeCampuses.Select(c => c.campus_name))}");
        
//         // Clear existing map elements
//         ClearMap();
        
//         // Regenerate map elements for active campuses
//         RegenerateMapElements();
//     }
    
//     /// <summary>
//     /// Get list of campus IDs that should be shown on current map
//     /// </summary>
//     public string[] GetActiveCampusIds()
//     {
//         if (currentMap == null) return new string[0];
//         return currentMap.campus_included;
//     }
    
//     /// <summary>
//     /// Get list of campus names that should be shown on current map
//     /// </summary>
//     public string[] GetActiveCampusNames()
//     {
//         if (activeCampuses == null) return new string[0];
//         return activeCampuses.Select(c => c.campus_name).ToArray();
//     }
    
//     /// <summary>
//     /// Check if a campus should be shown on current map
//     /// </summary>
//     public bool IsCampusActive(string campusId)
//     {
//         if (currentMap == null) return false;
//         return currentMap.campus_included.Contains(campusId);
//     }
    
//     void ClearMap()
//     {
//         // Clear all spawned elements
//         foreach (Transform child in mapContainer)
//         {
//             if (child.name.Contains("Barrier") || child.name.Contains("Path") || child.name.Contains("Building"))
//             {
//                 DestroyImmediate(child.gameObject);
//             }
//         }
//     }
    
//     void RegenerateMapElements()
//     {
//         // Update spawners to use active campuses
//         if (barrierSpawner != null)
//         {
//             barrierSpawner.campusesToSpawn = GetActiveCampusNames();
//         }
        
//         // Trigger regeneration (you'll need to add these methods to your spawners)
//         StartCoroutine(RegenerateAllElements());
//     }
    
//     System.Collections.IEnumerator RegenerateAllElements()
//     {
//         // Wait a frame to ensure everything is cleared
//         yield return null;
        
//         // Regenerate barriers first
//         if (barrierSpawner != null)
//         {
//             barrierSpawner.LoadAndSpawnBarriers(); // Make this method public
//         }
        
//         // Wait for barriers to complete
//         yield return new WaitForSeconds(0.1f);
        
//         // Regenerate buildings
//         if (buildingSpawner != null)
//         {
//             buildingSpawner.LoadAndSpawnBuildings(); // Make this method public
//         }
        
//         // Wait for buildings to complete  
//         yield return new WaitForSeconds(0.1f);
        
//         // Regenerate paths
//         if (pathRenderer != null)
//         {
//             pathRenderer.LoadAndRenderAllPathways(); // Make this method public
//         }
//     }
    
//     /// <summary>
//     /// UI method to switch maps
//     /// </summary>
//     public void SwitchToMainMap()
//     {
//         SetActiveMap("M-001");
//     }
    
//     /// <summary>
//     /// UI method to switch maps
//     /// </summary>
//     public void SwitchToCampusCMap()
//     {
//         SetActiveMap("M-002");
//     }
// }