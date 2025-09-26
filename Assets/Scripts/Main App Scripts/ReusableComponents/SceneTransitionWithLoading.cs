using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransitionWithLoading : MonoBehaviour
{
    [Header("Loading Screen")]
    public GameObject loadingScreen;
    public Slider loadingBar;

    public static SceneTransitionWithLoading Instance;

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
    
    // Go to scene WITH loading bar
    public void GoToTargetScene(string sceneName)
    {
        StartCoroutine(LoadSceneAsyncCoroutine(sceneName));
    }
    
    // Simple scene transition WITHOUT loading bar
    public void GoToTargetSceneSimple(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
    
    // Load scene asynchronously with loading screen
    private System.Collections.IEnumerator LoadSceneAsyncCoroutine(string sceneName)
    {
        // Show loading screen if available
        if (loadingScreen != null)
            loadingScreen.SetActive(true);
        
        // Start loading the scene
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;
        
        // Update loading bar
        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            
            if (loadingBar != null)
                loadingBar.value = progress;
            
            // Scene is ready to activate
            if (operation.progress >= 0.9f)
            {
                // Optional: wait for user input or timer
                yield return new WaitForSeconds(0.5f);
                operation.allowSceneActivation = true;
            }
            
            yield return null;
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