using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;

[RequireComponent( typeof( Button ) )]
public class PanelOpener : MonoBehaviour
{
    [Header( "Panels to Open" )]
    public List<GameObject> panelsToOpen = new List<GameObject>();
    
    [Header( "Background Panel" )]
    public GameObject BackgroundForPanel;
    
    [Header( "Animation Settings" )]
    public float animationDuration = 0.3f;
    public Ease easeType = Ease.OutBack;
    
    private Button button;
    private Dictionary<GameObject, Vector3> originalScales = new Dictionary<GameObject, Vector3>();

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener( OpenPanels );
        
        // Store original scales
        foreach (var panel in panelsToOpen)
        {
            if (panel != null && !originalScales.ContainsKey(panel))
            {
                originalScales.Add(panel, panel.transform.localScale);
            }
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OpenPanels);
        }
    }
    
    private void OpenPanels()
    {
        // Open the BG Panel first (instantly)
        if (BackgroundForPanel != null)
        {
            BackgroundForPanel.SetActive(true);
        }
        
        // Animate panels opening
        foreach (GameObject panel in panelsToOpen)
        {
            if (panel != null)
            {
                panel.SetActive(true);
                
                // Start from scale zero
                panel.transform.localScale = Vector3.zero;
                
                // Animate to original scale with ease in
                panel.transform.DOScale(originalScales[panel], animationDuration)
                    .SetEase(easeType)
                    .SetUpdate(true); 
            }
        }
    }
}