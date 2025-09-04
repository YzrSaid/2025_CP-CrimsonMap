using UnityEngine;
using UnityEngine.SceneManagement;

public class LauncherScene : MonoBehaviour
{
    void Start()
    {
        // Wait for GlobalManager to be initialized and load data
        if (GlobalManager.Instance == null)
        {
            // If GlobalManager doesn't exist yet, wait a frame
            StartCoroutine(WaitAndLoad());
        }
        else
        {
            LoadAppropriateScene();
        }
    }
    
    System.Collections.IEnumerator WaitAndLoad()
    {
        yield return null; // Wait one frame for GlobalManager to initialize
        LoadAppropriateScene();
    }
    
    void LoadAppropriateScene()
    {
        if (GlobalManager.Instance.onboardingComplete)
        {
            SceneManager.LoadScene("MainAppScene");
        }
        else
        {
            SceneManager.LoadScene("OnboardingScreensScene");
        }
    }
}