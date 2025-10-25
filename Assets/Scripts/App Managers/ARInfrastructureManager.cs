using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using UnityEngine.XR.ARSubsystems;

public class ARInfrastructureManager : MonoBehaviour
{
    [Header( "AR Exit Settings" )]
    [SerializeField] private Button backToMainButton;
    [SerializeField] private string mainSceneName = "MainAppScene";
    private bool isExitingAR = false;

    [Header( "AR Settings" )]
    public GameObject buildingMarkerPrefab;
    public Camera arCamera;
    public float maxVisibleDistance = 500f;
    public float markerScale = 1f;
    public float minMarkerDistance = 2f;
    public float markerHeightOffset = 0f;

    [Header( "UI References" )]
    public TextMeshProUGUI gpsStrengthText;
    public TextMeshProUGUI debugText;
    public TextMeshProUGUI loadingText;

    [Header( "GPS Stability Settings" )]
    public int gpsHistorySize = 5;
    public float positionUpdateThreshold = 1f;
    public float positionSmoothingFactor = 0.3f;

    [Header( "Data" )]
    private List<Node> currentNodes = new List<Node>();
    private List<Infrastructure> currentInfrastructures = new List<Infrastructure>();
    private List<GameObject> activeMarkers = new List<GameObject>();
    private Dictionary<string, MarkerAnchor> markerAnchors = new Dictionary<string, MarkerAnchor>();

    [Header( "GPS" )]
    private Vector2 userLocation;
    private Vector2 lastStableLocation;
    private Queue<Vector2> gpsLocationHistory = new Queue<Vector2>();
    private bool gpsInitialized = false;

    [Header( "Feature Flags" )]
    private ARFeatureMode currentFeatureMode = ARFeatureMode.None;
    private enum ARFeatureMode { None, DirectAR, ARNavigation }

    void Start()
    {
        if ( arCamera == null )
            arCamera = Camera.main;

        UpdateLoadingUI( "Initializing AR Scene..." );

        SetupBackButton();
        DetermineARFeatureMode();

        StartCoroutine( InitializeARScene() );
    }

    private void DetermineARFeatureMode()
    {
        string arMode = PlayerPrefs.GetString( "ARMode", "DirectAR" );

        if ( arMode == "Navigation" ) {
            currentFeatureMode = ARFeatureMode.ARNavigation;
            Debug.Log( "[ARInfrastructureManager] 🧭 AR NAVIGATION MODE - Route-based navigation active" );
        } else {
            currentFeatureMode = ARFeatureMode.DirectAR;
            Debug.Log( "[ARInfrastructureManager] 🗺️ DIRECT AR MODE - Free exploration of all buildings" );
        }
    }

    private void SetupBackButton()
    {
        if ( backToMainButton == null ) {
            GameObject backBtn = GameObject.Find( "BackButton" ) ??
                                 GameObject.Find( "Back Button" ) ??
                                 GameObject.Find( "ExitARButton" ) ??
                                 GameObject.Find( "ARBackButton" );
            if ( backBtn != null )
                backToMainButton = backBtn.GetComponent<Button>();
        }

        if ( backToMainButton != null ) {
            backToMainButton.onClick.RemoveAllListeners();
            backToMainButton.onClick.AddListener( ExitARScene );
            Debug.Log( "AR exit button connected successfully" );
        } else {
            Debug.LogWarning( "AR exit button not found - make sure you have a button named 'BackButton', 'ExitARButton', or assign it in inspector" );
        }
    }

    public void ExitARScene()
    {
        if ( isExitingAR ) return;

        Debug.Log( "Exiting AR scene..." );
        isExitingAR = true;

        ClearMarkers();
        CancelInvoke();

        if ( GlobalManager.Instance != null ) {
            GlobalManager.Instance.StartCoroutine( GlobalManager.Instance.SafeARCleanupAndExit( mainSceneName ) );
        } else {
            UnityEngine.SceneManagement.SceneManager.LoadScene( mainSceneName );
        }
    }

    public void GoToTargetSceneSimple( string sceneName )
    {
        if ( isExitingAR ) return;

        mainSceneName = sceneName;
        StartCoroutine( SafeExitAR() );
    }

    private IEnumerator SafeExitAR()
    {
        isExitingAR = true;
        UpdateLoadingUI( "Exiting AR..." );

        CancelInvoke();
        StopAllCoroutines();

        StartCoroutine( FinishARExitAfterStop() );
        yield break;
    }

