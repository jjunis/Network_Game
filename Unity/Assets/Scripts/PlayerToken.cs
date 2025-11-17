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

    public IEnumerator MoveStepsWithCallback(int stepCount, System.Action onFinish)
    {
        if (isEliminated) { onFinish?.Invoke(); yield break; }
        yield return StartCoroutine(MoveRoutine(stepCount));
        if (currentIndex == 0) OnWin?.Invoke();
        onFinish?.Invoke();
    }

    private IEnumerator MoveRoutine(int stepCount)
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
        }
    }

    public void Eliminate()
    {
        isEliminated = true;
        gameObject.SetActive(false);
    }
}
