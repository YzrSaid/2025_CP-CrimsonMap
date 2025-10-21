using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ARNavigationMarkerSpawner : MonoBehaviour
{
    [Header( "AR Marker Prefabs" )]
    public GameObject arrowMarkerPrefab;
    public GameObject nodeMarkerPrefab;
    public GameObject destinationMarkerPrefab;

    [Header( "AR Camera" )]
    public Camera arCamera;

    [Header( "Marker Settings" )]
    public float markerScale = 1.5f;
    public float markerHeightOffset = 0f;
    public float arrowSpacing = 10f; // meters between arrows
    public float nodeMarkerDistance = 3f; // show when within 3m of node
    public float arrowVisibilityDistance = 50f;

    [Header( "Colors" )]
    public Color navigationArrowColor = new Color( 0.74f, 0.06f, 0.18f, 0.9f );
    public Color navigationNodeColor = new Color( 0.74f, 0.06f, 0.18f, 1f );
    public Color destinationColor = new Color( 0.2f, 0.8f, 0.2f, 1f );

    [Header( "Settings" )]
    public bool enableDebugLogs = true;

    private List<Node> pathNodes = new List<Node>();
    private Dictionary<string, GameObject> spawnedNodeMarkers = new Dictionary<string, GameObject>();
    private List<GameObject> spawnedArrowMarkers = new List<GameObject>();

    private Vector2 userLocation;
    private DirectionDisplayManager directionManager;
    private bool isARNavigationMode = false;

    void Start()
    {
        if ( arCamera == null )
            arCamera = Camera.main;

        directionManager = GetComponent<DirectionDisplayManager>();
        if ( directionManager == null )
            directionManager = FindObjectOfType<DirectionDisplayManager>();

        DetermineNavigationMode();

        if ( isARNavigationMode ) {
            LoadPathNodesFromPlayerPrefs();
            StartCoroutine( InitializeNavigationMarkers() );
        } else {
            DebugLog( "🗺️ Direct AR Mode - Markers disabled" );
        }
    }

    private void DetermineNavigationMode()
    {
        string arMode = PlayerPrefs.GetString( "ARMode", "DirectAR" );
        isARNavigationMode = ( arMode == "Navigation" );

        DebugLog( $"AR Mode: {arMode} | Navigation Markers: {isARNavigationMode}" );
    }

    private void LoadPathNodesFromPlayerPrefs()
    {
        int pathNodeCount = PlayerPrefs.GetInt( "ARNavigation_PathNodeCount", 0 );

        if ( pathNodeCount == 0 ) {
            DebugLog( "⚠️ No path nodes found" );
            return;
        }

        pathNodes.Clear();

        List<string> pathNodeIds = new List<string>();
        for ( int i = 0; i < pathNodeCount; i++ ) {
            string nodeId = PlayerPrefs.GetString( $"ARNavigation_PathNode_{i}", "" );
            if ( !string.IsNullOrEmpty( nodeId ) )
                pathNodeIds.Add( nodeId );
        }

        StartCoroutine( LoadNodesData( pathNodeIds ) );
    }

    private IEnumerator LoadNodesData( List<string> nodeIds )
    {
        string mapId = PlayerPrefs.GetString( "ARScene_MapId", "MAP-01" );
        string fileName = $"nodes_{mapId}.json";

        bool loadComplete = false;

        yield return StartCoroutine( CrossPlatformFileLoader.LoadJsonFile(
                                         fileName,
        ( jsonContent ) => {
            try {
                Node[] allNodes = JsonHelper.FromJson<Node>( jsonContent );

                foreach ( string nodeId in nodeIds ) {
                    Node node = System.Array.Find( allNodes, n => n.node_id == nodeId );
                    if ( node != null )
                        pathNodes.Add( node );
                }

                DebugLog( $"✅ Loaded {pathNodes.Count} path nodes for navigation" );
                loadComplete = true;
            } catch ( System.Exception e ) {
                DebugLog( $"❌ Error loading nodes: {e.Message}" );
                loadComplete = true;
            }
        },
        ( error ) => {
            DebugLog( $"❌ Failed to load nodes file: {error}" );
            loadComplete = true;
        }
                                     ) );

        yield return new WaitUntil( () => loadComplete );
    }

    private IEnumerator InitializeNavigationMarkers()
    {
        yield return new WaitForSeconds( 1f );

        if ( pathNodes.Count < 2 ) {
            DebugLog( "⚠️ Not enough path nodes to create markers" );
            yield break;
        }

        DebugLog( $"🎯 Initializing navigation markers for {pathNodes.Count} nodes" );

        InvokeRepeating( nameof( UpdateNavigationMarkers ), 0.5f, 1f );
    }

    private void UpdateNavigationMarkers()
    {
        if ( !isARNavigationMode || pathNodes.Count == 0 )
            return;

        if ( GPSManager.Instance == null )
            return;

        userLocation = GPSManager.Instance.GetSmoothedCoordinates();

        UpdateNodeMarkers();
        UpdateDirectionalArrows();
    }

    private void UpdateNodeMarkers()
    {
        if ( nodeMarkerPrefab == null && destinationMarkerPrefab == null )
            return;

        for ( int i = 0; i < pathNodes.Count; i++ ) {
            Node node = pathNodes[i];
            float distance = CalculateDistance( userLocation, new Vector2( node.latitude, node.longitude ) );

            bool shouldShow = distance <= nodeMarkerDistance;
            bool isDestination = ( i == pathNodes.Count - 1 );
            string markerId = $"node_{node.node_id}";

            if ( shouldShow && !spawnedNodeMarkers.ContainsKey( markerId ) ) {
                GameObject prefab = isDestination ? destinationMarkerPrefab : nodeMarkerPrefab;
                if ( prefab == null )
                    prefab = nodeMarkerPrefab;

                if ( prefab != null ) {
                    Vector3 worldPos = GPSToWorldPosition( node.latitude, node.longitude );
                    worldPos.y += markerHeightOffset;

                    GameObject marker = Instantiate( prefab, worldPos, Quaternion.identity );
                    marker.transform.localScale = Vector3.one * markerScale;

                    Renderer[] renderers = marker.GetComponentsInChildren<Renderer>();
                    Color targetColor = isDestination ? destinationColor : navigationNodeColor;
                    foreach ( var rend in renderers ) {
                        if ( rend.material != null )
                            rend.material.color = targetColor;
                    }

                    spawnedNodeMarkers[markerId] = marker;
                    DebugLog( $"📍 Spawned node marker at {node.name}" );
                }
            } else if ( !shouldShow && spawnedNodeMarkers.ContainsKey( markerId ) ) {
                Destroy( spawnedNodeMarkers[markerId] );
                spawnedNodeMarkers.Remove( markerId );
                DebugLog( $"🗑️ Removed node marker from {node.name}" );
            }
        }

        foreach ( var marker in spawnedNodeMarkers.Values ) {
            if ( marker != null && arCamera != null ) {
                marker.transform.LookAt( arCamera.transform );
                marker.transform.Rotate( 0, 180, 0 );
            }
        }
    }

    private void UpdateDirectionalArrows()
    {
        if ( arrowMarkerPrefab == null || directionManager == null )
            return;

        NavigationDirection currentDir = directionManager.GetCurrentDirection();
        if ( currentDir == null )
            return;

        int currentIndex = directionManager.GetCurrentDirectionIndex();
        if ( currentIndex >= pathNodes.Count - 1 )
            return;

        ClearArrowMarkers();

        Node currentNode = pathNodes[currentIndex];
        Node nextNode = pathNodes[currentIndex + 1];

        float segmentDistance = CalculateDistance(
                                    new Vector2( currentNode.latitude, currentNode.longitude ),
                                    new Vector2( nextNode.latitude, nextNode.longitude )
                                );

        int arrowCount = Mathf.CeilToInt( segmentDistance / arrowSpacing );
        arrowCount = Mathf.Min( arrowCount, 10 );

        for ( int i = 1; i <= arrowCount; i++ ) {
            float t = i / ( float )( arrowCount + 1 );

            float lat = Mathf.Lerp( currentNode.latitude, nextNode.latitude, t );
            float lng = Mathf.Lerp( currentNode.longitude, nextNode.longitude, t );

            float distanceFromUser = CalculateDistance( userLocation, new Vector2( lat, lng ) );

            if ( distanceFromUser <= arrowVisibilityDistance ) {
                Vector3 worldPos = GPSToWorldPosition( lat, lng );
                worldPos.y += markerHeightOffset + 0.5f;

                GameObject arrow = Instantiate( arrowMarkerPrefab, worldPos, Quaternion.identity );
                arrow.transform.localScale = Vector3.one * markerScale;

                Vector3 nextWorldPos = GPSToWorldPosition( nextNode.latitude, nextNode.longitude );
                Vector3 direction = ( nextWorldPos - worldPos ).normalized;
                arrow.transform.rotation = Quaternion.LookRotation( direction );

                Renderer[] renderers = arrow.GetComponentsInChildren<Renderer>();
                foreach ( var rend in renderers ) {
                    if ( rend.material != null )
                        rend.material.color = navigationArrowColor;
                }

                spawnedArrowMarkers.Add( arrow );
            }
        }

        DebugLog( $"🏹 Spawned {spawnedArrowMarkers.Count} arrow markers" );
    }

    private void ClearArrowMarkers()
    {
        foreach ( var arrow in spawnedArrowMarkers ) {
            if ( arrow != null )
                Destroy( arrow );
        }
        spawnedArrowMarkers.Clear();
    }

    private void ClearAllMarkers()
    {
        ClearArrowMarkers();

        foreach ( var marker in spawnedNodeMarkers.Values ) {
            if ( marker != null )
                Destroy( marker );
        }
        spawnedNodeMarkers.Clear();
    }

    private Vector3 GPSToWorldPosition( float latitude, float longitude )
    {
        Vector2 userCoords = userLocation;
        float deltaLat = latitude - userCoords.x;
        float deltaLng = longitude - userCoords.y;

        float meterPerDegree = 111000f;
        float x = deltaLng * meterPerDegree * Mathf.Cos( userCoords.x * Mathf.Deg2Rad );
        float z = deltaLat * meterPerDegree;

        return new Vector3( x, 0, z );
    }

    private float CalculateDistance( Vector2 coord1, Vector2 coord2 )
    {
        float lat1Rad = coord1.x * Mathf.Deg2Rad;
        float lat2Rad = coord2.x * Mathf.Deg2Rad;
        float deltaLatRad = ( coord2.x - coord1.x ) * Mathf.Deg2Rad;
        float deltaLngRad = ( coord2.y - coord1.y ) * Mathf.Deg2Rad;

        float a = Mathf.Sin( deltaLatRad / 2 ) * Mathf.Sin( deltaLatRad / 2 ) +
                  Mathf.Cos( lat1Rad ) * Mathf.Cos( lat2Rad ) *
                  Mathf.Sin( deltaLngRad / 2 ) * Mathf.Sin( deltaLngRad / 2 );

        float c = 2 * Mathf.Atan2( Mathf.Sqrt( a ), Mathf.Sqrt( 1 - a ) );

        return 6371000 * c;
    }

    private void DebugLog( string message )
    {
        if ( enableDebugLogs )
            Debug.Log( $"[ARNavMarkers] {message}" );
    }

    void OnDestroy()
    {
        CancelInvoke();
        ClearAllMarkers();
    }
}