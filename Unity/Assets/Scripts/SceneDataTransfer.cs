using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneDataTransfer : MonoBehaviour
{
    public static SceneDataTransfer Instance;
    public List<string> PlayerNicknames = new List<string>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static void CleanUp()
    {
        if (Instance != null)
        {
            Destroy(Instance.gameObject);
            Instance = null;
        }
    }
}