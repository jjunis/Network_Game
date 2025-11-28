using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // 텍스트 띄울 거니까 필수

public class RoomInfoDisplay : MonoBehaviour
{
    public TMP_Text infoText; // 화면에 글자 띄울 컴포넌트

    void Start()
    {
        // LobbyUI에 저장해뒀던 방 이름과 내 닉네임을 가져옴
        string room = LobbyUI.CurrentRoomName;
        string nick = LobbyUI.MyNickName;

        if (string.IsNullOrEmpty(room))
        {
            infoText.text = "방 정보 없음 (그냥 실행했음)";
        }
        else
        {
            infoText.text = $" 현재 방: {room}\n 플레이어 이름: {nick}";
            Debug.Log($"[게임시작] 방: {room}, 닉네임: {nick}");
        }
    }
}