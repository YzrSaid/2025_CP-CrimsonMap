using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

public class SceneTransitionWithoutLoading : MonoBehaviour
{
    public static SceneTransitionWithoutLoading Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Go to scene WITH loading (but no visual loading screen)
    public void GoToTargetScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
    public void GoToTargetSceneSimple(string sceneName)
    {
        Debug.Log("AR Back Button clicked - returning to MainApp");

        // Run the cleanup from GlobalManager, not from the current GameObject
        if (GlobalManager.Instance != null)
        {
            GlobalManager.Instance.StartCoroutine(GlobalManager.Instance.SafeARCleanupAndExit(sceneName));
        }
        else
        {
            // Fallback
            SceneManager.LoadScene(sceneName);
        }
    }

    // Restart current scene
    public void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Quit application
    public void QuitGame()
    {
        Application.Quit();
    }
}