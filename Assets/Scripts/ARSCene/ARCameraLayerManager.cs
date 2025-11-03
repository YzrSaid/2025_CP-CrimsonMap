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
        StartCoroutine(BackupLayerAssignment());
    }

    private void OnDestroy()
    {
        ARMapManager.OnSpawningComplete -= OnMapSpawningComplete;
    }

    private void OnMapSpawningComplete()
    {
        AssignLayersNow();
    }

    private IEnumerator BackupLayerAssignment()
    {
        yield return new WaitForSeconds(10f);

        if (mapboxMapRoot != null && mapboxMapRoot.transform.childCount > 0)
        {
            AssignLayersNow();
        }
    }

    private void AssignLayersNow()
    {
        int mapboxLayer = LayerMask.NameToLayer(mapboxLayerName);
        if (mapboxLayer != -1 && mapboxMapRoot != null)
        {
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
        arCamera.cullingMask = ~(1 << mapboxLayer);

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
        if (arCamera != null)
        {
            var arCamData = arCamera.GetUniversalAdditionalCameraData();
        }

        if (mapboxCamera != null)
        {
            var mapCamData = mapboxCamera.GetUniversalAdditionalCameraData();
        }
    }

    [ContextMenu("Debug: Check Spawned Objects")]
    public void CheckSpawnedObjects()
    {
        if (mapboxMapRoot == null)
        {
            return;
        }

        int pathCount = 0;
        int nodeCount = 0;
        int barrierCount = 0;

        foreach (Transform child in mapboxMapRoot.transform)
        {
            string name = child.name.ToLower();

            if (name.Contains("pathway") || name.Contains("path") || name.Contains("edge"))
            {
                pathCount++;
            }
            else if (name.Contains("node") || name.Contains("infrastructure"))
            {
                nodeCount++;
            }
            else if (name.Contains("barrier"))
            {
                barrierCount++;
            }
        }
    }

    [ContextMenu("Debug: List All Map Children")]
    public void ListAllMapChildren()
    {
        if (mapboxMapRoot == null)
        {
            return;
        }

        int count = 0;
        foreach (Transform child in mapboxMapRoot.transform)
        {
            count++;
            if (count >= 20)
            {
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