using UnityEngine;

#if UNITY_EDITOR
using UnityEngine.InputSystem;

/// <summary>
/// Simulates AR functionality in Unity Editor for testing
/// Attach this to your AR Camera in the scene
/// </summary>
public class AREditorTester : MonoBehaviour
{
    [Header("Simulated User Position")]
    public Vector2 simulatedGPS = new Vector2(6.91463f, 122.11927f); // Manila coords
    public Vector2 simulatedXY = new Vector2(0f, 0f); // Offline coordinates

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float rotateSpeed = 30f;
    public float gpsUpdateInterval = 1f;

    [Header("References")]
    public UnifiedARManager unifiedARManager;
    public UnifiedARNavigationMarkerSpawner markerSpawner;

    private float lastGPSUpdate = 0f;
    private LocalizationMode currentMode;

    private enum LocalizationMode { GPS, Offline }

    void Start()
    {
        // Auto-find references
        if (unifiedARManager == null)
            unifiedARManager = FindObjectOfType<UnifiedARManager>();

        if (markerSpawner == null)
            markerSpawner = FindObjectOfType<UnifiedARNavigationMarkerSpawner>();

        // Determine mode
        string locMode = PlayerPrefs.GetString("LocalizationMode", "GPS");
        currentMode = locMode == "Offline" ? LocalizationMode.Offline : LocalizationMode.GPS;

        Debug.Log($"[AREditorTester] Mode: {currentMode}");
        Debug.Log("CONTROLS: WASD to move, QE to rotate, Arrow keys to adjust GPS/XY");
    }

    void Update()
    {
        // Camera movement with WASD
        Vector3 move = Vector3.zero;
        
        if (Keyboard.current.wKey.isPressed)
            move += transform.forward;
        if (Keyboard.current.sKey.isPressed)
            move -= transform.forward;
        if (Keyboard.current.aKey.isPressed)
            move -= transform.right;
        if (Keyboard.current.dKey.isPressed)
            move += transform.right;

        transform.position += move * moveSpeed * Time.deltaTime;

        // Camera rotation with Q/E
        if (Keyboard.current.qKey.isPressed)
            transform.Rotate(0, -rotateSpeed * Time.deltaTime, 0);
        if (Keyboard.current.eKey.isPressed)
            transform.Rotate(0, rotateSpeed * Time.deltaTime, 0);

        // Update simulated position
        if (Time.time - lastGPSUpdate >= gpsUpdateInterval)
        {
            lastGPSUpdate = Time.time;

            if (currentMode == LocalizationMode.GPS)
            {
                UpdateSimulatedGPS();
            }
            else
            {
                UpdateSimulatedXY();
            }
        }

        // Manual position adjustment with arrow keys
        if (currentMode == LocalizationMode.GPS)
        {
            if (Keyboard.current.upArrowKey.isPressed)
                simulatedGPS.x += 0.00001f; // Move north
            if (Keyboard.current.downArrowKey.isPressed)
                simulatedGPS.x -= 0.00001f; // Move south
            if (Keyboard.current.leftArrowKey.isPressed)
                simulatedGPS.y -= 0.00001f; // Move west
            if (Keyboard.current.rightArrowKey.isPressed)
                simulatedGPS.y += 0.00001f; // Move east
        }
        else
        {
            if (Keyboard.current.upArrowKey.isPressed)
                simulatedXY.y += 0.1f;
            if (Keyboard.current.downArrowKey.isPressed)
                simulatedXY.y -= 0.1f;
            if (Keyboard.current.leftArrowKey.isPressed)
                simulatedXY.x -= 0.1f;
            if (Keyboard.current.rightArrowKey.isPressed)
                simulatedXY.x += 0.1f;
        }

        // Debug info
        if (Keyboard.current.pKey.wasPressedThisFrame)
        {
            if (currentMode == LocalizationMode.GPS)
                Debug.Log($"Current GPS: {simulatedGPS}");
            else
                Debug.Log($"Current XY: {simulatedXY}");
        }
    }

    void UpdateSimulatedGPS()
    {
        // Simulate GPS drift based on movement
        Vector3 worldMovement = transform.position;
        
        // Convert world movement to GPS offset (very rough approximation)
        float latOffset = worldMovement.z / 111000f; // ~111km per degree
        float lngOffset = worldMovement.x / (111000f * Mathf.Cos(simulatedGPS.x * Mathf.Deg2Rad));
        
        Vector2 newGPS = new Vector2(
            simulatedGPS.x + latOffset,
            simulatedGPS.y + lngOffset
        );

        // Update GPSManager if it exists
        if (GPSManager.Instance != null)
        {
            // Simulate GPS update (you might need to make a public method in GPSManager for this)
            Debug.Log($"[AREditorTester] Simulated GPS: {newGPS}");
        }
    }

    void UpdateSimulatedXY()
    {
        // Update based on camera movement
        Vector3 cameraPos = transform.position;
        simulatedXY = new Vector2(cameraPos.x, cameraPos.z);

        // Update UnifiedARManager
        if (markerSpawner != null)
        {
            markerSpawner.UpdateUserXY(simulatedXY);
        }

        Debug.Log($"[AREditorTester] Simulated XY: {simulatedXY}");
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.white;
        style.padding = new RectOffset(10, 10, 10, 10);

        string info = $"AR Editor Tester\n\n";
        info += $"Mode: {currentMode}\n";
        
        if (currentMode == LocalizationMode.GPS)
            info += $"GPS: {simulatedGPS.x:F6}, {simulatedGPS.y:F6}\n";
        else
            info += $"XY: {simulatedXY.x:F2}, {simulatedXY.y:F2}\n";
        
        info += $"Camera: {transform.position}\n\n";
        info += "WASD - Move camera\n";
        info += "QE - Rotate\n";
        info += "Arrows - Adjust position\n";
        info += "P - Print debug info";

        GUI.Label(new Rect(10, 10, 400, 300), info, style);
    }
}
#endif