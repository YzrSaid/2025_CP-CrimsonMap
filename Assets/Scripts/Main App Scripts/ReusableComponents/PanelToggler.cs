using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;

[RequireComponent(typeof(Button))]
public class PanelToggler : MonoBehaviour
{
    [Header("Panels To Toggle")]
    public List<GameObject> panelsToToggle = new List<GameObject>();

    private Button button;
    private Dictionary<GameObject, Vector3> originalScales = new Dictionary<GameObject, Vector3>();

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(TogglePanels);

        // Save original scales
        foreach (var panel in panelsToToggle)
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
            button.onClick.RemoveListener(TogglePanels);
        }
    }

    private void TogglePanels()
    {
        foreach (GameObject panel in panelsToToggle)
        {
            if (panel != null)
            {
                if (panel.activeSelf)
                {
                    // Animate hide
                    panel.transform.DOScale(Vector3.zero, 0.18f).SetEase(Ease.InBack)
                        .OnComplete(() => panel.SetActive(false));
                }
                else
                {
                    panel.SetActive(true);
                    panel.transform.localScale = Vector3.zero;
                    panel.transform.DOScale(originalScales[panel], 0.18f).SetEase(Ease.OutBack);
                }
            }
        }
    }
}
