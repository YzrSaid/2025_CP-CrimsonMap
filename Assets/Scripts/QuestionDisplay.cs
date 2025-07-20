using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestionDisplay : MonoBehaviour
{
    public GameObject screenQuestion;
    public GameObject trueBtn;
    public GameObject falseBtn;
    public static string newQuestion;
    public static string trueBtnNew;
    public static string falseBtnNew;
    void Start()
    {
        StartCoroutine(PushTextOnScreen());
    }

    void Update()
    {

    }

    IEnumerator PushTextOnScreen()
    {
        yield return new WaitForSeconds(0.25f);
        screenQuestion.GetComponent<TextMeshProUGUI>().text = newQuestion;
        trueBtn.GetComponentInChildren<TextMeshProUGUI>().text = trueBtnNew;
        falseBtn.GetComponentInChildren<TextMeshProUGUI>().text = falseBtnNew;

    }
}
