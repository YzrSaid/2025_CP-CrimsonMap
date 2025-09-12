using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Mapbox.Utils;
using Mapbox.Unity.Map;
using Mapbox.Unity.Location;

public class SimpleDotTester : MonoBehaviour
{
    [Header("Map Reference")]
    public AbstractMap mapboxMap;
    
    [Header("Prefab")]
    public GameObject dotPrefab;
    
    [Header("Test Settings")]
    public bool spawnOnStart = true;
    public float dotSize = 5f;
    public float heightOffset = 10f;
    
    private List<LocationBasedDot> spawnedDots = new List<LocationBasedDot>();
    
    void Start()
    {
        // Find map if not assigned
        if (mapboxMap == null)
        {
            mapboxMap = FindObjectOfType<AbstractMap>();
        }
        
        if (spawnOnStart)
        {
            StartCoroutine(WaitAndSpawnTestDots());
        }
    }
    
    IEnumerator WaitAndSpawnTestDots()
    {
        Debug.Log("🔴 SimpleDotTester: Waiting for map...");
        
        // Wait for map to initialize
        yield return new WaitForSeconds(3f);
        
        SpawnTestDots();
    }
    
    void SpawnTestDots()
    {
        if (mapboxMap == null)
        {
            Debug.LogError("❌ No map found!");
            return;
        }
        
        if (dotPrefab == null)
        {
            Debug.LogError("❌ No dot prefab assigned!");
            return;
        }
        
        Debug.Log("🔴 Spawning location-based test dots...");
        
        // Test coordinates around your area
        Vector2d[] testCoords = new Vector2d[]
        {
            new Vector2d(6.9136, 122.0614),  // Your exact coordinate
            new Vector2d(6.9140, 122.0614),  // Slightly north
            new Vector2d(6.9132, 122.0614),  // Slightly south
            new Vector2d(6.9136, 122.0618),  // Slightly east
            new Vector2d(6.9136, 122.0610),  // Slightly west
        };
        
        Color[] colors = { Color.red, Color.yellow, Color.green, Color.blue, Color.magenta };
        
        for (int i = 0; i < testCoords.Length; i++)
        {
            Vector2d geoCoord = testCoords[i];
            
            // Create the dot GameObject
            GameObject dot = Instantiate(dotPrefab, Vector3.zero, Quaternion.identity, mapboxMap.transform);
            dot.name = $"TestDot_{i}_({geoCoord.x:F4},{geoCoord.y:F4})";
            dot.transform.localScale = Vector3.one * dotSize;
            
            // Set color if possible
            Renderer renderer = dot.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = colors[i % colors.Length];
            }
            
            // Add the location-based component
            LocationBasedDot locationDot = dot.AddComponent<LocationBasedDot>();
            locationDot.Initialize(mapboxMap, geoCoord, heightOffset);
            
            spawnedDots.Add(locationDot);
            
            Debug.Log($"🔴 Spawned LocationBasedDot_{i} at geo: ({geoCoord.x}, {geoCoord.y})");
        }
        
        Debug.Log("✅ Location-based test dots spawned! They should follow map movement now.");
    }
    
    void Update()
    {
        // Manual spawn for testing
        if (Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log("🔴 Manual test spawn triggered with 'T' key");
            SpawnTestDots();
        }
        
        // Clear test dots
        if (Input.GetKeyDown(KeyCode.C))
        {
            Debug.Log("🧹 Clearing test dots");
            foreach (var dot in spawnedDots)
            {
                if (dot != null && dot.gameObject != null)
                {
                    DestroyImmediate(dot.gameObject);
                }
            }
            spawnedDots.Clear();
        }
    }
}

// Component that keeps a GameObject at a specific geographic location
public class LocationBasedDot : MonoBehaviour
{
    private AbstractMap map;
    private Vector2d geoLocation;
    private float heightOffset;
    
    public void Initialize(AbstractMap mapReference, Vector2d geoCoordinate, float height)
    {
        map = mapReference;
        geoLocation = geoCoordinate;
        heightOffset = height;
        
        // Initial position
        UpdatePosition();
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
        
        // Update our position
        transform.position = worldPos;
    }
}