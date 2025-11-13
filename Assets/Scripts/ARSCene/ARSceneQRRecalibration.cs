using UnityEngine;
using UnityEngine.UI;
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
using System.Linq;

public class ARSceneQRRecalibration : MonoBehaviour
{
    [Header("AR References")]
    public ARCameraManager arCameraManager;
    public UnifiedARManager unifiedARManager;
    public ARUIManager arUIManager;

    [Header("UI References")]
    public GameObject scanTriggerButton;
    public TextMeshProUGUI scanButtonText;
    public GameObject qrFrameContainer;

    [Header("Confirmation Panel")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationTitle;
    public TextMeshProUGUI confirmationBody;
    public TextMeshProUGUI confirmationNote;
    public Button confirmButton;
    public Button cancelButton;

    [Header("Security Settings")]
    public string qrSignature = "CRIMSON";
    public string qrDelimiter = "_";

    [Header("Scanning Settings")]
    public bool autoScanMode = false;
    public int frameSkip = 2;

    [Header("Test Mode (Editor Only)")]
    public bool enableTestMode = false;
    public string testNodeId = "ND-001";

    private bool isScanning = false;
    private bool isScanningActive = false;
    private string scannedNodeId;
    private Node scannedNodeInfo;
    private List<string> availableMapIds = new List<string>();
    private Texture2D cameraImageTexture;
    private int frameCount = 0;

    private IBarcodeReader barcodeReader = new BarcodeReader
    {
        AutoRotate = false,
        Options = new DecodingOptions
        {
            TryHarder = false
        }
    };

    void Start()
    {
        if (arCameraManager == null)
            arCameraManager = FindObjectOfType<ARCameraManager>();

        if (unifiedARManager == null)
            unifiedARManager = FindObjectOfType<UnifiedARManager>();

        if (arUIManager == null)
            arUIManager = FindObjectOfType<ARUIManager>();

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (qrFrameContainer != null)
            qrFrameContainer.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmRecalibration);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelRecalibration);

        if (scanTriggerButton != null)
        {
            Button btn = scanTriggerButton.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(ToggleScanMode);
        }

        StartCoroutine(InitializeScanner());
    }

    void Update()
    {
#if UNITY_EDITOR
        if (enableTestMode && Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            string simulatedQRData = qrSignature + qrDelimiter + testNodeId;
            OnQRCodeScanned(simulatedQRData);
        }
#endif
    }

    public void ToggleScanMode()
    {
        if (isScanningActive)
        {
            StopScanning();
        }
        else
        {
            StartScanning();
        }
    }

    public void StartScanning()
    {
        isScanningActive = true;

        if (qrFrameContainer != null)
            qrFrameContainer.SetActive(true);

        if (scanButtonText != null)
            scanButtonText.text = "Cancel Scan";
    }

    public void StopScanning()
    {
        isScanningActive = false;

        if (qrFrameContainer != null)
            qrFrameContainer.SetActive(false);

        if (scanButtonText != null)
            scanButtonText.text = "Scan QR to Recalibrate";
    }

    IEnumerator InitializeScanner()
    {
        yield return StartCoroutine(LoadAvailableMaps());

        if (arCameraManager == null)
        {
            yield break;
        }

        float timeout = 0f;
        while (!arCameraManager.enabled && timeout < 5f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        if (!arCameraManager.enabled)
        {
            yield break;
        }

        arCameraManager.frameReceived += OnCameraFrameReceived;
        isScanning = true;

        if (autoScanMode)
        {
            StartScanning();
        }
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
                catch (Exception)
                {
                }
            },
            (error) =>
            {
            }
        ));
    }

    void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        frameCount++;

        if (!isScanning || !isScanningActive || arCameraManager == null)
            return;

        if (frameCount % frameSkip != 0)
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

            Color32[] pixels = cameraImageTexture.GetPixels32();
            Result result = barcodeReader.Decode(pixels, cameraImageTexture.width, cameraImageTexture.height);

            if (result != null)
            {
                OnQRCodeScanned(result.Text);
            }
        }
        catch (Exception)
        {
        }
    }

    void OnQRCodeScanned(string qrData)
    {
        isScanningActive = false;

        if (!ValidateQRCode(qrData, out string nodeId))
        {
            StartCoroutine(ShowErrorAndResume("Invalid QR code. Please scan a valid CRIMSON campus QR code."));
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
            if (scannedNodeInfo.type == "infrastructure")
            {
                ShowConfirmation();
            }
            else
            {
                StartCoroutine(ShowErrorAndResume("This QR code is not for an outdoor location."));
            }
        }
        else
        {
            StartCoroutine(ShowErrorAndResume("Location not found. This QR code may not be registered in the system."));
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
        catch (Exception)
        {
        }

        return null;
    }

    void ShowConfirmation()
    {
        if (qrFrameContainer != null)
            qrFrameContainer.SetActive(false);

        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(true);

            CanvasGroup canvasGroup = confirmationPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = confirmationPanel.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0;
            canvasGroup.DOFade(1, 0.5f).SetEase(Ease.OutQuad);
        }

        string coordinateInfo = $"GPS: {scannedNodeInfo.latitude:F6}, {scannedNodeInfo.longitude:F6}";

        string titleText = "<b>Recalibrate GPS Position</b>";
        
        string bodyText = $"<b>{scannedNodeInfo.name}</b>\n\n" +
            $"{coordinateInfo}";
        
        string noteText = "Your GPS position will be updated to this location.";

        if (confirmationTitle != null)
            confirmationTitle.text = titleText;

        if (confirmationBody != null)
            confirmationBody.text = bodyText;

        if (confirmationNote != null)
            confirmationNote.text = noteText;
    }

    IEnumerator ShowErrorAndResume(string errorMessage)
    {
        Debug.LogWarning($"QR Scan Error: {errorMessage}");
        yield return new WaitForSeconds(2f);
        ResumeScanning();
    }

    void ResumeScanning()
    {
        isScanningActive = true;

        if (qrFrameContainer != null)
            qrFrameContainer.SetActive(true);
    }

    void OnConfirmRecalibration()
    {
        if (GPSManager.Instance != null && scannedNodeInfo != null)
        {
            GPSManager.Instance.SetQRLocationOverride(
                scannedNodeInfo.latitude,
                scannedNodeInfo.longitude,
                0f
            );
        }

        if (unifiedARManager != null)
        {
            unifiedARManager.OnQRCodeScanned(scannedNodeInfo);
        }

        UserIndicator arUserIndicator = FindObjectOfType<UserIndicator>();
        if (arUserIndicator != null)
        {
            arUserIndicator.ForceUpdate();
        }

        if (confirmationPanel != null)
        {
            CanvasGroup canvasGroup = confirmationPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = confirmationPanel.AddComponent<CanvasGroup>();

            canvasGroup.DOFade(0, 0.5f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                confirmationPanel.SetActive(false);
            });
        }

        StopScanning();
    }

    void OnCancelRecalibration()
    {
        if (confirmationPanel != null)
        {
            CanvasGroup canvasGroup = confirmationPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = confirmationPanel.AddComponent<CanvasGroup>();

            canvasGroup.DOFade(0, 0.5f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                confirmationPanel.SetActive(false);
            });
        }

        ResumeScanning();

        if (qrFrameContainer != null)
            qrFrameContainer.SetActive(true);
    }

    void OnDestroy()
    {
        isScanning = false;

        if (arCameraManager != null)
        {
            arCameraManager.frameReceived -= OnCameraFrameReceived;
        }

        if (cameraImageTexture != null)
        {
            Destroy(cameraImageTexture);
        }
    }
}