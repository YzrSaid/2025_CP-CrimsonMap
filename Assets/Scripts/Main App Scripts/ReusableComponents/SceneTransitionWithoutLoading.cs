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
    public static void GoToTargetSceneSimple(string sceneName)
    {
        GlobalManager.SetSkipFullInitialization(true);
        // Check if its in Navigation Mode (AR MODE) if yes, then save and delete the route data)
        if (GlobalManager.Instance != null && ARModeHelper.IsNavigationMode())
        {
            // Save and delete the navigation data
            ARNavigationDataHelper.SaveAndClearARNavigationData();
            // Delete the highlights in the map
            if (ARMapManager.Instance != null)
            {
                ARMapManager.Instance.ClearNavigationHighlights();
            }
            GlobalManager.Instance.StartCoroutine(GlobalManager.Instance.SafeARCleanupAndExit(sceneName));
        }
        else if (GlobalManager.Instance != null && ARModeHelper.IsDirectARMode())
        {
            GlobalManager.Instance.StartCoroutine(GlobalManager.Instance.SafeARCleanupAndExit(sceneName));
        }
    }
}