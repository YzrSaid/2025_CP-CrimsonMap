using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using System.Collections;
using System.IO;
using System.Linq;

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
            if ( GlobalManager.Instance != null && MapManager.Instance != null && MapManager.Instance.IsReady() && IsDataInitializationComplete() ) {
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
        List<string> currentCampusIds = MapManager.Instance.GetCurrentCampusIds();
        string currentMapId = MapManager.Instance.GetCurrentMap()?.map_id;

        if ( string.IsNullOrEmpty( currentMapId ) || currentCampusIds.Count == 0 ) {
            yield break;
        }

        Node[] allNodes = null;
        Infrastructure[] allInfrastructures = null;
        CategoryList categoryList = null;

        yield return StartCoroutine( LoadNodesForMap( currentMapId, ( nodes ) => allNodes = nodes ) );
        yield return StartCoroutine( LoadInfrastructureData( ( infras ) => allInfrastructures = infras ) );
        yield return StartCoroutine( LoadCategoryData( ( cats ) => categoryList = cats ) );

        if ( allNodes == null || allInfrastructures == null || categoryList == null ) {
            yield break;
        }

        HashSet<string> usedCategoryIds = GetUsedCategoryIds( allNodes, allInfrastructures, currentCampusIds );

        foreach ( Category category in categoryList.categories ) {
            if ( !usedCategoryIds.Contains( category.category_id ) ) {
                continue;
            }

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

    private HashSet<string> GetUsedCategoryIds( Node[] nodes, Infrastructure[] infrastructures, List<string> currentCampusIds )
    {
        HashSet<string> usedCategories = new HashSet<string>();

        Dictionary<string, Infrastructure> infraDict = new Dictionary<string, Infrastructure>();
        foreach ( var infra in infrastructures ) {
            infraDict[infra.infra_id] = infra;
        }

        foreach ( var node in nodes ) {
            if ( node.type != "infrastructure" || !node.is_active ) {
                continue;
            }

            if ( !currentCampusIds.Contains( node.campus_id ) ) {
                continue;
            }

            if ( !string.IsNullOrEmpty( node.related_infra_id ) && infraDict.TryGetValue( node.related_infra_id, out Infrastructure infra ) ) {
                if ( !string.IsNullOrEmpty( infra.category_id ) ) {
                    usedCategories.Add( infra.category_id );
                }
            }
        }

        return usedCategories;
    }

    private IEnumerator LoadNodesForMap( string mapId, System.Action<Node[]> onComplete )
    {
        bool loadComplete = false;
        Node[] nodes = null;

        yield return StartCoroutine( CrossPlatformFileLoader.LoadJsonFile(
            $"nodes_{mapId}.json",
            ( jsonContent ) => {
                try {
                    nodes = JsonHelper.FromJson<Node>( jsonContent );
                    loadComplete = true;
                } catch ( System.Exception e ) {
                    loadComplete = true;
                }
            },
            ( error ) => {
                loadComplete = true;
            }
        ) );

        yield return new WaitUntil( () => loadComplete );
        onComplete?.Invoke( nodes );
    }

    private IEnumerator LoadInfrastructureData( System.Action<Infrastructure[]> onComplete )
    {
        bool loadComplete = false;
        Infrastructure[] infrastructures = null;

        yield return StartCoroutine( CrossPlatformFileLoader.LoadJsonFile(
            "infrastructure.json",
            ( jsonContent ) => {
                try {
                    infrastructures = JsonHelper.FromJson<Infrastructure>( jsonContent );
                    loadComplete = true;
                } catch ( System.Exception e ) {
                    loadComplete = true;
                }
            },
            ( error ) => {
                loadComplete = true;
            }
        ) );

        yield return new WaitUntil( () => loadComplete );
        onComplete?.Invoke( infrastructures );
    }

    private IEnumerator LoadCategoryData( System.Action<CategoryList> onComplete )
    {
        bool loadComplete = false;
        CategoryList categoryList = null;

        yield return StartCoroutine( CrossPlatformFileLoader.LoadJsonFile(
            "categories.json",
            ( jsonContent ) => {
                try {
                    categoryList = JsonUtility.FromJson<CategoryList>( "{\"categories\":" + jsonContent + "}" );
                    loadComplete = true;
                } catch ( System.Exception e ) {
                    loadComplete = true;
                }
            },
            ( error ) => {
                loadComplete = true;
            }
        ) );

        yield return new WaitUntil( () => loadComplete );
        onComplete?.Invoke( categoryList );
    }
}