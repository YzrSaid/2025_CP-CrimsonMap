using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Collections;

public class AccordionSpawner : MonoBehaviour
{
    public GameObject accordionItemPrefab;
    public Transform accordionContainer;
    public AccordionManager manager;

    [Header( "Loading Check" )]
    public float maxWaitTime = 30f;

    private List<string> staticCategories = new List<string> { "Saved", "Recent" };

    void Start()
    {
        foreach ( string name in staticCategories ) {
            SpawnAccordionItem( name, null );
        }

        StartCoroutine( WaitForDataInitializationThenLoad() );
    }

    private IEnumerator WaitForDataInitializationThenLoad()
    {
        float waitTime = 0f;

        while ( waitTime < maxWaitTime ) {
            if ( GlobalManager.Instance != null && IsDataInitializationComplete() ) {
                yield return StartCoroutine( LoadDynamicCategoriesFromFirebase() );
                yield break;
            }

            waitTime += Time.deltaTime;
            yield return new WaitForSeconds( 0.1f );
        }

        yield return StartCoroutine( LoadDynamicCategoriesFromFirebase() );
    }

    private bool IsDataInitializationComplete()
    {
        string filePath = GetJsonFilePath( "categories.json" );

        if ( !File.Exists( filePath ) ) {
            return false;
        }

        try {
            string content = File.ReadAllText( filePath );
            if ( string.IsNullOrEmpty( content ) || content.Length < 10 ) {
                return false;
            }

            string wrappedJson = "{\"categories\":" + content + "}";
            CategoryList testList = JsonUtility.FromJson<CategoryList>( wrappedJson );

            if ( testList == null || testList.categories == null || testList.categories.Count == 0 ) {
                return false;
            }

            return true;
        } catch (Exception)
        {
            return false;
        }
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

    IEnumerator LoadDynamicCategoriesFromFirebase()
    {
        yield return StartCoroutine( CrossPlatformFileLoader.LoadJsonFile(
                                         "categories.json",
                                         OnCategoriesLoadSuccess,
                                         OnCategoriesLoadError
                                     ) );
    }

    void OnCategoriesLoadSuccess( string jsonData )
    {
        try {
            string wrappedJson = "{\"categories\":" + jsonData + "}";
            CategoryList categoryList = JsonUtility.FromJson<CategoryList>( wrappedJson );

            foreach ( Category cat in categoryList.categories ) {
                SpawnAccordionItem( cat.name, cat.category_id );
            }
        } catch (Exception)
        {
        }
    }

    void OnCategoriesLoadError( string errorMessage )
    {
    }

    [Header( "Prefab References" )]
    public GameObject infrastructurePrefab;

    void SpawnAccordionItem( string categoryName, string categoryId )
    {
        GameObject newItem = Instantiate( accordionItemPrefab, accordionContainer );
        AccordionItem item = newItem.GetComponent<AccordionItem>();

        if ( item == null ) {
            Destroy( newItem );
            return;
        }

        RectTransform itemRect = newItem.GetComponent<RectTransform>();
        if ( itemRect != null ) {
            itemRect.anchorMin = new Vector2( 0, 1 );
            itemRect.anchorMax = new Vector2( 1, 1 );
            itemRect.pivot = new Vector2( 0.5f, 1 );
            itemRect.offsetMin = new Vector2( 0, itemRect.offsetMin.y );
            itemRect.offsetMax = new Vector2( 0, itemRect.offsetMax.y );
            itemRect.localScale = Vector3.one;
            itemRect.localPosition = new Vector3( 0, itemRect.localPosition.y, 0 );
        }

        item.manager = manager;

        if ( infrastructurePrefab != null ) {
            item.infrastructurePrefab = infrastructurePrefab;
        }

        if ( item.infrastructureContainer == null ) {
            Transform contentPanel = item.contentPanel;
            if ( contentPanel != null ) {
                Transform container = contentPanel.Find( "InfrastructureContainer" );
                if ( container == null && contentPanel.childCount > 0 ) {
                    container = contentPanel.GetChild( 0 );
                }

                if ( container != null ) {
                    item.infrastructureContainer = container;
                }
            }
        }

        if ( !string.IsNullOrEmpty( categoryId ) ) {
            item.SetCategoryId( categoryId );
        }

        if ( item.headerButton != null ) {
            TMPro.TextMeshProUGUI headerText = item.headerButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if ( headerText != null ) {
                headerText.text = categoryName;
            }
        }

        item.headerButton.onClick.RemoveAllListeners();
        item.headerButton.onClick.AddListener( () => manager.ToggleItem( item ) );

        manager.accordionItems.Add( item );
    }
}