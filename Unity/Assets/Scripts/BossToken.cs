using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BossToken : MonoBehaviour
{
    public GameBoard gameBoard;
    public int currentIndex = 0;
    public float moveSpeed = 5.0f;

    // 반드시 이렇게 호출 (코루틴)
    public IEnumerator MoveStepsWithCallback(int stepCount, List<PlayerToken> players, System.Action onFinish)
    {
        yield return StartCoroutine(MoveRoutine(stepCount, players));
        onFinish?.Invoke();
    }

    private IEnumerator MoveRoutine(int stepCount, List<PlayerToken> players)
    {
        int targetIndex = currentIndex + stepCount;
        targetIndex %= gameBoard.boardSpaces.Count;

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

            // 보스 칸과 같은 칸인 플레이어 탈락 처리
            foreach (var player in players)
            {
                if (!player.isEliminated && player.currentIndex == currentIndex)
                {
                    player.Eliminate();
                }
            }
        }
    }
}
