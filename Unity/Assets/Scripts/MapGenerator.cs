using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MapGenerator : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Create Square Board Spaces")]
    public static void CreateBoard()
    {
        GameObject board = GameObject.Find("GameBoard");
        if (board == null)
        {
            board = new GameObject("GameBoard");
        }

        int totalSpaces = 61;
        // 사각형 테두리의 한 변에 들어갈 칸 개수 (보통 1/4로 분할, 약 15~16개씩)
        int edgeCount = 15;
        float spacing = 1.5f;
        int current = 0;

        // 아래변(좌->우)
        for (int i = 0; i < edgeCount; i++, current++)
        {
            CreateSpace(board, current, new Vector3(i * spacing, 0, 0));
        }
        // 오른쪽변(아래->위)
        for (int i = 1; i < edgeCount; i++, current++)
        {
            CreateSpace(board, current, new Vector3((edgeCount - 1) * spacing, 0, i * spacing));
        }
        // 윗변(우->좌)
        for (int i = 1; i < edgeCount; i++, current++)
        {
            CreateSpace(board, current, new Vector3((edgeCount - 1 - i) * spacing, 0, (edgeCount - 1) * spacing));
        }
        // 왼쪽변(위->아래)
        for (int i = 1; i < edgeCount; i++, current++)
        {
            CreateSpace(board, current, new Vector3(0, 0, (edgeCount - 1 - i) * spacing));
        }
        // 남은 칸 중앙에 배치 (원형 테두리 이후)
        for (; current < totalSpaces; current++)
        {
            // 중앙에 배치(맵 중앙 좌표 ex: (Edge의 중간 위치))
            float center = (edgeCount - 1) * spacing / 2f;
            CreateSpace(board, current, new Vector3(center, 0, center));
        }

        Debug.Log("? 61칸 사각형 보드 생성 완료!");
    }

    static void CreateSpace(GameObject board, int idx, Vector3 pos)
    {
        GameObject space = GameObject.CreatePrimitive(PrimitiveType.Cube);
        space.name = "Space_" + idx;
        space.transform.SetParent(board.transform);
        space.transform.localPosition = pos;
        space.transform.localScale = new Vector3(1f, 0.2f, 1f);
    }
#endif
}
