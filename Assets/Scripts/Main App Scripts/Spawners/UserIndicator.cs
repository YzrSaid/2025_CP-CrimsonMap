using System.Collections;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using UnityEngine;

public class UserIndicator : MonoBehaviour
{
    [Header("References")]
    public AbstractMap mapboxMap;
    public GameObject userIndicatorPrefab;
    public GameObject shadowConePrefab;
    public PathfindingController pathfindingController;

    [Header("Settings")]
    public float heightOffset = 2f;
    public float updateInterval = 0.05f;
    public float positionSmoothness = 0.3f;
    public float rotationSmoothness = 10f;

    [Header("Shadow/Direction Indicator")]
    public float shadowDistance = 5f;
    public float shadowAngle = 45f;
    public Color shadowColor = new Color(0.2f, 0.6f, 1f, 0.3f);
    public Material shadowMaterial;

    private GameObject userIndicatorInstance;
    private GameObject shadowConeInstance;
    private Vector3 lastWorldPos = Vector3.zero;
    private Vector3 lockedPosition = Vector3.zero;
    private float lastHeading = 0f;
    private float lockedHeading = 0f;
    private float lastUpdateTime = 0f;
    private bool isInitialized = false;
    private bool wasLocked = false;

    private MapInteraction mapInteraction;

    void Awake()
    {
        if (mapboxMap == null)
        {
            mapboxMap = FindObjectOfType<AbstractMap>();
        }

        mapInteraction = FindObjectOfType<MapInteraction>();
        
        if (pathfindingController == null)
        {
            pathfindingController = FindObjectOfType<PathfindingController>();
        }
    }

    private IEnumerator Start()
    {
        if (mapboxMap == null)
        {
            yield break;
        }

        while (mapboxMap == null || !mapboxMap.gameObject.activeInHierarchy)
        {
            yield return new WaitForSeconds(0.5f);
        }

        while (GPSManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        int maxWait = 10;
        while (!GPSManager.Instance.IsCompassReady() && maxWait > 0)
        {
            yield return new WaitForSeconds(0.5f);
            maxWait--;
        }

        yield return new WaitForSeconds(1f);

        SpawnUserIndicator();
    }

    void SpawnUserIndicator()
    {
        if (userIndicatorPrefab == null)
        {
            return;
        }

        userIndicatorInstance = Instantiate(userIndicatorPrefab, Vector3.zero, Quaternion.identity, mapboxMap.transform);
        userIndicatorInstance.name = "UserIndicatorInstance";

        if (shadowConePrefab != null)
        {
            shadowConeInstance = Instantiate(shadowConePrefab, Vector3.zero, Quaternion.identity, mapboxMap.transform);
            shadowConeInstance.name = "UserDirectionShadow";
            ApplyShadowAppearance();
        }

        Renderer[] renderers = userIndicatorInstance.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.material.renderQueue = 3001;
        }

        if (shadowConeInstance != null)
        {
            Renderer[] shadowRenderers = shadowConeInstance.GetComponentsInChildren<Renderer>();
            foreach (var shadowRenderer in shadowRenderers)
            {
                shadowRenderer.material.renderQueue = 2999;
            }
        }

        isInitialized = true;
    }

    void ApplyShadowAppearance()
    {
        if (shadowConeInstance == null) return;

        Renderer[] renderers = shadowConeInstance.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (shadowMaterial != null)
            {
                renderer.material = shadowMaterial;
            }

            if (renderer.material != null)
            {
                renderer.material.color = shadowColor;

                if (renderer.material.HasProperty("_Mode"))
                {
                    renderer.material.SetFloat("_Mode", 3);
                    renderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    renderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    renderer.material.SetInt("_ZWrite", 0);
                    renderer.material.DisableKeyword("_ALPHATEST_ON");
                    renderer.material.EnableKeyword("_ALPHABLEND_ON");
                    renderer.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    renderer.material.renderQueue = 3000;
                }
            }
        }
    }

    void Update()
    {
        if (!isInitialized || GPSManager.Instance == null || userIndicatorInstance == null || mapboxMap == null)
            return;

        if (Time.time - lastUpdateTime < updateInterval)
            return;

        lastUpdateTime = Time.time;

        bool isLocked = IsLocationLocked();

        if (isLocked && !wasLocked)
        {
            lockedPosition = userIndicatorInstance.transform.position;
            lockedHeading = lastHeading;
        }

        wasLocked = isLocked;

        if (isLocked)
        {
            UpdateLockedIndicator();
        }
        else
        {
            UpdateUserIndicatorPosition();
            UpdateUserIndicatorRotation();
        }

        UpdateDirectionShadow();
    }

    private bool IsLocationLocked()
    {
        if (pathfindingController != null)
        {
            return pathfindingController.IsLocationLocked();
        }
        return false;
    }

    void UpdateLockedIndicator()
    {
        userIndicatorInstance.transform.position = lockedPosition;
        UpdateUserIndicatorRotation();
    }

    void UpdateUserIndicatorPosition()
    {
        Vector2 gpsCoords = GPSManager.Instance.GetSmoothedCoordinates();

        Vector3 worldPos = mapboxMap.GeoToWorldPosition(new Vector2d(gpsCoords.x, gpsCoords.y), false);
        worldPos.y = heightOffset;

        Vector3 smoothedPos = Vector3.Lerp(lastWorldPos, worldPos, positionSmoothness);
        userIndicatorInstance.transform.position = smoothedPos;
        lastWorldPos = smoothedPos;
    }

    void UpdateUserIndicatorRotation()
    {
        if (!GPSManager.Instance.IsCompassReady())
        {
            return;
        }

        float compassHeading = GPSManager.Instance.GetHeading();
        
        float mapBearing = 0f;
        if (mapInteraction != null)
        {
            mapBearing = mapInteraction.GetCurrentBearing();
        }

        float targetHeading = compassHeading - mapBearing;

        while (targetHeading > 180f) targetHeading -= 360f;
        while (targetHeading < -180f) targetHeading += 360f;

        userIndicatorInstance.transform.localRotation = Quaternion.Euler(0, targetHeading, 0);
        
        lastHeading = targetHeading;
    }

    void UpdateDirectionShadow()
    {
        if (shadowConeInstance == null || userIndicatorInstance == null) return;

        Vector3 shadowPos = userIndicatorInstance.transform.position;
        shadowPos.y = heightOffset - 0.1f;
        shadowConeInstance.transform.position = shadowPos;

        shadowConeInstance.transform.localRotation = userIndicatorInstance.transform.localRotation;

        Vector3 shadowScale = shadowConeInstance.transform.localScale;
        shadowScale.z = shadowDistance;
        shadowScale.x = shadowDistance * Mathf.Tan(shadowAngle * 0.5f * Mathf.Deg2Rad);
        shadowScale.y = shadowScale.x;
        shadowConeInstance.transform.localScale = shadowScale;
    }

    public void ForceUpdate()
    {
        if (isInitialized)
        {
            lastUpdateTime = 0f;
            
            if (IsLocationLocked())
            {
                UpdateLockedIndicator();
            }
            else
            {
                UpdateUserIndicatorPosition();
                UpdateUserIndicatorRotation();
            }
            
            UpdateDirectionShadow();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (userIndicatorInstance != null && Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(userIndicatorInstance.transform.position, 1f);

            Vector3 forward = userIndicatorInstance.transform.forward * 3f;
            Gizmos.DrawRay(userIndicatorInstance.transform.position, forward);
            
            if (IsLocationLocked())
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(lockedPosition, 1.5f);
            }
        }
    }
}