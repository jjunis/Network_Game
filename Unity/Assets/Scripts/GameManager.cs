using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public List<PlayerToken> players;      // Inspector에서 순서대로 등록
    public BossToken boss;                 // Inspector에서 등록
    public GameBoard gameBoard;
    public Text infoText;

    private List<object> turnOrder = new List<object>();
    private int currentTurnIndex = 0;

    private void Start()
    {
        foreach (var player in players)
        {
            turnOrder.Add(player);
            player.OnWin = OnPlayerWin;
        }
        turnOrder.Add(boss);

        infoText.text = $"{((PlayerToken)turnOrder[0]).playerName}의 턴!";
        StartCoroutine(GameLoop());
    }

    IEnumerator GameLoop()
    {
        while (true)
        {
            object current = turnOrder[currentTurnIndex];
            if (current is PlayerToken)
            {
                PlayerToken player = (PlayerToken)current;

                if (!player.isEliminated)
                {
                    infoText.text = $"{player.playerName}의 턴!";
                    int dice = Random.Range(1, 7);

                    bool moveFinished = false;
                    // 여기서 MoveSteps가 아니라 MoveStepsWithCallback만 사용!
                    yield return StartCoroutine(player.MoveStepsWithCallback(dice, () => moveFinished = true));
                    yield return new WaitUntil(() => moveFinished);
                    yield return new WaitForSeconds(0.5f);
                }
            }
            else if (current is BossToken)
            {
                infoText.text = "보스의 턴!";
                int bossDice = Random.Range(2, 7);
                bool moveFinished = false;
                yield return StartCoroutine(boss.MoveStepsWithCallback(bossDice, players, () => moveFinished = true));
                yield return new WaitUntil(() => moveFinished);
                yield return new WaitForSeconds(0.5f);
            }

            NextTurn();
            yield return null;
        }
    }

    void NextTurn()
    {
        currentTurnIndex++;
        if (currentTurnIndex >= turnOrder.Count)
            currentTurnIndex = 0;
        int alive = 0;
        foreach (var p in players)
        {
            if (!p.isEliminated) alive++;
        }
        if (alive == 0)
        {
            infoText.text = "플레이어 전원 탈락! 게임 오버!";
            StopAllCoroutines();
        }
    }

    void OnPlayerWin()
    {
        infoText.text = "승리! 게임 종료!";
        StopAllCoroutines();
    }
}
