using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 데이터 관리용 싱글톤 클래스 (아무 씬에서나 접근 가능)
public class UserManager : MonoBehaviour
{
    public static UserManager Instance;
    public string MyID; // 로그인할 때 저장된 내 아이디

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 바뀌어도 파괴되지 않음
        }
        else
        {
            Destroy(gameObject);
        }
    }
}