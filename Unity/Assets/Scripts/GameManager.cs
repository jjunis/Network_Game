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

    private string serverUrl = "http://172.100.242.217:3000";
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

        // ✅ 백그라운드 동기화만 (실제 진행은 로컬에서)
        StartCoroutine(BackgroundSync());

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
        Debug.Log("📡 게임 초기화 요청");

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
            else
            {
                Debug.LogError("❌ 초기화 실패: " + www.error);
            }
        }
    }

    // ✅ 백그라운드에서 0.5초마다 다른 플레이어 위치만 동기화
    IEnumerator BackgroundSync()
    {
        while (!gameOver)
        {
            yield return new WaitForSeconds(0.5f);

            string url = $"{serverUrl}/game/state?roomName={roomName}";

            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    SyncOtherPlayers(www.downloadHandler.text);
                }
            }
        }
    }

    // ✅ 다른 플레이어들의 위치만 동기화 (자신은 제외)
    void SyncOtherPlayers(string json)
    {
        try
        {
            foreach (var player in players)
            {
                // 자신은 제외
                if (player.playerName == myPlayerName) continue;

                int serverPos = ExtractIntFromJson(json, $"\"{player.playerName}\":", ",");
                if (serverPos >= 0 && player.currentIndex != serverPos)
                {
                    player.currentIndex = serverPos;
                    Debug.Log($"🔄 {player.playerName}: {serverPos}칸");
                }
            }

            // 보스 위치 동기화
            int bosPos = ExtractIntFromJson(json, "\"bossPosition\":", ",");
            if (bosPos >= 0 && boss.currentIndex != bosPos)
            {
                boss.currentIndex = bosPos;
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

        // ✅ Space 키로 주사위 굴리기
        if (isPlayerTurn && Input.GetKeyDown(KeyCode.Space))
        {
            if (turnOrder.Count > currentTurnIndex && turnOrder[currentTurnIndex] is PlayerToken)
            {
                PlayerToken currentPlayer = (PlayerToken)turnOrder[currentTurnIndex];
                if (currentPlayer.playerName == myPlayerName)
                {
                    Debug.Log("🎲 자신의 턴 - 주사위 굴리기!");
                    StartCoroutine(RollDiceAndMove());
                }
                else
                {
                    Debug.Log($"⏳ {currentPlayer.playerName}의 턴 - 기다리는 중...");
                }
            }
        }
    }

    // ✅ 주사위 굴리고 이동
    IEnumerator RollDiceAndMove()
    {
        isPlayerTurn = false;

        // 1️⃣ 주사위 굴리기 (로컬)
        infoText.text = "주사위 굴리는 중...";
        diceReader.RollDice();
        yield return new WaitUntil(() => !diceReader.isRolling);

        int diceValue = diceReader.GetTopNumber();
        Debug.Log($"🎲 주사위: {diceValue}");

        infoText.text = $"🎲 주사위: {diceValue}";
        yield return new WaitForSeconds(0.5f);

        // 2️⃣ 플레이어 이동
        yield return StartCoroutine(ProcessPlayerTurn(diceValue));
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
                Debug.Log($"🎮 {p.playerName} 이동 시작");

                // 3️⃣ 로컬에서 이동
                bool done = false;
                StartCoroutine(p.MoveStepsWithCallback(dice, () => done = true));
                yield return new WaitUntil(() => done);
                yield return new WaitForSeconds(0.3f);

                Debug.Log($"✅ {p.playerName} 이동 완료: {p.currentIndex}칸");

                // 4️⃣ 서버에 이동 정보 전송
                yield return StartCoroutine(SendPlayerMove(p.playerName, dice));

                // 5️⃣ 보스 활성화 체크
                yield return StartCoroutine(CheckBossActivate());

                if (!bossActive && AllPlayersOver15())
                {
                    bossActive = true;
                    turnOrder.Add(boss);
                    infoText.text = "⚠️ 모든 플레이어가 15칸 이상! 보스 등장!";
                    Debug.Log("🔴 보스 활성화!");
                    yield return new WaitForSeconds(1f);
                }
            }

            // 6️⃣ 다음 턴
            NextTurn();

            if (gameOver) yield break;

            // 7️⃣ 보스 턴이면
            if (currentTurnIndex < turnOrder.Count && turnOrder[currentTurnIndex] is BossToken && bossActive)
            {
                Debug.Log("🔴 보스 턴!");
                yield return new WaitForSeconds(0.5f);
                StartCoroutine(ProcessBossTurn());
            }
            else
            {
                // 8️⃣ 플레이어 턴으로
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

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("✅ 서버에 전송");
            }
        }
    }

    IEnumerator CheckBossActivate()
    {
        string json = "{\"roomName\":\"" + roomName + "\"}";

        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/game/check_boss", "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(body);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                bool active = www.downloadHandler.text.Contains("\"bossActive\":true");
                if (active) bossActive = true;
            }
        }
    }

    IEnumerator ProcessBossTurn()
    {
        if (gameOver) yield break;

        isPlayerTurn = false;

        infoText.text = "🔴 보스 턴!";
        yield return new WaitForSeconds(0.8f);

        diceReader.RollDice();
        yield return new WaitUntil(() => !diceReader.isRolling);
        yield return new WaitForSeconds(0.4f);

        int bossDice = diceReader.GetTopNumber();
        Debug.Log($"✅ 보스 주사위: {bossDice}");
        infoText.text = $"🔴 보스 이동... (주사위: {bossDice})";

        bool done = false;
        StartCoroutine(boss.MoveStepsWithCallback(bossDice, players, () => done = true));
        yield return new WaitUntil(() => done);
        yield return new WaitForSeconds(0.4f);

        yield return StartCoroutine(SendBossMove(bossDice));

        int alive = 0;
        foreach (var p in players)
        {
            if (!p.isEliminated) alive++;
        }

        if (alive == 0)
        {
            infoText.text = "🔴 보스가 모든 플레이어를 잡았습니다!";
            gameOver = true;
            yield break;
        }

        NextTurn();

        if (currentTurnIndex < turnOrder.Count && turnOrder[currentTurnIndex] is PlayerToken)
        {
            isPlayerTurn = true;
            UpdateUI();
        }
        else if (currentTurnIndex < turnOrder.Count && turnOrder[currentTurnIndex] is BossToken && bossActive)
        {
            yield return new WaitForSeconds(0.5f);
            StartCoroutine(ProcessBossTurn());
        }
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
