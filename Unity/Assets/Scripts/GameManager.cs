using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public List<PlayerToken> players;
    public BossToken boss;
    public GameBoard gameBoard;
    public Text infoText;
    public DiceReader diceReader;

    private List<object> turnOrder = new List<object>();
    private int currentTurnIndex = 0;
    private bool isWaitingForDice = true;
    private bool bossActive = false;
    private int bossActivationThreshold = 15;
    private bool gameOver = false;

    // ★ 서버 URL
    //private string serverUrl = "http://172.30.1.13:3000";
    private string serverUrl = "http://localhost:3000";
    private string currentRoomName;

    private void Start()
    {
        currentRoomName = LobbyUI.CurrentRoomName;

        // 서버에 게임 초기화 요청
        StartCoroutine(InitializeGameOnServer());

        foreach (var player in players)
        {
            turnOrder.Add(player);
            player.OnWin = OnPlayerWin;
        }

        infoText.text = $"{((PlayerToken)turnOrder[0]).playerName}의 턴! 주사위를 굴려주세요.";
    }

    IEnumerator InitializeGameOnServer()
    {
        string json = "{\"roomName\":\"" + currentRoomName + "\"}";
        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/init_game", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();
            Debug.Log("🟢 서버에서 게임 초기화 완료");
        }
    }

    private void Update()
    {
        if (gameOver) return;

        if (isWaitingForDice && Input.GetKeyDown(KeyCode.Space))
        {
            // ★ 서버로 주사위 굴리기 요청
            StartCoroutine(RequestDiceRollFromServer());
        }
    }

    IEnumerator RequestDiceRollFromServer()
    {
        isWaitingForDice = false;

        string json = "{\"roomName\":\"" + currentRoomName + "\"}";
        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/roll_dice", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                // ★ 서버에서 받은 주사위 값
                string response = www.downloadHandler.text;
                int diceValue = int.Parse(response.Split(':')[1].Split('}')[0]);

                Debug.Log("🎲 서버 주사위: " + diceValue);
                yield return StartCoroutine(ProcessTurn(diceValue));
            }
        }

        isWaitingForDice = true;
    }

    IEnumerator ProcessTurn(int diceValue)
    {
        object current = turnOrder[currentTurnIndex];

        if (current is PlayerToken)
        {
            PlayerToken player = (PlayerToken)current;

            if (!player.isEliminated)
            {
                infoText.text = $"{player.playerName}의 턴! 주사위: {diceValue}";

                bool moveFinished = false;
                yield return StartCoroutine(player.MoveStepsWithCallback(diceValue, () => moveFinished = true));
                yield return new WaitUntil(() => moveFinished);
                yield return new WaitForSeconds(0.5f);

                // ★ 서버에 플레이어 이동 알리기
                yield return StartCoroutine(NotifyPlayerMove(player.playerName, diceValue));

                if (!bossActive && AllPlayersPassedThreshold())
                {
                    bossActive = true;
                    turnOrder.Add(boss);
                    infoText.text = "⚠️ 모든 플레이어가 15칸을 지났습니다! 보스가 나타났습니다!";
                    yield return new WaitForSeconds(1f);
                }
            }

            NextTurn();

            if (gameOver) yield break;

            if (currentTurnIndex < turnOrder.Count && turnOrder[currentTurnIndex] is BossToken && bossActive)
            {
                yield return new WaitForSeconds(0.5f);
                StartCoroutine(ProcessBossTurn());
            }
        }
    }

    IEnumerator NotifyPlayerMove(string playerName, int steps)
    {
        string json = "{\"roomName\":\"" + currentRoomName + "\", \"nickName\":\"" + playerName + "\", \"steps\":" + steps + "}";
        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/move_player", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();
            Debug.Log($"✅ {playerName}의 이동이 서버에 기록됨");
        }
    }

    IEnumerator ProcessBossTurn()
    {
        isWaitingForDice = false;

        infoText.text = "보스가 주사위를 굴리는 중...";
        yield return new WaitForSeconds(1f);

        diceReader.RollDice();
        yield return new WaitUntil(() => !diceReader.isRolling);
        yield return new WaitForSeconds(0.5f);

        int bossDiceValue = diceReader.GetTopNumber();
        infoText.text = $"보스의 턴! 주사위: {bossDiceValue}";

        bool moveFinished = false;
        yield return StartCoroutine(boss.MoveStepsWithCallback(bossDiceValue, players, () => moveFinished = true));
        yield return new WaitUntil(() => moveFinished);
        yield return new WaitForSeconds(0.5f);

        // ★ 서버에 보스 이동 알리기
        yield return StartCoroutine(NotifyBossMove(bossDiceValue));

        int alive = 0;
        foreach (var p in players)
        {
            if (!p.isEliminated) alive++;
        }
        if (alive == 0)
        {
            infoText.text = "🎮 보스가 모든 플레이어를 잡았습니다! 게임 오버!";
            gameOver = true;
            yield break;
        }

        NextTurn();
        isWaitingForDice = true;
    }

    IEnumerator NotifyBossMove(int steps)
    {
        string json = "{\"roomName\":\"" + currentRoomName + "\", \"steps\":" + steps + "}";
        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/move_boss", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();
            Debug.Log("🔴 보스 이동이 서버에 기록됨");
        }
    }

    bool AllPlayersPassedThreshold()
    {
        foreach (var player in players)
        {
            if (!player.isEliminated && player.currentIndex < bossActivationThreshold)
            {
                return false;
            }
        }
        return true;
    }

    void NextTurn()
    {
        currentTurnIndex++;
        if (currentTurnIndex >= turnOrder.Count)
            currentTurnIndex = 0;

        while (currentTurnIndex < turnOrder.Count && turnOrder[currentTurnIndex] is PlayerToken)
        {
            PlayerToken player = (PlayerToken)turnOrder[currentTurnIndex];
            if (player.isEliminated)
            {
                currentTurnIndex++;
                if (currentTurnIndex >= turnOrder.Count)
                    currentTurnIndex = 0;
            }
            else
            {
                break;
            }
        }
    }

    void OnPlayerWin()
    {
        infoText.text = "🎉 플레이어가 시작점에 도달했습니다! 게임 종료!";
        gameOver = true;
    }
}