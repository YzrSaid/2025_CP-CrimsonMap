using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class ARManagerCleanup : MonoBehaviour
{
    [SerializeField] private Button arButton;
    [SerializeField] private string arSceneName = "ARScene";
    
    // Store manager states before destroying them
    private static bool hadJSONManager = false;
    private static bool hadFirestoreManager = false;
    private static bool hadMapboxManager = false;
    
    private void Awake() 
    {
        if (arButton != null) 
        {
            arButton.onClick.AddListener(LoadARWithCleanup);
        }
    }

    private void LoadARWithCleanup()
    {
        Debug.Log("Loading AR scene with manager cleanup...");
        StartCoroutine(CleanupAndLoadAR());
    }
    
    private IEnumerator CleanupAndLoadAR()
    {
        // Record which managers existed before cleanup
        RecordManagerStates();
        
        // Destroy non-essential managers
        DestroyNonEssentialManagers();
        
        yield return new WaitForEndOfFrame();
        
        // Load AR scene
        Debug.Log($"Loading clean AR scene: {arSceneName}");
        SceneManager.LoadScene(arSceneName, LoadSceneMode.Single);
    }
    
    private void RecordManagerStates()
    {
        hadJSONManager = JSONFileManager.Instance != null;
        hadFirestoreManager = FirestoreManager.Instance != null;
        hadMapboxManager = FindObjectOfType<MapboxOfflineManager>() != null;
        
        Debug.Log($"Recorded manager states - JSON: {hadJSONManager}, Firestore: {hadFirestoreManager}, Mapbox: {hadMapboxManager}");
    }
    
    private void DestroyNonEssentialManagers()
    {
        // Destroy MainAppLoader if it exists
        MainAppLoader mainAppLoader = FindObjectOfType<MainAppLoader>();
        if (mainAppLoader != null)
        {
            Debug.Log("Destroying MainAppLoader for AR");
            Destroy(mainAppLoader.gameObject);
        }
    
        if (GlobalManager.Instance != null)
        {
            Debug.Log("Preserving GlobalManager for AR");
            GlobalManager.Instance.isInARMode = true;
        }
    }
    
    // Static methods to get manager states (called from other scenes)
    public static bool ShouldRecreateJSONManager() 
    { 
        return hadJSONManager; 
    }
    
    public static bool ShouldRecreateFirestoreManager() 
    { 
        return hadFirestoreManager; 
    }
    
    public static bool ShouldRecreateMapboxManager() 
    { 
        return hadMapboxManager; 
    }
    
    // Reset the states when managers are successfully recreated
    public static void ResetManagerStates()
    {
        hadJSONManager = false;
        hadFirestoreManager = false;
        hadMapboxManager = false;
        Debug.Log("Manager states reset");
    }
}