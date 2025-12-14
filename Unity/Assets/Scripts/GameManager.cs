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

    private string serverUrl = "http://172.30.1.13:3000";
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

        infoText.text = $"{((PlayerToken)turnOrder[0]).playerName}의 턴! (Space 키)";
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

        // ✅ Space 키로 주사위 굴리기
        if (isWaitingForDice && Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("🎲 Space 눌림 - 주사위 굴리기 시작");
            StartCoroutine(RollDiceAndMove());
        }
    }

    // ✅ 주사위 굴리기 (로컬 + 서버 동기화)
    IEnumerator RollDiceAndMove()
    {
        isWaitingForDice = false;

        // 1️⃣ 로컬에서 주사위 굴리기 (즉시 화면에 표시)
        Debug.Log("🎲 로컬 주사위 굴리기 시작");
        infoText.text = "주사위 굴리는 중...";

        diceReader.RollDice();
        yield return new WaitUntil(() => !diceReader.isRolling);
        yield return new WaitForSeconds(0.3f);

        // 2️⃣ 로컬 주사위 값 가져오기
        int diceValue = diceReader.GetTopNumber();
        Debug.Log($"🎲 주사위 값: {diceValue}");

        infoText.text = $"주사위: {diceValue}";
        yield return new WaitForSeconds(0.5f);

        // 3️⃣ 게임 진행
        yield return StartCoroutine(ProcessTurn(diceValue));

        // 4️⃣ 서버에 주사위 값 전송 (백그라운드)
        StartCoroutine(NotifyDiceRollToServer(diceValue));

        isWaitingForDice = true;
    }

    // ✅ 서버에 주사위 값 전송 (비동기)
    IEnumerator NotifyDiceRollToServer(int diceValue)
    {
        string json = "{\"roomName\":\"" + currentRoomName + "\", \"diceValue\":" + diceValue + "}";
        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/roll_dice", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("📡 서버에 주사위 값 전송 완료");
            }
            else
            {
                Debug.LogError("❌ 서버 통신 실패: " + www.error);
            }
        }
    }

    IEnumerator ProcessTurn(int diceValue)
    {
        object current = turnOrder[currentTurnIndex];

        if (current is PlayerToken)
        {
            PlayerToken player = (PlayerToken)current;

            if (!player.isEliminated)
            {
                infoText.text = $"{player.playerName} 이동 중... (주사위: {diceValue})";
                Debug.Log($"🎮 {player.playerName} 이동 시작");

                bool moveFinished = false;
                StartCoroutine(player.MoveStepsWithCallback(diceValue, () => moveFinished = true));
                yield return new WaitUntil(() => moveFinished);
                yield return new WaitForSeconds(0.5f);

                Debug.Log($"✅ {player.playerName} 이동 완료: {player.currentIndex}칸");

                // 서버에 플레이어 이동 알리기
                yield return StartCoroutine(NotifyPlayerMove(player.playerName, diceValue));

                // 보스 활성화 체크
                if (!bossActive && AllPlayersPassedThreshold())
                {
                    bossActive = true;
                    turnOrder.Add(boss);
                    infoText.text = "⚠️ 모든 플레이어가 15칸을 지났습니다! 보스가 나타났습니다!";
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

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"✅ {playerName}의 이동이 서버에 기록됨");
            }
        }
    }

    IEnumerator ProcessBossTurn()
    {
        infoText.text = "🔴 보스가 주사위를 굴리는 중...";
        Debug.Log("🔴 보스 턴");
        yield return new WaitForSeconds(0.8f);

        // ✅ 보스 주사위 굴리기
        diceReader.RollDice();
        yield return new WaitUntil(() => !diceReader.isRolling);
        yield return new WaitForSeconds(0.4f);

        int bossDiceValue = diceReader.GetTopNumber();
        Debug.Log($"🔴 보스 주사위: {bossDiceValue}");
        infoText.text = $"🔴 보스 이동 중... (주사위: {bossDiceValue})";

        bool moveFinished = false;
        StartCoroutine(boss.MoveStepsWithCallback(bossDiceValue, players, () => moveFinished = true));
        yield return new WaitUntil(() => moveFinished);
        yield return new WaitForSeconds(0.5f);

        Debug.Log($"🔴 보스 이동 완료: {boss.currentIndex}칸");

        // 서버에 보스 이동 알리기
        yield return StartCoroutine(NotifyBossMove(bossDiceValue));

        int alive = 0;
        foreach (var p in players)
        {
            if (!p.isEliminated) alive++;
        }

        if (alive == 0)
        {
            infoText.text = "🔴 보스가 모든 플레이어를 잡았습니다! 게임 오버!";
            gameOver = true;
            Debug.Log("🏁 게임 오버: 보스 승리!");
            yield break;
        }

        NextTurn();

        if (currentTurnIndex < turnOrder.Count && turnOrder[currentTurnIndex] is PlayerToken)
        {
            infoText.text = $"{((PlayerToken)turnOrder[currentTurnIndex]).playerName}의 턴! (Space 키)";
        }
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

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("✅ 보스 이동이 서버에 기록됨");
            }
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
        Debug.Log("🏁 게임 오버: 플레이어 승리!");
    }
}
