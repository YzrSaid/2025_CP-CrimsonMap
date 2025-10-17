using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class ARManagerCleanup : MonoBehaviour
{
    [SerializeField] private Button arButton;
    [SerializeField] private string arSceneName = "ARScene";

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
        StartCoroutine(CleanupAndLoadAR());
    }
    
    private IEnumerator CleanupAndLoadAR()
    {
        RecordManagerStates();
        
        DestroyNonEssentialManagers();
        
        yield return new WaitForEndOfFrame();
        
        SceneManager.LoadScene(arSceneName, LoadSceneMode.Single);
    }
    
    private void RecordManagerStates()
    {
        hadJSONManager = JSONFileManager.Instance != null;
        hadFirestoreManager = FirestoreManager.Instance != null;
        hadMapboxManager = FindObjectOfType<MapboxOfflineManager>() != null;
    }
    
    private void DestroyNonEssentialManagers()
    {
        MainAppLoader mainAppLoader = FindObjectOfType<MainAppLoader>();
        if (mainAppLoader != null)
        {          
            Destroy(mainAppLoader.gameObject);
        }
    
        if (GlobalManager.Instance != null)
        {
            GlobalManager.Instance.isInARMode = true;
        }
    }
    
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
    
    public static void ResetManagerStates()
    {
        hadJSONManager = false;
        hadFirestoreManager = false;
        hadMapboxManager = false;
    }
}