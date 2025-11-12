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
    public float updateInterval = 0.05f;
    public float positionSmoothness = 0.3f;
    public float rotationSmoothness = 5f;

    [Header("User Indicator Appearance")]
    public Color userIndicatorColor = new Color(0.2f, 0.6f, 1f, 1f);
    public Material userIndicatorMaterial;
    public bool useTransparentIndicator = false;
    [Range(0f, 1f)]
    public float userIndicatorAlpha = 1f;

    [Header("Shadow/Direction Indicator")]
    public float shadowDistance = 5f;
    public float shadowAngle = 45f;
    public Color shadowColor = new Color(0.2f, 0.6f, 1f, 0.3f);
    public Material shadowMaterial;

    private GameObject userIndicatorInstance;
    private GameObject shadowConeInstance;
    private Vector3 lastWorldPos = Vector3.zero;
    private float lastHeading = 0f;
    private float lastUpdateTime = 0f;
    private bool isInitialized = false;

    private bool isMapDragging = false;

    void Awake()
    {
        if (mapboxMap == null)
        {
            mapboxMap = FindObjectOfType<AbstractMap>();
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

        ApplyUserIndicatorAppearance();

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

    void ApplyUserIndicatorAppearance()
    {
        if (userIndicatorInstance == null) return;

        Renderer[] renderers = userIndicatorInstance.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (userIndicatorMaterial != null)
            {
                renderer.material = userIndicatorMaterial;
            }

            if (renderer.material != null)
            {
                Color finalColor = userIndicatorColor;
                if (useTransparentIndicator)
                {
                    finalColor.a = userIndicatorAlpha;
                }

                renderer.material.color = finalColor;

                if (useTransparentIndicator && finalColor.a < 1f)
                {
                    SetupTransparentMaterial(renderer.material);
                }
                else
                {
                    SetupOpaqueMaterial(renderer.material);
                }
            }
        }
    }

    void SetupTransparentMaterial(Material mat)
    {
        if (mat.HasProperty("_Mode"))
        {
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
    }

    void SetupOpaqueMaterial(Material mat)
    {
        if (mat.HasProperty("_Mode"))
        {
            mat.SetFloat("_Mode", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = -1;
        }
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

        if (isMapDragging)
        {
            UpdateUserIndicatorPosition();
            UpdateUserIndicatorRotation();
            UpdateDirectionShadow();
        }
        else
        {
            if (Time.time - lastUpdateTime < updateInterval)
                return;

            lastUpdateTime = Time.time;

            UpdateUserIndicatorPosition();
            UpdateUserIndicatorRotation();
            UpdateDirectionShadow();
        }
    }

    void UpdateUserIndicatorPosition()
    {
        Vector2 gpsCoords = GPSManager.Instance.GetSmoothedCoordinates();

        Vector3 localPos = mapboxMap.GeoToWorldPosition(new Vector2d(gpsCoords.x, gpsCoords.y), false);
        localPos.y = heightOffset;

        if (isMapDragging)
        {
            userIndicatorInstance.transform.localPosition = localPos;
            lastWorldPos = localPos;
        }
        else
        {
            Vector3 smoothedPos = Vector3.Lerp(lastWorldPos, localPos, positionSmoothness);
            userIndicatorInstance.transform.localPosition = smoothedPos;
            lastWorldPos = smoothedPos;
        }
    }

    void UpdateUserIndicatorRotation()
    {
        if (!GPSManager.Instance.IsCompassReady())
        {
            return;
        }

        float compassHeading = GPSManager.Instance.GetHeading();

        Quaternion targetRotation = Quaternion.Euler(0, compassHeading, 0);
        
        // Smooth rotation for nice feel
        userIndicatorInstance.transform.localRotation = Quaternion.Slerp(
            userIndicatorInstance.transform.localRotation,
            targetRotation,
            Time.deltaTime * rotationSmoothness
        );
        
        lastHeading = compassHeading;
    }

    void UpdateDirectionShadow()
    {
        if (shadowConeInstance == null || userIndicatorInstance == null) return;

        Vector3 shadowPos = userIndicatorInstance.transform.localPosition;
        shadowPos.y = heightOffset - 0.1f;
        shadowConeInstance.transform.localPosition = shadowPos;

        shadowConeInstance.transform.localRotation = userIndicatorInstance.transform.localRotation;

        Vector3 shadowScale = shadowConeInstance.transform.localScale;
        shadowScale.z = shadowDistance;
        shadowScale.x = shadowDistance * Mathf.Tan(shadowAngle * 0.5f * Mathf.Deg2Rad);
        shadowScale.y = shadowScale.x;
        shadowConeInstance.transform.localScale = shadowScale;
    }

    public void SetMapDragging(bool isDragging)
    {
        isMapDragging = isDragging;
    }

    public void UpdatePosition()
    {
        if (isInitialized)
        {
            UpdateUserIndicatorPosition();
        }
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

    public void SetUserIndicatorColor(Color newColor)
    {
        userIndicatorColor = newColor;
        if (isInitialized)
        {
            ApplyUserIndicatorAppearance();
        }
    }

    public void SetShadowColor(Color newColor)
    {
        shadowColor = newColor;
        if (isInitialized && shadowConeInstance != null)
        {
            ApplyShadowAppearance();
        }
    }

    public void RefreshAppearance()
    {
        if (isInitialized)
        {
            ApplyUserIndicatorAppearance();
            ApplyShadowAppearance();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (userIndicatorInstance != null && Application.isPlaying)
        {
            Gizmos.color = userIndicatorColor;
            Gizmos.DrawWireSphere(userIndicatorInstance.transform.position, 1f);

            Vector3 forward = userIndicatorInstance.transform.forward * 3f;
            Gizmos.DrawRay(userIndicatorInstance.transform.position, forward);
        }
    }
}