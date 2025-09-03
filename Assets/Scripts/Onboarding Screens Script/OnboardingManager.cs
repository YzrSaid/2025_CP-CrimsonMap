using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class OnboardingManager : MonoBehaviour
{
    public List<GameObject> pages;
    public PageIndicator pageIndicator;
    public Button nextButton;
    public Button skipButton;
    public Button getStartedButton;
    private int currentPage = 0;

    void Start()
    {
        if (GlobalManager.Instance.onboardingComplete)
        {
            // If onboarding is already complete, skip to the main scene
            SceneManager.LoadScene("MainAppScene");
            return;
        }
        ShowPage(0);
        pageIndicator.SetupIndicators(pages.Count);
        pageIndicator.SetActivePage(0);

        nextButton.onClick.AddListener(NextPage);
        skipButton.onClick.AddListener(SkipOnboarding);
        getStartedButton.onClick.AddListener(FinishOnboarding);
    }

    void ShowPage(int index)
    {
        StopAllCoroutines();

        if (index == 0)
        {
            // First page will just show immediately, no fade
            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].SetActive(i == 0);

                if (pages[i].TryGetComponent<CanvasGroup>(out var cg))
                {
                    cg.alpha = (i == 0) ? 1 : 0;
                    cg.interactable = (i == 0);
                    cg.blocksRaycasts = (i == 0);
                }
            }

            currentPage = 0;
        }
        else
        {
            // Normal fade transition for other pages
            StartCoroutine(FadeToPage(index));
        }

        pageIndicator.SetActivePage(index);

        nextButton.gameObject.SetActive(index < pages.Count - 1);
        skipButton.gameObject.SetActive(index < pages.Count - 1);
        getStartedButton.gameObject.SetActive(index == pages.Count - 1);
    }

    IEnumerator FadeToPage(int targetIndex)
    {
        float duration = 0.1f;

        // Fade out current page
        if (currentPage < pages.Count)
        {
            CanvasGroup currentGroup = pages[currentPage].GetComponent<CanvasGroup>();
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                currentGroup.alpha = Mathf.Lerp(1, 0, t / duration);
                yield return null;
            }
            currentGroup.alpha = 0;
            currentGroup.interactable = false;
            currentGroup.blocksRaycasts = false;
            pages[currentPage].SetActive(false);
        }

        // Fade in new page
        GameObject newPage = pages[targetIndex];
        newPage.SetActive(true);
        CanvasGroup newGroup = newPage.GetComponent<CanvasGroup>();
        newGroup.alpha = 0;
        newGroup.interactable = true;
        newGroup.blocksRaycasts = true;

        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            newGroup.alpha = Mathf.Lerp(0, 1, t / duration);
            yield return null;
        }
        newGroup.alpha = 1;

        currentPage = targetIndex;
    }


    void NextPage()
    {
        if (currentPage < pages.Count - 1)
        {
            currentPage++;
            ShowPage(currentPage);
        }
    }

    void SkipOnboarding()
    {
        ShowPage(3);
    }

    void FinishOnboarding()
    {
        GlobalManager.Instance.onboardingComplete = true;
        GlobalManager.Instance.SaveData();
        SceneManager.LoadScene("MainAppScene");
    }
}
