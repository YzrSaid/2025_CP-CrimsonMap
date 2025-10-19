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
    [Header( "Dropdowns" )]
    public TMP_Dropdown dropdownTo;

    [Header( "Data" )]
    public InfrastructureList infrastructureList;

    [Header( "Loading Check" )]
    public float maxWaitTime = 30f;

    void Start()
    {
        StartCoroutine( WaitForDataInitializationThenLoad() );
    }

    private IEnumerator WaitForDataInitializationThenLoad()
    {

        float waitTime = 0f;
        while ( waitTime < maxWaitTime ) {
            if ( GlobalManager.Instance != null && IsDataInitializationComplete() ) {
                yield return StartCoroutine( LoadInfrastructuresCoroutine() );
                yield break;
            }

            waitTime += Time.deltaTime;
            yield return new WaitForSeconds( 0.1f );
        }

        yield return StartCoroutine( LoadInfrastructuresCoroutine() );
    }

    private bool IsDataInitializationComplete()
    {
        string filePath = GetJsonFilePath( "infrastructure.json" );

        if ( !File.Exists( filePath ) ) {
            return false;
        }

        try {
            string content = File.ReadAllText( filePath );
            if ( string.IsNullOrEmpty( content ) || content.Length < 10 ) {
                return false;
            }
        } catch {
            return false;
        }

        return true;
    }
    private string GetJsonFilePath( string fileName )
    {
#if UNITY_EDITOR
        string streamingPath = Path.Combine( Application.streamingAssetsPath, fileName );
        if ( File.Exists( streamingPath ) ) {
            return streamingPath;
        }
#endif
        return Path.Combine( Application.persistentDataPath, fileName );
    }

    private IEnumerator LoadInfrastructuresCoroutine()
    {
        yield return StartCoroutine( CrossPlatformFileLoader.LoadJsonFile(
                                         "infrastructure.json",
                                         OnInfrastructureDataLoaded,
                                         OnInfrastructureDataError
                                     ) );
    }

    private void OnInfrastructureDataLoaded( string jsonContent )
    {
        try {
            string wrappedJson = "{\"infrastructures\":" + jsonContent + "}";

            infrastructureList = JsonUtility.FromJson<InfrastructureList>( wrappedJson );

            PopulateDropdown( dropdownTo );

        } catch ( Exception e ) {
            Debug.LogError( $"Error parsing infrastructure JSON: {e.Message}" );
        }
    }

    private void OnInfrastructureDataError( string errorMessage )
    {
        Debug.LogError( $"Failed to load infrastructure data: {errorMessage}" );
    }

    private void PopulateDropdown( TMP_Dropdown dropdown )
    {
        dropdown.ClearOptions();

        if ( infrastructureList == null || infrastructureList.infrastructures.Length == 0 ) {
            return;
        }

        List<string> options = new List<string>();
        foreach ( var i in infrastructureList.infrastructures ) {
            options.Add( i.name );
        }

        dropdown.AddOptions( options );
    }

    public Infrastructure GetSelectedInfrastructure( TMP_Dropdown dropdown )
    {
        int index = dropdown.value;
        if ( index >= 0 && index < infrastructureList.infrastructures.Length ) {
            return infrastructureList.infrastructures[index];
        }
        return null;
    }
}