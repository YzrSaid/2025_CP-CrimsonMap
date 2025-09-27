using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AppReloadManager : MonoBehaviour
{
    [Header("UI References")]
    public ScrollToReload scrollToReload;

    [Header("Reload Settings")]
    public bool reinitializeData = true; // Re-run data initialization instead of scene reload
    public bool reloadCurrentScene = false; // Fallback: reload scene
    public float reloadDelay = 1f; // Delay before reload

    void Start()
    {
        // Subscribe to the scroll-to-reload event
        if (scrollToReload != null)
        {
            scrollToReload.OnReloadTriggered.AddListener(OnScrollReloadTriggered);
        }
    }

    // Called when user triggers scroll-to-reload
    void OnScrollReloadTriggered()
    {
        Debug.Log("🔄 AppReloadManager: Starting app reload...");
        StartCoroutine(ReloadApp());
    }

    IEnumerator ReloadApp()
    {
        // Optional: Add a small delay to show loading state
        yield return new WaitForSeconds(reloadDelay);

        if (reinitializeData)
        {
            // Re-run the data initialization process
            Debug.Log("🔄 Re-initializing data systems...");
            yield return StartCoroutine(CallMainAppLoaderInitialization());
        }
        else if (reloadCurrentScene)
        {
            // Fallback: reload scene
            string currentScene = SceneManager.GetActiveScene().name;
            Debug.Log($"🔄 Reloading scene: {currentScene}");
            SceneManager.LoadScene(currentScene);
        }

        // Complete the scroll-to-reload animation
        if (scrollToReload != null)
        {
            scrollToReload.CompleteReload();
        }
    }

    IEnumerator CallMainAppLoaderInitialization()
    {
        // Reset and restart the initialization
        MainAppLoader mainAppLoader = FindObjectOfType<MainAppLoader>();
        if (mainAppLoader != null)
        {
            // Hide main app UI and show loading
            if (mainAppLoader.mainAppUI != null)
                mainAppLoader.mainAppUI.SetActive(false);
                
            if (mainAppLoader.loadingPanel != null)
                mainAppLoader.loadingPanel.SetActive(true);

            mainAppLoader.ResetForReload();

            // Call the public InitializeApp method
            yield return StartCoroutine(mainAppLoader.InitializeApp());
        }
        else
        {
            Debug.LogError("MainAppLoader instance not found in the scene.");
        }
    }

    void OnDestroy()
    {
        if (scrollToReload != null)
        {
            scrollToReload.OnReloadTriggered.RemoveListener(OnScrollReloadTriggered);
        }
    }
}