using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class QuestionGenerate : MonoBehaviour
{

    public static string actualAnswer;
    public bool displayingQuestion = false;

    void Update()
    {
        if (!displayingQuestion)
        {
            displayingQuestion = true;
            // Display the new set question and answers
            QuestionDisplay.newQuestion = "Papasa ba kami sa Capstone?";
            QuestionDisplay.trueBtnNew = "True";
            QuestionDisplay.falseBtnNew = "False";
            actualAnswer = "False"; // Set the actual answer for the question
        }
    }
}
