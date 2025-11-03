using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using System.Linq;
using Mapbox.Utils;
using Mapbox.Unity.Map;

public class PathRenderer : MonoBehaviour
{
    [Header( "Mapbox" )]
    public AbstractMap mapboxMap;

    [Header( "Path Prefabs" )]
    public GameObject pathPrefab;

    [Header( "Settings" )]
    public bool enableDebugLogs = true;
    public float pathWidth = 1f;
    public float pathHeightOffset = 1f;

    [Header( "Path Appearance" )]
    public Color pathwayColor = new Color( 0.8f, 0.6f, 0.4f, 0.9f );

    private string currentMapId;
    private List<string> currentCampusIds = new List<string>();

    private List<PathEdge> spawnedPaths = new List<PathEdge>();
    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>();

    private bool isRendering = false;

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
        ClearSpawnedPaths();
    }

    public IEnumerator LoadAndRenderPathsForMap( string mapId, List<string> campusIds )
    {
        if ( isRendering ) {
            yield break;
        }

        SetCurrentMapData( mapId, campusIds );

        yield return StartCoroutine( WaitForMapReady() );

        yield return StartCoroutine( LoadAndRenderPaths() );
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

        string fileName = $"nodes_{currentMapId}.json";
        return fileName;
    }

    private string GetEdgesFileName()
    {
        if ( string.IsNullOrEmpty( currentMapId ) ) {
            return "edges.json";
        }

        string fileName = $"edges_{currentMapId}.json";
        return fileName;
    }

    public IEnumerator LoadAndRenderPaths()
    {
        if ( isRendering ) {
            yield break;
        }

        isRendering = true;

        if ( string.IsNullOrEmpty( currentMapId ) ) {
            isRendering = false;
            yield break;
        }

        List<Edge> validEdges = null;
        bool errorOccurred = false;

        try {
            ClearSpawnedPaths();
        } catch (System.Exception)
        {
            errorOccurred = true;
        } finally {
            isRendering = false;
        }

        if ( errorOccurred ) {
            yield break;
        }

        yield return StartCoroutine( LoadFilteredNodes( currentCampusIds ) );

        if ( allNodes.Count == 0 ) {
            yield break;
        }

        yield return StartCoroutine( LoadEdgesFromJSONAsync( ( edges ) => {
            if ( edges == null || edges.Length == 0 ) {
                return;
            }

            validEdges = FilterValidPathwayEdges( edges );
        } ) );

        if ( validEdges == null ) {
            yield break;
        }

        if ( validEdges.Count == 0 ) {
            yield break;
        }

        yield return StartCoroutine( RenderPathEdges( validEdges ) );
    }

    private IEnumerator LoadFilteredNodes( List<string> campusIds )
    {
        bool loadCompleted = false;

        yield return StartCoroutine( CrossPlatformFileLoader.LoadJsonFile(
                                         GetNodesFileName(),
        ( jsonContent ) => {
            try {
                Node[] nodes = JsonHelper.FromJson<Node>( jsonContent );

                allNodes.Clear();

                var pathwayNodes = nodes.Where( n =>
                                                n != null &&
                                                n.is_active &&
                                                ( n.type == "pathway" || n.type == "infrastructure" || n.type == "intermediate" ) &&
                                                ( campusIds == null || campusIds.Count == 0 || campusIds.Contains( n.campus_id ) ) &&
                                                IsValidCoordinate( n.latitude, n.longitude )
                                              ).ToList();

                foreach ( var node in pathwayNodes ) {
                    allNodes[node.node_id] = node;
                }

                loadCompleted = true;
            } catch (System.Exception)
            {
                loadCompleted = true;
            }
        },
        ( error ) => {
            loadCompleted = true;
        }
                                     ) );

        yield return new WaitUntil( () => loadCompleted );
    }

    private IEnumerator LoadEdgesFromJSONAsync( System.Action<Edge[]> onComplete )
    {
        bool loadCompleted = false;
        Edge[] edges = null;

        yield return StartCoroutine( CrossPlatformFileLoader.LoadJsonFile(
                                         GetEdgesFileName(),
        ( jsonContent ) => {
            try {
                edges = JsonHelper.FromJson<Edge>( jsonContent );
                loadCompleted = true;
            } catch (System.Exception)
            {
                loadCompleted = true;
            }
        },
        ( error ) => {
            loadCompleted = true;
        }
                                     ) );

        yield return new WaitUntil( () => loadCompleted );
        onComplete?.Invoke( edges );
    }

    private List<Edge> FilterValidPathwayEdges( Edge[] allEdges )
    {
        var validEdges = new List<Edge>();
        var activeEdges = allEdges.Where( e => e != null && e.is_active ).ToList();

        foreach ( var edge in activeEdges ) {
            bool hasFromNode = allNodes.ContainsKey( edge.from_node );
            bool hasToNode = allNodes.ContainsKey( edge.to_node );

            if ( hasFromNode && hasToNode ) {
                validEdges.Add( edge );
            }
        }

        return validEdges;
    }

    private IEnumerator RenderPathEdges( List<Edge> edges )
    {
        int renderedCount = 0;
        foreach ( var edge in edges ) {
            bool shouldYield = false;
            try {
                if ( pathPrefab == null ) {
                    break;
                }

                if ( !allNodes.TryGetValue( edge.from_node, out Node fromNode ) ||
                        !allNodes.TryGetValue( edge.to_node, out Node toNode ) ) {
                    continue;
                }

                GameObject pathObj = Instantiate( pathPrefab, Vector3.zero, Quaternion.identity, mapboxMap.transform );
                pathObj.name = $"Pathway_{edge.edge_id}_{edge.from_node}_to_{edge.to_node}";

                PathEdge pathComponent = pathObj.AddComponent<PathEdge>();
                pathComponent.Initialize( mapboxMap, edge, fromNode, toNode, pathWidth, pathHeightOffset, pathwayColor );

                spawnedPaths.Add( pathComponent );
                renderedCount++;

                if ( renderedCount % 10 == 0 ) {
                    shouldYield = true;
                }
            } catch (System.Exception)
            {
            }

            if ( shouldYield ) {
                yield return null;
            }
        }
    }

    public void ClearSpawnedPaths()
    {
        foreach ( var path in spawnedPaths ) {
            if ( path != null && path.gameObject != null ) {
                DestroyImmediate( path.gameObject );
            }
        }

        spawnedPaths.Clear();
        allNodes.Clear();
    }

    public void ForceUpdateAllPaths()
    {
        foreach ( var path in spawnedPaths ) {
            if ( path != null ) {
                path.ForceUpdate();
            }
        }
    }

    public void ManualRender()
    {
        if ( !string.IsNullOrEmpty( currentMapId ) ) {
            StartCoroutine( LoadAndRenderPaths() );
        }
    }

    public void ForceResetRendering()
    {
        isRendering = false;
        StopAllCoroutines();
    }

    private bool IsValidCoordinate( float lat, float lon )
    {
        return !float.IsNaN( lat ) && !float.IsNaN( lon ) &&
               !float.IsInfinity( lat ) && !float.IsInfinity( lon ) &&
               lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180;
    }

    private void DebugLog( string message )
    {
        if ( enableDebugLogs ) {
            Debug.Log( $"[PathRenderer] {message}" );
        }
    }

    void Update()
    {
    }
}

