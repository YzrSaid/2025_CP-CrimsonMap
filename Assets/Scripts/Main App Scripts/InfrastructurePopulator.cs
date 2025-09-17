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
        StartCoroutine(LoadInfrastructuresCoroutine());
    }

    private IEnumerator LoadInfrastructuresCoroutine()
    {
        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "infrastructure.json",
            OnInfrastructureDataLoaded,
            OnInfrastructureDataError
        ));
    }

    private void OnInfrastructureDataLoaded(string jsonContent)
    {
        try
        {
            // wrap it
            string wrappedJson = "{\"infrastructures\":" + jsonContent + "}";

            infrastructureList = JsonUtility.FromJson<InfrastructureList>(wrappedJson);

            if (infrastructureList == null || infrastructureList.infrastructures.Length == 0)
            {
                Debug.LogWarning("No infrastructure data loaded.");
                return;
            }

            // Populate dropdowns after successful loading
            PopulateDropdown(dropdownFrom);
            PopulateDropdown(dropdownTo);

            Debug.Log($"Successfully loaded {infrastructureList.infrastructures.Length} infrastructures");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing infrastructure JSON: {e.Message}");
        }
    }

    private void OnInfrastructureDataError(string errorMessage)
    {
        Debug.LogError($"Failed to load infrastructure data: {errorMessage}");
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
}