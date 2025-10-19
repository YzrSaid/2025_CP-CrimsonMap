using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using System.Collections;
using System.IO;

public class CategoryDropdown : MonoBehaviour
{
    [Header( "UI References" )]
    public GameObject panel;
    public Button categoryButton;
    public Transform panelContent;

    [Header( "CategoryItem Prefab" )]
    public Button categoryItemPrefab;
    private List<Button> spawnedItems = new List<Button>();

    [Header( "Loading Check" )]
    public float maxWaitTime = 30f;

    void Start()
    {
        panel.SetActive( false );
        StartCoroutine( WaitForDataInitializationThenPopulate() );
    }

    private IEnumerator WaitForDataInitializationThenPopulate()
    {
        float waitTime = 0f;

        while ( waitTime < maxWaitTime ) {
            if ( GlobalManager.Instance != null && IsDataInitializationComplete() ) {
                yield return StartCoroutine( PopulatePanel() );
                yield break;
            }

            waitTime += Time.deltaTime;
            yield return new WaitForSeconds( 0.1f );
        }

        yield return StartCoroutine( PopulatePanel() );
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

    IEnumerator PopulatePanel()
    {
        bool dataLoaded = false;
        CategoryList categoryList = null;
        string errorMessage = "";

        yield return StartCoroutine( CrossPlatformFileLoader.LoadJsonFile( "categories.json",
        ( jsonContent ) => {
            try {
                categoryList = JsonUtility.FromJson<CategoryList>( "{\"categories\":" + jsonContent + "}" );
                dataLoaded = true;
            } catch ( System.Exception e ) {
                errorMessage = $"Error parsing JSON: {e.Message}";
            }
        },
        ( error ) => {
            errorMessage = error;
        } ) );

        if ( !dataLoaded ) {
            yield break;
        }

        if ( categoryList == null || categoryList.categories == null ) {
            yield break;
        }

        foreach ( Category category in categoryList.categories ) {
            Button item = Instantiate( categoryItemPrefab, panelContent );
            spawnedItems.Add( item );

            TMP_Text label = item.GetComponentInChildren<TMP_Text>();
            if ( label != null ) {
                label.text = category.name;
            }

            Image colorImage = null;
            Image[] allImages = item.GetComponentsInChildren<Image>( true );

            foreach ( var img in allImages ) {
                if ( img.name == "Image_Color" || img.name == "Image" ||
                        img.gameObject.name.Contains( "Color" ) ) {
                    colorImage = img;
                    break;
                }
            }

            if ( colorImage == null && allImages.Length > 1 ) {
                colorImage = allImages[1];
            }

            if ( colorImage != null && !string.IsNullOrEmpty( category.color ) ) {
                if ( ColorUtility.TryParseHtmlString( category.color, out Color parsedColor ) ) {
                    colorImage.color = parsedColor;
                }
            }
        }
    }
}