using UnityEngine;
using System.Collections;

public class PlayerToken : MonoBehaviour
{
    public GameBoard gameBoard; // Inspector에 할당
    public int currentIndex = 0;
    public float moveSpeed = 5.0f;

    public void MoveSteps(int stepCount)
    {
        StartCoroutine(MoveRoutine(stepCount));
    }

    IEnumerator MoveRoutine(int stepCount)
    {
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
        }
    }
}
