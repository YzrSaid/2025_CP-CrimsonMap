using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using ZXing;
using ZXing.Common;
using Unity.Collections;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class QRScanner : MonoBehaviour
{
    [Header( "AR References" )]
    public ARCameraManager arCameraManager;

    [Header( "UI References" )]
    public TextMeshProUGUI instructionsText;
    public TextMeshProUGUI debugText;
    public Button backButton;
    public GameObject qrFrameContainer;

    [Header( "Confirmation Panel" )]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationText;
    public Button confirmButton;
    public Button tryAgainButton;

    [Header( "Security Settings" )]
    public string qrSignature = "CRIMSON";
    public string qrDelimiter = "_";

    [Header( "Test Mode (Editor Only)" )]
    public bool enableTestMode = false;
    public string testNodeId = "node_001";

    private bool isScanning = false;
    private string scannedNodeId;
    private Node scannedNodeInfo;
    private List<string> availableMapIds = new List<string>();
    private Texture2D cameraImageTexture;
    private int frameCount = 0;
    private int qrDetectCount = 0;

    private IBarcodeReader barcodeReader = new BarcodeReader {
        AutoRotate = false,
        Options = new DecodingOptions
        {
            TryHarder = false
        }
    };

    void Start()
    {
        if ( confirmationPanel != null ) {
            confirmationPanel.SetActive( false );
        }

        if ( instructionsText != null ) {
            instructionsText.text = "Point camera at QR code";
        }

        if ( confirmButton != null ) {
            confirmButton.onClick.AddListener( OnConfirm );
        }

        if ( tryAgainButton != null ) {
            tryAgainButton.onClick.AddListener( OnTryAgain );
        }

        StartCoroutine( InitializeScanner() );
    }

    void Update()
    {
#if UNITY_EDITOR
        if ( enableTestMode && Keyboard.current.spaceKey.wasPressedThisFrame ) {
            string simulatedQRData = qrSignature + qrDelimiter + testNodeId;
            OnQRCodeScanned( simulatedQRData );
        }
#endif
    }

    IEnumerator InitializeScanner()
    {
        yield return StartCoroutine( LoadAvailableMaps() );

        if ( arCameraManager == null ) {
            if ( instructionsText != null ) {
                instructionsText.text = "AR Camera Manager not found!";
                instructionsText.color = Color.red;
            }
            yield break;
        }

        float timeout = 0f;
        while ( !arCameraManager.enabled && timeout < 5f ) {
            timeout += Time.deltaTime;
            yield return null;
        }

        if ( !arCameraManager.enabled ) {
            if ( instructionsText != null ) {
                instructionsText.text = "AR system failed to initialize!";
                instructionsText.color = Color.red;
            }
            yield break;
        }

        arCameraManager.frameReceived += OnCameraFrameReceived;
        isScanning = true;
    }

    IEnumerator LoadAvailableMaps()
    {
        yield return StartCoroutine( CrossPlatformFileLoader.LoadJsonFile(
                                         "maps.json",
        ( jsonContent ) => {
            try {
                MapList mapList = JsonUtility.FromJson<MapList>( "{\"maps\":" + jsonContent + "}" );

                if ( mapList != null && mapList.maps != null && mapList.maps.Count > 0 ) {
                    availableMapIds.Clear();
                    foreach ( var map in mapList.maps ) {
                        availableMapIds.Add( map.map_id );
                    }
                }
            } catch ( Exception ex ) {
                Debug.LogError( "Error loading maps: " + ex.Message );
            }
        },
        ( error ) => {
            Debug.LogError( "Error: " + error );
        }
                                     ) );
    }

    void OnCameraFrameReceived( ARCameraFrameEventArgs eventArgs )
    {
        frameCount++;

        if ( !isScanning || arCameraManager == null )
            return;

        if ( !arCameraManager.TryAcquireLatestCpuImage( out XRCpuImage image ) )
            return;

        try {
            var conversionParams = new XRCpuImage.ConversionParams {
                inputRect = new RectInt( 0, 0, image.width, image.height ),
                outputDimensions = new Vector2Int( image.width / 2, image.height / 2 ),
                outputFormat = TextureFormat.RGBA32,
                transformation = XRCpuImage.Transformation.MirrorY
            };

            int size = image.GetConvertedDataSize( conversionParams );
            var buffer = new NativeArray<byte>( size, Allocator.Temp );

            image.Convert( conversionParams, buffer );
            image.Dispose();

            if ( cameraImageTexture == null ) {
                cameraImageTexture = new Texture2D(
                    conversionParams.outputDimensions.x,
                    conversionParams.outputDimensions.y,
                    conversionParams.outputFormat,
                    false );
            }

            cameraImageTexture.LoadRawTextureData( buffer );
            cameraImageTexture.Apply();
            buffer.Dispose();

            Color32[] pixels = cameraImageTexture.GetPixels32();
            Result result = barcodeReader.Decode( pixels, cameraImageTexture.width, cameraImageTexture.height );

            if ( result != null ) {
                qrDetectCount++;
                if ( debugText != null )
                    debugText.text = $"QR FOUND: {result.Text}";
                OnQRCodeScanned( result.Text );
            } else {
                if ( frameCount % 30 == 0 ) {
                    if ( debugText != null )
                        debugText.text = $"Frames: {frameCount} | Scanning...";
                }
            }
        } catch ( Exception ex ) {
            Debug.LogError( "Error processing frame: " + ex.Message );
        }
    }

    void OnQRCodeScanned( string qrData )
    {
        isScanning = false;

        if ( !ValidateQRCode( qrData, out string nodeId ) ) {
            ShowError( "Invalid QR code. Please scan a valid CRIMSON campus location QR code." );
            return;
        }

        scannedNodeId = nodeId;
        StartCoroutine( SearchNodeInLocalFiles( scannedNodeId ) );
    }

    bool ValidateQRCode( string qrData, out string nodeId )
    {
        nodeId = null;

        if ( !qrData.Contains( qrDelimiter ) )
            return false;

        string[] parts = qrData.Split( new string[] { qrDelimiter }, StringSplitOptions.None );

        if ( parts.Length != 2 )
            return false;

        string signature = parts[0];
        if ( signature != qrSignature )
            return false;

        nodeId = parts[1];

        if ( string.IsNullOrEmpty( nodeId ) || nodeId.Length < 3 )
            return false;

        return true;
    }

    IEnumerator SearchNodeInLocalFiles( string nodeId )
    {
        if ( instructionsText != null ) {
            instructionsText.text = "Searching for location...";
        }

        bool foundNode = false;

        if ( availableMapIds.Count > 0 ) {
            foreach ( string mapId in availableMapIds ) {
                string nodesFileName = $"nodes_{mapId}.json";
                bool searchComplete = false;

                yield return StartCoroutine( CrossPlatformFileLoader.LoadJsonFile(
                                                 nodesFileName,
                ( jsonContent ) => {
                    Node foundNodeInfo = SearchNodeInJson( jsonContent, nodeId );
                    if ( foundNodeInfo != null ) {
                        scannedNodeInfo = foundNodeInfo;
                        foundNode = true;
                    }
                    searchComplete = true;
                },
                ( error ) => {
                    searchComplete = true;
                }
                                             ) );

                while ( !searchComplete )
                    yield return null;

                if ( foundNode )
                    break;
            }
        }

        if ( foundNode ) {
            ShowConfirmation();
        } else {
            ShowError( "Location not found. This QR code may not be registered in the system." );
        }
    }

    Node SearchNodeInJson( string jsonContent, string nodeId )
    {
        try {
            NodeList nodeList = JsonUtility.FromJson<NodeList>( "{\"nodes\":" + jsonContent + "}" );

            if ( nodeList != null && nodeList.nodes != null && nodeList.nodes.Count > 0 ) {
                Node foundNode = nodeList.nodes.FirstOrDefault( n => n.node_id == nodeId );
                return foundNode;
            }
        } catch ( Exception ex ) {
            Debug.LogError( "Error searching node: " + ex.Message );
        }

        return null;
    }

    void ShowConfirmation()
    {
        if ( instructionsText != null ) {
            instructionsText.gameObject.SetActive( false );
        }

        if ( qrFrameContainer != null ) {
            qrFrameContainer.SetActive( false );
        }

        if ( confirmationPanel != null ) {
            confirmationPanel.SetActive( true );

            CanvasGroup canvasGroup = confirmationPanel.GetComponent<CanvasGroup>();
            if ( canvasGroup == null ) {
                canvasGroup = confirmationPanel.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 0;
            canvasGroup.DOFade( 1, 0.5f ).SetEase( Ease.OutQuad );
        }

        if ( backButton != null ) {
            backButton.gameObject.SetActive( false );
        }

        if ( confirmationText != null ) {
            confirmationText.text = $"The QR code you scanned is referring to [{scannedNodeInfo.name}], use this as your current location?";
        }
    }

    void ShowError( string errorMessage )
    {
        if ( instructionsText != null ) {
            instructionsText.text = errorMessage;
            instructionsText.color = Color.red;
        }

        Invoke( "OnTryAgain", 2f );
    }

    void OnConfirm()
    {
        PlayerPrefs.SetString( "ScannedNodeID", scannedNodeInfo.node_id );
        PlayerPrefs.SetString( "ScannedLocationName", scannedNodeInfo.name );
        PlayerPrefs.SetFloat( "ScannedLat", scannedNodeInfo.latitude );
        PlayerPrefs.SetFloat( "ScannedLng", scannedNodeInfo.longitude );
        PlayerPrefs.SetString( "ScannedCampusID", scannedNodeInfo.campus_id );
        PlayerPrefs.SetFloat( "ScannedX", scannedNodeInfo.x_coordinate );
        PlayerPrefs.SetFloat( "ScannedY", scannedNodeInfo.y_coordinate );
        PlayerPrefs.Save();

        SceneTransitionWithoutLoading.GoToTargetSceneSimple( "MainAppScene" );
    }

    void OnTryAgain()
    {
        if ( confirmationPanel != null ) {
            CanvasGroup canvasGroup = confirmationPanel.GetComponent<CanvasGroup>();
            if ( canvasGroup == null ) {
                canvasGroup = confirmationPanel.AddComponent<CanvasGroup>();
            }

            canvasGroup.DOFade( 0, 0.5f ).SetEase( Ease.OutQuad ).OnComplete( () => {
                confirmationPanel.SetActive( false );
            } );
        }

        if ( instructionsText != null ) {
            instructionsText.color = Color.white;
            instructionsText.gameObject.SetActive( true );
            instructionsText.text = "Point camera at QR code";
        }

        if ( qrFrameContainer != null ) {
            qrFrameContainer.SetActive( true );
        }

        if ( backButton != null ) {
            backButton.gameObject.SetActive( true );
        }

        isScanning = true;
    }

    void StopScanning()
    {
        isScanning = false;

        if ( arCameraManager != null ) {
            arCameraManager.frameReceived -= OnCameraFrameReceived;
        }
    }

    void OnDestroy()
    {
        StopScanning();

        if ( cameraImageTexture != null ) {
            Destroy( cameraImageTexture );
        }
    }
}