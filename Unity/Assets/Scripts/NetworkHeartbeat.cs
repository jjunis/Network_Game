using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class NetworkHeartbeat : MonoBehaviour
{
    string serverUrl = "http://localhost:3000";

    void Start()
    {
        // 게임 씬 시작하자마자 생존신고 시작
        StartCoroutine(KeepAliveLoop());
    }

    IEnumerator KeepAliveLoop()
    {
        while (true)
        {
            // 내 닉네임 가져오기
            string myNick = LobbyUI.MyNickName;

            if (!string.IsNullOrEmpty(myNick))
            {
                // JSON 만들기
                string json = "{\"nickName\":\"" + myNick + "\"}";

                // 서버로 쏘기
                using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/ping", "POST"))
                {
                    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                    www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    www.downloadHandler = new DownloadHandlerBuffer();
                    www.SetRequestHeader("Content-Type", "application/json");

                    yield return www.SendWebRequest();
                }
            }

            // 1초 쉬고 다시 보냄
            yield return new WaitForSeconds(1f);
        }
    }
}