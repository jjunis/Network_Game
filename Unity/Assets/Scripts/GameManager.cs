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
    private bool isPlayerTurn = false;  // ✅ 플레이어 턴인지 체크
    private bool bossActive = false;
    private bool gameOver = false;

    private string serverUrl = "http://localhost:3000";
    private string roomName;

    void Start()
    {
        roomName = LobbyUI.CurrentRoomName;

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
                isPlayerTurn = true;  // ✅ 게임 시작 = 플레이어 턴
                UpdateUI();
            }
            else
            {
                Debug.LogError("❌ 초기화 실패");
            }
        }
    }

    void Update()
    {
        if (gameOver) return;

        // ✅ 플레이어 턴일 때만 Space 받음
        if (isPlayerTurn && Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("🎲 Space 눌림!");
            StartCoroutine(RollDice());
        }
    }

    IEnumerator RollDice()
    {
        isPlayerTurn = false;  // ✅ 주사위 굴리는 동안 Space 못 누르게
        Debug.Log("⏳ 주사위 굴리는 중...");

        string json = "{\"roomName\":\"" + roomName + "\"}";
        int diceValue = 0;

        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/game/roll_dice", "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(body);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                diceValue = GetInt(www.downloadHandler.text, "diceValue");
                Debug.Log($"✅ 서버 주사위: {diceValue}");
            }
            else
            {
                Debug.LogError("❌ 주사위 요청 실패");
            }
        }

        if (diceValue > 0)
        {
            yield return StartCoroutine(PlayTurn(diceValue));
        }
        else
        {
            isPlayerTurn = true;  // ✅ 실패하면 다시 플레이어 턴
        }
    }

    IEnumerator PlayTurn(int dice)
    {
        object cur = turnOrder[currentTurnIndex];

        if (cur is PlayerToken)
        {
            PlayerToken p = (PlayerToken)cur;

            if (!p.isEliminated)
            {
                infoText.text = $"{p.playerName} 이동 중... (주사위: {dice})";
                Debug.Log($"🎮 {p.playerName} 이동 시작");

                bool done = false;
                StartCoroutine(p.MoveStepsWithCallback(dice, () => done = true));
                yield return new WaitUntil(() => done);
                yield return new WaitForSeconds(0.5f);

                Debug.Log($"📍 {p.playerName} 이동 완료: {p.currentIndex}");

                // 서버에 이동 전송
                yield return StartCoroutine(SendMove(p.playerName, dice));

                // 보스 활성화 확인
                yield return StartCoroutine(CheckBoss());

                // 보스 활성화 체크
                if (!bossActive && AllOver15())
                {
                    bossActive = true;
                    turnOrder.Add(boss);
                    infoText.text = "⚠️ 모든 플레이어가 15칸을 넘었습니다! 보스가 나타났습니다!";
                    Debug.Log("🔴 보스 활성화!");
                    yield return new WaitForSeconds(1f);
                }
            }

            // 다음 턴
            NextTurn();

            if (gameOver)
            {
                Debug.Log("🏁 게임 오버!");
                yield break;
            }

            // ✅ 보스 턴이면 보스턴 실행
            if (currentTurnIndex < turnOrder.Count && turnOrder[currentTurnIndex] is BossToken && bossActive)
            {
                Debug.Log("🔴 보스 턴 시작!");
                yield return new WaitForSeconds(0.5f);
                StartCoroutine(BossTurn());
            }
            else
            {
                // ✅ 플레이어 턴이면 플레이어가 Space 누를 수 있게
                isPlayerTurn = true;
                UpdateUI();
            }
        }
    }

    IEnumerator SendMove(string nick, int steps)
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
                Debug.Log($"📡 서버에 이동 전송: {nick}");
            }
        }
    }

    IEnumerator CheckBoss()
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
                bossActive = www.downloadHandler.text.Contains("\"bossActive\":true");
                Debug.Log($"✅ 보스 활성화 상태: {bossActive}");
            }
        }
    }

    IEnumerator BossTurn()
    {
        if (gameOver) yield break;

        isPlayerTurn = false;  // ✅ 보스 턴 중에는 플레이어가 못 움직이게

        infoText.text = "🔴 보스가 주사위를 굴리는 중...";
        Debug.Log("⏳ 보스 주사위 굴리는 중...");

        yield return new WaitForSeconds(1f);

        // ✅ 주사위 굴리기
        diceReader.RollDice();
        yield return new WaitUntil(() => !diceReader.isRolling);
        yield return new WaitForSeconds(0.5f);

        int bossDice = diceReader.GetTopNumber();
        Debug.Log($"✅ 보스 주사위: {bossDice}");

        infoText.text = $"🔴 보스 이동 중... (주사위: {bossDice})";

        // ✅ 보스 이동
        bool done = false;
        StartCoroutine(boss.MoveStepsWithCallback(bossDice, players, () => done = true));
        yield return new WaitUntil(() => done);
        yield return new WaitForSeconds(0.5f);

        Debug.Log($"🔴 보스 이동 완료: {boss.currentIndex}");

        // 서버에 보스 이동 전송
        yield return StartCoroutine(SendBossMove(bossDice));

        // 살아있는 플레이어 수 확인
        int alive = 0;
        foreach (var p in players)
        {
            if (!p.isEliminated) alive++;
        }

        Debug.Log($"👥 살아있는 플레이어: {alive}명");

        // ✅ 모든 플레이어 탈락했으면 게임 오버
        if (alive == 0)
        {
            infoText.text = "🔴 보스가 모든 플레이어를 잡았습니다! 게임 오버!";
            gameOver = true;
            Debug.Log("🏁 게임 오버: 보스 승리!");
            yield break;
        }

        // 다음 턴
        NextTurn();

        // ✅ 보스 턴 후 플레이어 턴으로 돌아가기
        if (currentTurnIndex < turnOrder.Count && turnOrder[currentTurnIndex] is PlayerToken)
        {
            isPlayerTurn = true;
            Debug.Log("✅ 플레이어 턴으로 전환");
            UpdateUI();
        }
        else if (currentTurnIndex < turnOrder.Count && turnOrder[currentTurnIndex] is BossToken && bossActive)
        {
            // 다시 보스 턴
            yield return new WaitForSeconds(0.5f);
            StartCoroutine(BossTurn());
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
                Debug.Log($"📡 서버에 보스 이동 전송");
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
                    infoText.text = $"❌ {p.playerName}는 탈락했습니다.";
                }
                else
                {
                    infoText.text = $"✅ {p.playerName}의 턴! (Space를 눌러주세요)";
                }
            }
            else if (next is BossToken && bossActive)
            {
                infoText.text = "🔴 보스의 턴!";
            }
        }
    }

    void NextTurn()
    {
        currentTurnIndex++;
        if (currentTurnIndex >= turnOrder.Count)
            currentTurnIndex = 0;

        // 탈락한 플레이어 스킵
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

        Debug.Log($"📌 다음 턴 인덱스: {currentTurnIndex}");
    }

    bool AllOver15()
    {
        foreach (var p in players)
        {
            if (!p.isEliminated && p.currentIndex < 15)
            {
                Debug.Log($"❌ {p.playerName}: {p.currentIndex}칸 (15칸 미만)");
                return false;
            }
        }
        Debug.Log("✅ 모든 플레이어가 15칸 이상!");
        return true;
    }

    void OnWin()
    {
        infoText.text = "🎉 플레이어가 처음 위치로 돌아왔습니다! 승리!";
        gameOver = true;
        Debug.Log("🏁 게임 오버: 플레이어 승리!");
    }

    int GetInt(string json, string key)
    {
        string find = "\"" + key + "\":";
        int start = json.IndexOf(find);
        if (start == -1) return 0;

        start += find.Length;
        int end = json.IndexOf(",", start);
        if (end == -1) end = json.IndexOf("}", start);

        if (end > start)
        {
            string val = json.Substring(start, end - start).Trim();
            if (int.TryParse(val, out int result))
                return result;
        }
        return 0;
    }
}
