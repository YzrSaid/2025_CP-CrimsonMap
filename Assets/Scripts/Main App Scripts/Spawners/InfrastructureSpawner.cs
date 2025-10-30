using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using System.Linq;
using Mapbox.Utils;
using Mapbox.Unity.Map;
using TMPro;

public class InfrastructureSpawner : MonoBehaviour
{
    [Header( "Mapbox" )]
    public AbstractMap mapboxMap;

    [Header( "Prefabs" )]
    public GameObject infrastructurePrefab;

    [Header( "JSON Files - Static Files" )]
    public string infrastructureFileName = "infrastructure.json";
    public string categoriesFileName = "categories.json";

    [Header( "Settings" )]
    public bool enableDebugLogs = true;
    public float infrastructureSize = 3.0f;
    public float heightOffset = 1f;

    private string currentMapId;
    private List<string> currentCampusIds = new List<string>();

    private List<InfrastructureNode> spawnedInfrastructure = new List<InfrastructureNode>();
    private Dictionary<string, InfrastructureNode> infraIdToComponent = new Dictionary<string, InfrastructureNode>();

    private bool isSpawning = false;

    void Awake()
    {
        if ( mapboxMap == null ) {
            mapboxMap = FindObjectOfType<AbstractMap>();
        }
    }

    void Start()
    {
        if ( mapboxMap == null ) {
            return;
        }

        if ( MapManager.Instance != null ) {
            MapManager.Instance.OnMapChanged += OnMapChanged;
            MapManager.Instance.OnMapLoadingStarted += OnMapLoadingStarted;
        }
    }

    void OnDestroy()
    {
        if ( MapManager.Instance != null ) {
            MapManager.Instance.OnMapChanged -= OnMapChanged;
            MapManager.Instance.OnMapLoadingStarted -= OnMapLoadingStarted;
        }
    }

    public void SetTargetCampusIds( List<string> campusIds )
    {
        currentCampusIds.Clear();
        if ( campusIds != null ) {
            currentCampusIds.AddRange( campusIds );
        }
    }

    public void SetCurrentMapData( string mapId, List<string> campusIds )
    {
        currentMapId = mapId;
        currentCampusIds.Clear();
        if ( campusIds != null ) {
            currentCampusIds.AddRange( campusIds );
        }
    }

    private void OnMapChanged( MapInfo mapInfo )
    {
        SetCurrentMapData( mapInfo.map_id, mapInfo.campus_included );
    }

    private void OnMapLoadingStarted()
    {
        ClearSpawnedInfrastructure();
    }

    public IEnumerator LoadAndSpawnForCampuses( List<string> campusIds )
    {
        if ( isSpawning ) {
            yield break;
        }

        SetTargetCampusIds( campusIds );

        yield return StartCoroutine( WaitForMapReady() );

        yield return StartCoroutine( LoadAndSpawnInfrastructure() );
    }

    private IEnumerator WaitForMapReady()
    {
        float timeout = 30f;
        float elapsed = 0f;

        while ( elapsed < timeout ) {
            if ( mapboxMap != null && mapboxMap.gameObject.activeInHierarchy ) {
                break;
            }

            yield return new WaitForSeconds( 0.5f );
            elapsed += 0.5f;
        }

        if ( elapsed >= timeout ) {
            yield break;
        }

        yield return new WaitForSeconds( 1f );
    }

    private string GetNodesFileName()
    {
        if ( string.IsNullOrEmpty( currentMapId ) ) {
            return "nodes.json";
        }

        return $"nodes_{currentMapId}.json";
    }

    public IEnumerator LoadAndSpawnInfrastructure()
    {
        if ( isSpawning ) {
            yield break;
        }

        isSpawning = true;

        List<InfrastructureData> infrastructureToSpawn = null;
        bool errorOccurred = false;

        Node[] nodes = null;
        Infrastructure[] infrastructures = null;
        Category[] categories = null;

        ClearSpawnedInfrastructure();

        yield return StartCoroutine( LoadNodesFromJSONAsync( ( loadedNodes ) => {
            nodes = loadedNodes;
        } ) );

        yield return StartCoroutine( LoadInfrastructureFromJSONAsync( ( loadedInfra ) => {
            infrastructures = loadedInfra;
        } ) );

        yield return StartCoroutine( LoadCategoriesFromJSONAsync( ( loadedCategories ) => {
            categories = loadedCategories;
        } ) );

        try {
            if ( nodes == null || infrastructures == null ) {
                errorOccurred = true;
            } else {
                infrastructureToSpawn = BuildInfrastructureData( nodes, infrastructures, categories, currentCampusIds );
            }
        } catch ( System.Exception e ) {
            errorOccurred = true;
        } finally {
            isSpawning = false;
        }

        if ( errorOccurred || infrastructureToSpawn == null ) {
            yield break;
        }

        yield return StartCoroutine( SpawnInfrastructureItems( infrastructureToSpawn ) );
    }

