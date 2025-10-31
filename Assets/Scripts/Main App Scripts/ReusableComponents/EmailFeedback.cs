using UnityEngine;

public class EmailFeedback : MonoBehaviour
{
    [Header("Email Settings")]
    public string recipientEmail = "said.mohammadaldrin.2021@gmail.com";
    
    public string emailSubject = "Bug Report / Feedback";
    [TextArea(3, 10)]
    public string emailBody = "Please describe your issue or feedback here:\n\n";

    public void OpenEmailClient()
    {
        string subject = EscapeURL(emailSubject);
        string body = EscapeURL(emailBody);
        string mailtoURL = $"mailto:{recipientEmail}?subject={subject}&body={body}";
        Application.OpenURL(mailtoURL);
    }
    
    private string EscapeURL(string url)
    {
        return UnityEngine.Networking.UnityWebRequest.EscapeURL(url).Replace("+", "%20");
    }
}