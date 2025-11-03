using UnityEngine;
using TMPro;
using System.Collections;

public class ARLoadingManager : MonoBehaviour
{
    [Header("Loading Panel")]
    public GameObject loadingPanel;
    public TextMeshProUGUI loadingText;
    public GameObject loadingAnimation;

    [Header("References")]
    public UnifiedARManager unifiedARManager;
    public ARMapManager arMapManager;
    public ARCameraLayerManager cameraLayerManager;
    public UnifiedARNavigationMarkerSpawner navigationMarkerSpawner;

    private bool isARReady = false;
    private bool isMapReady = false;
    private bool isCameraSetupReady = false;
    private bool isNavigationReady = false;
    private bool isARModeNavigation = false;

    void Awake()
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        if (loadingAnimation != null)
            loadingAnimation.SetActive(true);

        string arMode = PlayerPrefs.GetString("ARMode", "DirectAR");
        isARModeNavigation = (arMode == "Navigation");

        FindReferences();
    }

    void Start()
    {
        StartCoroutine(WaitForAllSystems());
    }

    private void FindReferences()
    {
        if (unifiedARManager == null)
            unifiedARManager = FindObjectOfType<UnifiedARManager>();

        if (arMapManager == null)
            arMapManager = FindObjectOfType<ARMapManager>();

        if (cameraLayerManager == null)
            cameraLayerManager = FindObjectOfType<ARCameraLayerManager>();

        if (navigationMarkerSpawner == null)
            navigationMarkerSpawner = FindObjectOfType<UnifiedARNavigationMarkerSpawner>();
    }

    private IEnumerator WaitForAllSystems()
    {
        UpdateLoadingText("Initializing AR...");
        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(WaitForARManager());

        yield return StartCoroutine(WaitForMapSpawning());

        yield return StartCoroutine(WaitForCameraSetup());

        if (isARModeNavigation)
        {
            yield return StartCoroutine(WaitForNavigationMarkers());
        }

        yield return new WaitForSeconds(0.5f);

        yield return new WaitForSeconds(2f);

        HideLoadingPanel();
    }

    private IEnumerator WaitForARManager()
    {
        float timeout = 10f;
        float elapsed = 0f;

        while (!isARReady && elapsed < timeout)
        {
            if (unifiedARManager != null && unifiedARManager.isActiveAndEnabled)
            {
                isARReady = true;
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!isARReady)
        {
            isARReady = true;
        }
    }

    private IEnumerator WaitForMapSpawning()
    {
        float timeout = 15f;
        float elapsed = 0f;

        while (!isMapReady && elapsed < timeout)
        {
            if (arMapManager != null && arMapManager.IsSpawningComplete())
            {
                isMapReady = true;
                break;
            }

            elapsed += Time.deltaTime;
            yield return new WaitForSeconds(0.2f);
        }

        if (!isMapReady)
        {
            isMapReady = true;
        }

        yield return new WaitForSeconds(1f);
    }

    private IEnumerator WaitForCameraSetup()
    {
        yield return new WaitForSeconds(1f);
        isCameraSetupReady = true;
    }

    private IEnumerator WaitForNavigationMarkers()
    {
        float timeout = 5f;
        float elapsed = 0f;

        while (!isNavigationReady && elapsed < timeout)
        {
            if (navigationMarkerSpawner != null && navigationMarkerSpawner.isActiveAndEnabled)
            {
                isNavigationReady = true;
                break;
            }

            elapsed += Time.deltaTime;
            yield return new WaitForSeconds(0.2f);
        }

        if (!isNavigationReady)
        {
            isNavigationReady = true;
        }
    }

    private void UpdateLoadingText(string message)
    {
        if (loadingText != null)
        {
            loadingText.text = message;
        }
    }

    private void HideLoadingPanel()
    {
        if (loadingAnimation != null)
            loadingAnimation.SetActive(false);

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
    }

    public void ShowLoadingPanel(string message = "Loading...")
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            UpdateLoadingText(message);

            if (loadingAnimation != null)
                loadingAnimation.SetActive(true);
        }
    }

    public bool IsLoadingComplete()
    {
        return isARReady && isMapReady && isCameraSetupReady && 
               (!isARModeNavigation || isNavigationReady);
    }
}