    private IEnumerator LoadNodesFromJSONAsync( System.Action<Node[]> onComplete )
    {
        bool loadCompleted = false;
        Node[] nodes = null;

        yield return StartCoroutine( CrossPlatformFileLoader.LoadJsonFile(
                                         GetNodesFileName(),
        ( jsonContent ) => {
            try {
                nodes = JsonHelper.FromJson<Node>( jsonContent );
                loadCompleted = true;
            } catch ( System.Exception e ) {
                loadCompleted = true;
            }
        },
        ( error ) => {
            loadCompleted = true;
        }
                                     ) );

        yield return new WaitUntil( () => loadCompleted );
        onComplete?.Invoke( nodes );
    }

    private IEnumerator LoadInfrastructureFromJSONAsync( System.Action<Infrastructure[]> onComplete )
    {
        bool loadCompleted = false;
        Infrastructure[] infrastructures = null;

        yield return StartCoroutine( CrossPlatformFileLoader.LoadJsonFile(
                                         infrastructureFileName,
        ( jsonContent ) => {
            try {
                infrastructures = JsonHelper.FromJson<Infrastructure>( jsonContent );
                loadCompleted = true;
            } catch ( System.Exception e ) {
                loadCompleted = true;
            }
        },
        ( error ) => {
            loadCompleted = true;
        }
                                     ) );

        yield return new WaitUntil( () => loadCompleted );
        onComplete?.Invoke( infrastructures );
    }

    private IEnumerator LoadCategoriesFromJSONAsync( System.Action<Category[]> onComplete )
    {
        bool loadCompleted = false;
        Category[] categories = null;

        yield return StartCoroutine( CrossPlatformFileLoader.LoadJsonFile(
                                         categoriesFileName,
        ( jsonContent ) => {
            try {
                categories = JsonHelper.FromJson<Category>( jsonContent );
                loadCompleted = true;
            } catch ( System.Exception e ) {
                loadCompleted = true;
            }
        },
        ( error ) => {
            loadCompleted = true;
        }
                                     ) );

        yield return new WaitUntil( () => loadCompleted );
        onComplete?.Invoke( categories );
    }

    private List<InfrastructureData> BuildInfrastructureData( Node[] nodes, Infrastructure[] infrastructures,
            Category[] categories, List<string> campusIds )
    {
        var infrastructureData = new List<InfrastructureData>();

        var infraDict = new Dictionary<string, Infrastructure>();

        for ( int i = 0; i < infrastructures.Length; i++ ) {
            var infra = infrastructures[i];

            if ( string.IsNullOrEmpty( infra.infra_id ) ) {
                continue;
            }

            if ( !infraDict.ContainsKey( infra.infra_id ) ) {
                infraDict[infra.infra_id] = infra;
            }
        }

        var categoryDict = new Dictionary<string, Category>();

        if ( categories != null ) {
            for ( int i = 0; i < categories.Length; i++ ) {
                var category = categories[i];
                string key = category.category_id;

                if ( string.IsNullOrEmpty( key ) ) {
                    continue;
                }

                if ( !categoryDict.ContainsKey( key ) ) {
                    categoryDict[key] = category;
                }
            }
        }

        var infrastructureNodes = nodes.Where( n =>
                                               n != null &&
                                               n.type == "infrastructure" &&
                                               n.is_active &&
                                               ( campusIds == null || campusIds.Count == 0 || campusIds.Contains( n.campus_id ) ) &&
                                               !string.IsNullOrEmpty( n.related_infra_id ) &&
                                               IsValidCoordinate( n.latitude, n.longitude )
                                             ).ToList();

        foreach ( var node in infrastructureNodes ) {
            if ( infraDict.TryGetValue( node.related_infra_id, out Infrastructure infrastructure ) ) {
                categoryDict.TryGetValue( infrastructure.category_id, out Category category );

                var data = new InfrastructureData {
                    Node = node,
                    Infrastructure = infrastructure,
                    Category = category
                };

                infrastructureData.Add( data );
            }
        }

        return infrastructureData;
    }

