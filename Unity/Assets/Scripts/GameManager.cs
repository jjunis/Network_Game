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

    private string serverUrl = "http://localhost:3000";
    private string currentRoomName;

    private void Start()
    {
        currentRoomName = LobbyUI.CurrentRoomName;
        Debug.Log($"🎮 게임 시작 - 방: {currentRoomName}");

        StartCoroutine(InitializeGameOnServer());

        foreach (var player in players)
        {
            turnOrder.Add(player);
            player.OnWin = OnPlayerWin;
        }

        if (players.Count > 0)
        {
            infoText.text = $"{players[0].playerName}의 턴! 주사위를 굴려주세요.";
        }
    }

    IEnumerator InitializeGameOnServer()
    {
        string playersJson = "[";
        for (int i = 0; i < players.Count; i++)
        {
            playersJson += "\"" + players[i].playerName + "\"";
            if (i < players.Count - 1) playersJson += ",";
        }
        playersJson += "]";

        string json = "{\"roomName\":\"" + currentRoomName + "\", \"players\":" + playersJson + "}";
        Debug.Log("📡 서버 게임 초기화: " + json);

        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/game/init", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("✅ 서버 게임 초기화 완료");
            }
            else
            {
                Debug.LogError("❌ 초기화 실패: " + www.error);
            }
        }
    }

    private void Update()
    {
        if (gameOver) return;

        if (isWaitingForDice && Input.GetKeyDown(KeyCode.Space))
        {
            StartCoroutine(RequestDiceFromServer());
        }
    }

    IEnumerator RequestDiceFromServer()
    {
        isWaitingForDice = false;

        string json = "{\"roomName\":\"" + currentRoomName + "\"}";

        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/game/roll_dice", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string response = www.downloadHandler.text;
                int diceValue = ExtractIntFromJSON(response, "diceValue");

                if (diceValue > 0)
                {
                    Debug.Log($"✅ 주사위: {diceValue}");
                    yield return StartCoroutine(ProcessTurn(diceValue));
                }
            }
            else
            {
                Debug.LogError("❌ 주사위 요청 실패: " + www.error);
            }
        }

        isWaitingForDice = true;
    }

    IEnumerator ProcessTurn(int diceValue)
    {
        if (gameOver) yield break;

        object current = turnOrder[currentTurnIndex];

        if (current is PlayerToken)
        {
            PlayerToken player = (PlayerToken)current;

            if (!player.isEliminated)
            {
                infoText.text = $"{player.playerName}의 턴! 주사위: {diceValue}";
                Debug.Log($"🎮 {player.playerName} 이동 시작");

                bool moveFinished = false;
                yield return StartCoroutine(player.MoveStepsWithCallback(diceValue, () => moveFinished = true));
                yield return new WaitUntil(() => moveFinished);
                yield return new WaitForSeconds(0.5f);

                yield return StartCoroutine(NotifyPlayerMove(player.playerName, diceValue));
                yield return StartCoroutine(CheckBossActivation());

                if (!bossActive && AllPlayersPassedThreshold())
                {
                    bossActive = true;
                    turnOrder.Add(boss);
                    infoText.text = "⚠️ 모든 플레이어가 15칸을 지났습니다! 보스가 나타났습니다!";
                    yield return new WaitForSeconds(1f);
                }
            }

            NextTurn();

            if (gameOver)
            {
                Debug.Log("🏁 게임 오버");
                yield break;
            }

            isWaitingForDice = true;

            if (currentTurnIndex < turnOrder.Count && turnOrder[currentTurnIndex] is BossToken && bossActive)
            {
                yield return new WaitForSeconds(0.5f);
                StartCoroutine(ProcessBossTurn());
            }
            else
            {
                UpdateNextTurnDisplay();
            }
        }
    }

    IEnumerator NotifyPlayerMove(string playerName, int steps)
    {
        string json = "{\"roomName\":\"" + currentRoomName + "\", \"nickName\":\"" + playerName + "\", \"steps\":" + steps + "}";

        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/game/move_player", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();
        }
    }

    IEnumerator CheckBossActivation()
    {
        string json = "{\"roomName\":\"" + currentRoomName + "\"}";

        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/game/check_boss_activation", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string response = www.downloadHandler.text;
                bossActive = response.Contains("\"bossActive\":true");
            }
        }
    }

    IEnumerator ProcessBossTurn()
    {
        if (gameOver) yield break;

        isWaitingForDice = false;

        infoText.text = "보스가 주사위를 굴리는 중...";
        yield return new WaitForSeconds(1f);

        diceReader.RollDice();
        yield return new WaitUntil(() => !diceReader.isRolling);
        yield return new WaitForSeconds(0.5f);

        int bossDiceValue = diceReader.GetTopNumber();
        infoText.text = $"보스의 턴! 주사위: {bossDiceValue}";
        Debug.Log($"🔴 보스 주사위: {bossDiceValue}");

        bool moveFinished = false;
        yield return StartCoroutine(boss.MoveStepsWithCallback(bossDiceValue, players, () => moveFinished = true));
        yield return new WaitUntil(() => moveFinished);
        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(NotifyBossMove(bossDiceValue));

        int alive = 0;
        foreach (var p in players)
        {
            if (!p.isEliminated) alive++;
        }

        Debug.Log($"👥 살아있는 플레이어: {alive}명");

        if (alive == 0)
        {
            infoText.text = "🎮 보스가 모든 플레이어를 잡았습니다! 게임 오버!";
            gameOver = true;
            Debug.Log("🔴 게임 오버: 보스 승리!");
            yield break;
        }

        NextTurn();
        isWaitingForDice = true;

        Debug.Log($"✅ 다음 턴: {currentTurnIndex}");
        UpdateNextTurnDisplay();
    }

    IEnumerator NotifyBossMove(int steps)
    {
        string json = "{\"roomName\":\"" + currentRoomName + "\", \"steps\":" + steps + "}";

        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/game/move_boss", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();
        }
    }

    void UpdateNextTurnDisplay()
    {
        if (currentTurnIndex < turnOrder.Count)
        {
            object nextTurn = turnOrder[currentTurnIndex];
            if (nextTurn is PlayerToken)
            {
                PlayerToken nextPlayer = (PlayerToken)nextTurn;
                infoText.text = nextPlayer.isEliminated ?
                    $"{nextPlayer.playerName}는 탈락했습니다!" :
                    $"{nextPlayer.playerName}의 턴! 주사위를 굴려주세요.";
            }
            else if (nextTurn is BossToken && bossActive)
            {
                infoText.text = "보스의 턴!";
            }
        }
    }

    bool AllPlayersPassedThreshold()
    {
        foreach (var player in players)
        {
            if (!player.isEliminated && player.currentIndex < bossActivationThreshold)
                return false;
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
        Debug.Log("🏁 게임 오버: 플레이어 승리!");
    }

    int ExtractIntFromJSON(string json, string key)
    {
        try
        {
            string searchKey = "\"" + key + "\":";
            int startIndex = json.IndexOf(searchKey) + searchKey.Length;
            int endIndex = json.IndexOf(",", startIndex);
            if (endIndex == -1) endIndex = json.IndexOf("}", startIndex);

            string valueStr = json.Substring(startIndex, endIndex - startIndex).Trim();
            if (int.TryParse(valueStr, out int value))
                return value;
        }
        catch { }
        return 0;
    }
}
