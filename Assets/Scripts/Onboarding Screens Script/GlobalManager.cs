using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Android;

public class GlobalManager : MonoBehaviour
{
    public static GlobalManager Instance { get; private set; }

    // Global Variables
    public bool onboardingComplete = false;

    private string savePath;

    void Start()
    {
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);
        }
    }

    void Awake()
    {
        // Singleton pattern to ensure only one instance exists
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);

            savePath = Application.persistentDataPath + "/saveData.json";
            LoadData();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void LoadData()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            this.onboardingComplete = data.onboardingComplete;
        }
        else
        {
            Debug.LogError("Save file not found at: " + savePath);
            return;
        }
    }

    public void SaveData()
    {
        SaveData data = new SaveData();
        data.onboardingComplete = this.onboardingComplete;

        // Convert SaveData to JSON and write to file
        if (!Directory.Exists(Application.persistentDataPath))
        {
            string json = JsonUtility.ToJson(data);
            File.WriteAllText(savePath, json);
        }
        else
        {
            Debug.LogError("Save path does not exist: " + savePath);
        }
    }
}
