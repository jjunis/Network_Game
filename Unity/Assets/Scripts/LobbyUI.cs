using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LobbyUI : MonoBehaviour
{
    public TMP_InputField roomNameInput;
    public GameObject createRoomPanel;

    public void OnClickCreateRoom()
    {
        createRoomPanel.SetActive(true);
        Debug.Log("버튼눌림.");

    }

    public void OnClickConfirmCreateRoom()
    {
        string roomName = roomNameInput.text;

        if (string.IsNullOrEmpty(roomName))
        {
            Debug.Log("방 이름이 비어있습니다.");
            return;
        }

        Debug.Log("방 생성 요청: " + roomName);

        // 나중에 서버랑 연결할 때 사용됨
        // CreateRoom(roomName);

        createRoomPanel.SetActive(false);
    }
}
