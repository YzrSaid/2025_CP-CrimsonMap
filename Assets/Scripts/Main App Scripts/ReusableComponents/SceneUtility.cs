using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

public class SceneUtility : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
    
    void OnEnable()
    {
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }
    
    void OnSceneUnloaded(Scene current)
    {
        if (IsARScene(current.name))
        {      
            var xrManagerSettings = XRGeneralSettings.Instance?.Manager;
            if (xrManagerSettings != null)
            {
                if (xrManagerSettings.isInitializationComplete)
                {
                    xrManagerSettings.DeinitializeLoader();
                }
                
                xrManagerSettings.InitializeLoaderSync();
            }
        }
    }
    
    private bool IsARScene(string sceneName)
    {
        string[] arScenes = { "ARScene"};
        return System.Array.Exists(arScenes, scene =>
            sceneName.Equals(scene, System.StringComparison.OrdinalIgnoreCase));
    }
    
    void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }
}