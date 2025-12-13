using UnityEngine;
using UnityEngine.UI;
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

    private void Start()
    {
        foreach (var player in players)
        {
            turnOrder.Add(player);
            player.OnWin = OnPlayerWin;
        }

        infoText.text = $"{((PlayerToken)turnOrder[0]).playerName}의 턴! 주사위를 굴려주세요.";
    }

    private void Update()
    {
        if (gameOver) return;

        if (isWaitingForDice && Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("🎲 Space 눌림!");
            StartCoroutine(ProcessDiceRoll());
        }
    }

    IEnumerator ProcessDiceRoll()
    {
        isWaitingForDice = false;

        diceReader.RollDice();
        Debug.Log("⏳ 주사위 굴러가는 중...");

        yield return new WaitUntil(() => !diceReader.isRolling);
        yield return new WaitForSeconds(0.5f);

        int diceValue = diceReader.GetTopNumber();
        Debug.Log($"✅ 주사위 결과: {diceValue}");

        yield return StartCoroutine(ProcessTurn(diceValue));
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
                Debug.Log($"🎮 {player.playerName} 이동 시작 (주사위: {diceValue})");

                bool moveFinished = false;
                yield return StartCoroutine(player.MoveStepsWithCallback(diceValue, () => moveFinished = true));
                yield return new WaitUntil(() => moveFinished);
                yield return new WaitForSeconds(0.5f);

                Debug.Log($"✅ {player.playerName} 이동 완료: {player.currentIndex}칸");

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
            isWaitingForDice = true;

            if (gameOver)
            {
                Debug.Log("🏁 게임 오버!");
                yield break;
            }

            if (currentTurnIndex < turnOrder.Count && turnOrder[currentTurnIndex] is BossToken && bossActive)
            {
                Debug.Log("🔴 보스 턴 시작");
                yield return new WaitForSeconds(0.5f);
                StartCoroutine(ProcessBossTurn());
            }
            else
            {
                UpdateNextTurnDisplay();
            }
        }
    }

    IEnumerator ProcessBossTurn()
    {
        isWaitingForDice = false;

        infoText.text = "보스가 주사위를 굴리는 중...";
        Debug.Log("⏳ 보스 주사위 굴러가는 중...");

        yield return new WaitForSeconds(1f);

        diceReader.RollDice();
        yield return new WaitUntil(() => !diceReader.isRolling);
        yield return new WaitForSeconds(0.5f);

        int bossDiceValue = diceReader.GetTopNumber();
        Debug.Log($"✅ 보스 주사위 결과: {bossDiceValue}");

        infoText.text = $"보스의 턴! 주사위: {bossDiceValue}";

        bool moveFinished = false;
        yield return StartCoroutine(boss.MoveStepsWithCallback(bossDiceValue, players, () => moveFinished = true));
        yield return new WaitUntil(() => moveFinished);
        yield return new WaitForSeconds(0.5f);

        Debug.Log($"🔴 보스 이동 완료: {boss.currentIndex}칸");

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
            Debug.Log("🏁 게임 오버: 보스 승리!");
            yield break;
        }

        NextTurn();
        isWaitingForDice = true;

        Debug.Log($"✅ 다음 턴으로 진행 (currentTurnIndex: {currentTurnIndex})");
        UpdateNextTurnDisplay();
    }

    void UpdateNextTurnDisplay()
    {
        if (currentTurnIndex < turnOrder.Count)
        {
            object nextTurn = turnOrder[currentTurnIndex];
            if (nextTurn is PlayerToken)
            {
                PlayerToken nextPlayer = (PlayerToken)nextTurn;
                if (nextPlayer.isEliminated)
                {
                    infoText.text = $"{nextPlayer.playerName}는 탈락했습니다!";
                }
                else
                {
                    infoText.text = $"{nextPlayer.playerName}의 턴! 주사위를 굴려주세요.";
                }
            }
            else if (nextTurn is BossToken)
            {
                if (bossActive)
                {
                    infoText.text = "보스의 턴! (자동 진행 중...)";
                }
            }
        }
    }

    bool AllPlayersPassedThreshold()
    {
        foreach (var player in players)
        {
            if (!player.isEliminated && player.currentIndex < bossActivationThreshold)
            {
                Debug.Log($"📍 {player.playerName}: {player.currentIndex}칸 (15칸 미만)");
                return false;
            }
        }
        Debug.Log("✅ 모든 플레이어가 15칸 이상!");
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
