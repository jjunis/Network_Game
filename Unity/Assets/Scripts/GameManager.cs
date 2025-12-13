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
        }
    }

    void Update()
    {
        if (gameOver) return;

        if (isWaitingForDice && Input.GetKeyDown(KeyCode.Space))
        {
            StartCoroutine(RollDice());
        }
    }

    IEnumerator RollDice()
    {
        isWaitingForDice = false;

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
            }
        }

        if (diceValue > 0)
        {
            yield return StartCoroutine(PlayTurn(diceValue));
        }

        isWaitingForDice = true;
    }

    IEnumerator PlayTurn(int dice)
    {
        object cur = turnOrder[currentTurnIndex];

        if (cur is PlayerToken)
        {
            PlayerToken p = (PlayerToken)cur;

            if (!p.isEliminated)
            {
                infoText.text = $"{p.playerName}: {dice}";

                bool done = false;
                StartCoroutine(p.MoveStepsWithCallback(dice, () => done = true));
                yield return new WaitUntil(() => done);
                yield return new WaitForSeconds(0.5f);

                yield return StartCoroutine(SendMove(p.playerName, dice));
                yield return StartCoroutine(CheckBoss());

                if (!bossActive && AllOver15())
                {
                    bossActive = true;
                    turnOrder.Add(boss);
                    infoText.text = "⚠️ 보스!";
                    yield return new WaitForSeconds(1f);
                }
            }
        }

        NextTurn();

        if (gameOver) yield break;

        if (currentTurnIndex < turnOrder.Count && turnOrder[currentTurnIndex] is BossToken && bossActive)
        {
            yield return new WaitForSeconds(0.5f);
            StartCoroutine(BossTurn());
        }
        else
        {
            UpdateUI();
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
                bossActive = www.downloadHandler.text.Contains("true");
            }
        }
    }

    IEnumerator BossTurn()
    {
        if (gameOver) yield break;

        isWaitingForDice = false;
        infoText.text = "보스 주사위...";
        yield return new WaitForSeconds(1f);

        diceReader.RollDice();
        yield return new WaitUntil(() => !diceReader.isRolling);
        yield return new WaitForSeconds(0.5f);

        int bossDice = diceReader.GetTopNumber();
        infoText.text = $"보스: {bossDice}";

        bool done = false;
        StartCoroutine(boss.MoveStepsWithCallback(bossDice, players, () => done = true));
        yield return new WaitUntil(() => done);
        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(SendBossMove(bossDice));

        int alive = 0;
        foreach (var p in players)
        {
            if (!p.isEliminated) alive++;
        }

        if (alive == 0)
        {
            infoText.text = "보스 승리!";
            gameOver = true;
            yield break;
        }

        NextTurn();
        isWaitingForDice = true;
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
        if (currentTurnIndex < turnOrder.Count)
        {
            object next = turnOrder[currentTurnIndex];
            if (next is PlayerToken)
            {
                PlayerToken p = (PlayerToken)next;
                infoText.text = p.isEliminated ? $"{p.playerName} 탈락" : $"{p.playerName}의 턴! (Space)";
            }
            else if (next is BossToken && bossActive)
            {
                infoText.text = "보스 턴!";
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
    }

    bool AllOver15()
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
        infoText.text = "승리!";
        gameOver = true;
    }

    int GetInt(string json, string key)
    {
        string find = "\"" + key + "\":";
        int start = json.IndexOf(find) + find.Length;
        int end = json.IndexOf(",", start);
        if (end == -1) end = json.IndexOf("}", start);
        if (start > find.Length - 1 && int.TryParse(json.Substring(start, end - start).Trim(), out int val))
            return val;
        return 0;
    }
}
