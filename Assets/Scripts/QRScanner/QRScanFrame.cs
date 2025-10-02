using UnityEngine;
using UnityEngine.UI;

public class QRScanFrame : MonoBehaviour
{
    [Header("Frame Settings")]
    public Color frameColor = new Color(0.925f, 0.69f, 0.204f, 1f); // #ECB034
    public float frameSize = 280f;
    public float cornerLength = 50f;
    public float lineThickness = 5f;
    
    void Start()
    {
        CreateFrame();
    }

    void CreateFrame()
    {
        // Create 8 lines (2 per corner = 4 corners x 2 lines)
        for (int i = 0; i < 8; i++)
        {
            GameObject line = new GameObject("FrameLine" + i);
            line.transform.SetParent(transform);
            line.transform.localPosition = Vector3.zero;
            line.transform.localScale = Vector3.one;
            
            Image img = line.AddComponent<Image>();
            img.color = frameColor;
            
            RectTransform rt = img.rectTransform;
            
            // Position based on corner
            int corner = i / 2; // 0=TopLeft, 1=TopRight, 2=BottomLeft, 3=BottomRight
            bool isHorizontal = (i % 2 == 0);
            
            // Set size based on orientation
            if (isHorizontal)
            {
                rt.sizeDelta = new Vector2(cornerLength, lineThickness);
            }
            else
            {
                rt.sizeDelta = new Vector2(lineThickness, cornerLength);
            }
            
            float offset = frameSize / 2;
            
            // Position each line
            switch (corner)
            {
                case 0: // Top Left corner
                    if (isHorizontal)
                        rt.anchoredPosition = new Vector2(-offset + cornerLength/2, offset);
                    else
                        rt.anchoredPosition = new Vector2(-offset, offset - cornerLength/2);
                    break;
                    
                case 1: // Top Right corner
                    if (isHorizontal)
                        rt.anchoredPosition = new Vector2(offset - cornerLength/2, offset);
                    else
                        rt.anchoredPosition = new Vector2(offset, offset - cornerLength/2);
                    break;
                    
                case 2: // Bottom Left corner
                    if (isHorizontal)
                        rt.anchoredPosition = new Vector2(-offset + cornerLength/2, -offset);
                    else
                        rt.anchoredPosition = new Vector2(-offset, -offset + cornerLength/2);
                    break;
                    
                case 3: // Bottom Right corner
                    if (isHorizontal)
                        rt.anchoredPosition = new Vector2(offset - cornerLength/2, -offset);
                    else
                        rt.anchoredPosition = new Vector2(offset, -offset + cornerLength/2);
                    break;
            }
        }
    }
}