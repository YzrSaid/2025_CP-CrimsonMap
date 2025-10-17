using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class MainAppManager : MonoBehaviour
{
    public static MainAppManager Instance { get; private set; }

    public Button homeButton;
    public Button navigateButton;
    public Button settingsButton;

    public Image homeButtonImage;
    public Image navigateButtonImage;
    public Image settingsButtonImage;

    public GameObject homeUnderline;
    public GameObject navigateUnderline;
    public GameObject settingsUnderline;
    public Color activeColor = new Color32(184, 16, 19, 255);
    public Color inactiveColor = new Color32(30, 30, 30, 255);

    public GameObject homePanel;
    public GameObject explorePanel;
    public GameObject settingsPanel;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        homeButton.onClick.AddListener(OnHomeButtonClicked);
        navigateButton.onClick.AddListener(OnNavigateButtonClicked);
        settingsButton.onClick.AddListener(OnSettingsButtonClicked);
    }

    void OnHomeButtonClicked()
    {
        homeButtonImage.color = activeColor;
        navigateButtonImage.color = inactiveColor;
        settingsButtonImage.color = inactiveColor;

        homeUnderline.SetActive(true);
        navigateUnderline.SetActive(false);
        settingsUnderline.SetActive(false);

        homePanel.SetActive(true);
        explorePanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    void OnNavigateButtonClicked()
    {
        navigateButtonImage.color = activeColor;
        homeButtonImage.color = inactiveColor;
        settingsButtonImage.color = inactiveColor;

        homeUnderline.SetActive(false);
        navigateUnderline.SetActive(true);
        settingsUnderline.SetActive(false);

        homePanel.SetActive(false);
        explorePanel.SetActive(true);
        settingsPanel.SetActive(false);
    }

    void OnSettingsButtonClicked()
    {
        settingsButtonImage.color = activeColor;
        homeButtonImage.color = inactiveColor;
        navigateButtonImage.color = inactiveColor;

        homeUnderline.SetActive(false);
        navigateUnderline.SetActive(false);
        settingsUnderline.SetActive(true);

        homePanel.SetActive(false);
        explorePanel.SetActive(false);
        settingsPanel.SetActive(true);
    }
}