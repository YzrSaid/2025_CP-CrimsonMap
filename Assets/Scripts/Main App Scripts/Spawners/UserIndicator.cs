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

    [Header("Settings")]
    public float heightOffset = 2f;
    public float updateInterval = 0.05f; // Faster update for snappier response
    public float positionSmoothness = 0.3f;
    public float rotationSmoothness = 10f; // Much faster rotation

    [Header("Shadow/Direction Indicator")]
    public float shadowDistance = 5f;
    public float shadowAngle = 45f;
    public Color shadowColor = new Color(0.2f, 0.6f, 1f, 0.3f);
    public Material shadowMaterial;

    [Header("Debug")]
    public bool enableDebugLogs = false;

    private GameObject userIndicatorInstance;
    private GameObject shadowConeInstance;
    private Vector3 lastWorldPos = Vector3.zero;
    private float lastHeading = 0f;
    private float lastUpdateTime = 0f;
    private bool isInitialized = false;

    private MapInteraction mapInteraction;

    void Awake()
    {
        if (mapboxMap == null)
        {
            mapboxMap = FindObjectOfType<AbstractMap>();
        }

        mapInteraction = FindObjectOfType<MapInteraction>();
    }

    private IEnumerator Start()
    {
        if (mapboxMap == null)
        {
            Debug.LogError("[UserIndicator] MapboxMap not found!");
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
            Debug.LogError("[UserIndicator] User indicator prefab not assigned!");
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
        Debug.Log("[UserIndicator] ✅ Spawned successfully");
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

        UpdateUserIndicatorPosition();
        UpdateUserIndicatorRotation();
        UpdateDirectionShadow();
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
            if (enableDebugLogs && Time.frameCount % 120 == 0)
                Debug.LogWarning("[UserIndicator] Compass not ready");
            return;
        }

        // ✅ SIMPLE FIX: Just use compass heading directly, counter the map rotation
        float compassHeading = GPSManager.Instance.GetHeading(); // Don't use smoothed for snappy response
        
        // Get map bearing
        float mapBearing = 0f;
        if (mapInteraction != null)
        {
            mapBearing = mapInteraction.GetCurrentBearing();
        }

        // ✅ The indicator should point in the compass direction in world space
        // Since it's a child of the rotated map, we subtract the map's rotation
        float targetHeading = compassHeading - mapBearing;

        // Normalize
        while (targetHeading > 180f) targetHeading -= 360f;
        while (targetHeading < -180f) targetHeading += 360f;

        // Apply rotation directly (snappy like Google Maps)
        userIndicatorInstance.transform.localRotation = Quaternion.Euler(0, targetHeading, 0);
        
        lastHeading = targetHeading;

        // Debug
        if (enableDebugLogs && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[UserIndicator] Compass: {compassHeading:F1}°, Map: {mapBearing:F1}°, Indicator: {targetHeading:F1}°");
        }
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
            UpdateUserIndicatorPosition();
            UpdateUserIndicatorRotation();
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
        }
    }
}