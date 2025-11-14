using UnityEngine;
using UnityEngine.UI;   // ← 변경

public class GameManager : MonoBehaviour
{
    public PlayerToken playerToken;
    public Text infoText;   // ← 타입 변경

    private void Start()
    {
        playerToken.OnWin = OnPlayerWin;
    }

    private void OnPlayerWin()
    {
        infoText.text = "승리! 게임 종료!";
        Debug.Log("게임이 승리로 종료됨.");
    }
}
