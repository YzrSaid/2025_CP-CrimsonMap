using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;

[RequireComponent(typeof(Button))]
public class PanelCloser : MonoBehaviour
{
    [Header("Panels to Close")]
    public List<GameObject> panelsToClose = new List<GameObject>();

    [Header("Background Panel")]
    public GameObject BackgroundForPanel;

    [Header("Animation Settings")]
    public float animationDuration = 0.25f;
    public Ease easeType = Ease.InBack;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(ClosePanels);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(ClosePanels);
        }
    }

    private void ClosePanels()
    {
        int panelsToAnimate = 0;
        int animationsCompleted = 0;

        foreach (GameObject panel in panelsToClose)
        {
            if (panel != null && panel.activeSelf)
            {
                panelsToAnimate++;
            }
        }

        if (panelsToAnimate == 0)
        {
            if (BackgroundForPanel != null)
            {
                BackgroundForPanel.SetActive(false);
            }
            return;
        }

        foreach (GameObject panel in panelsToClose)
        {
            if (panel != null && panel.activeSelf)
            {
                panel.transform.DOScale(Vector3.zero, animationDuration)
                    .SetEase(easeType)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        panel.SetActive(false);
                        panel.transform.localScale = Vector3.one;
                        animationsCompleted++;

                        if (animationsCompleted >= panelsToAnimate)
                        {
                            if (BackgroundForPanel != null)
                            {
                                BackgroundForPanel.SetActive(false);
                            }
                        }
                    });
            }
        }
    }
}