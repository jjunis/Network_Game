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

    private bool hasWon = false;

    public IEnumerator MoveStepsWithCallback(int stepCount, System.Action onFinish)
    {
        if (isEliminated) { onFinish?.Invoke(); yield break; }
        if (hasWon) { onFinish?.Invoke(); yield break; }

        yield return StartCoroutine(MoveRoutine(stepCount));
        onFinish?.Invoke();
    }

    private IEnumerator MoveRoutine(int stepCount)
    {
        int targetIndex = currentIndex + stepCount;
        targetIndex %= gameBoard.boardSpaces.Count;

        Debug.Log($"🎮 {playerName} 이동 시작: {currentIndex} → {targetIndex} (주사위: {stepCount})");

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

            Debug.Log($"📍 {playerName} 현재: {currentIndex}");

            // ✅ 이동이 완료된 후에만 0칸 도달 체크!
            if (currentIndex == 0 && !hasWon)
            {
                hasWon = true;
                Debug.Log($"🎉 {playerName}가 시작점에 도달! 승리!");
                OnWin?.Invoke();
                yield break;
            }
        }

        Debug.Log($"✅ {playerName} 이동 완료: {currentIndex}");
    }

    public void Eliminate()
    {
        isEliminated = true;
        gameObject.SetActive(false);
    }
}
