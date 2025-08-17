using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Canvas))]
public class CanvasHelper : MonoBehaviour
{
    [Tooltip("Assign any RectTransform that should stay within the safe area (e.g. header, footer panels)")]
    public List<RectTransform> safeAreaPanels = new List<RectTransform>();

    private static List<CanvasHelper> helpers = new List<CanvasHelper>();

    public static UnityEvent OnResolutionOrOrientationChanged = new UnityEvent();

    private static bool screenChangeVarsInitialized = false;
    private static ScreenOrientation lastOrientation = ScreenOrientation.LandscapeLeft;
    private static Vector2 lastResolution = Vector2.zero;
    private static Rect lastSafeArea = Rect.zero;

    private Canvas canvas;

    void Awake()
    {
        if (!helpers.Contains(this))
            helpers.Add(this);

        canvas = GetComponent<Canvas>();

        if (!screenChangeVarsInitialized)
        {
            lastOrientation = Screen.orientation;
            lastResolution = new Vector2(Screen.width, Screen.height);
            lastSafeArea = Screen.safeArea;
            screenChangeVarsInitialized = true;
        }

        ApplySafeArea();
    }

    void Update()
    {
        if (helpers.Count == 0 || helpers[0] != this)
            return;

        if (Screen.orientation != lastOrientation)
            OrientationChanged();

        if (Screen.safeArea != lastSafeArea)
            SafeAreaChanged();

        if (Screen.width != lastResolution.x || Screen.height != lastResolution.y)
            ResolutionChanged();
    }


    private void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;

        foreach (RectTransform panel in safeAreaPanels)
        {
            if (panel == null)
                continue;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= canvas.pixelRect.width;
            anchorMin.y /= canvas.pixelRect.height;
            anchorMax.x /= canvas.pixelRect.width;
            anchorMax.y /= canvas.pixelRect.height;

            panel.anchorMin = anchorMin;
            panel.anchorMax = anchorMax;
        }
    }

    private void OnDestroy()
    {
        if (helpers.Contains(this))
            helpers.Remove(this);
    }

    private static void OrientationChanged()
    {
        lastOrientation = Screen.orientation;
        lastResolution = new Vector2(Screen.width, Screen.height);
        OnResolutionOrOrientationChanged.Invoke();
    }

    private static void ResolutionChanged()
    {
        lastResolution = new Vector2(Screen.width, Screen.height);
        OnResolutionOrOrientationChanged.Invoke();
    }

    private static void SafeAreaChanged()
    {
        lastSafeArea = Screen.safeArea;

        foreach (CanvasHelper helper in helpers)
        {
            helper.ApplySafeArea();
        }
    }
}
