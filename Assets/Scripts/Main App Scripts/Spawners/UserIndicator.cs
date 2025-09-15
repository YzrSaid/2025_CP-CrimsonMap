using System.Collections;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using UnityEngine;

public class UserIndicator : MonoBehaviour
{
    [Header("References")]
    public AbstractMap mapboxMap;
    public GameObject userIndicatorPrefab;
    public GameObject shadowConePrefab; // Create a cone or triangle prefab for the direction shadow

    [Header("Settings")]
    public float heightOffset = 2f;
    public float updateInterval = 0.2f; // Update frequency
    public float positionSmoothness = 0.3f;
    public float rotationSmoothness = 0.5f;

    [Header("Shadow/Direction Indicator")]
    public float shadowDistance = 5f; // How far the shadow extends
    public float shadowAngle = 45f; // Cone angle of the shadow
    public Color shadowColor = new Color(0.2f, 0.6f, 1f, 0.3f); // Semi-transparent blue
    public Material shadowMaterial; // Optional: custom material for shadow

    private GameObject userIndicatorInstance;
    private GameObject shadowConeInstance;
    private Vector3 lastWorldPos = Vector3.zero;
    private float lastHeading = 0f;
    private float lastUpdateTime = 0f;
    private bool isInitialized = false;

    void Awake()
    {
        // Find map if not assigned
        if (mapboxMap == null)
        {
            mapboxMap = FindObjectOfType<AbstractMap>();
        }
    }

    private IEnumerator Start()
    {
        if (mapboxMap == null)
        {
            Debug.LogError("No AbstractMap found! Please assign mapboxMap in inspector");
            yield break;
        }

        // Wait for map to be ready
        while (mapboxMap == null || !mapboxMap.gameObject.activeInHierarchy)
        {
            yield return new WaitForSeconds(0.5f);
        }

        // Wait for GPSManager
        while (GPSManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        // Additional delay to ensure map is fully initialized
        yield return new WaitForSeconds(2f);

        SpawnUserIndicator();
    }

    void SpawnUserIndicator()
    {
        if (userIndicatorPrefab == null)
        {
            Debug.LogError("User indicator prefab is null!");
            return;
        }

        // Spawn the main indicator
        userIndicatorInstance = Instantiate(userIndicatorPrefab, Vector3.zero, Quaternion.identity, mapboxMap.transform);
        userIndicatorInstance.name = "UserIndicatorInstance";

        // Spawn the shadow cone if prefab is provided
        if (shadowConePrefab != null)
        {
            shadowConeInstance = Instantiate(shadowConePrefab, Vector3.zero, Quaternion.identity, mapboxMap.transform);
            shadowConeInstance.name = "UserDirectionShadow";

            // Apply shadow material and color
            ApplyShadowAppearance();
        }

        // Make sure user indicator renders on top
        Renderer[] renderers = userIndicatorInstance.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.material.renderQueue = 3001; // Higher than default (3000)
        }

        // Also for the shadow
        if (shadowConeInstance != null)
        {
            Renderer[] shadowRenderers = shadowConeInstance.GetComponentsInChildren<Renderer>();
            foreach (var shadowRenderer in shadowRenderers)
            {
                shadowRenderer.material.renderQueue = 2999; // Below user indicator but above ground
            }
        }

        isInitialized = true;
        Debug.Log("User indicator spawned successfully");
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

            // Set shadow color
            if (renderer.material != null)
            {
                renderer.material.color = shadowColor;

                // Make sure it's transparent
                if (renderer.material.HasProperty("_Mode"))
                {
                    renderer.material.SetFloat("_Mode", 3); // Transparent mode
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

        // Only update at intervals, not every frame
        if (Time.time - lastUpdateTime < updateInterval)
            return;

        lastUpdateTime = Time.time;

        UpdateUserIndicatorPosition();
        UpdateDirectionShadow();
    }

    void UpdateUserIndicatorPosition()
    {
        Vector2 gpsCoords = GPSManager.Instance.GetSmoothedCoordinates();
        float heading = GPSManager.Instance.GetSmoothedHeading();

        // Convert GPS to world position using Mapbox
        Vector3 worldPos = mapboxMap.GeoToWorldPosition(new Vector2d(gpsCoords.x, gpsCoords.y), false);
        worldPos.y = heightOffset;

        // Smooth position transition
        Vector3 smoothedPos = Vector3.Lerp(lastWorldPos, worldPos, positionSmoothness);
        userIndicatorInstance.transform.position = smoothedPos;
        lastWorldPos = smoothedPos;

        float unityYRotation = 90f - heading;
        float smoothedHeading = Mathf.LerpAngle(lastHeading, unityYRotation, rotationSmoothness);

        userIndicatorInstance.transform.rotation = Quaternion.Euler(0, smoothedHeading, 0);
        lastHeading = smoothedHeading;
    }

    void UpdateDirectionShadow()
    {
        if (shadowConeInstance == null || userIndicatorInstance == null) return;

        // Position shadow at same location as user indicator
        Vector3 shadowPos = userIndicatorInstance.transform.position;
        shadowPos.y = heightOffset - 0.1f; // Slightly below the indicator
        shadowConeInstance.transform.position = shadowPos;

        // Same rotation as user indicator
        shadowConeInstance.transform.rotation = userIndicatorInstance.transform.rotation;

        // Scale the shadow cone based on settings
        Vector3 shadowScale = shadowConeInstance.transform.localScale;
        shadowScale.z = shadowDistance; // Length of the cone
        shadowScale.x = shadowDistance * Mathf.Tan(shadowAngle * 0.5f * Mathf.Deg2Rad); // Width based on angle
        shadowScale.y = shadowScale.x; // Keep it proportional
        shadowConeInstance.transform.localScale = shadowScale;
    }

    // Public method to manually update position (useful for testing)
    public void ForceUpdate()
    {
        if (isInitialized)
        {
            lastUpdateTime = 0f; // Force next update
            UpdateUserIndicatorPosition();
            UpdateDirectionShadow();
        }
    }

    // Debug methods
    void OnDrawGizmosSelected()
    {
        if (userIndicatorInstance != null && Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(userIndicatorInstance.transform.position, 1f);

            // Draw direction arrow
            Vector3 forward = userIndicatorInstance.transform.forward * 3f;
            Gizmos.DrawRay(userIndicatorInstance.transform.position, forward);
        }
    }
}