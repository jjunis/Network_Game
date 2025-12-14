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
    private bool isPlayerTurn = false;
    private bool bossActive = false;
    private bool gameOver = false;

    private string serverUrl = "http://172.100.242.217:3000";  // ✅ 수정
    private string roomName;
    private string myPlayerName;

    void Start()
    {
        roomName = LobbyUI.CurrentRoomName;
        myPlayerName = LobbyUI.CurrentPlayerName;
        Debug.Log($"🎮 게임 시작 - 방: {roomName}, 플레이어: {myPlayerName}");

        StartCoroutine(InitGame());

        foreach (var p in players)
        {
            turnOrder.Add(p);
            p.OnWin = () => OnWin();
        }

        // ✅ 실시간 동기화: 0.3초마다
        StartCoroutine(RealTimeSyncGameState());

        UpdateUI();
    }

    IEnumerator InitGame()
    {
        string plist = "[";
        for (int i = 0; i < players.Count; i++)
        {
            plist += "\"" + players[i].playerName + "\"";
            if (i < players.Count - 1) plist += ",";
        }
        plist += "]";

        string json = "{\"roomName\":\"" + roomName + "\",\"players\":" + plist + "}";
        Debug.Log("📡 게임 초기화 요청: " + json);

        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/game/init", "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(body);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("✅ 게임 초기화 완료");
                isPlayerTurn = true;
                UpdateUI();
            }
        }
    }

    // ✅ 실시간 동기화
    IEnumerator RealTimeSyncGameState()
    {
        while (!gameOver)
        {
            yield return new WaitForSeconds(0.2f);  // 0.2초마다 동기화

            string url = $"{serverUrl}/game/state?roomName={roomName}";

            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    SyncAllGameState(www.downloadHandler.text);
                }
            }
        }
    }

    // ✅ 모든 상태 동기화
    void SyncAllGameState(string json)
    {
        try
        {
            // 1️⃣ 플레이어 위치 동기화
            foreach (var player in players)
            {
                int serverPos = ExtractIntFromJson(json, $"\"{player.playerName}\":", ",");
                if (serverPos >= 0 && player.currentIndex != serverPos)
                {
                    player.currentIndex = serverPos;
                    Debug.Log($"🔄 {player.playerName}: {serverPos}칸");
                }
            }

            // 2️⃣ 보스 위치 동기화
            int bosPos = ExtractIntFromJson(json, "\"bossPosition\":", ",");
            if (bosPos >= 0 && boss.currentIndex != bosPos)
            {
                boss.currentIndex = bosPos;
            }

            // 3️⃣ 보스 활성화 동기화
            bool serverBossActive = json.Contains("\"bossActive\":true");
            if (serverBossActive && !bossActive)
            {
                bossActive = true;
                infoText.text = "⚠️ 보스 등장!";
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("⚠️ 동기화 오류: " + e.Message);
        }
    }

    int ExtractIntFromJson(string json, string startKey, string endKey)
    {
        int idx = json.IndexOf(startKey);
        if (idx == -1) return -1;

        idx += startKey.Length;
        int endIdx = json.IndexOf(endKey, idx);
        if (endIdx == -1) endIdx = json.Length;

        string valueStr = json.Substring(idx, endIdx - idx).Trim();
        if (int.TryParse(valueStr, out int result))
            return result;
        return -1;
    }

    void Update()
    {
        if (gameOver) return;

        if (isPlayerTurn && Input.GetKeyDown(KeyCode.Space))
        {
            if (turnOrder.Count > currentTurnIndex && turnOrder[currentTurnIndex] is PlayerToken)
            {
                PlayerToken currentPlayer = (PlayerToken)turnOrder[currentTurnIndex];
                if (currentPlayer.playerName == myPlayerName)
                {
                    Debug.Log("🎲 자신의 턴!");
                    StartCoroutine(RequestDiceAndRoll());
                }
            }
        }
    }

    IEnumerator RequestDiceAndRoll()
    {
        isPlayerTurn = false;

        string json = "{\"roomName\":\"" + roomName + "\"}";
        int serverDiceValue = 0;

        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/game/roll_dice", "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(body);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                serverDiceValue = GetInt(www.downloadHandler.text, "diceValue");
            }
        }

        if (serverDiceValue <= 0)
        {
            isPlayerTurn = true;
            yield break;
        }

        // ✅ UI에 주사위 표시 (서버에서 받은 값)
        infoText.text = $"🎲 주사위: {serverDiceValue}";

        diceReader.RollDice();
        yield return new WaitUntil(() => !diceReader.isRolling);
        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(ProcessPlayerTurn(serverDiceValue));
    }

    IEnumerator ProcessPlayerTurn(int dice)
    {
        if (gameOver) yield break;

        object cur = turnOrder[currentTurnIndex];

        if (cur is PlayerToken)
        {
            PlayerToken p = (PlayerToken)cur;

            if (!p.isEliminated)
            {
                infoText.text = $"{p.playerName} 이동 중... (주사위: {dice})";

                bool done = false;
                StartCoroutine(p.MoveStepsWithCallback(dice, () => done = true));
                yield return new WaitUntil(() => done);
                yield return new WaitForSeconds(0.3f);

                // ✅ 서버에 이동 전송
                yield return StartCoroutine(SendPlayerMove(p.playerName, dice));

                yield return new WaitForSeconds(0.3f);

                if (!bossActive && AllPlayersOver15())
                {
                    bossActive = true;
                    turnOrder.Add(boss);
                    infoText.text = "⚠️ 모든 플레이어가 15칸 이상!";
                    yield return new WaitForSeconds(1f);
                }
            }

            NextTurn();
            yield return StartCoroutine(UpdateServerTurn());

            if (gameOver) yield break;

            if (currentTurnIndex < turnOrder.Count && turnOrder[currentTurnIndex] is BossToken && bossActive)
            {
                yield return new WaitForSeconds(0.5f);
                StartCoroutine(ProcessBossTurn());
            }
            else
            {
                isPlayerTurn = true;
                UpdateUI();
            }
        }
    }

    IEnumerator SendPlayerMove(string nick, int steps)
    {
        string json = "{\"roomName\":\"" + roomName + "\",\"nickName\":\"" + nick + "\",\"steps\":" + steps + "}";

        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/game/move_player", "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(body);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            yield return www.SendWebRequest();
        }
    }

    IEnumerator UpdateServerTurn()
    {
        string json = "{\"roomName\":\"" + roomName + "\"}";

        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/game/next_turn", "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(body);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            yield return www.SendWebRequest();
        }
    }

    IEnumerator ProcessBossTurn()
    {
        if (gameOver) yield break;

        isPlayerTurn = false;
        infoText.text = "🔴 보스 턴!";
        yield return new WaitForSeconds(1f);

        diceReader.RollDice();
        yield return new WaitUntil(() => !diceReader.isRolling);
        yield return new WaitForSeconds(0.4f);

        int bossDice = diceReader.GetTopNumber();
        infoText.text = $"🔴 보스 이동... (주사위: {bossDice})";

        bool done = false;
        StartCoroutine(boss.MoveStepsWithCallback(bossDice, players, () => done = true));
        yield return new WaitUntil(() => done);

        yield return StartCoroutine(SendBossMove(bossDice));

        int alive = 0;
        foreach (var p in players)
        {
            if (!p.isEliminated) alive++;
        }

        if (alive == 0)
        {
            infoText.text = "🔴 보스 승리!";
            gameOver = true;
            yield break;
        }

        NextTurn();
        yield return StartCoroutine(UpdateServerTurn());

        isPlayerTurn = true;
        UpdateUI();
    }

    IEnumerator SendBossMove(int steps)
    {
        string json = "{\"roomName\":\"" + roomName + "\",\"steps\":" + steps + "}";

        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/game/move_boss", "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(body);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            yield return www.SendWebRequest();
        }
    }

    void UpdateUI()
    {
        if (gameOver) return;

        if (currentTurnIndex < turnOrder.Count)
        {
            object next = turnOrder[currentTurnIndex];

            if (next is PlayerToken)
            {
                PlayerToken p = (PlayerToken)next;
                infoText.text = p.isEliminated ?
                    $"❌ {p.playerName} 탈락!" :
                    $"✅ {p.playerName}의 턴! (Space)";
            }
            else if (next is BossToken && bossActive)
            {
                infoText.text = "🔴 보스 턴!";
            }
        }
    }

    void NextTurn()
    {
        currentTurnIndex++;
        if (currentTurnIndex >= turnOrder.Count)
            currentTurnIndex = 0;
    }

    bool AllPlayersOver15()
    {
        foreach (var p in players)
        {
            if (!p.isEliminated && p.currentIndex < 15)
                return false;
        }
        return true;
    }

    void OnWin()
    {
        infoText.text = "🎉 승리!";
        gameOver = true;
    }

    int GetInt(string json, string key)
    {
        string find = "\"" + key + "\":";
        int idx = json.IndexOf(find);
        if (idx == -1) return 0;

        idx += find.Length;
        int end = json.IndexOf(",", idx);
        if (end == -1) end = json.IndexOf("}", idx);

        string val = json.Substring(idx, end - idx).Trim();
        if (int.TryParse(val, out int result))
            return result;
        return 0;
    }
}
