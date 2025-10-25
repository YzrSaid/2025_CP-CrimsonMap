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

/// <summary>
/// QR Scanner for AR Scene - Works with UnifiedARManager
/// Supports both GPS and Offline (X,Y) localization modes
/// GPS mode: Gets latitude/longitude from scanned node
/// Offline mode: Gets x_coordinate/y_coordinate from scanned node
/// </summary>
public class ARSceneQRRecalibration : MonoBehaviour
{
    [Header("AR References")]
    public ARCameraManager arCameraManager;
    public UnifiedARManager unifiedARManager; // UPDATED: Now uses UnifiedARManager
    public ARUIManager arUIManager;

    [Header("UI References")]
    public GameObject scanTriggerButton; // Button to activate scanning mode (after first scan)
    public GameObject scanTriggerButton2; // Button in the "Scan Required" panel
    public TextMeshProUGUI scanButtonText;
    public GameObject qrFrameContainer; // Visual QR frame overlay
    public TextMeshProUGUI debugText;

    [Header("Confirmation Panel")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationText;
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

    private bool isScanning = false; // Scanner initialized and ready
    private bool isScanningActive = false; // Currently looking for QR codes
    private bool isFirstScan = true; // Track if this is the first QR scan in Direct AR + Offline mode
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
        // Find AR components if not assigned
        if (arCameraManager == null)
            arCameraManager = FindObjectOfType<ARCameraManager>();

        if (unifiedARManager == null)
            unifiedARManager = FindObjectOfType<UnifiedARManager>();

        if (arUIManager == null)
            arUIManager = FindObjectOfType<ARUIManager>();

        // Setup UI
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (qrFrameContainer != null)
            qrFrameContainer.SetActive(false);

        // Setup buttons
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

        if (scanTriggerButton2 != null)
        {
            Button btn = scanTriggerButton2.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(ToggleScanMode);
        }

        StartCoroutine(InitializeScanner());
    }

    void Update()
    {
#if UNITY_EDITOR
        // Test mode - press T key to simulate QR scan
        if (enableTestMode && Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            string simulatedQRData = qrSignature + qrDelimiter + testNodeId;
            Debug.Log($"🧪 Test Mode: Simulating QR scan with {simulatedQRData}");
            OnQRCodeScanned(simulatedQRData);
        }
#endif
    }

    /// <summary>
    /// Toggle scanning mode on/off
    /// </summary>
    public void ToggleScanMode()
    {
        if (isScanningActive)
        {
            StopScanning();
        }
        else
        {
            // Hide the "Scan Required" panel if it exists
            if (arUIManager != null && arUIManager.scanQRRequiredPanel != null)
            {
                arUIManager.scanQRRequiredPanel.SetActive(false);
            }
            StartScanning();
        }
    }

    /// <summary>
    /// Start QR scanning mode
    /// </summary>
    public void StartScanning()
    {
        isScanningActive = true;

        // Show QR frame
        if (qrFrameContainer != null)
            qrFrameContainer.SetActive(true);

        // Update button text
        if (scanButtonText != null)
            scanButtonText.text = "Cancel Scan";

        Debug.Log("📷 QR Scanning mode activated");
    }

    /// <summary>
    /// Stop QR scanning mode
    /// </summary>
    public void StopScanning()
    {
        isScanningActive = false;

        if (qrFrameContainer != null)
            qrFrameContainer.SetActive(false);

        if (scanButtonText != null)
            scanButtonText.text = "Scan QR to Recalibrate";

        Debug.Log("📷 QR Scanning mode deactivated");
    }

