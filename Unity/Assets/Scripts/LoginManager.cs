using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Collections;

public class LoginManager : MonoBehaviour
{
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public TMP_Text resultText;

    string serverUrl = "http://localhost:3000"; // Node 서버 주소

    public void OnRegisterClick()
    {
        StartCoroutine(Register());
    }

    public void OnLoginClick()
    {
        StartCoroutine(Login());
    }

    IEnumerator Register()
    {
        WWWForm form = new WWWForm();
        form.AddField("username", usernameInput.text);
        form.AddField("password", passwordInput.text);

        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl + "/register", form))
        {
            yield return www.SendWebRequest();
            resultText.text = www.downloadHandler.text;
        }
    }

    IEnumerator Login()
    {
        WWWForm form = new WWWForm();
        form.AddField("username", usernameInput.text);
        form.AddField("password", passwordInput.text);

        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl + "/login", form))
        {
            yield return www.SendWebRequest();
            resultText.text = www.downloadHandler.text;
        }
    }
}
