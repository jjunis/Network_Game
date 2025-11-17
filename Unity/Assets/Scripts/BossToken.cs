using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BossToken : MonoBehaviour
{
    public GameBoard gameBoard;
    public int currentIndex = 0;
    public float moveSpeed = 5.0f;
    private List<PlayerToken> playersRef;

    public IEnumerator MoveStepsWithCallback(int stepCount, List<PlayerToken> players, System.Action onFinish)
    {
        playersRef = players;
        yield return StartCoroutine(MoveRoutine(stepCount));
        onFinish?.Invoke();
    }

    private IEnumerator MoveRoutine(int stepCount)
    {
        int targetIndex = currentIndex + stepCount;
        targetIndex %= gameBoard.boardSpaces.Count;

        Debug.Log($"🔴 보스 시작: {currentIndex} → 목표: {targetIndex} (주사위: {stepCount})");

        while (currentIndex != targetIndex)
        {
            int nextIndex = (currentIndex + 1) % gameBoard.boardSpaces.Count;
            Vector3 targetPos = gameBoard.GetSpacePosition(nextIndex);

            float t = 0f;
            Vector3 start = transform.position;
            while (t < 1f)
            {
                t += Time.deltaTime * moveSpeed;
                transform.position = Vector3.Lerp(start, targetPos, t);
                yield return null;
            }
            currentIndex = nextIndex;

            Debug.Log($"보스 현재: {currentIndex}");

            // 플레이어 잡기 체크
            foreach (var player in playersRef)
            {
                if (!player.isEliminated && player.currentIndex == currentIndex)
                {
                    player.Eliminate();
                    Debug.Log($"⚠️ 보스가 {player.playerName}를 잡았습니다!");
                }
            }
        }

        Debug.Log($"🔴 보스 완료: {currentIndex}");
    }
}
