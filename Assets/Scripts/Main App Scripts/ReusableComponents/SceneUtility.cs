using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

public class SceneUtility : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Debug.Log("SceneUtility: XR scene management initialized");
    }
    
    void OnEnable()
    {
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }
    
    void OnSceneUnloaded(Scene current)
    {
        Debug.Log($"SceneUtility: Scene unloaded - {current.name}");
        
        // Check if we're unloading an AR scene
        if (IsARScene(current.name))
        {
            Debug.Log("SceneUtility: AR scene detected - reinitializing XR subsystems");
            
            // Deinitialize and reinitialize XR subsystems to prevent stale references
            var xrManagerSettings = XRGeneralSettings.Instance?.Manager;
            if (xrManagerSettings != null)
            {
                if (xrManagerSettings.isInitializationComplete)
                {
                    xrManagerSettings.DeinitializeLoader();
                    Debug.Log("SceneUtility: XR Loader deinitialized");
                }
                
                xrManagerSettings.InitializeLoaderSync();
                Debug.Log("SceneUtility: XR Loader reinitialized");
            }
        }
    }
    
    private bool IsARScene(string sceneName)
    {
        // Add your AR scene names here
        string[] arScenes = { "ARScene", "AR Scene" };
        return System.Array.Exists(arScenes, scene =>
            sceneName.Equals(scene, System.StringComparison.OrdinalIgnoreCase));
    }
    
    void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }
    
    void OnDestroy()
    {
        Debug.Log("SceneUtility: XR scene management destroyed");
    }
}