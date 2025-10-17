using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using ZXing;
using ZXing.Common;
using Unity.Collections;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class QRScanner : MonoBehaviour
{
    [Header("AR References")]
    public ARCameraManager arCameraManager;

    [Header("UI References")]
    public TextMeshProUGUI instructionsText;
    public Button backButton;
    public RawImage cameraDisplayImage; 

    [Header("Confirmation Panel")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationText;
    public Button confirmButton;
    public Button tryAgainButton;

    [Header("Security Settings")]
    public string qrSignature = "CRIMSON";
    public string qrDelimiter = "_";

    private bool isScanning = false;
    private string scannedNodeId;
    private Node scannedNodeInfo;
    private List<string> availableMapIds = new List<string>();
    private Texture2D cameraImageTexture;
    private RenderTexture renderTexture;

    private IBarcodeReader barcodeReader = new BarcodeReader
    {
        AutoRotate = true,
        Options = new DecodingOptions
        {
            TryHarder = true,
            PossibleFormats = new[] { BarcodeFormat.QR_CODE }
        }
    };

    void Start()
    {
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }

        if (instructionsText != null)
        {
            instructionsText.text = "Point camera at QR code";
        }


        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirm);
        }

        if (tryAgainButton != null)
        {
            tryAgainButton.onClick.AddListener(OnTryAgain);
        }

        StartCoroutine(InitializeScanner());
    }

    IEnumerator InitializeScanner()
    {
        yield return StartCoroutine(LoadAvailableMaps());

        if (arCameraManager == null)
        {
            if (instructionsText != null)
            {
                instructionsText.text = "AR Camera Manager not found!";
                instructionsText.color = Color.red;
            }
            yield break;
        }

        // Wait for AR system to initialize
        float timeout = 0f;
        while (!arCameraManager.enabled && timeout < 5f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        if (!arCameraManager.enabled)
        {
            if (instructionsText != null)
            {
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
        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "maps.json",
            (jsonContent) =>
            {
                try
                {
                    MapList mapList = JsonUtility.FromJson<MapList>("{\"maps\":" + jsonContent + "}");

                    if (mapList != null && mapList.maps != null && mapList.maps.Count > 0)
                    {
                        availableMapIds.Clear();
                        foreach (var map in mapList.maps)
                        {
                            availableMapIds.Add(map.map_id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError("Error loading maps: " + ex.Message);
                }
            },
            (error) =>
            {
                Debug.LogError("Error: " + error);
            }
        ));
    }

    void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        if (!isScanning || arCameraManager == null)
            return;

        if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
            return;

        try
        {
            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(0, 0, image.width, image.height),
                outputDimensions = new Vector2Int(image.width / 2, image.height / 2),
                outputFormat = TextureFormat.RGBA32,
                transformation = XRCpuImage.Transformation.MirrorY
            };

            int size = image.GetConvertedDataSize(conversionParams);
            var buffer = new NativeArray<byte>(size, Allocator.Temp);

            image.Convert(conversionParams, buffer);
            image.Dispose();

            if (cameraImageTexture == null)
            {
                cameraImageTexture = new Texture2D(
                    conversionParams.outputDimensions.x,
                    conversionParams.outputDimensions.y,
                    conversionParams.outputFormat,
                    false);
            }

            cameraImageTexture.LoadRawTextureData(buffer);
            cameraImageTexture.Apply();
            buffer.Dispose();

            // Display camera feed on RawImage
            if (cameraDisplayImage != null)
            {
                cameraDisplayImage.texture = cameraImageTexture;
            }

            Result result = barcodeReader.Decode(
                cameraImageTexture.GetPixels32(),
                cameraImageTexture.width,
                cameraImageTexture.height);

            if (result != null)
            {
                OnQRCodeScanned(result.Text);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error processing frame: " + ex.Message);
        }
    }

    void OnQRCodeScanned(string qrData)
    {
        isScanning = false;

        if (!ValidateQRCode(qrData, out string nodeId))
        {
            ShowError("Invalid QR code. Please scan a valid CRIMSON campus location QR code.");
            return;
        }

        scannedNodeId = nodeId;
        StartCoroutine(SearchNodeInLocalFiles(scannedNodeId));
    }

    bool ValidateQRCode(string qrData, out string nodeId)
    {
        nodeId = null;

        if (!qrData.Contains(qrDelimiter))
            return false;

        string[] parts = qrData.Split(new string[] { qrDelimiter }, StringSplitOptions.None);

        if (parts.Length != 2)
            return false;

        string signature = parts[0];
        if (signature != qrSignature)
            return false;

        nodeId = parts[1];

        if (string.IsNullOrEmpty(nodeId) || nodeId.Length < 3)
            return false;

        return true;
    }

    IEnumerator SearchNodeInLocalFiles(string nodeId)
    {
        if (instructionsText != null)
        {
            instructionsText.text = "Searching for location...";
        }

        bool foundNode = false;

        if (availableMapIds.Count > 0)
        {
            foreach (string mapId in availableMapIds)
            {
                string nodesFileName = $"nodes_{mapId}.json";
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
                        }
                        searchComplete = true;
                    },
                    (error) =>
                    {
                        searchComplete = true;
                    }
                ));

                while (!searchComplete)
                    yield return null;

                if (foundNode)
                    break;
            }
        }

        if (foundNode)
        {
            ShowConfirmation();
        }
        else
        {
            ShowError("Location not found. This QR code may not be registered in the system.");
        }
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
            Debug.LogError("Error searching node: " + ex.Message);
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
        if (instructionsText != null)
        {
            instructionsText.text = errorMessage;
            instructionsText.color = Color.red;
        }

        Invoke("OnTryAgain", 2f);
    }

    void OnConfirm()
    {
        PlayerPrefs.SetString("ScannedNodeID", scannedNodeInfo.node_id);
        PlayerPrefs.SetString("ScannedLocationName", scannedNodeInfo.name);
        PlayerPrefs.SetFloat("ScannedLat", scannedNodeInfo.latitude);
        PlayerPrefs.SetFloat("ScannedLng", scannedNodeInfo.longitude);
        PlayerPrefs.SetString("ScannedCampusID", scannedNodeInfo.campus_id);
        PlayerPrefs.SetFloat("ScannedX", scannedNodeInfo.x_coordinate);
        PlayerPrefs.SetFloat("ScannedY", scannedNodeInfo.y_coordinate);
        PlayerPrefs.Save();

    }

    void OnTryAgain()
    {
        if (instructionsText != null)
        {
            instructionsText.color = Color.white;
            instructionsText.gameObject.SetActive(true);
            instructionsText.text = "Point camera at QR code";
        }

        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }

        isScanning = true;
    }


    void StopScanning()
    {
        isScanning = false;

        if (arCameraManager != null)
        {
            arCameraManager.frameReceived -= OnCameraFrameReceived;
        }
    }

    void OnDestroy()
    {
        StopScanning();
        
        if (cameraImageTexture != null)
        {
            Destroy(cameraImageTexture);
        }
        
        if (renderTexture != null)
        {
            Destroy(renderTexture);
        }
    }
}