    private IEnumerator FinishARExitAfterStop()
    {
        ClearMarkers();

        yield return new WaitForEndOfFrame();

        var arSession = FindObjectOfType<UnityEngine.XR.ARFoundation.ARSession>();
        if ( arSession != null ) {
            arSession.enabled = false;
        }

        yield return new WaitForEndOfFrame();

        yield return StartCoroutine( StopXRSubsystems() );

        if ( GlobalManager.Instance != null ) {
            yield return StartCoroutine( GlobalManager.Instance.SafeARCleanupAndExit( mainSceneName ) );
        } else {
            Debug.LogWarning( "GlobalManager not found, using direct scene transition" );
            UnityEngine.SceneManagement.SceneManager.LoadScene( mainSceneName );
        }
    }

    private IEnumerator StopXRSubsystems()
    {
        Debug.Log( "Stopping XR subsystems to prevent camera reference errors..." );

        try {
            var sessionSubsystems = new List<XRSessionSubsystem>();
            var planeSubsystems = new List<XRPlaneSubsystem>();
            var raycastSubsystems = new List<XRRaycastSubsystem>();

            SubsystemManager.GetInstances( sessionSubsystems );
            SubsystemManager.GetInstances( planeSubsystems );
            SubsystemManager.GetInstances( raycastSubsystems );

            foreach ( var subsystem in sessionSubsystems ) {
                if ( subsystem.running ) {
                    Debug.Log( "Stopping XR Session Subsystem..." );
                    subsystem.Stop();
                }
            }
        } catch ( System.Exception ex ) {
            Debug.LogWarning( $"Error stopping XR session subsystems: {ex.Message}" );
        }

        yield return new WaitForSeconds( 0.1f );

        try {
            var planeSubsystems = new List<XRPlaneSubsystem>();
            var raycastSubsystems = new List<XRRaycastSubsystem>();
            SubsystemManager.GetInstances( planeSubsystems );
            SubsystemManager.GetInstances( raycastSubsystems );

            foreach ( var subsystem in planeSubsystems ) {
                if ( subsystem.running ) {
                    subsystem.Stop();
                }
            }

            foreach ( var subsystem in raycastSubsystems ) {
                if ( subsystem.running ) {
                    subsystem.Stop();
                }
            }
        } catch ( System.Exception ex ) {
            Debug.LogWarning( $"Error stopping XR plane/raycast subsystems: {ex.Message}" );
        }

        yield return new WaitForSeconds( 0.1f );
        Debug.Log( "XR subsystems stopped successfully" );
    }

    IEnumerator InitializeARScene()
    {
        UpdateLoadingUI( "Waiting for GPS Manager..." );

        while ( GPSManager.Instance == null ) {
            yield return new WaitForSeconds( 0.1f );
        }

        UpdateLoadingUI( "GPS Manager found, starting location services..." );
        yield return new WaitForSeconds( 1f );

        UpdateLoadingUI( "Loading map data..." );

        string currentMapId = GetCurrentMapId();
        yield return StartCoroutine( LoadCurrentMapData( currentMapId ) );

        UpdateLoadingUI( "Starting AR tracking..." );
        InvokeRepeating( nameof( UpdateMarkers ), 2f, 1f );

        HideLoadingUI();

        Debug.Log( $"✅ AR Scene initialized - Nodes: {currentNodes.Count}, Infra: {currentInfrastructures.Count}" );
    }

    private string GetCurrentMapId()
    {
        if ( MapManager.Instance != null && MapManager.Instance.GetCurrentMap() != null ) {
            string mapId = MapManager.Instance.GetCurrentMap().map_id;
            Debug.Log( $"[ARInfrastructureManager] Got map ID from MapManager: {mapId}" );
            return mapId;
        }

        if ( PlayerPrefs.HasKey( "CurrentMapId" ) ) {
            string mapId = PlayerPrefs.GetString( "CurrentMapId" );
            Debug.Log( $"[ARInfrastructureManager] Got map ID from PlayerPrefs: {mapId}" );
            return mapId;
        }

        Debug.LogWarning( "[ARInfrastructureManager] Could not determine current map, using default MAP-01" );
        return "MAP-01";
    }

