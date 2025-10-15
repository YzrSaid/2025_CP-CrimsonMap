using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using ZXing;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class QRScanner : MonoBehaviour
{
    [Header("UI References")]
    public RawImage cameraView;
    public TextMeshProUGUI instructionsText;
    public Button backButton;

    [Header("Confirmation Panel")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationText;
    public Button confirmButton;
    public Button tryAgainButton;

    [Header("Security Settings")]
    public string qrSignature = "CRIMSON";
    public string qrDelimiter = "_";

    private WebCamTexture webcamTexture;
    private bool isScanning = false;
    private string scannedNodeId;
    private Node scannedNodeInfo;
    private List<string> availableMapIds = new List<string>();

    void Start()
    {
        // Hide confirmation panel initially
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }

        // Set initial instruction text
        if (instructionsText != null)
        {
            instructionsText.text = "Point camera at QR code";
        }

        // Setup buttons
        if (backButton != null)
        {
            backButton.onClick.AddListener(GoBack);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirm);
        }

        if (tryAgainButton != null)
        {
            tryAgainButton.onClick.AddListener(OnTryAgain);
        }

        // Load available maps first, then start scanning
        StartCoroutine(InitializeScanner());
    }

    IEnumerator InitializeScanner()
    {
        // Load maps.json to get available map IDs
        yield return StartCoroutine(LoadAvailableMaps());

        // Start scanning after maps are loaded
        StartScanning();
    }

    IEnumerator LoadAvailableMaps()
    {
        bool mapsLoaded = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "maps.json",
            (jsonContent) =>
            {
                try
                {
                    // Parse maps using MapList wrapper
                    MapList mapList = JsonUtility.FromJson<MapList>("{\"maps\":" + jsonContent + "}");

                    if (mapList != null && mapList.maps != null && mapList.maps.Count > 0)
                    {
                        availableMapIds.Clear();
                        foreach (var map in mapList.maps)
                        {
                            availableMapIds.Add(map.map_id);
                            Debug.Log($"Loaded map: {map.map_name} ({map.map_id})");
                        }
                        mapsLoaded = true;
                        Debug.Log($"Total maps loaded: {availableMapIds.Count}");
                    }
                    else
                    {
                        Debug.LogWarning("No maps found in maps.json");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing maps.json: {ex.Message}");
                }
            },
            (error) =>
            {
                Debug.LogError($"Failed to load maps.json: {error}");
            }
        ));

        if (!mapsLoaded)
        {
            Debug.LogWarning("Maps not loaded, will search all nodes*.json files");
        }
    }

    public void StartScanning()
    {
        // Request camera permission (especially for mobile)
        if (Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            InitializeCamera();
        }
        else
        {
            StartCoroutine(RequestCameraPermission());
        }
    }

    IEnumerator RequestCameraPermission()
    {
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

        if (Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            InitializeCamera();
        }
        else
        {
            if (instructionsText != null)
            {
                instructionsText.text = "Camera permission denied!";
            }
            Debug.LogError("Camera permission denied");
        }
    }

    void InitializeCamera()
    {
        if (webcamTexture != null)
        {
            webcamTexture.Stop();
        }

        // Get available cameras
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length == 0)
        {
            if (instructionsText != null)
            {
                instructionsText.text = "No camera found!";
            }
            Debug.LogError("No camera devices found");
            return;
        }

        // Use back camera if available (for mobile)
        string deviceName = devices[0].name;
        for (int i = 0; i < devices.Length; i++)
        {
            if (!devices[i].isFrontFacing)
            {
                deviceName = devices[i].name;
                break;
            }
        }

        // Initialize webcam
        webcamTexture = new WebCamTexture(deviceName);

        if (cameraView != null)
        {
            cameraView.texture = webcamTexture;
        }

        webcamTexture.Play();

        isScanning = true;

        // Start scanning loop
        InvokeRepeating("ScanQRCode", 0.5f, 0.5f);

        Debug.Log("Camera started: " + deviceName);
    }

    void ScanQRCode()
    {
        if (!isScanning || webcamTexture == null || !webcamTexture.isPlaying)
            return;

        try
        {
            // Create barcode reader
            IBarcodeReader barcodeReader = new BarcodeReader();

            // Decode the current camera frame
            var result = barcodeReader.Decode(webcamTexture.GetPixels32(),
                                              webcamTexture.width,
                                              webcamTexture.height);

            if (result != null)
            {
                // QR Code detected!
                OnQRCodeScanned(result.Text);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("QR Scan Error: " + ex.Message);
        }
    }

    void OnQRCodeScanned(string qrData)
    {
        Debug.Log("QR Code Scanned: " + qrData);

        // Stop scanning to prevent multiple reads
        isScanning = false;
        CancelInvoke("ScanQRCode");

        // Validate QR code format and signature
        if (!ValidateQRCode(qrData, out string nodeId))
        {
            ShowError("Invalid QR code. Please scan a valid CRIMSON campus location QR code.");
            return;
        }

        // Parse and search for node
        scannedNodeId = nodeId; // Just the node ID (e.g., "ND-001")
        StartCoroutine(SearchNodeInLocalFiles(scannedNodeId));
    }

    bool ValidateQRCode(string qrData, out string nodeId)
    {
        nodeId = null;

        // Check if QR code contains the delimiter
        if (!qrData.Contains(qrDelimiter))
        {
            Debug.LogWarning($"QR code doesn't contain delimiter '{qrDelimiter}': {qrData}");
            return false;
        }

        // Split by delimiter
        string[] parts = qrData.Split(new string[] { qrDelimiter }, StringSplitOptions.None);

        if (parts.Length != 2)
        {
            Debug.LogWarning($"QR code has invalid format. Expected format: {qrSignature}{qrDelimiter}ND-XXX");
            return false;
        }

        // Verify signature
        string signature = parts[0];
        if (signature != qrSignature)
        {
            Debug.LogWarning($"QR code signature mismatch. Expected '{qrSignature}', got '{signature}'");
            return false;
        }

        // Extract node ID
        nodeId = parts[1];

        // Basic validation of node ID format
        if (string.IsNullOrEmpty(nodeId) || nodeId.Length < 3)
        {
            Debug.LogWarning("Node ID is too short or empty");
            return false;
        }

        Debug.Log($"QR code validated successfully. Signature: {signature}, Node ID: {nodeId}");
        return true;
    }

    IEnumerator SearchNodeInLocalFiles(string nodeId)
    {
        if (instructionsText != null)
        {
            instructionsText.text = "Searching for location...";
        }

        bool foundNode = false;

        // Search through nodes files for each available map
        if (availableMapIds.Count > 0)
        {
            // Search using known map IDs
            foreach (string mapId in availableMapIds)
            {
                string nodesFileName = $"nodes_{mapId}.json";

                Debug.Log($"Searching in {nodesFileName}...");

                bool searchComplete = false;

                yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
                    nodesFileName,
                    (jsonContent) =>
                    {
                        Node foundNodeInfo = SearchNodeInJson(jsonContent, nodeId);
                        if (foundNodeInfo != null)
                        {
                            scannedNodeInfo = foundNodeInfo;
                            foundNode = true;
                            Debug.Log($"Found node in {nodesFileName}: {foundNodeInfo.name}");
                        }
                        searchComplete = true;
                    },
                    (error) =>
                    {
                        Debug.LogWarning($"Could not load {nodesFileName}: {error}");
                        searchComplete = true;
                    }
                ));

                // Wait for search to complete
                while (!searchComplete)
                {
                    yield return null;
                }

                // If found, stop searching
                if (foundNode)
                    break;
            }
        }
        else
        {
            // Fallback: Search all nodes*.json files in the directory
            Debug.LogWarning("No map IDs available, searching all nodes files...");
            yield return StartCoroutine(SearchAllNodesFiles(nodeId, (found, nodeInfo) =>
            {
                foundNode = found;
                if (found)
                {
                    scannedNodeInfo = nodeInfo;
                }
            }));
        }

        // Show result
        if (foundNode)
        {
            ShowConfirmation();
        }
        else
        {
            ShowError("Location not found. This QR code may not be registered in the system.");
        }
    }

    IEnumerator SearchAllNodesFiles(string nodeId, Action<bool, Node> callback)
    {
        string dataPath = Application.isEditor ? Application.streamingAssetsPath : Application.persistentDataPath;
        DirectoryInfo dirInfo = new DirectoryInfo(dataPath);

        if (!dirInfo.Exists)
        {
            Debug.LogError($"Directory does not exist: {dataPath}");
            callback?.Invoke(false, null);
            yield break;
        }

        FileInfo[] jsonFiles = dirInfo.GetFiles("nodes*.json");
        Debug.Log($"Found {jsonFiles.Length} node files to search");

        foreach (FileInfo file in jsonFiles)
        {
            bool searchComplete = false;
            Node foundNodeInfo = null;

            yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
                file.Name,
                (jsonContent) =>
                {
                    foundNodeInfo = SearchNodeInJson(jsonContent, nodeId);
                    searchComplete = true;
                },
                (error) =>
                {
                    Debug.LogError($"Error reading {file.Name}: {error}");
                    searchComplete = true;
                }
            ));

            while (!searchComplete)
            {
                yield return null;
            }

            if (foundNodeInfo != null)
            {
                callback?.Invoke(true, foundNodeInfo);
                yield break;
            }
        }

        callback?.Invoke(false, null);
    }

    Node SearchNodeInJson(string jsonContent, string nodeId)
    {
        try
        {
            NodeList nodeList = JsonUtility.FromJson<NodeList>("{\"nodes\":" + jsonContent + "}");

            if (nodeList != null && nodeList.nodes != null && nodeList.nodes.Count > 0)
            {
                Node foundNode = nodeList.nodes.FirstOrDefault(n => n.node_id == nodeId);
                return foundNode;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error parsing nodes JSON: {ex.Message}");
        }

        return null;
    }

    void ShowConfirmation()
    {
        if (instructionsText != null)
        {
            instructionsText.gameObject.SetActive(false);
        }

        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(true);
        }

        if (confirmationText != null)
        {
            confirmationText.text = $"The QR code you scanned is referring to [{scannedNodeInfo.name}], use this as your current location?";
        }
    }

    void ShowError(string errorMessage)
    {
        Debug.LogError(errorMessage);

        if (instructionsText != null)
        {
            instructionsText.text = errorMessage;
            instructionsText.color = Color.red;
        }

        Invoke("OnTryAgain", 2f);
    }

    void OnConfirm()
    {
        Debug.Log("User confirmed location: " + scannedNodeInfo.name);

        PlayerPrefs.SetString("ScannedNodeID", scannedNodeInfo.node_id);
        PlayerPrefs.SetString("ScannedLocationName", scannedNodeInfo.name);
        PlayerPrefs.SetFloat("ScannedLat", scannedNodeInfo.latitude);
        PlayerPrefs.SetFloat("ScannedLng", scannedNodeInfo.longitude);
        PlayerPrefs.SetString("ScannedCampusID", scannedNodeInfo.campus_id);
        PlayerPrefs.SetFloat("ScannedX", scannedNodeInfo.x_coordinate);
        PlayerPrefs.SetFloat("ScannedY", scannedNodeInfo.y_coordinate);
        PlayerPrefs.Save();

        StopScanning();

        SceneManager.LoadScene("MainAppScene");
    }

    void OnTryAgain()
    {
        Debug.Log("User wants to scan again");

        if (instructionsText != null)
        {
            instructionsText.color = Color.white;
        }

        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }

        if (instructionsText != null)
        {
            instructionsText.gameObject.SetActive(true);
            instructionsText.text = "Point camera at QR code";
        }

        isScanning = true;
        InvokeRepeating("ScanQRCode", 0.5f, 0.5f);
    }

    public void GoBack()
    {
        StopScanning();

       SceneManager.UnloadSceneAsync("ReadQRCode");
    }

    void StopScanning()
    {
        isScanning = false;
        CancelInvoke("ScanQRCode");

        if (webcamTexture != null)
        {
            webcamTexture.Stop();
            webcamTexture = null;
        }
    }

    void OnDestroy()
    {
        StopScanning();
    }

    void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            StopScanning();
        }
        else
        {
            if (isScanning)
            {
                StartScanning();
            }
        }
    }
}

