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
        if (isWaitingForDice && Input.GetKeyDown(KeyCode.Space))
        {
            object current = turnOrder[currentTurnIndex];

            if (current is PlayerToken)
            {
                PlayerToken player = (PlayerToken)current;
                if (player.isEliminated)
                {
                    infoText.text = $"{player.playerName}는 탈락했습니다!";
                    return;
                }
            }

            if (current is BossToken && !bossActive)
            {
                infoText.text = "아직 보스가 나타나지 않았습니다!";
                return;
            }

            diceReader.RollDice();
            StartCoroutine(WaitForDiceResult());
        }
    }

    IEnumerator WaitForDiceResult()
    {
        isWaitingForDice = false;

        yield return new WaitUntil(() => !diceReader.isRolling);
        yield return new WaitForSeconds(0.5f);

        // ✅ 플레이어든 보스든 같은 주사위 값 사용
        int diceValue = diceReader.GetTopNumber();
        Debug.Log("주사위 결과: " + diceValue);

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

                bool moveFinished = false;
                yield return StartCoroutine(player.MoveStepsWithCallback(diceValue, () => moveFinished = true));
                yield return new WaitUntil(() => moveFinished);
                yield return new WaitForSeconds(0.5f);

                // 플레이어가 15칸을 지나갔는지 확인
                if (player.currentIndex >= bossActivationThreshold && !bossActive)
                {
                    bossActive = true;
                    turnOrder.Add(boss);
                    infoText.text = "⚠️ 보스가 나타났습니다!";
                    yield return new WaitForSeconds(1f);
                }
            }
        }
        else if (current is BossToken)
        {
            // ✅ 보스도 diceValue를 그대로 사용 (Random 제거)
            if (bossActive)
            {
                infoText.text = $"보스의 턴! 주사위: {diceValue}";
                bool moveFinished = false;
                yield return StartCoroutine(boss.MoveStepsWithCallback(diceValue, players, () => moveFinished = true));
                yield return new WaitUntil(() => moveFinished);
                yield return new WaitForSeconds(0.5f);
            }
        }

        NextTurn();
        isWaitingForDice = true;

        if (currentTurnIndex < turnOrder.Count)
        {
            object nextTurn = turnOrder[currentTurnIndex];
            if (nextTurn is PlayerToken)
            {
                PlayerToken nextPlayer = (PlayerToken)nextTurn;
                if (nextPlayer.isEliminated)
                {
                    infoText.text = $"{nextPlayer.playerName}는 탈락했습니다! 다음 턴으로 넘어갑니다...";
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
                    infoText.text = "보스의 턴! 주사위를 굴려주세요.";
                }
                else
                {
                    infoText.text = "아직 보스가 나타나지 않았습니다!";
                }
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

        int alive = 0;
        foreach (var p in players)
        {
            if (!p.isEliminated) alive++;
        }
        if (alive == 0)
        {
            infoText.text = "플레이어 전원 탈락! 게임 오버!";
        }
    }

    void OnPlayerWin()
    {
        infoText.text = "승리! 게임 종료!";
    }
}
