using UnityEngine;
using UnityEngine.UI;

public class QRScanFrame : MonoBehaviour
{
    [Header("Frame Settings")]
    public Color frameColor;
    public float frameSize = 280f;
    public float cornerLength = 75f;
    public float lineThickness = 5f;
    
    void Start()
    {
        CreateFrame();
    }
    
    void CreateFrame()
    {
        float halfFrame = frameSize / 2;
        float offset = lineThickness / 2;
        
        // Top-left horizontal (extends right from corner)
        CreateLine(new Vector2(-halfFrame + cornerLength/2, halfFrame - offset), true, cornerLength, lineThickness);
        
        // Top-left vertical (extends down from corner)
        CreateLine(new Vector2(-halfFrame + offset, halfFrame - cornerLength/2), false, cornerLength, lineThickness);
        
        // Top-right horizontal (extends left from corner)
        CreateLine(new Vector2(halfFrame - cornerLength/2, halfFrame - offset), true, cornerLength, lineThickness);
        
        // Top-right vertical (extends down from corner)
        CreateLine(new Vector2(halfFrame - offset, halfFrame - cornerLength/2), false, cornerLength, lineThickness);
        
        // Bottom-left horizontal (extends right from corner)
        CreateLine(new Vector2(-halfFrame + cornerLength/2, -halfFrame + offset), true, cornerLength, lineThickness);
        
        // Bottom-left vertical (extends up from corner)
        CreateLine(new Vector2(-halfFrame + offset, -halfFrame + cornerLength/2), false, cornerLength, lineThickness);
        
        // Bottom-right horizontal (extends left from corner)
        CreateLine(new Vector2(halfFrame - cornerLength/2, -halfFrame + offset), true, cornerLength, lineThickness);
        
        // Bottom-right vertical (extends up from corner)
        CreateLine(new Vector2(halfFrame - offset, -halfFrame + cornerLength/2), false, cornerLength, lineThickness);
    }
    
    void CreateLine(Vector2 position, bool isHorizontal, float length, float thickness)
    {
        GameObject line = new GameObject("FrameLine");
        line.transform.SetParent(transform);
        line.transform.localPosition = Vector3.zero;
        line.transform.localScale = Vector3.one;
        
        Image img = line.AddComponent<Image>();
        img.color = frameColor;
        
        RectTransform rt = img.rectTransform;
        rt.pivot = new Vector2(0.5f, 0.5f);
        
        if (isHorizontal)
        {
            rt.sizeDelta = new Vector2(length, thickness);
        }
        else
        {
            rt.sizeDelta = new Vector2(thickness, length);
        }
        
        rt.anchoredPosition = position;
    }
}