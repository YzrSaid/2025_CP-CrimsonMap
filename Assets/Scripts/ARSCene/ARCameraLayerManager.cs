using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class ARCameraLayerManager : MonoBehaviour
{
    [Header("Camera References")]
    [Tooltip("The AR Camera (usually AR Camera from XR Origin)")]
    public Camera arCamera;

    [Tooltip("The Mapbox Camera (the one looking at the 2D map)")]
    public Camera mapboxCamera;

    [Header("Mapbox Objects")]
    [Tooltip("The root Mapbox Map GameObject (usually called 'Map' or 'AbstractMap')")]
    public GameObject mapboxMapRoot;

    [Header("Layer Settings")]
    public string mapboxLayerName = "MapboxMap";

    [Header("Debug")]
    public bool enableDebugLogs = true;

    private Vector3 originalMapPosition;

    void Awake()
    {
        FindCameras();

        if (mapboxMapRoot == null)
        {
            mapboxMapRoot = GameObject.Find("Map") ??
                            GameObject.Find("AbstractMap") ??
                            GameObject.Find("MapboxMap");
        }

        if (mapboxMapRoot != null)
        {
            originalMapPosition = mapboxMapRoot.transform.position;
        }

        // Set up cameras immediately in Awake
        SetupURPCameras();
    }

    void Start()
    {
        // Subscribe to spawning completion event
        ARMapManager.OnSpawningComplete += OnMapSpawningComplete;

        // Also start a backup timer in case event doesn't fire
        StartCoroutine(BackupLayerAssignment());
    }

    private void OnDestroy()
    {
        // Clean up subscription
        ARMapManager.OnSpawningComplete -= OnMapSpawningComplete;
    }

    private void OnMapSpawningComplete()
    {
        DebugLog("🎯 Received spawning complete event!");
        AssignLayersNow();
    }


    private IEnumerator BackupLayerAssignment()
    {
        // Backup: If event doesn't fire after 10 seconds, force assignment
        yield return new WaitForSeconds(10f);

        if (mapboxMapRoot != null && mapboxMapRoot.transform.childCount > 0)
        {
            DebugLog("⏰ Backup timer triggered - assigning layers now");
            AssignLayersNow();
        }
    }

    private void AssignLayersNow()
    {
        int mapboxLayer = LayerMask.NameToLayer(mapboxLayerName);
        if (mapboxLayer != -1 && mapboxMapRoot != null)
        {
            DebugLog("\n=== ASSIGNING LAYERS AFTER SPAWN ===");
            AssignMapTerrainToLayer(mapboxMapRoot, mapboxLayer);
         
        }
    }

   

    private void FindCameras()
    {
        if (arCamera == null)
        {
            arCamera = Camera.main;
            if (arCamera == null)
            {
                arCamera = FindObjectOfType<UnityEngine.XR.ARFoundation.ARCameraManager>()?.GetComponent<Camera>();
            }
        }

        if (mapboxCamera == null)
        {
            GameObject mapboxCamObj = GameObject.Find("MapCamera") ??
                                       GameObject.Find("MapboxCamera") ??
                                       GameObject.Find("Map Camera");

            if (mapboxCamObj != null)
            {
                mapboxCamera = mapboxCamObj.GetComponent<Camera>();
            }
        }
    }

    private void SetupURPCameras()
    {
        if (arCamera == null || mapboxCamera == null)
        {
            DebugLog("❌ Cannot setup - cameras not found!");
            DebugLog($"   AR Camera: {(arCamera != null ? arCamera.name : "NULL")}");
            DebugLog($"   Mapbox Camera: {(mapboxCamera != null ? mapboxCamera.name : "NULL")}");
            return;
        }

        int mapboxLayer = LayerMask.NameToLayer(mapboxLayerName);

        if (mapboxLayer == -1)
        {
            DebugLog($"❌ Layer '{mapboxLayerName}' not found!");
            DebugLog("   Create it: Edit → Project Settings → Tags and Layers");
            return;
        }

        DebugLog("=== SETTING UP URP CAMERAS ===");
        DebugLog($"AR Camera found: {arCamera.name}");
        DebugLog($"Mapbox Camera found: {mapboxCamera.name}");
        DebugLog($"MapboxMap Layer ID: {mapboxLayer}");

        // === MAPBOX CAMERA (Renders ONLY the map to Render Texture) ===
        var mapboxCamData = mapboxCamera.GetUniversalAdditionalCameraData();
        if (mapboxCamData != null)
        {
            mapboxCamData.renderType = CameraRenderType.Base; // Base camera
            mapboxCamData.renderPostProcessing = false;
            DebugLog("   • Set Mapbox Camera to Base render type");
        }
        else
        {
            DebugLog("   ⚠️ No URP Camera Data on Mapbox Camera!");
        }

        mapboxCamera.clearFlags = CameraClearFlags.SolidColor;
        mapboxCamera.backgroundColor = Color.black;
        mapboxCamera.cullingMask = (1 << mapboxLayer); // ONLY MapboxMap layer

        DebugLog($"✅ Mapbox Camera configured:");
        DebugLog($"   • Camera: {mapboxCamera.name}");
        DebugLog($"   • Render Type: Base");
        DebugLog($"   • Culling Mask: ONLY {mapboxLayerName} (layer {mapboxLayer})");
        DebugLog($"   • Renders to: {(mapboxCamera.targetTexture != null ? mapboxCamera.targetTexture.name : "Screen")}");

        // === AR CAMERA (Renders everything EXCEPT the map) ===
        var arCamData = arCamera.GetUniversalAdditionalCameraData();
        if (arCamData != null)
        {
            arCamData.renderType = CameraRenderType.Base; // Base camera
            arCamData.renderPostProcessing = true;
            DebugLog("   • Set AR Camera to Base render type");
        }
        else
        {
            DebugLog("   ⚠️ No URP Camera Data on AR Camera!");
        }

        arCamera.clearFlags = CameraClearFlags.SolidColor;
        arCamera.backgroundColor = new Color(0, 0, 0, 0); // Transparent for AR
        arCamera.cullingMask = ~(1 << mapboxLayer); // Everything EXCEPT MapboxMap

        DebugLog($"✅ AR Camera configured:");
        DebugLog($"   • Camera: {arCamera.name}");
        DebugLog($"   • Render Type: Base");
        DebugLog($"   • Culling Mask: Everything EXCEPT {mapboxLayerName} (excluding layer {mapboxLayer})");
        DebugLog($"   • Will see: spawned objects, AR markers, UI");

        // === ASSIGN TERRAIN TO LAYER ===
        if (mapboxMapRoot != null)
        {
            DebugLog($"\n=== ASSIGNING MAP TERRAIN TO LAYER ===");
            AssignMapTerrainToLayer(mapboxMapRoot, mapboxLayer);
        }

        DebugLog("");
        DebugLog("✅ URP SETUP COMPLETE!");
        DebugLog($"   • Map position: {mapboxMapRoot?.transform.position}");
        DebugLog("   • Spawned objects should use Default layer (visible to AR)");
    }

    private void AssignMapTerrainToLayer(GameObject mapRoot, int layer)
    {
        int tilesAssigned = 0;

        // First, set the Map root itself
        mapRoot.layer = layer;
        DebugLog($"   • Assigned Map root '{mapRoot.name}' to {mapboxLayerName} layer");

        // Look for TileProvider and its children
        Transform tileProvider = mapRoot.transform.Find("TileProvider");
        if (tileProvider != null)
        {
            DebugLog("   • Found TileProvider - assigning all children recursively");
            SetLayerRecursively(tileProvider.gameObject, layer);
            tilesAssigned += tileProvider.childCount;
        }

        // Also check for any other direct children that aren't spawned objects
        foreach (Transform child in mapRoot.transform)
        {
            string childName = child.name.ToLower();

            // If it's NOT TileProvider (already handled) and NOT a spawned object, assign it
            if (child != tileProvider)
            {
                SetLayerRecursively(child.gameObject, layer);
                tilesAssigned++;
                DebugLog($"   • Assigned '{child.name}' to {mapboxLayerName} layer");
            }
        }

        if (tilesAssigned == 0)
        {
            DebugLog("   ⚠️ WARNING: No terrain tiles found!");
            DebugLog("   Attempting fallback method...");

            // Fallback: Just assign everything under Map except spawned objects
            MeshRenderer[] renderers = mapRoot.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                // Skip spawned objects
                string rendererName = renderer.name.ToLower();
                if (rendererName.Contains("pathway") ||
                    rendererName.Contains("path") ||
                    rendererName.Contains("edge") ||
                    rendererName.Contains("node") ||
                    rendererName.Contains("barrier") ||
                    rendererName.Contains("infrastructure"))
                {
                    continue;
                }

                SetLayerRecursively(renderer.gameObject, layer);
                tilesAssigned++;
            }

            DebugLog($"   • Fallback: Assigned {tilesAssigned} terrain renderers to {mapboxLayerName} layer");
        }
        else
        {
            DebugLog($"✅ Successfully assigned {tilesAssigned} tile objects to {mapboxLayerName} layer");
        }
    }
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[URP ARCameraLayer] {message}");
        }
    }

    [ContextMenu("Apply URP Layer Fix")]
    public void ApplyLayerFix()
    {
        FindCameras();
        SetupURPCameras();
    }

    [ContextMenu("Debug: Check Camera Settings")]
    public void DebugCameraSettings()
    {
        DebugLog("=== CHECKING CAMERA SETTINGS ===");

        if (arCamera != null)
        {
            var arCamData = arCamera.GetUniversalAdditionalCameraData();
            DebugLog("\n=== AR CAMERA ===");
            DebugLog($"Name: {arCamera.name}");
            DebugLog($"GameObject: {arCamera.gameObject.name}");
            DebugLog($"Culling Mask: {LayerMaskToString(arCamera.cullingMask)}");
            DebugLog($"Clear Flags: {arCamera.clearFlags}");
            DebugLog($"URP Render Type: {(arCamData != null ? arCamData.renderType.ToString() : "No URP Data")}");
        }
        else
        {
            DebugLog("AR Camera: NULL");
        }

        if (mapboxCamera != null)
        {
            var mapCamData = mapboxCamera.GetUniversalAdditionalCameraData();
            DebugLog("\n=== MAPBOX CAMERA ===");
            DebugLog($"Name: {mapboxCamera.name}");
            DebugLog($"GameObject: {mapboxCamera.gameObject.name}");
            DebugLog($"Culling Mask: {LayerMaskToString(mapboxCamera.cullingMask)}");
            DebugLog($"Clear Flags: {mapboxCamera.clearFlags}");
            DebugLog($"Target Texture: {(mapboxCamera.targetTexture != null ? mapboxCamera.targetTexture.name : "None (renders to screen)")}");
            DebugLog($"URP Render Type: {(mapCamData != null ? mapCamData.renderType.ToString() : "No URP Data")}");
        }
        else
        {
            DebugLog("Mapbox Camera: NULL");
        }
    }

    [ContextMenu("Debug: Check Spawned Objects")]
    public void CheckSpawnedObjects()
    {
        if (mapboxMapRoot == null)
        {
            DebugLog("❌ Map root not assigned");
            return;
        }

        DebugLog("=== CHECKING SPAWNED OBJECTS ===");
        DebugLog($"Map position: {mapboxMapRoot.transform.position}");
        DebugLog($"Map layer: {LayerMask.LayerToName(mapboxMapRoot.layer)}");

        int pathCount = 0;
        int nodeCount = 0;
        int barrierCount = 0;

        foreach (Transform child in mapboxMapRoot.transform)
        {
            string name = child.name.ToLower();
            string childLayer = LayerMask.LayerToName(child.gameObject.layer);

            if (name.Contains("pathway") || name.Contains("path") || name.Contains("edge"))
            {
                pathCount++;
                if (pathCount <= 3)
                {
                    DebugLog($"  Path: {child.name} (layer: {childLayer}, pos: {child.position})");
                }
            }
            else if (name.Contains("node") || name.Contains("infrastructure"))
            {
                nodeCount++;
                if (nodeCount <= 3)
                {
                    DebugLog($"  Node: {child.name} (layer: {childLayer}, pos: {child.position})");
                }
            }
            else if (name.Contains("barrier"))
            {
                barrierCount++;
                if (barrierCount <= 3)
                {
                    DebugLog($"  Barrier: {child.name} (layer: {childLayer})");
                }
            }
        }

        DebugLog($"\nTotal spawned objects:");
        DebugLog($"  • Paths: {pathCount}");
        DebugLog($"  • Nodes: {nodeCount}");
        DebugLog($"  • Barriers: {barrierCount}");
    }

    [ContextMenu("Debug: List All Map Children")]
    public void ListAllMapChildren()
    {
        if (mapboxMapRoot == null)
        {
            DebugLog("❌ Map root not assigned");
            return;
        }

        DebugLog("=== ALL MAP CHILDREN ===");
        DebugLog($"Map: {mapboxMapRoot.name} (layer: {LayerMask.LayerToName(mapboxMapRoot.layer)})");

        int count = 0;
        foreach (Transform child in mapboxMapRoot.transform)
        {
            string childLayer = LayerMask.LayerToName(child.gameObject.layer);
            DebugLog($"  [{count}] {child.name} (layer: {childLayer})");
            count++;

            if (count >= 20)
            {
                DebugLog($"  ... and {mapboxMapRoot.transform.childCount - 20} more children");
                break;
            }
        }
    }

    private string LayerMaskToString(int mask)
    {
        string result = "";
        for (int i = 0; i < 32; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    result += layerName + ", ";
                }
            }
        }
        return string.IsNullOrEmpty(result) ? "Nothing" : result.TrimEnd(',', ' ');
    }
}