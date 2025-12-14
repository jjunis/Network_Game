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

    private string serverUrl = "http://localhost:3000";
    private string roomName;

    void Start()
    {
        roomName = LobbyUI.CurrentRoomName;
        Debug.Log($"🎮 게임 시작 - 방: {roomName}");

        StartCoroutine(InitGame());

        foreach (var p in players)
        {
            turnOrder.Add(p);
            p.OnWin = () => OnWin();
        }

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
            else
            {
                Debug.LogError("❌ 초기화 실패: " + www.error);
            }
        }
    }

    void Update()
    {
        if (gameOver) return;

        if (isPlayerTurn && Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("🎲 Space 눌림!");
            StartCoroutine(RequestDiceAndRoll());
        }
    }

    // ✅ 개선: 서버 요청 후 로컬 주사위 굴리기
    IEnumerator RequestDiceAndRoll()
    {
        isPlayerTurn = false;
        Debug.Log("⏳ 서버에 주사위 값 요청 중...");

        string json = "{\"roomName\":\"" + roomName + "\"}";
        int serverDiceValue = 0;

        // 1단계: 서버에서 주사위 값 받기
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
                Debug.Log($"✅ 서버 주사위 값 받음: {serverDiceValue}");
            }
            else
            {
                Debug.LogError("❌ 서버 통신 실패: " + www.error);
                isPlayerTurn = true;
                yield break;
            }
        }

        if (serverDiceValue <= 0)
        {
            Debug.LogError("❌ 잘못된 주사위 값: " + serverDiceValue);
            isPlayerTurn = true;
            yield break;
        }

        // 2단계: 로컬 주사위 물리 시뮬레이션
        Debug.Log("🎲 로컬 주사위 애니메이션 시작...");
        infoText.text = "주사위 굴리는 중...";

        diceReader.RollDice();
        yield return new WaitUntil(() => !diceReader.isRolling);
        yield return new WaitForSeconds(0.3f);

        int localDiceValue = diceReader.GetTopNumber();
        Debug.Log($"🎲 로컬 주사위 값: {localDiceValue}");

        // ✅ 실제 게임에서는 서버 값 사용 (동기화 보장)
        int usedDiceValue = serverDiceValue;
        Debug.Log($"✅ 최종 사용 주사위: {usedDiceValue}");

        yield return StartCoroutine(ProcessPlayerTurn(usedDiceValue));
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
                Debug.Log($"🎮 {p.playerName} 이동 시작 - 주사위: {dice}");

                bool done = false;
                StartCoroutine(p.MoveStepsWithCallback(dice, () => done = true));
                yield return new WaitUntil(() => done);
                yield return new WaitForSeconds(0.3f);

                Debug.Log($"✅ {p.playerName} 이동 완료: {p.currentIndex}칸");

                // 서버에 전송
                yield return StartCoroutine(SendPlayerMove(p.playerName, dice));

                // 보스 활성화 확인
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

            NextTurn();

            if (gameOver) yield break;

            if (currentTurnIndex < turnOrder.Count && turnOrder[currentTurnIndex] is BossToken && bossActive)
            {
                Debug.Log("🔴 보스 턴 시작");
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
        Debug.Log("📡 플레이어 이동 전송: " + json);

        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/game/move_player", "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(body);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("✅ 서버에 전송 완료");
            }
            else
            {
                Debug.LogError("❌ 전송 실패: " + www.error);
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
                if (active) Debug.Log("⚠️ 서버에서 보스 활성화 수신");
                bossActive = active;
            }
        }
    }

    IEnumerator ProcessBossTurn()
    {
        if (gameOver) yield break;

        isPlayerTurn = false;

        infoText.text = "🔴 보스가 주사위를 굴리는 중...";
        Debug.Log("⏳ 보스 주사위 시작");
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

        Debug.Log($"🔴 보스 이동 완료: {boss.currentIndex}");

        yield return StartCoroutine(SendBossMove(bossDice));

        int alive = 0;
        foreach (var p in players)
        {
            if (!p.isEliminated) alive++;
        }

        Debug.Log($"👥 살아있는 플레이어: {alive}명");

        if (alive == 0)
        {
            infoText.text = "🔴 보스가 모든 플레이어를 잡았습니다!";
            gameOver = true;
            Debug.Log("🏁 게임 오버: 보스 승리!");
            yield break;
        }

        NextTurn();

        if (currentTurnIndex < turnOrder.Count && turnOrder[currentTurnIndex] is PlayerToken)
        {
            isPlayerTurn = true;
            Debug.Log("✅ 플레이어 턴으로 전환");
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

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("✅ 보스 이동 전송 완료");
            }
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
                if (p.isEliminated)
                {
                    infoText.text = $"❌ {p.playerName}는 탈락!";
                }
                else
                {
                    infoText.text = $"✅ {p.playerName}의 턴! (Space)";
                }
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

        while (currentTurnIndex < turnOrder.Count && turnOrder[currentTurnIndex] is PlayerToken)
        {
            PlayerToken p = (PlayerToken)turnOrder[currentTurnIndex];
            if (p.isEliminated)
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

        Debug.Log($"📌 다음 턴: {currentTurnIndex}");
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
        Debug.Log("🏁 게임 오버: 플레이어 승리!");
    }

    int GetInt(string json, string key)
    {
        string find = "\"" + key + "\":";
        int idx = json.IndexOf(find);
        if (idx == -1) return 0;

        idx += find.Length;
        int end = json.IndexOf(",", idx);
        if (end == -1) end = json.IndexOf("}", idx);

        if (end > idx)
        {
            string val = json.Substring(idx, end - idx).Trim();
            if (int.TryParse(val, out int result))
                return result;
        }
        return 0;
    }
}