    IEnumerator LoadCurrentMapData( string currentMapId )
    {
        bool nodesLoaded = false;
        bool infraLoaded = false;

        UpdateLoadingUI( $"Loading nodes for map {currentMapId}..." );

        yield return StartCoroutine( LoadNodesData( currentMapId, ( success ) => {
            nodesLoaded = success;
            Debug.Log( $"📦 Nodes Load Result: {success}, Count: {currentNodes.Count}" );

            if ( success ) {
                foreach ( var node in currentNodes ) {
                    Debug.Log( $"  Node: {node.node_id}, Lat: {node.latitude:F6}, Lon: {node.longitude:F6}, InfraID: {node.related_infra_id}" );
                }
            }
        } ) );

        UpdateLoadingUI( "Loading infrastructure data..." );

        yield return StartCoroutine( LoadInfrastructureData( ( success ) => {
            infraLoaded = success;
            Debug.Log( $"🏢 Infrastructure Load Result: {success}, Count: {currentInfrastructures.Count}" );

            if ( success ) {
                foreach ( var infra in currentInfrastructures ) {
                    Debug.Log( $"  Infra: {infra.infra_id}, Name: {infra.name}" );
                }
            }
        } ) );

        if ( nodesLoaded && infraLoaded ) {
            Debug.Log( $"✅ DATA LOADED SUCCESSFULLY - Nodes: {currentNodes.Count}, Infra: {currentInfrastructures.Count}" );
            if ( debugText != null )
                debugText.text = $"Loaded: {currentNodes.Count} nodes, {currentInfrastructures.Count} infra";
        } else {
            Debug.LogError( $"❌ Failed to load data - Nodes: {nodesLoaded}, Infrastructure: {infraLoaded}" );
        }
    }

    IEnumerator LoadNodesData( string mapId, System.Action<bool> onComplete )
    {
        string fileName = $"nodes_{mapId}.json";
        bool loadSuccess = false;

        yield return StartCoroutine( CrossPlatformFileLoader.LoadJsonFile(
                                         fileName,
        ( jsonData ) => {
            try {
                Node[] nodes = JsonHelper.FromJson<Node>( jsonData );
                currentNodes = nodes.Where( n => n.type == "infrastructure" && n.is_active ).ToList();
                Debug.Log( $"✅ Found {currentNodes.Count} active infrastructure nodes" );
                loadSuccess = true;
            } catch ( System.Exception e ) {
                Debug.LogError( $"❌ Error parsing nodes JSON: {e.Message}" );
                loadSuccess = false;
            }
        },
        ( error ) => {
            Debug.LogError( $"❌ Failed to load nodes file: {error}" );
            loadSuccess = false;
        }
                                     ) );

        onComplete?.Invoke( loadSuccess );
    }

    IEnumerator LoadInfrastructureData( System.Action<bool> onComplete )
    {
        string fileName = "infrastructure.json";
        bool loadSuccess = false;

        yield return StartCoroutine( CrossPlatformFileLoader.LoadJsonFile(
                                         fileName,
        ( jsonData ) => {
            try {
                Infrastructure[] infrastructures = JsonHelper.FromJson<Infrastructure>( jsonData );
                currentInfrastructures = infrastructures.Where( i => !i.is_deleted ).ToList();
                Debug.Log( $"✅ Found {currentInfrastructures.Count} active infrastructures" );
                loadSuccess = true;
            } catch ( System.Exception e ) {
                Debug.LogError( $"❌ Error loading infrastructure: {e.Message}" );
                loadSuccess = false;
            }
        },
        ( error ) => {
            Debug.LogError( $"❌ Failed to load infrastructure file: {error}" );
            loadSuccess = false;
        }
                                     ) );

        onComplete?.Invoke( loadSuccess );
    }

    void UpdateMarkers()
    {
        if ( isExitingAR ) {
            Debug.LogWarning( "UpdateMarkers BLOCKED - Exiting AR" );
            return;
        }

        Vector2 rawGpsLocation = GPSManager.Instance.GetSmoothedCoordinates();

        // Check if GPS is valid
        if ( rawGpsLocation.magnitude < 0.0001f ) {
            Debug.LogWarning( $"❌ Invalid GPS location: {rawGpsLocation}" );
            return;
        }

        // Apply GPS stabilization with anchoring
        userLocation = StabilizeGPSLocation( rawGpsLocation );

        Debug.Log( $"📍 Raw GPS: {rawGpsLocation.x:F6}, {rawGpsLocation.y:F6} -> Stabilized: {userLocation.x:F6}, {userLocation.y:F6}" );

        UpdateGPSStrengthUI();

        // Update existing anchored marker positions smoothly
        UpdateAnchoredMarkerPositions();

        // Add/remove markers based on range
        ReconcileVisibleMarkers();

        UpdateDebugInfo();
    }

