using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class LoginManager : MonoBehaviour
{
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public TMP_Text resultText;

    //string serverUrl = "http://localhost:3000";
    string serverUrl = "http://172.30.1.13:3000";  //이것도

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
        // 사용자가 입력한 아이디를 변수에 담아둠
        string currentID = usernameInput.text;

        form.AddField("username", currentID);
        form.AddField("password", passwordInput.text);

        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl + "/login", form))
        {
            yield return www.SendWebRequest();
            string result = www.downloadHandler.text;
            resultText.text = result;

            if (result.Contains("\"success\":true"))
            {
                // ★ 수정된 부분: 로그인 성공하면 아이디를 LobbyUI의 변수에 저장!
                // (LobbyUI 스크립트에 MyNickName 변수가 static으로 있어서 접근 가능함)
                LobbyUI.MyNickName = currentID;

                SceneManager.LoadScene("LobbyScene");
            }
        }
    }
}