public class PathEdge : MonoBehaviour
{
    private AbstractMap map;
    private Edge edgeData;
    private Node fromNode;
    private Node toNode;
    private float baseWidth;
    private float heightOffset;
    private Color pathColor;

    private float referenceZoomLevel;
    private Vector3 referenceFromPos;
    private Vector3 referenceToPos;
    private float referenceDistance;
    private bool isInitialized = false;

    public Edge GetEdgeData() => edgeData;
    public Node GetFromNode() => fromNode;
    public Node GetToNode() => toNode;

    public void Initialize( AbstractMap mapReference, Edge edge, Node from, Node to,
                            float pathWidth, float height, Color color )
    {
        map = mapReference;
        edgeData = edge;
        fromNode = from;
        toNode = to;
        baseWidth = pathWidth;
        heightOffset = height;
        pathColor = color;

        if ( map != null ) {
            referenceZoomLevel = map.Zoom;

            referenceFromPos = map.GeoToWorldPosition( new Vector2d( fromNode.latitude, fromNode.longitude ), false );
            referenceToPos = map.GeoToWorldPosition( new Vector2d( toNode.latitude, toNode.longitude ), false );
            referenceDistance = Vector3.Distance( referenceFromPos, referenceToPos );

            isInitialized = true;
        }

        ApplyColorToPath( pathColor );

        UpdatePathTransform();
    }

    private void ApplyColorToPath( Color color )
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach ( var renderer in renderers ) {
            if ( renderer.material != null ) {
                renderer.material.color = color;
            }
        }
    }

    void LateUpdate()
    {
        if ( map != null && fromNode != null && toNode != null && isInitialized ) {
            if ( Time.frameCount % 2 == 0 ) {
                UpdatePathTransform();
            }
        }
    }

    void UpdatePathTransform()
    {
        if ( fromNode == null || toNode == null || map == null || !isInitialized ) return;

        Vector3 fromPos = map.GeoToWorldPosition( new Vector2d( fromNode.latitude, fromNode.longitude ), false );
        Vector3 toPos = map.GeoToWorldPosition( new Vector2d( toNode.latitude, toNode.longitude ), false );

        fromPos.y = heightOffset;
        toPos.y = heightOffset;

        Vector3 direction = toPos - fromPos;
        float currentDistance = direction.magnitude;

        if ( currentDistance < 0.001f ) {
            gameObject.SetActive( false );
            return;
        }

        gameObject.SetActive( true );

        Vector3 centerPos = ( fromPos + toPos ) * 0.5f;
        transform.position = centerPos;

        if ( direction != Vector3.zero ) {
            transform.rotation = Quaternion.LookRotation( direction.normalized, Vector3.up );
        }

        float visualDistance = referenceDistance;

        transform.localScale = new Vector3( baseWidth, baseWidth, visualDistance );
    }

    public void ForceUpdate()
    {
        if ( map != null && isInitialized ) {
            UpdatePathTransform();
        }
    }

    void OnDrawGizmosSelected()
    {
        if ( fromNode != null && toNode != null && map != null ) {
            Vector3 fromPos = map.GeoToWorldPosition( new Vector2d( fromNode.latitude, fromNode.longitude ), false );
            Vector3 toPos = map.GeoToWorldPosition( new Vector2d( toNode.latitude, toNode.longitude ), false );

            fromPos.y = heightOffset;
            toPos.y = heightOffset;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere( transform.TransformPoint( fromPos ), 0.2f );
            Gizmos.DrawWireSphere( transform.TransformPoint( toPos ), 0.2f );
            Gizmos.color = Color.green;
            Gizmos.DrawLine( transform.TransformPoint( fromPos ), transform.TransformPoint( toPos ) );

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube( transform.position, Vector3.one * 0.1f );
        }
    }
}