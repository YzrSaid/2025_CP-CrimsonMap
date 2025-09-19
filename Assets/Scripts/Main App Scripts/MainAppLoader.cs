using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class MainAppLoader : MonoBehaviour
{
    [Header("Loading UI")]
    public GameObject loadingPanel;
    public Image loadingBar;
    public TextMeshProUGUI loadingText;
    public TextMeshProUGUI progressText; // Add this for percentage display

    [Header("Main App UI")]
    public GameObject mainAppUI;

    [Header("Error Handling")]
    public GameObject errorContainer; // Reference to ErrorContainer GameObject
    public Button retryButton;
    public TextMeshProUGUI errorText;
    public float maxWaitTimeForGlobalManager = 10f;

    private bool isInitialized = false;
    private bool hasError = false;

    void Start()
    {
        // Show loading UI, hide main app UI and error container
        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (mainAppUI != null) mainAppUI.SetActive(false);
        if (errorContainer != null) errorContainer.SetActive(false);
        
        // Setup retry button
        if (retryButton != null)
        {
            retryButton.onClick.AddListener(RetryInitialization);
        }

        // Start initialization
        StartCoroutine(InitializeApp());
    }

    private IEnumerator InitializeApp()
    {
        hasError = false;
        
        // Hide error container at start of initialization
        if (errorContainer != null) errorContainer.SetActive(false);

        UpdateLoadingUI("Starting app...", 0.1f);
        yield return new WaitForSeconds(0.5f);

        // Wait for GlobalManager with timeout
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

        // Double-check that we're in the right scene (user completed onboarding)
        if (!GlobalManager.Instance.onboardingComplete)
        {
            Debug.LogWarning("User reached MainApp without completing onboarding - redirecting");
            SceneManager.LoadScene("OnboardingScreensScene");
            yield break;
        }

        // Initialize data systems
        UpdateLoadingUI("Setting up data systems...", 0.3f);
        yield return new WaitForSeconds(0.3f);

        bool dataInitComplete = false;

        // Subscribe to completion event
        System.Action onComplete = () => { dataInitComplete = true; };
        GlobalManager.Instance.OnDataInitializationComplete += onComplete;

        // Start the initialization
        try
        {
            UpdateLoadingUI("Creating managers...", 0.4f);
            GlobalManager.Instance.InitializeDataSystems();
        }
        catch (System.Exception ex)
        {
            GlobalManager.Instance.OnDataInitializationComplete -= onComplete;
            Debug.LogError($"Error during data initialization: {ex.Message}");
            ShowError("Failed to load map data. Check your internet connection.", false);
            yield break;
        }

        // Wait for completion with timeout and progress simulation
        float initWaitTime = 0f;
        float maxInitWaitTime = 30f;
        float lastProgress = 0.4f;

        while (!dataInitComplete && initWaitTime < maxInitWaitTime)
        {
            initWaitTime += Time.deltaTime;

            // Simulate progress based on time and phases
            float timeProgress = initWaitTime / maxInitWaitTime;
            float currentProgress = 0.4f + (timeProgress * 0.5f); // From 40% to 90%
            
            // Update loading message based on progress
            if (currentProgress < 0.5f && lastProgress < 0.5f)
            {
                UpdateLoadingUI("Initializing JSON files...", currentProgress);
            }
            else if (currentProgress < 0.6f && lastProgress < 0.6f)
            {
                UpdateLoadingUI("Connecting to Firebase...", currentProgress);
            }
            else if (currentProgress < 0.8f && lastProgress < 0.8f)
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

        // Unsubscribe from event
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

        // Hide loading UI and show main app
        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (mainAppUI != null) mainAppUI.SetActive(true);

        isInitialized = true;
        Debug.Log("MainApp fully loaded and ready!");
        
        // Optional: Log final system status
        if (GlobalManager.Instance != null)
        {
            Debug.Log(GlobalManager.Instance.GetSystemStatus());
        }
    }

    private void UpdateLoadingUI(string message, float progress)
    {
        // Update loading text
        if (loadingText != null) 
            loadingText.text = message;

        // Update progress bar
        if (loadingBar != null) 
            loadingBar.fillAmount = progress;

        // Update percentage text
        if (progressText != null)
            progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";

        Debug.Log($"Loading: {message} ({progress * 100:F0}%)");
    }

    private void ShowError(string message, bool showRestartMessage = false)
    {
        hasError = true;
        
        // Show error container
        if (errorContainer != null)
            errorContainer.SetActive(true);
        
        // Set error message
        if (errorText != null)
            errorText.text = message;
        
        // Hide/show retry button based on error type
        if (retryButton != null)
            retryButton.gameObject.SetActive(!showRestartMessage);

        // Update loading UI to show error state
        if (loadingBar != null) loadingBar.fillAmount = 0f;
        if (progressText != null) progressText.text = "0%";
        if (loadingText != null) 
            loadingText.text = showRestartMessage ? "Please restart the app" : "Tap retry to try again";

        Debug.LogError($"MainAppLoader Error: {message}");
    }

    // Public method to retry initialization
    public void RetryInitialization()
    {
        if (!hasError)
        {
            Debug.Log("Cannot retry - no error occurred or initialization in progress");
            return;
        }

        Debug.Log("Retrying initialization...");
        isInitialized = false;
        StopAllCoroutines();
        StartCoroutine(InitializeApp());
    }
    void OnDestroy()
    {
        // Clean up event subscriptions to prevent memory leaks
        if (GlobalManager.Instance != null && GlobalManager.Instance.OnDataInitializationComplete != null)
        {
            // Remove all listeners (this is a safer way)
            System.Delegate[] invocationList = GlobalManager.Instance.OnDataInitializationComplete.GetInvocationList();
            foreach (System.Action action in invocationList)
            {
                GlobalManager.Instance.OnDataInitializationComplete -= action;
            }
        }
    }
}