    private Vector2 StabilizeGPSLocation( Vector2 rawLocation )
    {
        // Initialize on first valid GPS reading
        if ( !gpsInitialized && rawLocation.magnitude > 0.0001f ) {
            lastStableLocation = rawLocation;
            gpsLocationHistory.Enqueue( rawLocation );
            gpsInitialized = true;
            Debug.Log( $"🎯 GPS Stabilization initialized with: {rawLocation}" );
            return lastStableLocation;
        }

        // Add to history
        gpsLocationHistory.Enqueue( rawLocation );
        if ( gpsLocationHistory.Count > gpsHistorySize ) {
            gpsLocationHistory.Dequeue();
        }

        // Average the location history for smoothing
        Vector2 averagedLocation = Vector2.zero;
        foreach ( Vector2 loc in gpsLocationHistory ) {
            averagedLocation += loc;
        }
        averagedLocation /= gpsLocationHistory.Count;

        // Check if this is a significant movement
        float distanceFromLast = Vector2.Distance( averagedLocation, lastStableLocation );

        if ( distanceFromLast >= positionUpdateThreshold ) {
            // Smooth transition to new location
            lastStableLocation = Vector2.Lerp( lastStableLocation, averagedLocation, positionSmoothingFactor );
            Debug.Log( $"📍 GPS moved {distanceFromLast:F2}m, updating to: {lastStableLocation}" );
        }

        return lastStableLocation;
    }

    private void UpdateAnchoredMarkerPositions()
    {
        foreach ( var kvp in markerAnchors ) {
            MarkerAnchor anchor = kvp.Value;

            if ( anchor.markerGameObject == null ) {
                continue;
            }

            // Recalculate world position based on stabilized user location
            Vector3 newWorldPos = GPSToWorldPosition( anchor.nodeLatitude, anchor.nodeLongitude );
            newWorldPos.y += markerHeightOffset;

            // Smooth position update to prevent jitter
            anchor.markerGameObject.transform.position = Vector3.Lerp(
                        anchor.markerGameObject.transform.position,
                        newWorldPos,
                        positionSmoothingFactor
                    );
        }
    }

    private void ReconcileVisibleMarkers()
    {
        if ( currentNodes == null || currentNodes.Count == 0 ) {
            Debug.LogWarning( "❌ No nodes available!" );
            return;
        }

        // Remove markers that are now out of range
        List<string> nodesToRemove = new List<string>();

        foreach ( var kvp in markerAnchors ) {
            MarkerAnchor anchor = kvp.Value;
            Node node = anchor.node;

            if ( !ShouldShowMarker( node ) ) {
                if ( anchor.markerGameObject != null ) {
                    Destroy( anchor.markerGameObject );
                }
                nodesToRemove.Add( kvp.Key );
                Debug.Log( $"🗑️ Removing marker for node {node.node_id} - out of range" );
            }
        }

        foreach ( string nodeId in nodesToRemove ) {
            markerAnchors.Remove( nodeId );
            activeMarkers.RemoveAll( m => m == null );
        }

        // Create markers for nodes now in range but not yet spawned
        int created = 0;
        int alreadyExists = 0;
        int shouldNotShow = 0;

        foreach ( Node node in currentNodes ) {
            if ( markerAnchors.ContainsKey( node.node_id ) ) {
                alreadyExists++;
                continue;
            }

            if ( ShouldShowMarker( node ) ) {
                CreateMarkerForNode( node );
                created++;
            } else {
                shouldNotShow++;
            }
        }

        if ( created > 0 || nodesToRemove.Count > 0 ) {
            Debug.Log( $"📊 Marker Update: Created {created}, Removed {nodesToRemove.Count}, Existing {alreadyExists}, OutOfRange {shouldNotShow}" );
            Debug.Log( $"📊 Total active markers: {markerAnchors.Count}" );
        }
    }

    void UpdateGPSStrengthUI()
    {
        if ( gpsStrengthText != null ) {
            Vector2 coords = GPSManager.Instance.GetCoordinates();
            if ( coords.magnitude > 0 ) {
                gpsStrengthText.text = $"GPS: {coords.x:F5}, {coords.y:F5}";
                gpsStrengthText.color = Color.green;
            } else {
                gpsStrengthText.text = "GPS: No Signal";
                gpsStrengthText.color = Color.red;
            }
        }
    }

    void UpdateDebugInfo()
    {
        if ( debugText != null ) {
            string modeText = currentFeatureMode == ARFeatureMode.DirectAR ? "🗺️ Direct AR" : "🧭 AR Navigation";

            debugText.text = $"{modeText}\n" +
                             $"User: {userLocation.x:F5}, {userLocation.y:F5}\n" +
                             $"Active Markers: {markerAnchors.Count} | History: {gpsLocationHistory.Count}";
        }
    }

