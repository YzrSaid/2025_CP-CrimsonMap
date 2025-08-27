using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class InfrastructurePopulator : MonoBehaviour
{
    [Header("Dropdowns")]
    public TMP_Dropdown dropdownFrom;
    public TMP_Dropdown dropdownTo;

    [Header("Data")]
    public InfrastructureList infrastructureList;

    void Start()
    {
        LoadInfrastructures();
        PopulateDropdown(dropdownFrom);
        PopulateDropdown(dropdownTo);
    }

    private void PopulateDropdown(TMP_Dropdown dropdown)
    {
        // Clear first the dropdown
        dropdown.ClearOptions();

        if (infrastructureList == null || infrastructureList.infrastructures.Length == 0)
        {
            return;
        }

        List<string> options = new List<string>();
        foreach (var i in infrastructureList.infrastructures)
        {
            options.Add(i.name);
        }

        dropdown.AddOptions(options);
    }

    // Get the selected building object
    public Infrastructure GetSelectedInfrastructure(TMP_Dropdown dropdown)
    {
        int index = dropdown.value;
        if (index >= 0 && index < infrastructureList.infrastructures.Length)
        {
            return infrastructureList.infrastructures[index];
        }
        return null;
    }

    private void LoadInfrastructures()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "infrastructures.json");

        if (!File.Exists(path))
        {
            Debug.LogError("infrastructures.json not found at " + path);
        }
        else
        {
            string json = File.ReadAllText(path);

            // wrap it
            string wrappedJson = "{\"infrastructures\":" + json + "}";

            infrastructureList = JsonUtility.FromJson<InfrastructureList>(wrappedJson);

            if (infrastructureList == null || infrastructureList.infrastructures.Length == 0)
            {
                 Debug.LogWarning("No infrastructure data loaded.");
            }
        }
    }
}
