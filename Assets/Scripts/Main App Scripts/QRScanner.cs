using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
// using ZXing;
// using ZXing.QrCode;

public class ARQRScanner : MonoBehaviour
{
    [Header("QR Scanner Settings")]
    public RawImage backgroundRawImage;
    public AspectRatioFitter aspectRatioFitter;
    public Text qrResultText;
    public GameObject qrScanPanel; // Panel to show when QR is detected
    public Button useQRLocationButton;
    
    [Header("Camera Settings")]
    public bool isCamAvaible;
    public WebCamTexture backCam;
    
    [Header("QR Processing")]
    private bool isScanning = true;
    private float scanInterval = 0.5f; // Scan every 0.5 seconds to avoid performance issues
    private float lastScanTime;
    
    [Header("Location Override")]
    public ARInfrastructureManager arManager;
    
    void Start()
    {
        if (backgroundRawImage == null)
            backgroundRawImage = GetComponent<RawImage>();
            
        SetupCamera();
        
        if (useQRLocationButton != null)
            useQRLocationButton.onClick.AddListener(UseQRLocation);
    }
    
    void SetupCamera()
    {
        WebCamDevice[] devices = WebCamTexture.devices;
        
        if (devices.Length == 0)
        {
            Debug.Log("No camera detected");
            isCamAvaible = false;
            return;
        }
        
        // Try to get back camera first
        for (int i = 0; i < devices.Length; i++)
        {
            if (!devices[i].isFrontFacing)
            {
                backCam = new WebCamTexture(devices[i].name, Screen.width, Screen.height);
                break;
            }
        }
        
        // If no back camera found, use the first available camera
        if (backCam == null)
            backCam = new WebCamTexture(devices[0].name, Screen.width, Screen.height);
            
        backCam.Play();
        
        if (backgroundRawImage != null)
            backgroundRawImage.texture = backCam;
            
        // Set aspect ratio
        if (aspectRatioFitter != null)
            aspectRatioFitter.aspectRatio = (float)backCam.width / (float)backCam.height;
            
        isCamAvaible = true;
        Debug.Log("Camera setup complete");
    }
    
    void Update()
    {
        if (!isCamAvaible || !isScanning)
            return;
            
        // Limit scanning frequency for performance
        if (Time.time - lastScanTime < scanInterval)
            return;
            
        lastScanTime = Time.time;
        ScanQRCode();
    }
    
    void ScanQRCode()
    {
        if (backCam == null || !backCam.isPlaying)
            return;
            
        try
        {
            // IBarcodeReader barcodeReader = new BarcodeReader();
            
            // Get current camera frame
            // var result = barcodeReader.Decode(backCam.GetPixels32(), backCam.width, backCam.height);
            
            // if (result != null)
            // {
            //     ProcessQRResult(result.Text);
            // }
        }
        catch (Exception e)
        {
            Debug.LogError($"QR Scanning error: {e.Message}");
        }
    }
    
    void ProcessQRResult(string qrData)
    {
        Debug.Log($"QR Code detected: {qrData}");
        
        if (qrResultText != null)
            qrResultText.text = $"QR Detected: {qrData}";
            
        // Parse QR data - assuming it contains node information
        try
        {
            QRLocationData locationData = JsonUtility.FromJson<QRLocationData>(qrData);
            ShowQRLocationOption(locationData);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Could not parse QR data as location: {e.Message}");
            // Handle other QR code formats here if needed
        }
    }
    
    void ShowQRLocationOption(QRLocationData locationData)
    {
        // Stop scanning temporarily
        isScanning = false;
        
        // Show QR panel
        if (qrScanPanel != null)
        {
            qrScanPanel.SetActive(true);
            
            // Update UI with location info
            Text locationInfo = qrScanPanel.GetComponentInChildren<Text>();
            if (locationInfo != null)
            {
                locationInfo.text = $"QR Location Detected:\n{locationData.location_name}\nNode: {locationData.node_id}";
            }
        }
    }
    
    public void UseQRLocation()
    {
        // This would override the GPS location with QR location
        Debug.Log("Using QR location as current position");
        
        // Hide QR panel
        if (qrScanPanel != null)
            qrScanPanel.SetActive(false);
            
        // Resume scanning
        isScanning = true;
        
        // Here you would implement the logic to use QR location
        // For now, just log the action
        // You could call a method on GPSManager to override location
        OverrideGPSWithQR();
    }
    
    void OverrideGPSWithQR()
    {
        // This is where you'd implement QR location override
        // You might want to modify your GPSManager to accept location overrides
        Debug.Log("GPS location overridden with QR data");
    }
    
    public void DismissQRPanel()
    {
        if (qrScanPanel != null)
            qrScanPanel.SetActive(false);
            
        isScanning = true;
    }
    
    public void ToggleScanning()
    {
        isScanning = !isScanning;
        Debug.Log($"QR Scanning: {(isScanning ? "Enabled" : "Disabled")}");
    }
    
    void OnDestroy()
    {
        if (backCam != null && backCam.isPlaying)
            backCam.Stop();
    }
}

// Data structure for QR code location information
[System.Serializable]
public class QRLocationData
{
    public string node_id;
    public string location_name;
    public float latitude;
    public float longitude;
    public string campus_id;
    public string additional_info;
}