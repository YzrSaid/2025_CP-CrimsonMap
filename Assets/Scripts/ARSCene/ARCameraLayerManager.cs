using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class ARCameraLayerManager : MonoBehaviour
{
    [Header("Camera References")]
    public Camera arCamera;
    public Camera mapboxCamera;

    [Header("Mapbox Objects")]
    public GameObject mapboxMapRoot;

    [Header("Layer Settings")]
    public string mapboxLayerName = "MapboxMap";

    [Header("Debug")]
    public bool enableDebugLogs = true;

    private Vector3 originalMapPosition;
    private Coroutine backupLayerCoroutine;
    private bool hasAssignedLayers = false;

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

        SetupURPCameras();
    }

    void Start()
    {
        ARMapManager.OnSpawningComplete += OnMapSpawningComplete;
        backupLayerCoroutine = StartCoroutine(BackupLayerAssignment());
    }

    private void OnDestroy()
    {
        ARMapManager.OnSpawningComplete -= OnMapSpawningComplete;
        if (backupLayerCoroutine != null)
        {
            StopCoroutine(backupLayerCoroutine);
        }
    }

    private void OnMapSpawningComplete()
    {
        AssignLayersNow();
        hasAssignedLayers = true;
        
        // Stop backup coroutine since we've already assigned
        if (backupLayerCoroutine != null)
        {
            StopCoroutine(backupLayerCoroutine);
            backupLayerCoroutine = null;
        }
    }

    private IEnumerator BackupLayerAssignment()
    {
        yield return new WaitForSeconds(10f);

        // Only assign if we haven't already
        if (!hasAssignedLayers && mapboxMapRoot != null && mapboxMapRoot.transform.childCount > 0)
        {
            AssignLayersNow();
            hasAssignedLayers = true;
        }
        
        backupLayerCoroutine = null;
    }

    private void AssignLayersNow()
    {
        int mapboxLayer = LayerMask.NameToLayer(mapboxLayerName);
        if (mapboxLayer != -1 && mapboxMapRoot != null)
        {
            AssignMapTerrainToLayer(mapboxMapRoot, mapboxLayer);
            
            // Ensure UI layer is NOT affected
            PreserveUILayers();
        }
    }

    private void PreserveUILayers()
    {
        // Make sure AR camera can still see UI layer
        if (arCamera != null)
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer != -1)
            {
                arCamera.cullingMask |= (1 << uiLayer);
            }
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
            return;
        }

        int mapboxLayer = LayerMask.NameToLayer(mapboxLayerName);

        if (mapboxLayer == -1)
        {
            return;
        }

        var mapboxCamData = mapboxCamera.GetUniversalAdditionalCameraData();
        if (mapboxCamData != null)
        {
            mapboxCamData.renderType = CameraRenderType.Base;
            mapboxCamData.renderPostProcessing = false;
        }

        mapboxCamera.clearFlags = CameraClearFlags.SolidColor;
        mapboxCamera.backgroundColor = Color.black;
        mapboxCamera.cullingMask = (1 << mapboxLayer);

        var arCamData = arCamera.GetUniversalAdditionalCameraData();
        if (arCamData != null)
        {
            arCamData.renderType = CameraRenderType.Base;
            arCamData.renderPostProcessing = true;
        }

        arCamera.clearFlags = CameraClearFlags.SolidColor;
        arCamera.backgroundColor = new Color(0, 0, 0, 0);
        
        // Exclude Mapbox layer but INCLUDE UI layer
        int uiLayer = LayerMask.NameToLayer("UI");
        arCamera.cullingMask = ~(1 << mapboxLayer);
        if (uiLayer != -1)
        {
            arCamera.cullingMask |= (1 << uiLayer);
        }

        if (mapboxMapRoot != null)
        {
            AssignMapTerrainToLayer(mapboxMapRoot, mapboxLayer);
        }
    }

    private void AssignMapTerrainToLayer(GameObject mapRoot, int layer)
    {
        int tilesAssigned = 0;

        mapRoot.layer = layer;

        Transform tileProvider = mapRoot.transform.Find("TileProvider");
        if (tileProvider != null)
        {
            SetLayerRecursively(tileProvider.gameObject, layer);
            tilesAssigned += tileProvider.childCount;
        }

        foreach (Transform child in mapRoot.transform)
        {
            string childName = child.name.ToLower();

            if (child != tileProvider)
            {
                SetLayerRecursively(child.gameObject, layer);
                tilesAssigned++;
            }
        }

        if (tilesAssigned == 0)
        {
            MeshRenderer[] renderers = mapRoot.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
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

    [ContextMenu("Apply URP Layer Fix")]
    public void ApplyLayerFix()
    {
        FindCameras();
        SetupURPCameras();
    }
}