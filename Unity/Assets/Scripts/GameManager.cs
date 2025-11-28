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

                diceReader.RollDice();
                StartCoroutine(WaitForDiceResult());
            }
        }
    }

    IEnumerator WaitForDiceResult()
    {
        isWaitingForDice = false;

        yield return new WaitUntil(() => !diceReader.isRolling);
        yield return new WaitForSeconds(0.5f);

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

                if (!bossActive && AllPlayersPassedThreshold())
                {
                    bossActive = true;
                    turnOrder.Add(boss);
                    infoText.text = "⚠️ 모든 플레이어가 15칸을 지났습니다! 보스가 나타났습니다!";
                    Debug.Log("보스 활성화됨!");
                    yield return new WaitForSeconds(1f);
                }
            }

            NextTurn();
            isWaitingForDice = true;

            // ✅ 다음 턴이 보스이면 자동으로 보스 턴 처리
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

    // ✅ 보스 턴 자동 처리 (별도 코루틴)
    IEnumerator ProcessBossTurn()
    {
        isWaitingForDice = false;

        infoText.text = "보스가 주사위를 굴리는 중...";
        yield return new WaitForSeconds(1f);

        // 보스 자동 주사위 굴림
        diceReader.RollDice();
        yield return new WaitUntil(() => !diceReader.isRolling);
        yield return new WaitForSeconds(0.5f);

        int bossDiceValue = diceReader.GetTopNumber();
        infoText.text = $"보스의 턴! 주사위: {bossDiceValue}";
        Debug.Log($"보스 주사위: {bossDiceValue}");

        bool moveFinished = false;
        yield return StartCoroutine(boss.MoveStepsWithCallback(bossDiceValue, players, () => moveFinished = true));
        yield return new WaitUntil(() => moveFinished);
        yield return new WaitForSeconds(0.5f);

        Debug.Log($"보스 이동 완료: {boss.currentIndex}칸");

        NextTurn();
        isWaitingForDice = true;

        // ✅ 다음이 플레이어면 플레이어 턴 정보 표시
        UpdateNextTurnDisplay();
    }

    // ✅ 다음 턴 정보 표시 함수
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
                Debug.Log($"{player.playerName}는 아직 {player.currentIndex}칸 (15칸 미만)");
                return false;
            }
        }
        Debug.Log("모든 플레이어가 15칸 이상을 지났습니다!");
        return true;
    }

    void NextTurn()
    {
        currentTurnIndex++;

        if (currentTurnIndex >= turnOrder.Count)
            currentTurnIndex = 0;

        // 탈락한 플레이어는 자동으로 턴 스킵
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

        // 남은 플레이어 확인
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

