using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class MainAppManager : MonoBehaviour
{
    public Button homeButton;
    public Button navigateButton;
    public Button settingsButton;

    public GameObject homeUnderline;
    public GameObject navigateUnderline;
    public GameObject settingsUnderline;
    public Color activeColor = new Color32(184, 16, 19, 255);
    public Color inactiveColor = new Color32(30, 30, 30, 255);

    public GameObject homePanel;
    public GameObject explorePanel;
    public GameObject settingsPanel;

    void Start()
    {
        // These listeners will be used to handle navigation bar buttons and when clicked it will make the appropriate panel active
        homeButton.onClick.AddListener(OnHomeButtonClicked);
        navigateButton.onClick.AddListener(OnNavigateButtonClicked);
        settingsButton.onClick.AddListener(OnSettingsButtonClicked);
    }

    void OnHomeButtonClicked()
    {
        // Set the home button to active color
        homeButton.GetComponent<Image>().color = activeColor;

        // Set other buttons to inactive color
        navigateButton.GetComponent<Image>().color = inactiveColor;
        settingsButton.GetComponent<Image>().color = inactiveColor;

        // Show underline only under the active button
        homeUnderline.SetActive(true);
        navigateUnderline.SetActive(false);
        settingsUnderline.SetActive(false);

        // Activate the home panel and deactivate others
        homePanel.SetActive(true);
        explorePanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    void OnNavigateButtonClicked()
    {
        // Set the home button to active color
        navigateButton.GetComponent<Image>().color = activeColor;

        // Set other buttons to inactive color
        homeButton.GetComponent<Image>().color = inactiveColor;
        settingsButton.GetComponent<Image>().color = inactiveColor;

        // Show underline only under the active button
        homeUnderline.SetActive(false);
        navigateUnderline.SetActive(true);
        settingsUnderline.SetActive(false);

        // Activate the home panel and deactivate others
        homePanel.SetActive(false);
        explorePanel.SetActive(true);
        settingsPanel.SetActive(false);
    }

    void OnSettingsButtonClicked()
    {
        // Set the home button to active color
        settingsButton.GetComponent<Image>().color = activeColor;

        // Set other buttons to inactive color
        homeButton.GetComponent<Image>().color = inactiveColor;
        navigateButton.GetComponent<Image>().color = inactiveColor;

        // Show underline only under the active button
        homeUnderline.SetActive(false);
        navigateUnderline.SetActive(false);
        settingsUnderline.SetActive(true);

        // Activate the home panel and deactivate others
        homePanel.SetActive(false);
        explorePanel.SetActive(false);
        settingsPanel.SetActive(true);
    }
}

