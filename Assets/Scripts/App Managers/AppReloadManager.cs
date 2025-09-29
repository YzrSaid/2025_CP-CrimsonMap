using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AppReloadManager : MonoBehaviour
{
    [Header("UI References")]
    public ScrollToReload scrollToReload;

    [Header("Reload Settings")]
    public bool reinitializeData = true;
    public bool reloadCurrentScene = false;
    public float reloadDelay = 1f;

    void Start()
    {
        if (scrollToReload != null)
        {
            scrollToReload.OnReloadTriggered.AddListener(OnScrollReloadTriggered);
        }
    }

    void OnScrollReloadTriggered()
    {
        Debug.Log("🔄 AppReloadManager: Starting app reload...");
        StartCoroutine(ReloadApp());
    }

    IEnumerator ReloadApp()
    {
        yield return new WaitForSeconds(reloadDelay);

        if (reinitializeData)
        {
            Debug.Log("🔄 Re-initializing data systems...");
            yield return StartCoroutine(CallMainAppLoaderInitialization());
        }
        else if (reloadCurrentScene)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            Debug.Log($"🔄 Reloading scene: {currentScene}");
            SceneManager.LoadScene(currentScene);
        }

        if (scrollToReload != null)
        {
            scrollToReload.CompleteReload();
        }
    }

    IEnumerator CallMainAppLoaderInitialization()
    {
        MainAppLoader mainAppLoader = FindObjectOfType<MainAppLoader>();
        if (mainAppLoader != null)
        {
            if (mainAppLoader.mainAppUI != null)
                mainAppLoader.mainAppUI.SetActive(false);
                
            if (mainAppLoader.loadingPanel != null)
                mainAppLoader.loadingPanel.SetActive(true);

            mainAppLoader.ResetForReload();

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