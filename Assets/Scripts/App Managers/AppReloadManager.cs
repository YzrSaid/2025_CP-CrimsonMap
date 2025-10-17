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
        StartCoroutine(ReloadApp());
    }

    IEnumerator ReloadApp()
    {
        yield return new WaitForSeconds(reloadDelay);

        if (reinitializeData)
        {
            yield return StartCoroutine(CallMainAppLoaderInitialization());
        }
        else if (reloadCurrentScene)
        {
            string currentScene = SceneManager.GetActiveScene().name;
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
    }

    void OnDestroy()
    {
        if (scrollToReload != null)
        {
            scrollToReload.OnReloadTriggered.RemoveListener(OnScrollReloadTriggered);
        }
    }
}