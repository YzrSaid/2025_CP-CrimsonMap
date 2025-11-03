using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class MainAppLoader : MonoBehaviour
{
    [Header("Mapbox Offline")]
    public MapboxOfflineManager mapboxOffline;

    [Header("Loading UI")]
    public GameObject loadingPanel;
    public Image loadingBar;
    public TextMeshProUGUI loadingText;
    public TextMeshProUGUI progressText;

    [Header("Main App UI")]
    public GameObject mainAppUI;

    [Header("Error Handling")]
    public GameObject errorContainer;
    public Button retryButton;
    public TextMeshProUGUI errorText;
    public float maxWaitTimeForGlobalManager = 10f;

    public bool isInitialized = false;
    public bool hasError = false;

    void Start()
    {
        bool skipFullInitialization = GlobalManager.ShouldSkipFullInitialization();
        
        if (skipFullInitialization)
        {
            if (loadingPanel != null) loadingPanel.SetActive(false);
            if (mainAppUI != null) mainAppUI.SetActive(true);
            if (errorContainer != null) errorContainer.SetActive(false);
            
            isInitialized = true;
            return;
        }

        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (mainAppUI != null) mainAppUI.SetActive(false);
        if (errorContainer != null) errorContainer.SetActive(false);

        if (retryButton != null)
        {
            retryButton.onClick.AddListener(RetryInitialization);
        }

        StartCoroutine(InitializeApp());
    }

    public IEnumerator InitializeApp()
    {
        hasError = false;
        if (errorContainer != null) errorContainer.SetActive(false);

        UpdateLoadingUI("Starting app...", 0.1f);
        yield return new WaitForSeconds(0.5f);

        UpdateLoadingUI("Waiting for system...", 0.2f);

        float waitTime = 0f;
        while (GlobalManager.Instance == null && waitTime < maxWaitTimeForGlobalManager)
        {
            waitTime += Time.deltaTime;
            yield return null;
        }

        if (GlobalManager.Instance == null)
        {
            ShowError("Failed to initialize system. Please restart the app.", true);
            yield break;
        }
        if (!GlobalManager.Instance.onboardingComplete)
        {
            SceneManager.LoadScene("OnboardingScreensScene");
            yield break;
        }

        UpdateLoadingUI("Checking map system...", 0.25f);
        yield return new WaitForSeconds(0.2f);

        if (mapboxOffline != null)
        {
            UpdateLoadingUI("Map system ready!", 0.35f);
        }
        else
        {
            UpdateLoadingUI("Map ready...", 0.35f);
        }
        
        yield return new WaitForSeconds(0.3f);

        UpdateLoadingUI("Setting up data systems...", 0.4f);
        yield return new WaitForSeconds(0.3f);

        bool dataInitComplete = false;

        System.Action onComplete = () => { dataInitComplete = true; };
        GlobalManager.Instance.OnDataInitializationComplete += onComplete;

        try
        {
            UpdateLoadingUI("Creating managers...", 0.5f);
            GlobalManager.Instance.InitializeDataSystems();
        }
        catch (System.Exception)
        {
            GlobalManager.Instance.OnDataInitializationComplete -= onComplete;
            ShowError("Failed to load map data. Check your internet connection.", false);
            yield break;
        }

        float initWaitTime = 0f;
        float maxInitWaitTime = 30f;
        float lastProgress = 0.5f;

        while (!dataInitComplete && initWaitTime < maxInitWaitTime)
        {
            initWaitTime += Time.deltaTime;

            float timeProgress = initWaitTime / maxInitWaitTime;
            float currentProgress = 0.5f + (timeProgress * 0.4f);

            if (currentProgress < 0.6f && lastProgress < 0.6f)
            {
                UpdateLoadingUI("Initializing app files...", currentProgress);
            }
            else if (currentProgress < 0.7f && lastProgress < 0.7f)
            {
                UpdateLoadingUI("Connecting to Firebase...", currentProgress);
            }
            else if (currentProgress < 0.85f && lastProgress < 0.85f)
            {
                UpdateLoadingUI("Syncing map data...", currentProgress);
            }
            else
            {
                UpdateLoadingUI("Loading map data...", currentProgress);
            }

            lastProgress = currentProgress;
            yield return new WaitForSeconds(0.1f);
        }

        GlobalManager.Instance.OnDataInitializationComplete -= onComplete;

        if (!dataInitComplete)
        {
            ShowError("Data initialization timed out. Check your internet connection.", false);
            yield break;
        }

        UpdateLoadingUI("Finalizing...", 0.95f);
        yield return new WaitForSeconds(0.5f);

        UpdateLoadingUI("Ready!", 1.0f);
        yield return new WaitForSeconds(0.5f);

        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (mainAppUI != null) mainAppUI.SetActive(true);

        isInitialized = true;
    }

    private void UpdateLoadingUI(string message, float progress)
    {
        if (loadingText != null)
            loadingText.text = message;

        if (loadingBar != null)
            loadingBar.fillAmount = progress;

        if (progressText != null)
            progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
    }

    private void ShowError(string message, bool showRestartMessage = false)
    {
        hasError = true;

        if (errorContainer != null)
            errorContainer.SetActive(true);

        if (errorText != null)
            errorText.text = message;

        if (retryButton != null)
            retryButton.gameObject.SetActive(!showRestartMessage);

        if (loadingBar != null) loadingBar.gameObject.SetActive(false);
        if (progressText != null) progressText.gameObject.SetActive(false);
        if (loadingText != null)
            loadingText.text = showRestartMessage ? "Please restart the app" : "Tap retry to try again";
    }

    public void RetryInitialization()
    {
        if (!hasError)
        {
            return;
        }

        isInitialized = false;
        StopAllCoroutines();
        StartCoroutine(InitializeApp());
    }

    void OnDestroy()
    {
        if (GlobalManager.Instance != null && GlobalManager.Instance.OnDataInitializationComplete != null)
        {
            System.Delegate[] invocationList = GlobalManager.Instance.OnDataInitializationComplete.GetInvocationList();
            foreach (System.Action action in invocationList)
            {
                GlobalManager.Instance.OnDataInitializationComplete -= action;
            }
        }
    }

    public void ResetForReload()
    {
        isInitialized = false;
        hasError = false;
        StopAllCoroutines();
    }
}