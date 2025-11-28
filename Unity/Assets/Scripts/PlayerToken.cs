using UnityEngine;
using System.Collections;

public class PlayerToken : MonoBehaviour
{
    public GameBoard gameBoard;
    public int currentIndex = 0;
    public float moveSpeed = 5.0f;
    public string playerName = "플레이어";
    public bool isEliminated = false;
    public System.Action OnWin;

    private bool hasWon = false;  // ? 이미 승리했는지 확인

    public IEnumerator MoveStepsWithCallback(int stepCount, System.Action onFinish)
    {
        if (isEliminated) { onFinish?.Invoke(); yield break; }
        if (hasWon) { onFinish?.Invoke(); yield break; }  // ? 이미 승리했으면 움직이지 않음

        yield return StartCoroutine(MoveRoutine(stepCount));
        onFinish?.Invoke();
    }

    private IEnumerator MoveRoutine(int stepCount)
    {
        int targetIndex = currentIndex + stepCount;
        targetIndex %= gameBoard.boardSpaces.Count;

        while (currentIndex != targetIndex)
        {
            // ? 0칸에 도달했는지 매번 확인
            if (currentIndex == 0 && !hasWon)
            {
                hasWon = true;
                OnWin?.Invoke();
                yield break;  // 이동 중단
            }

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

            // ? 0칸에 도달했는지 다시 확인
            if (currentIndex == 0 && !hasWon)
            {
                hasWon = true;
                OnWin?.Invoke();
                yield break;  // 이동 중단
            }
        }
    }

    public void Eliminate()
    {
        isEliminated = true;
        gameObject.SetActive(false);
    }
}
