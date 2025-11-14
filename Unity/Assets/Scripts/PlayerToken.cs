using UnityEngine;
using System.Collections;

public class PlayerToken : MonoBehaviour
{
    public GameBoard gameBoard;
    public int currentIndex = 0;
    public float moveSpeed = 5.0f;

    // 게임 종료 여부
    private bool hasWon = false;

    // 승리시 호출할 델리게이트/이벤트
    public System.Action OnWin;

    public void MoveSteps(int stepCount)
    {
        if (hasWon) return; // 이미 승리했으면 이동 안함
        StartCoroutine(MoveRoutine(stepCount));
    }

    IEnumerator MoveRoutine(int stepCount)
    {
        int previousIndex = currentIndex;
        int targetIndex = currentIndex + stepCount;
        targetIndex %= gameBoard.boardSpaces.Count;

        // 한 칸씩 이동
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

            // 이동하면서 0번 칸(시작점) 도달 체크
            if (currentIndex == 0 && previousIndex != 0)
            {
                // 게임 승리
                hasWon = true;
                Debug.Log("?? 승리! 플레이어가 시작점에 돌아왔습니다.");
                if (OnWin != null) OnWin.Invoke();
                yield break;
            }
        }
    }
}
