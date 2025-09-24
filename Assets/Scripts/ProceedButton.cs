using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ProceedButton : MonoBehaviour

{


    [SerializeField] private Button targetButton;   // assign in Inspector
    [SerializeField] private string sceneName;      // type your scene name
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

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
