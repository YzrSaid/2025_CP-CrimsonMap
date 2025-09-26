using UnityEngine;
using UnityEngine.SceneManagement;

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
    
    // Simple scene transition (same as above, just for consistency)
    public void GoToTargetSceneSimple(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
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