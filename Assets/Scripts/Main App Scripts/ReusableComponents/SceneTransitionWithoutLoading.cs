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
    public void GoToTargetSceneSimple(string sceneName)
    {
        GlobalManager.SetSkipFullInitialization(true);
    
        if (GlobalManager.Instance != null)
        {
            GlobalManager.Instance.StartCoroutine(GlobalManager.Instance.SafeARCleanupAndExit(sceneName));
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}