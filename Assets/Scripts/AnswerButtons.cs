using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AnswerButtons : MonoBehaviour
{
    public GameObject answer1backBlue;  // True button backgrounds
    public GameObject answer1backGreen;
    public GameObject answer1backRed;

    public GameObject answer2backBlue;  // False button backgrounds
    public GameObject answer2backGreen;
    public GameObject answer2backRed;

    public GameObject exitButton;  // Exit button

    private string lastSelectedAnswer; // To store user's selected answer

    void Start()
    {
        // Hide the exit button when the scene starts
        exitButton.SetActive(false);
    }

    public void OnAnswerClicked(string selectedAnswer)
    {
        lastSelectedAnswer = selectedAnswer;

        Debug.Log("Button clicked: " + selectedAnswer + " | Actual: " + QuestionGenerate.actualAnswer);

        if (selectedAnswer == QuestionGenerate.actualAnswer)
        {
            // Correct answer
            if (selectedAnswer == "True")
            {
                answer1backBlue.SetActive(false);
                answer1backGreen.SetActive(true);
                answer1backRed.SetActive(false);

                answer2backBlue.SetActive(false);
            }
            else if (selectedAnswer == "False")
            {
                answer2backBlue.SetActive(false);
                answer2backGreen.SetActive(true);
                answer2backRed.SetActive(false);

                answer1backBlue.SetActive(false);
            }
        }
        else
        {
            // Wrong answer
            if (selectedAnswer == "True")
            {
                answer1backBlue.SetActive(false);
                answer1backGreen.SetActive(false);
                answer1backRed.SetActive(true);

                answer2backBlue.SetActive(false);
            }
            else if (selectedAnswer == "False")
            {
                answer2backBlue.SetActive(false);
                answer2backGreen.SetActive(false);
                answer2backRed.SetActive(true);

                answer1backBlue.SetActive(false);
            }
        }

        // ✅ Show exit button AFTER showing results
        exitButton.SetActive(true);
    }

    public void ResetButtons()
    {
        // Reset both answer button visuals to blue only
        answer1backBlue.SetActive(true);
        answer1backGreen.SetActive(false);
        answer1backRed.SetActive(false);

        answer2backBlue.SetActive(true);
        answer2backGreen.SetActive(false);
        answer2backRed.SetActive(false);

        // Hide the exit button again
        exitButton.SetActive(false);
    }
}
