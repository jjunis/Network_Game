using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking; // 기본 통신 기능
using UnityEngine.SceneManagement;
using TMPro;

public class LobbyUI : MonoBehaviour
{
    [Header("UI 연결")]
    public TMP_InputField roomNameInput; // 방 이름 입력창
    public GameObject createRoomPanel;   // 방 만들기 패널
    public Transform contentParent;      // Scroll View > Content
    public GameObject roomItemPrefab;    // 버튼 프리팹

    string serverUrl = "http://localhost:3000";

    public static string CurrentRoomName;
    public static string MyNickName;

    // --- 버튼 연결 함수들 ---

    public void OnClickCreateRoom() { createRoomPanel.SetActive(true); }
    public void OnClickClosePanel() { createRoomPanel.SetActive(false); }

    void Start()
    {
        Debug.Log("🚀 로비 진입: 방 목록 자동 갱신 시작");
        StartCoroutine(RequestRoomList());
    }

    public void OnClickConfirmCreateRoom()
    {
        if (string.IsNullOrEmpty(roomNameInput.text)) return;

        StartCoroutine(RequestCreateRoom(roomNameInput.text, MyNickName));
        createRoomPanel.SetActive(false);
    }

    public void OnClickRefresh()
    {
        Debug.Log("🔄 새로고침 버튼 눌림 -> 서버 요청 시작");
        StartCoroutine(RequestRoomList());
    }

    // --- 서버 통신 로직 ---

    IEnumerator RequestCreateRoom(string rName, string nick)
    {
        string json = "{\"roomName\":\"" + rName + "\", \"nickName\":\"" + nick + "\"}";

        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/create_room", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("✅ 방 생성 성공");
                CurrentRoomName = rName;
                SceneManager.LoadScene("GameScene");
            }
            else
            {
                Debug.LogError("❌ 방 생성 실패: " + www.downloadHandler.text);
            }
        }
    }

    IEnumerator RequestRoomList()
    {
        using (UnityWebRequest www = UnityWebRequest.Get(serverUrl + "/room_list"))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string rawData = www.downloadHandler.text;
                // ★ 서버 응답 확인용 로그
                Debug.Log("📥 서버 응답 원본(Raw): " + rawData);
                UpdateRoomListUI(rawData);
            }
            else
            {
                Debug.LogError("❌ 서버 연결 실패: " + www.error);
            }
        }
    }

    IEnumerator RequestJoinRoom(string rName, string nick)
    {
        string json = "{\"roomName\":\"" + rName + "\", \"nickName\":\"" + nick + "\"}";

        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/join_room", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.downloadHandler.text.Contains("true"))
            {
                CurrentRoomName = rName;
                SceneManager.LoadScene("GameScene");
            }
            else
            {
                Debug.LogError("❌ 입장 실패: " + www.downloadHandler.text);
            }
        }
    }

    // --- UI 갱신 로직 (디버그 포함) ---

    void UpdateRoomListUI(string jsonArray)
    {
        // 1. 기존 목록 삭제
        foreach (Transform child in contentParent) Destroy(child.gameObject);

        // 2. 데이터 확인
        if (string.IsNullOrEmpty(jsonArray) || jsonArray.Length < 5)
        {
            Debug.Log("⚠️ 데이터가 없거나 비어있음 (방 없음)");
            return;
        }

        try
        {
            // 3. 파싱
            jsonArray = jsonArray.Replace("[", "").Replace("]", "");
            string[] rooms = jsonArray.Split(new string[] { "}," }, System.StringSplitOptions.None);

            foreach (string roomRaw in rooms)
            {
                if (string.IsNullOrEmpty(roomRaw)) continue;

                string cleanRoom = roomRaw.Replace("{", "").Replace("}", "").Replace("\"", "");
                string[] props = cleanRoom.Split(',');

                string rName = "Unknown";
                string rCount = "0";

                foreach (string prop in props)
                {
                    if (prop.Contains("name:")) rName = prop.Split(':')[1];
                    if (prop.Contains("count:")) rCount = prop.Split(':')[1];
                }

                // 4. 버튼 생성 및 텍스트 연결
                GameObject item = Instantiate(roomItemPrefab, contentParent);

                // 디버그: 이름 확인
                Debug.Log($"🔨 버튼 생성 시도 -> 이름: {rName}, 인원: {rCount}");

                // 프리팹 안의 텍스트 찾기
                Transform nameObj = item.transform.Find("RoomName");
                Transform countObj = item.transform.Find("UserCount");

                if (nameObj != null) nameObj.GetComponent<TMP_Text>().text = rName;
                else Debug.LogError("❌ 'RoomName' 오브젝트 못 찾음! 프리팹 이름 확인 필요.");

                if (countObj != null) countObj.GetComponent<TMP_Text>().text = $"{rCount}/3";

                // 크기 보정
                item.transform.localScale = Vector3.one;

                // 클릭 이벤트
                item.GetComponent<Button>().onClick.AddListener(() => {
                    StartCoroutine(RequestJoinRoom(rName, MyNickName));
                });
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("💥 파싱 중 에러 발생: " + e.Message);
        }
    }
}