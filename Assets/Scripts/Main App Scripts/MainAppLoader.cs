using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainAppLoader : MonoBehaviour
{
    [Header("Loading UI")]
    public GameObject loadingPanel;
    public Image loadingBar;
    public TextMeshProUGUI loadingText;
    public TextMeshProUGUI statusText;

    [Header("Main App UI")]
    public GameObject mainAppUI;

    private bool isInitialized = false;

    void Start()
    {
        // Show loading UI, hide main app UI
        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (mainAppUI != null) mainAppUI.SetActive(false);

        // Start initialization
        StartCoroutine(InitializeApp());
    }

    private IEnumerator InitializeApp()
    {
        UpdateLoadingUI("Starting app...", 0.1f);
        yield return new WaitForSeconds(0.5f);

        // Wait for GlobalManager to be available
        UpdateLoadingUI("Connecting to services...", 0.2f);
        yield return new WaitUntil(() => GlobalManager.Instance != null);

        // Initialize data systems
        UpdateLoadingUI("Loading map data...", 0.4f);
        bool dataInitComplete = false;

        // Subscribe to completion event
        GlobalManager.Instance.OnDataInitializationComplete += () => {
            dataInitComplete = true;
        };

        // Start the initialization
        GlobalManager.Instance.InitializeDataSystems();

        // Wait for completion
        yield return new WaitUntil(() => dataInitComplete);

        UpdateLoadingUI("Finalizing...", 0.9f);
        yield return new WaitForSeconds(0.5f);

        UpdateLoadingUI("Ready!", 1.0f);
        yield return new WaitForSeconds(0.3f);

        // Hide loading UI and show main app
        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (mainAppUI != null) mainAppUI.SetActive(true);

        isInitialized = true;
        Debug.Log("MainApp fully loaded and ready!");
    }

    private void UpdateLoadingUI(string message, float progress)
    {
        if (loadingText != null) loadingText.text = message;
        if (loadingBar != null) loadingBar.fillAmount = progress;

        // Optional: Show system status
        if (statusText != null && GlobalManager.Instance != null)
        {
            statusText.text = $"Maps: {GlobalManager.Instance.GetAvailableMaps().Count}\n" +
                             $"Data Ready: {GlobalManager.Instance.isDataInitialized}";
        }

        Debug.Log($"Loading: {message} ({progress * 100:F0}%)");
    }

    // Optional: Add a method to retry initialization if it fails
    public void RetryInitialization()
    {
        if (!isInitialized)
        {
            Debug.Log("Retrying initialization...");
            StartCoroutine(InitializeApp());
        }
    }

    // Optional: Show detailed status for debugging
    public void ShowDetailedStatus()
    {
        if (GlobalManager.Instance != null)
        {
            string status = GlobalManager.Instance.GetSystemStatus();
            Debug.Log(status);
            
            if (statusText != null)
            {
                statusText.text = status;
            }
        }
    }
}