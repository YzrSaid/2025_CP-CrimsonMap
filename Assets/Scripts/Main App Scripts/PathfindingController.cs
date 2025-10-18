using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public class PathfindingController : MonoBehaviour
{
    [Header( "References" )]
    public AStarPathfinding pathfinding;
    public InfrastructurePopulator infrastructurePopulator;
    public GPSManager gpsManager;

    [Header( "UI Elements" )]
    public TMP_Dropdown toDropdown;
    public Button findPathButton;

    [Header( "Location Lock Display" )]
    public GameObject locationLockDisplay;
    public TextMeshProUGUI locationLockText;
    public Button locationLockButton;

    [Header( "Confirmation Panel" )]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmFromText;
    public TextMeshProUGUI confirmToText;
    public TextMeshProUGUI confirmErrorText;
    public Button confirmButton;
    public Button cancelButton;

    [Header( "Location Conflict Panel" )]
    public GameObject locationConflictPanel;
    public TextMeshProUGUI conflictMessageText;
    public Button conflictConfirmButton;
    public Button conflictCancelButton;

    [Header( "Result Display" )]
    public GameObject resultPanel;
    public GameObject destinationPanel;
    public TextMeshProUGUI fromText;
    public TextMeshProUGUI toText;

    [Header( "Route List" )]
    public Transform routeListContainer;
    public GameObject routeItemPrefab;
    public ScrollRect routeScrollView;
    public Button confirmRouteButton;

    [Header( "Static Test Settings" )]
    public bool useStaticTesting = false;
    public string staticFromNodeId = "ND-025";
    public string staticToNodeId = "ND-017";

    [Header( "GPS Settings" )]
    public bool useGPSForFromLocation = true;
    public float nearestNodeSearchRadius = 500f;
    public bool autoUpdateGPSLocation = true;
    public float gpsUpdateInterval = 5f;
    public float qrConflictThresholdMeters = 100f;

    [Header( "Settings" )]
    public bool enableDebugLogs = true;

    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>();
    private Dictionary<string, string> infraIdToNodeId = new Dictionary<string, string>();
    private string selectedFromNodeId;
    private string selectedToNodeId;
    private Node currentNearestNode;
    private Node qrScannedNode;
    private bool isQRLocationLocked = false;
    private bool hasShownConflictPanel = false;
    private float lastGPSUpdateTime;
    private bool nodesLoaded = false;

    private string currentMapId;
    private List<string> currentCampusIds;

    private List<RouteData> currentRoutes = new List<RouteData>();
    private List<RouteItem> routeItemInstances = new List<RouteItem>();
    private int selectedRouteIndex = -1;

    void Start()
    {
        if ( findPathButton != null ) {
            findPathButton.onClick.AddListener( OnFindPathClicked );
        }

        if ( confirmButton != null ) {
            confirmButton.onClick.AddListener( OnConfirmClicked );
        }

        if ( cancelButton != null ) {
            cancelButton.onClick.AddListener( OnCancelClicked );
        }

        if ( toDropdown != null ) {
            toDropdown.onValueChanged.AddListener( OnToDropdownChanged );
        }
        if ( locationLockButton != null ) {
            locationLockButton.onClick.AddListener( UnlockFromQR );
        }

        if ( confirmRouteButton != null ) {
            confirmRouteButton.onClick.AddListener( OnConfirmRouteClicked );
            confirmRouteButton.gameObject.SetActive( false );
        }

        if ( conflictConfirmButton != null ) {
            conflictConfirmButton.onClick.AddListener( OnLocationConflictConfirm );
        }

        if ( conflictCancelButton != null ) {
            conflictCancelButton.onClick.AddListener( OnLocationConflictCancel );
        }

        if ( resultPanel != null ) {
            resultPanel.SetActive( false );
        }

        if ( destinationPanel != null ) {
            destinationPanel.SetActive( false );
        }

        if ( confirmationPanel != null ) {
            confirmationPanel.SetActive( false );
        }

        if ( locationConflictPanel != null ) {
            locationConflictPanel.SetActive( false );
        }

        if ( MapManager.Instance != null ) {
            MapManager.Instance.OnMapChanged += OnMapChanged;
        }

        if ( gpsManager == null ) {
            gpsManager = GPSManager.Instance;
        }
    }

    void OnDestroy()
    {
        if ( findPathButton != null )
            findPathButton.onClick.RemoveListener( OnFindPathClicked );
        if ( confirmButton != null )
            confirmButton.onClick.RemoveListener( OnConfirmClicked );
        if ( cancelButton != null )
            cancelButton.onClick.RemoveListener( OnCancelClicked );
        if ( toDropdown != null )
            toDropdown.onValueChanged.RemoveListener( OnToDropdownChanged );
        if ( confirmRouteButton != null )
            confirmRouteButton.onClick.RemoveListener( OnConfirmRouteClicked );
        if ( conflictConfirmButton != null )
            conflictConfirmButton.onClick.RemoveListener( OnLocationConflictConfirm );
        if ( conflictCancelButton != null )
            conflictCancelButton.onClick.RemoveListener( OnLocationConflictCancel );
        if ( MapManager.Instance != null )
            MapManager.Instance.OnMapChanged -= OnMapChanged;

        ClearQRData();
    }

    void Update()
    {
        if ( useGPSForFromLocation && autoUpdateGPSLocation && !useStaticTesting && nodesLoaded ) {
            if ( Time.time - lastGPSUpdateTime >= gpsUpdateInterval ) {
                UpdateFromLocationByGPS();
                lastGPSUpdateTime = Time.time;
            }
        }
    }

    #region QR Data Handling

    private void CheckForScannedQRData()
    {
        string scannedNodeId = PlayerPrefs.GetString( "ScannedNodeID", "" );

        if ( !string.IsNullOrEmpty( scannedNodeId ) && nodesLoaded ) {
            LoadQRScannedNode( scannedNodeId );
        }
    }

    private void LoadQRScannedNode( string nodeId )
    {
        if ( allNodes.TryGetValue( nodeId, out Node node ) ) {
            qrScannedNode = node;
            selectedFromNodeId = nodeId;
            isQRLocationLocked = true;
            hasShownConflictPanel = false;

            if ( locationLockDisplay != null ) {
                locationLockDisplay.SetActive( true );

                if ( locationLockText != null ) {
                    locationLockText.text = $"{node.name} 🔒";
                }
            }

            if ( enableDebugLogs ) {
                Debug.Log( $"QR scanned node loaded: {node.name} ({nodeId}) - Location LOCKED 🔒" );
            }
        } else {
            Debug.LogWarning( $"Scanned node ID {nodeId} not found in loaded nodes" );
        }
    }

    public void ClearQRData()
    {
        PlayerPrefs.DeleteKey( "ScannedNodeID" );
        PlayerPrefs.DeleteKey( "ScannedLocationName" );
        PlayerPrefs.DeleteKey( "ScannedLat" );
        PlayerPrefs.DeleteKey( "ScannedLng" );
        PlayerPrefs.DeleteKey( "ScannedCampusID" );
        PlayerPrefs.DeleteKey( "ScannedX" );
        PlayerPrefs.DeleteKey( "ScannedY" );
        PlayerPrefs.Save();

        qrScannedNode = null;
        isQRLocationLocked = false;
    }

    public void UnlockFromQR()
    {
        isQRLocationLocked = false;
        qrScannedNode = null;

        if ( locationLockDisplay != null ) {
            locationLockDisplay.SetActive( false );
        }

        UpdateFromLocationByGPS();

        ClearQRData();

        Debug.Log( "YAWA KA BAI" );
    }

    #endregion

    #region MapManager Integration

    private void OnMapChanged( MapInfo mapInfo )
    {
        ClearCurrentPath();
    }

    public IEnumerator InitializeForMap( string mapId, List<string> campusIds )
    {
        currentMapId = mapId;
        currentCampusIds = campusIds;

        yield return StartCoroutine( LoadNodesFromJSON( mapId ) );

        yield return StartCoroutine( BuildInfrastructureNodeMapping() );

        if ( nodesLoaded ) {
            CheckForScannedQRData();
        }

        if ( useGPSForFromLocation && !useStaticTesting ) {
            UpdateFromLocationByGPS();
        }

        if ( pathfinding != null ) {
            yield return StartCoroutine( pathfinding.LoadGraphDataForMap( mapId, campusIds ) );
        }
    }

    #endregion

    #region Node Loading from JSON

    private IEnumerator LoadNodesFromJSON( string mapId )
    {
        string fileName = $"nodes_{mapId}.json";
        bool loadSuccess = false;
        string errorMsg = "";

        yield return StartCoroutine( CrossPlatformFileLoader.LoadJsonFile(
                                         fileName,
        ( jsonContent ) => {
            try {
                Node[] nodesArray = JsonHelper.FromJson<Node>( jsonContent );

                allNodes.Clear();
                foreach ( Node node in nodesArray ) {
                    allNodes[node.node_id] = node;
                }

                nodesLoaded = true;
                loadSuccess = true;
            } catch ( System.Exception e ) {
                errorMsg = $"Error parsing nodes JSON: {e.Message}";
            }
        },
        ( error ) => {
            errorMsg = $"Failed to load {fileName}: {error}";
        }
                                     ) );

        yield return null;
    }

    #endregion

    #region Infrastructure to Node Mapping

    private IEnumerator BuildInfrastructureNodeMapping()
    {
        if ( allNodes == null || allNodes.Count == 0 ) {
            yield break;
        }

        infraIdToNodeId.Clear();

        foreach ( var kvp in allNodes ) {
            Node node = kvp.Value;

            if ( node.type == "infrastructure" && !string.IsNullOrEmpty( node.related_infra_id ) ) {
                infraIdToNodeId[node.related_infra_id] = node.node_id;
            }
        }

        yield return null;
    }

    #endregion

    #region GPS Location Handling

    private void UpdateFromLocationByGPS()
    {
        if ( gpsManager == null ) {
            return;
        }

        if ( !nodesLoaded || allNodes == null || allNodes.Count == 0 ) {
            return;
        }

        Vector2 coords = gpsManager.GetSmoothedCoordinates();

        Node nearestNode = FindNearestNode( coords.x, coords.y );

        if ( nearestNode != null ) {
            // If QR location is locked, check for conflict
            if ( isQRLocationLocked && qrScannedNode != null ) {
                float distanceFromQR = CalculateDistance(
                                           qrScannedNode.latitude, qrScannedNode.longitude,
                                           nearestNode.latitude, nearestNode.longitude
                                       );

                if ( distanceFromQR > qrConflictThresholdMeters ) {
                    // Only show panel once per QR lock
                    if ( !hasShownConflictPanel ) {
                        hasShownConflictPanel = true;
                        ShowLocationConflictPanel( qrScannedNode, nearestNode, distanceFromQR );
                    }
                    return; // Don't update location, stay locked
                }
            } else {
                // No QR lock - update normally
                selectedFromNodeId = nearestNode.node_id;
                currentNearestNode = nearestNode;
            }
        }
    }

    private Node FindNearestNode( float latitude, float longitude )
    {
        if ( allNodes == null || allNodes.Count == 0 ) {
            return null;
        }

        Node nearestNode = null;
        float nearestDistance = float.MaxValue;

        foreach ( var kvp in allNodes ) {
            Node node = kvp.Value;

            float distance = CalculateDistance( latitude, longitude, node.latitude, node.longitude );

            if ( distance < nearestDistance && distance <= nearestNodeSearchRadius ) {
                nearestDistance = distance;
                nearestNode = node;
            }
        }

        return nearestNode;
    }

    private float CalculateDistance( float lat1, float lon1, float lat2, float lon2 )
    {
        const float R = 6371000f;

        float dLat = ( lat2 - lat1 ) * Mathf.Deg2Rad;
        float dLon = ( lon2 - lon1 ) * Mathf.Deg2Rad;

        float a = Mathf.Sin( dLat / 2 ) * Mathf.Sin( dLat / 2 ) +
                  Mathf.Cos( lat1 * Mathf.Deg2Rad ) * Mathf.Cos( lat2 * Mathf.Deg2Rad ) *
                  Mathf.Sin( dLon / 2 ) * Mathf.Sin( dLon / 2 );

        float c = 2 * Mathf.Atan2( Mathf.Sqrt( a ), Mathf.Sqrt( 1 - a ) );

        return R * c;
    }

    #endregion

    #region Location Conflict Panel

    private void ShowLocationConflictPanel( Node qrNode, Node gpsNode, float distanceMeters )
    {
        if ( locationConflictPanel == null ) {
            return;
        }

        if ( conflictMessageText != null ) {
            conflictMessageText.text =
                $"Your location has changed!\n\n" +
                $"<b>QR Location:</b> {qrNode.name}\n" +
                $"<b>Current GPS:</b> {gpsNode.name}\n" +
                $"<b>Distance:</b> {distanceMeters:F0}m apart\n\n" +
                $"Would you like to update to your current GPS location?";
        }

        locationConflictPanel.SetActive( true );

        if ( enableDebugLogs ) {
            Debug.Log( $"⚠️ Location conflict: {distanceMeters:F0}m between QR ({qrNode.name}) and GPS ({gpsNode.name})" );
        }
    }

    private void OnLocationConflictConfirm()
    {
        if ( locationConflictPanel != null ) {
            locationConflictPanel.SetActive( false );
        }

        // User chose to use GPS - unlock from QR
        UnlockFromQR();

        if ( enableDebugLogs ) {
            Debug.Log( "User chose GPS - location unlocked" );
        }
    }

    private void OnLocationConflictCancel()
    {
        if ( locationConflictPanel != null ) {
            locationConflictPanel.SetActive( false );
        }

        if ( enableDebugLogs ) {
            Debug.Log( "User chose to stay with QR location" );
        }
    }

    #endregion

    #region Dropdown Handlers

    private void OnToDropdownChanged( int index )
    {
        if ( infrastructurePopulator == null || toDropdown == null ) {
            return;
        }

        Infrastructure selectedInfra = infrastructurePopulator.GetSelectedInfrastructure( toDropdown );

        if ( selectedInfra == null ) {
            selectedToNodeId = null;
            return;
        }

        if ( infraIdToNodeId.TryGetValue( selectedInfra.infra_id, out string nodeId ) ) {
            selectedToNodeId = nodeId;
        } else {
            selectedToNodeId = null;
        }
    }

    #endregion

    #region Confirmation Panel

    private void OnFindPathClicked()
    {
        string fromNodeId;
        string toNodeId;

        if ( useStaticTesting ) {
            fromNodeId = staticFromNodeId;
            toNodeId = staticToNodeId;
        } else {
            UpdateFromLocationByGPS();
            fromNodeId = selectedFromNodeId;
            toNodeId = selectedToNodeId;
        }

        if ( string.IsNullOrEmpty( toNodeId ) ) {
            ShowConfirmationError( "Please select a destination" );
            return;
        }

        if ( string.IsNullOrEmpty( fromNodeId ) ) {
            ShowConfirmationError( "Cannot determine your location. Please check GPS." );
            return;
        }

        if ( fromNodeId == toNodeId ) {
            ShowConfirmationError( "You are already at this location!" );
            return;
        }

        ShowConfirmationPanel( fromNodeId, toNodeId );
    }

    private void ShowConfirmationPanel( string fromNodeId, string toNodeId )
    {
        if ( confirmationPanel != null ) {
            confirmationPanel.SetActive( true );
        }

        if ( allNodes.TryGetValue( fromNodeId, out Node fromNode ) ) {
            if ( confirmFromText != null ) {
                string lockIndicator = isQRLocationLocked ? " 🔒" : "";
                confirmFromText.text = $"<b>From:</b> {fromNode.name}{lockIndicator}";
            }
        }

        if ( allNodes.TryGetValue( toNodeId, out Node toNode ) ) {
            if ( confirmToText != null ) {
                confirmToText.text = $"<b>To:</b> {toNode.name}";
            }
        }

        if ( confirmErrorText != null ) {
            confirmErrorText.text = "";
        }
    }

    private void ShowConfirmationError( string message )
    {
        if ( confirmationPanel != null ) {
            confirmationPanel.transform.Find( "Title" ).GetComponent<TextMeshProUGUI>().text = "Error";
            cancelButton.GetComponentInChildren<TextMeshProUGUI>().text = "Close";
            confirmationPanel.SetActive( true );
            confirmButton.gameObject.SetActive( false );
            confirmErrorText.gameObject.SetActive( true );
        }

        if ( confirmFromText != null ) {
            confirmFromText.text = "";
        }

        if ( confirmToText != null ) {
            confirmToText.text = "";
        }

        if ( confirmErrorText != null ) {
            confirmErrorText.text = message;
        }
    }

    private void OnConfirmClicked()
    {
        string fromNodeId;
        string toNodeId;

        if ( useStaticTesting ) {
            fromNodeId = staticFromNodeId;
            toNodeId = staticToNodeId;
        } else {
            fromNodeId = selectedFromNodeId;
            toNodeId = selectedToNodeId;
        }

        if ( confirmationPanel != null ) {
            confirmationPanel.SetActive( false );
        }

        StartCoroutine( FindAndDisplayPaths( fromNodeId, toNodeId ) );
    }

    private void OnCancelClicked()
    {
        if ( confirmationPanel != null ) {
            confirmationPanel.SetActive( false );
        }
    }

    #endregion

    #region Pathfinding Trigger

    private IEnumerator FindAndDisplayPaths( string fromNodeId, string toNodeId )
    {
        if ( pathfinding == null ) {
            yield break;
        }

        if ( findPathButton != null ) {
            findPathButton.interactable = false;
        }

        yield return StartCoroutine( pathfinding.FindMultiplePaths( fromNodeId, toNodeId, 3 ) );

        if ( findPathButton != null ) {
            findPathButton.interactable = true;
        }

        var routes = pathfinding.GetAllRoutes();

        if ( routes == null || routes.Count == 0 ) {
            ShowConfirmationError( "No path found between these locations" );
            yield break;
        }

        currentRoutes = routes;
        DisplayAllRoutes();
    }

    #endregion

    #region Result Display

    private void DisplayAllRoutes()
    {
        if ( resultPanel != null && destinationPanel != null ) {
            resultPanel.SetActive( true );
            destinationPanel.SetActive( true );
        }

        ClearRouteItems();

        if ( currentRoutes.Count == 0 ) {
            return;
        }

        var firstRoute = currentRoutes[0];

        if ( fromText != null ) {
            string lockIndicator = isQRLocationLocked ? " 🔒" : "";
            fromText.text = $"<b>From:</b> {firstRoute.startNode.name}{lockIndicator}";
        }

        if ( toText != null ) {
            toText.text = $"<b>To:</b> {firstRoute.endNode.name}";
        }

        for ( int i = 0; i < currentRoutes.Count; i++ ) {
            CreateRouteItem( i, currentRoutes[i] );
        }

        if ( currentRoutes.Count > 0 ) {
            OnRouteSelected( 0 );
        } else {
            if ( confirmRouteButton != null ) {
                confirmRouteButton.gameObject.SetActive( false );
            }
        }

        if ( routeScrollView != null ) {
            Canvas.ForceUpdateCanvases();
            routeScrollView.verticalNormalizedPosition = 1f;
        }
    }

    private void CreateRouteItem( int index, RouteData routeData )
    {
        if ( routeItemPrefab == null || routeListContainer == null ) {
            return;
        }

        GameObject itemObj = Instantiate( routeItemPrefab, routeListContainer );
        RouteItem routeItem = itemObj.GetComponent<RouteItem>();

        if ( routeItem != null ) {
            routeItem.Initialize( index, routeData, OnRouteSelected );
            routeItemInstances.Add( routeItem );
        }
    }

    private void ClearRouteItems()
    {
        if ( routeListContainer == null ) {
            return;
        }

        routeItemInstances.Clear();

        foreach ( Transform child in routeListContainer ) {
            Destroy( child.gameObject );
        }
    }

    private void OnRouteSelected( int routeIndex )
    {
        if ( routeIndex < 0 || routeIndex >= currentRoutes.Count ) {
            return;
        }

        selectedRouteIndex = routeIndex;

        for ( int i = 0; i < routeItemInstances.Count; i++ ) {
            routeItemInstances[i].SetSelected( i == routeIndex );
        }

        if ( confirmRouteButton != null ) {
            confirmRouteButton.gameObject.SetActive( true );
        }

        if ( pathfinding != null ) {
            pathfinding.SetActiveRoute( routeIndex );
        }
    }

    private void OnConfirmRouteClicked()
    {
        if ( selectedRouteIndex < 0 || selectedRouteIndex >= currentRoutes.Count ) {
            return;
        }

        RouteData selectedRoute = currentRoutes[selectedRouteIndex];
    }

    public void HideResults()
    {
        if ( resultPanel != null ) {
            resultPanel.SetActive( false );
        }

        if ( destinationPanel != null ) {
            destinationPanel.SetActive( false );
        }

        ClearRouteItems();
    }

    public void ClearCurrentPath()
    {
        selectedToNodeId = null;
        currentRoutes.Clear();
        routeItemInstances.Clear();
        selectedRouteIndex = -1;

        if ( confirmRouteButton != null ) {
            confirmRouteButton.gameObject.SetActive( false );
        }

        if ( pathfinding != null ) {
            pathfinding.ClearCurrentPath();
        }

        HideResults();
    }

    #endregion

    #region Public Methods for External Integration

    public void RefreshGPSLocation()
    {
        if ( useGPSForFromLocation && !useStaticTesting && nodesLoaded ) {
            UpdateFromLocationByGPS();
        }
    }

    public void SetFromLocationByQR( string nodeId )
    {
        if ( allNodes.ContainsKey( nodeId ) ) {
            selectedFromNodeId = nodeId;
            currentNearestNode = allNodes[nodeId];
        }
    }

    public void ToggleGPSMode( bool useGPS )
    {
        useGPSForFromLocation = useGPS;

        if ( useGPS && nodesLoaded ) {
            UpdateFromLocationByGPS();
        }
    }

    public RouteData GetSelectedRoute()
    {
        if ( selectedRouteIndex >= 0 && selectedRouteIndex < currentRoutes.Count ) {
            return currentRoutes[selectedRouteIndex];
        }
        return null;
    }

    public List<RouteData> GetAllRoutes()
    {
        return new List<RouteData>( currentRoutes );
    }

    public bool IsLocationLocked()
    {
        return isQRLocationLocked;
    }

    #endregion

    #region Utility Methods

    public string GetCurrentFromLocationName()
    {
        if ( currentNearestNode != null ) {
            return currentNearestNode.name;
        }
        return "Unknown";
    }

    public bool IsReadyForPathfinding()
    {
        return nodesLoaded &&
               !string.IsNullOrEmpty( selectedFromNodeId ) &&
               !string.IsNullOrEmpty( selectedToNodeId );
    }

    #endregion
}