    void UpdateLoadingUI( string message )
    {
        if ( loadingText != null ) {
            loadingText.text = message;
            loadingText.gameObject.SetActive( true );
        }
        Debug.Log( $"[Loading] {message}" );
    }

    void HideLoadingUI()
    {
        if ( loadingText != null ) {
            loadingText.gameObject.SetActive( false );
        }
    }

    void ClearMarkers()
    {
        foreach ( var kvp in markerAnchors ) {
            if ( kvp.Value.markerGameObject != null ) {
                Destroy( kvp.Value.markerGameObject );
            }
        }
        markerAnchors.Clear();
        activeMarkers.Clear();
        Debug.Log( "🗑️ All markers cleared" );
    }

    bool ShouldShowMarker( Node node )
    {
        float distance = CalculateDistance( userLocation, new Vector2( node.latitude, node.longitude ) );

        // In Direct AR mode, ignore minimum distance - show everything in max range
        if ( currentFeatureMode == ARFeatureMode.DirectAR ) {
            return distance <= maxVisibleDistance;
        }

        // In Navigation mode, use min/max range
        return distance <= maxVisibleDistance && distance >= minMarkerDistance;
    }

    void CreateMarkerForNode( Node node )
    {
        if ( buildingMarkerPrefab == null ) {
            Debug.LogError( "❌ Building marker prefab is not assigned!" );
            return;
        }

        Infrastructure infra = currentInfrastructures.FirstOrDefault( i => i.infra_id == node.related_infra_id );

        if ( infra == null ) {
            Debug.LogWarning( $"❌ No infrastructure found for node {node.node_id} with infra_id {node.related_infra_id}" );
            return;
        }

        Vector3 worldPosition = GPSToWorldPosition( node.latitude, node.longitude );
        worldPosition.y += markerHeightOffset;

        GameObject marker = Instantiate( buildingMarkerPrefab );
        marker.transform.position = worldPosition;
        marker.transform.localScale = Vector3.one * markerScale;

        UpdateMarkerText( marker, infra, node );

        // Create anchor for this marker - this prevents jittering!
        MarkerAnchor anchor = new MarkerAnchor {
            node = node,
            nodeLatitude = node.latitude,
            nodeLongitude = node.longitude,
            markerGameObject = marker
        };

        markerAnchors[node.node_id] = anchor;
        activeMarkers.Add( marker );

        float distance = CalculateDistance( userLocation, new Vector2( node.latitude, node.longitude ) );
        Debug.Log( $"✅ Created ANCHORED marker for {infra.name} at {worldPosition}, Distance: {distance:F1}m" );
    }

    void UpdateMarkerText( GameObject marker, Infrastructure infra, Node node )
    {
        TextMeshPro textMeshPro = marker.GetComponentInChildren<TextMeshPro>();
        if ( textMeshPro != null ) {
            textMeshPro.text = infra.name;
            StartCoroutine( UpdateTextRotation( textMeshPro.transform ) );
        }

        Text nameText = marker.GetComponentInChildren<Text>();
        if ( nameText != null && textMeshPro == null ) {
            nameText.text = infra.name;
        }
    }

    IEnumerator UpdateTextRotation( Transform textTransform )
    {
        while ( textTransform != null && !isExitingAR ) {
            if ( arCamera != null ) {
                textTransform.LookAt( arCamera.transform );
                textTransform.Rotate( 0, 180, 0 );
            }
            yield return new WaitForSeconds( 0.1f );
        }
    }

    Vector3 GPSToWorldPosition( float latitude, float longitude )
    {
        Vector2 userCoords = userLocation;
        float deltaLat = latitude - userCoords.x;
        float deltaLng = longitude - userCoords.y;

        float meterPerDegree = 111000f;
        float x = deltaLng * meterPerDegree * Mathf.Cos( userCoords.x * Mathf.Deg2Rad );
        float z = deltaLat * meterPerDegree;

        return new Vector3( x, 0, z );
    }

    float CalculateDistance( Vector2 coord1, Vector2 coord2 )
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

    void OnDestroy()
    {
        isExitingAR = true;
        CancelInvoke();
        ClearMarkers();
        StopAllCoroutines();
    }

    // Helper class to anchor markers and prevent jittering/drifting
    private class MarkerAnchor
    {
        public Node node;
        public float nodeLatitude;
        public float nodeLongitude;
        public GameObject markerGameObject;
    }
}