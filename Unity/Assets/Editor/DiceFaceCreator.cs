using UnityEngine;
using UnityEditor;

public class DiceFaceCreator : MonoBehaviour
{
    [MenuItem("Tools/Create Dice Faces")]
    static void CreateDiceFaces()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogError("⚠ 주사위 오브젝트를 선택한 뒤 실행하세요!");
            return;
        }

        GameObject dice = Selection.activeGameObject;
        Vector3 scale = dice.transform.localScale;

        // 좌표 계산 (크기 자동 인식)
        float x = scale.x * 0.5f;
        float y = scale.y * 0.5f;
        float z = scale.z * 0.5f;

        CreateFace(dice, "Top", new Vector3(0, y, 0), new Vector3(-90, 0, 0));
        CreateFace(dice, "Bottom", new Vector3(0, -y, 0), new Vector3(90, 0, 0));
        CreateFace(dice, "Front", new Vector3(0, 0, z), new Vector3(0, 0, 0));
        CreateFace(dice, "Back", new Vector3(0, 0, -z), new Vector3(0, 180, 0));
        CreateFace(dice, "Right", new Vector3(x, 0, 0), new Vector3(0, 90, 0));
        CreateFace(dice, "Left", new Vector3(-x, 0, 0), new Vector3(0, -90, 0));

        Debug.Log("✅ 주사위 6개 Face 자동 생성 완료!");
    }

    static void CreateFace(GameObject parent, string name, Vector3 pos, Vector3 rot)
    {
        GameObject face = new GameObject(name);
        face.transform.SetParent(parent.transform);
        face.transform.localPosition = pos;
        face.transform.localEulerAngles = rot;

        // Scene에서 보기 편하도록 Gizmo 아이콘 표시
        var icon = EditorGUIUtility.IconContent("sv_label_0").image as Texture2D;
        EditorGUIUtility.SetIconForObject(face, icon);
    }
}