    IEnumerator InitializeScanner()
    {
        yield return StartCoroutine(LoadAvailableMaps());

        if (arCameraManager == null)
        {
            Debug.LogError("❌ AR Camera Manager not found!");
            yield break;
        }

        // Wait for AR Camera to be ready
        float timeout = 0f;
        while (!arCameraManager.enabled && timeout < 5f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        if (!arCameraManager.enabled)
        {
            Debug.LogError("❌ AR system failed to initialize!");
            yield break;
        }

        arCameraManager.frameReceived += OnCameraFrameReceived;
        isScanning = true;

        // If auto-scan mode, start immediately
        if (autoScanMode)
        {
            StartScanning();
        }

        Debug.Log("✅ AR QR Recalibration scanner initialized");
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
                        Debug.Log($"✅ Loaded {availableMapIds.Count} maps for QR validation");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError("❌ Error loading maps: " + ex.Message);
                }
            },
            (error) =>
            {
                Debug.LogError("❌ Error loading maps.json: " + error);
            }
        ));
    }

    void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        frameCount++;

        // Only process if scanning mode is active AND scanner is ready
        if (!isScanning || !isScanningActive || arCameraManager == null)
            return;

        // Skip frames for performance
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
                if (debugText != null)
                    debugText.text = $"QR FOUND: {result.Text}";

                OnQRCodeScanned(result.Text);
            }
            else
            {
                if (debugText != null && frameCount % 30 == 0)
                    debugText.text = $"Scanning... (Frame: {frameCount})";
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("❌ Error processing frame: " + ex.Message);
        }
    }

    void OnQRCodeScanned(string qrData)
    {
        // Pause scanning while processing
        isScanningActive = false;

        if (!ValidateQRCode(qrData, out string nodeId))
        {
            ShowError("Invalid QR code. Please scan a valid CRIMSON campus QR code.");
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
            Debug.LogError("❌ Error searching node: " + ex.Message);
        }

        return null;
    }

    void ShowConfirmation()
    {
        // Hide QR frame
        if (qrFrameContainer != null)
            qrFrameContainer.SetActive(false);

        // Show confirmation panel
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(true);

            CanvasGroup canvasGroup = confirmationPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = confirmationPanel.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0;
            canvasGroup.DOFade(1, 0.5f).SetEase(Ease.OutQuad);
        }

        // Get current mode to show appropriate confirmation text
        string localizationMode = PlayerPrefs.GetString("LocalizationMode", "GPS");
        string coordinateInfo = "";

        if (localizationMode == "GPS")
        {
            coordinateInfo = $"GPS: {scannedNodeInfo.latitude:F6}, {scannedNodeInfo.longitude:F6}";
        }
        else
        {
            coordinateInfo = $"X,Y: {scannedNodeInfo.x_coordinate:F2}, {scannedNodeInfo.y_coordinate:F2}";
        }

        if (confirmationText != null)
        {
            confirmationText.text = $"Recalibrate your position to:\n\n<b>{scannedNodeInfo.name}</b>\n\n{coordinateInfo}\n\nYour current position will be updated to match this location.";
        }

        Debug.Log($"📍 QR Scanned: {scannedNodeInfo.name}");
        Debug.Log($"   GPS: Lat {scannedNodeInfo.latitude:F6}, Lng {scannedNodeInfo.longitude:F6}");
        Debug.Log($"   X,Y: X {scannedNodeInfo.x_coordinate:F2}, Y {scannedNodeInfo.y_coordinate:F2}");
    }

    void ShowError(string errorMessage)
    {
        Debug.LogWarning($"⚠️ {errorMessage}");

        if (debugText != null)
            debugText.text = errorMessage;

        // Resume scanning after 2 seconds
        Invoke(nameof(ResumeScanning), 2f);
    }

    void ResumeScanning()
    {
        isScanningActive = true;

        if (qrFrameContainer != null)
            qrFrameContainer.SetActive(true);
    }

    void OnConfirmRecalibration()
    {
        if (unifiedARManager == null)
        {
            Debug.LogError("❌ UnifiedARManager not found! Cannot recalibrate.");
            OnCancelRecalibration();
            return;
        }

        // Call UnifiedARManager's QR scanned method
        // It will handle both GPS and Offline modes internally
        unifiedARManager.OnQRCodeScanned(scannedNodeInfo);

        string localizationMode = PlayerPrefs.GetString("LocalizationMode", "GPS");
        
        if (localizationMode == "GPS")
        {
            Debug.Log($"✅ GPS recalibrated to: {scannedNodeInfo.name}");
            Debug.Log($"✅ New GPS: Lat {scannedNodeInfo.latitude:F6}, Lng {scannedNodeInfo.longitude:F6}");
        }
        else
        {
            Debug.Log($"✅ Position recalibrated to: {scannedNodeInfo.name}");
            Debug.Log($"✅ New reference point: X:{scannedNodeInfo.x_coordinate:F2}, Y:{scannedNodeInfo.y_coordinate:F2}");
        }

        // Check if this is the first scan in Direct AR + Offline mode
        string arMode = PlayerPrefs.GetString("ARMode", "DirectAR");
        
        if (isFirstScan && arMode == "DirectAR" && localizationMode == "Offline" && arUIManager != null)
        {
            arUIManager.OnQRScannedAndConfirmed();
            isFirstScan = false;
            Debug.Log("🔓 Direct AR + Offline Mode unlocked - UI now visible");
        }

        // Hide confirmation panel
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

        // Stop scanning mode
        StopScanning();

        // Show success message
        if (debugText != null)
        {
            debugText.text = "✅ Position calibrated successfully!";
            StartCoroutine(HideDebugTextAfterDelay(3f));
        }
    }

    void OnCancelRecalibration()
    {
        // Hide confirmation panel
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

        // Resume scanning
        ResumeScanning();

        if (qrFrameContainer != null)
            qrFrameContainer.SetActive(true);

        Debug.Log("❌ Recalibration cancelled");
    }

    IEnumerator HideDebugTextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (debugText != null)
            debugText.text = "";
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