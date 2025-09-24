using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ButtonSceneLoader : MonoBehaviour
{
    [SerializeField] private Button targetButton;   // assign in Inspector
    [SerializeField] private string sceneName;      // type your scene name

    private void Awake()
    {
        if (targetButton != null)
        {
            targetButton.onClick.AddListener(LoadScene);
        }
        else
        {
            Debug.LogError("❌ ButtonSceneLoader: No button assigned in Inspector!");
        }
    }

    private void LoadScene()
    {
        Debug.Log($"Loading scene: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }
}
