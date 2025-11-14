using UnityEngine;
using System.Collections.Generic;

public class GameBoard : MonoBehaviour
{
    public List<Transform> boardSpaces = new List<Transform>();

    void Awake()
    {
        boardSpaces.Clear();
        for (int i = 0; i < 61; i++)
        {
            Transform space = transform.Find("Space_" + i);
            if (space != null) boardSpaces.Add(space);
        }
    }

    public Vector3 GetSpacePosition(int index)
    {
        if (index < 0) index = 0;
        if (index >= boardSpaces.Count) index = boardSpaces.Count - 1;
        return boardSpaces[index].position + Vector3.up * 0.7f; // Ä­ À§·Î ¶ç¿ì±â
    }
}