    private IEnumerator SpawnInfrastructureItems( List<InfrastructureData> infrastructureData )
    {
        int spawnedCount = 0;
        foreach ( var data in infrastructureData ) {
            bool shouldYield = false;
            try {
                if ( infrastructurePrefab == null ) {
                    break;
                }

                GameObject infraObj = Instantiate( infrastructurePrefab, Vector3.zero, Quaternion.identity, mapboxMap.transform );
                infraObj.name = $"Infrastructure_{data.Infrastructure.name}_{data.Node.node_id}";
                infraObj.transform.localScale = Vector3.one * infrastructureSize;

                InfrastructureNode infraComponent = infraObj.AddComponent<InfrastructureNode>();
                infraComponent.Initialize( mapboxMap, data, heightOffset );

                spawnedInfrastructure.Add( infraComponent );

                infraIdToComponent[data.Infrastructure.infra_id] = infraComponent;

                spawnedCount++;

                if ( spawnedCount % 5 == 0 ) {
                    shouldYield = true;
                }
            } catch ( System.Exception e ) {
            }

            if ( shouldYield ) {
                yield return null;
            }
        }
    }

    public void ClearSpawnedInfrastructure()
    {
        foreach ( var infrastructure in spawnedInfrastructure ) {
            if ( infrastructure != null && infrastructure.gameObject != null ) {
                DestroyImmediate( infrastructure.gameObject );
            }
        }

        spawnedInfrastructure.Clear();
        infraIdToComponent.Clear();
    }

    public void ManualSpawn()
    {
        if ( currentCampusIds != null && currentCampusIds.Count > 0 ) {
            StartCoroutine( LoadAndSpawnInfrastructure() );
        }
    }

    public void ForceResetSpawning()
    {
        isSpawning = false;
        StopAllCoroutines();
    }

    private bool IsValidCoordinate( float lat, float lon )
    {
        return !float.IsNaN( lat ) && !float.IsNaN( lon ) &&
               !float.IsInfinity( lat ) && !float.IsInfinity( lon ) &&
               lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180;
    }

    void Update()
    {
    }
}

[System.Serializable]
public class InfrastructureData
{
    public Node Node;
    public Infrastructure Infrastructure;
    public Category Category;
}

public class InfrastructureNode : MonoBehaviour
{
    private AbstractMap map;
    private InfrastructureData infrastructureData;
    private Vector2d geoLocation;
    private float heightOffset;

    public InfrastructureData GetInfrastructureData() => infrastructureData;
    public Vector2d GetGeoLocation() => geoLocation;

    public void Initialize( AbstractMap mapReference, InfrastructureData data, float height )
    {
        map = mapReference;
        infrastructureData = data;
        geoLocation = new Vector2d( data.Node.latitude, data.Node.longitude );
        heightOffset = height;

        SetupInfrastructureDisplay();
        UpdatePosition();
    }

    private void SetupInfrastructureDisplay()
    {
        // Update the main label to show the legend instead of full name
        TextMeshPro label3D = GetComponentInChildren<TextMeshPro>();
        if ( label3D != null ) {
            if ( infrastructureData.Category != null && !string.IsNullOrEmpty( infrastructureData.Category.legend ) ) {
                label3D.text = infrastructureData.Category.legend;
            } else {
                // Fallback to infrastructure name if no legend
                label3D.text = infrastructureData.Infrastructure.name;
            }
        }

        SetupCircleBackground();
    }

    private void SetupCircleBackground()
    {
        Renderer circleRenderer = null;

        foreach ( Transform child in transform ) {
            MeshFilter meshFilter = child.GetComponent<MeshFilter>();
            if ( meshFilter != null && meshFilter.sharedMesh != null && meshFilter.sharedMesh.name == "Cylinder" ) {
                circleRenderer = child.GetComponent<Renderer>();
                break;
            }
        }
    }

    void Update()
    {
        if ( map != null ) {
            UpdatePosition();
        }
    }

    void UpdatePosition()
    {
        Vector3 worldPos = map.GeoToWorldPosition( geoLocation, true );
        worldPos.y += heightOffset;

        transform.position = worldPos;
    }
}