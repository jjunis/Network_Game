using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;
using System;

// ★ 이름 충돌 방지용 데이터 구조체 (기존 파일과의 충돌을 피하기 위해 필수)
[System.Serializable]
public class PlayerInfoData
{
    public string nickName;
    public bool isReady;
}
[System.Serializable]
public class PlayerInfoDataWrapper { public PlayerInfoData[] players; }


// ★ 클래스 이름 유지: LobbyUI
public class LobbyUI : MonoBehaviour
{
    // ★ 씬 상수 및 URL
    private const string GameSceneName = "GameScene";
    private const int MAX_PLAYERS = 3;
    private string serverUrl = "http://localhost:3000";

    // ★ 전역 상태 변수
    public static string CurrentRoomName;
    public static string MyNickName;
    public static bool IsHost = false;
    private bool isPlayerReady = false;
    private PlayerInfoData[] currentPlayers; // 서버 응답 저장을 위해 필요

    // --- UI 연결 변수 (기존 LobbyUI + WaitingRoomManager 통합) ---

    [Header("--- 1. 씬 패널 관리 ---")]
    public GameObject lobbyPanel;      // 로비 전체 UI (방 목록 포함)
    public GameObject waitingRoomPanel; // 대기실 전체 UI (플레이어 목록 포함)
    public GameObject createRoomPanel;  // 방 만들기 입력 패널

    [Header("--- 2. 로비 UI 연결 ---")]
    public TMP_InputField roomNameInput; // 방 이름 입력창
    public Transform contentParent;     // 방 목록 Scroll View Content
    public GameObject roomItemPrefab;    // 방 목록 아이템 프리팹

    [Header("--- 3. 대기실 UI 연결 ---")]
    public TMP_Text roomNameText_WR;        // 대기실의 방 이름 텍스트
    public Button startGameButton;        // 호스트 전용 게임 시작 버튼
    public Transform playerListParent;     // 플레이어 목록 표시 부모
    public GameObject playerItemPrefab;     // 플레이어 아이템 프리팹
    public Button readyButton;            // 일반 플레이어 준비 버튼


    void Start()
    {
        // UI 초기 상태 설정
        lobbyPanel.SetActive(true);
        waitingRoomPanel.SetActive(false);
        createRoomPanel.SetActive(false);

        if (startGameButton != null) startGameButton.onClick.RemoveAllListeners();
        if (readyButton != null) readyButton.onClick.RemoveAllListeners();

        // 주기적인 방 목록 갱신 시작 (★AutoRefreshRoomList 함수로 변경)
        Debug.Log("🚀 로비 진입: 방 목록 자동 갱신 시작");
        StartCoroutine(AutoRefreshRoomList(5.0f));
    }

    // --- 로비 UI 버튼 연결 함수 (기존 유지) ---

    public void OnClickCreateRoom() { createRoomPanel.SetActive(true); }
    public void OnClickClosePanel() { createRoomPanel.SetActive(false); }

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

    // **********************************
    // 1. 서버 통신 및 UI 갱신 로직 (LobbyUI)
    // **********************************

    IEnumerator AutoRefreshRoomList(float interval)
    {
        while (true)
        {
            yield return StartCoroutine(RequestRoomList());
            yield return new WaitForSeconds(interval);
        }
    }

    IEnumerator RequestRoomList()
    {
        using (UnityWebRequest www = UnityWebRequest.Get(serverUrl + "/room_list"))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                UpdateRoomListUI(www.downloadHandler.text);
            }
            else
            {
                Debug.LogError("❌ 서버 연결 실패: " + www.error);
            }
        }
    }

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
                IsHost = true;
                // ★ 씬 전환 대신 대기실 UI 활성화
                SwitchToWaitingRoomUI();
            }
            else
            {
                Debug.LogError("❌ 방 생성 실패: " + www.downloadHandler.text);
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
                IsHost = false;
                // ★ 씬 전환 대신 대기실 UI 활성화
                SwitchToWaitingRoomUI();
            }
            else
            {
                Debug.LogError("❌ 입장 실패: " + www.downloadHandler.text);
            }
        }
    }

    void UpdateRoomListUI(string jsonArray)
    {
        foreach (Transform child in contentParent) Destroy(child.gameObject);

        if (string.IsNullOrEmpty(jsonArray) || jsonArray.Length < 5) return;

        try
        {
            jsonArray = jsonArray.Replace("[", "").Replace("]", "");
            string[] rooms = jsonArray.Split(new string[] { "}," }, StringSplitOptions.None);

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

                GameObject item = Instantiate(roomItemPrefab, contentParent);
                TMP_Text nameObj = item.transform.Find("RoomName").GetComponent<TMP_Text>();
                TMP_Text countObj = item.transform.Find("UserCount").GetComponent<TMP_Text>();

                nameObj.text = rName;
                countObj.text = $"{rCount}/{MAX_PLAYERS}";

                item.transform.localScale = Vector3.one;
                item.GetComponent<Button>().onClick.AddListener(() => {
                    StartCoroutine(RequestJoinRoom(rName, MyNickName));
                });
            }
        }
        catch (Exception e)
        {
            Debug.LogError("💥 파싱 중 에러 발생: " + e.Message);
        }
    }

    // **********************************
    // 2. 대기실 전환 로직 (WaitingRoomManager 역할 통합)
    // **********************************

    void SwitchToWaitingRoomUI()
    {
        StopAllCoroutines(); // 로비 갱신 중지

        lobbyPanel.SetActive(false);
        waitingRoomPanel.SetActive(true);
        roomNameText_WR.text = "Room: " + CurrentRoomName;

        // 대기실 로직 시작 (주기적 플레이어 목록 갱신 시작)
        StartCoroutine(AutoRefreshPlayerList(5.0f));

        // UI 초기 설정
        if (IsHost)
        {
            readyButton.gameObject.SetActive(false);
            startGameButton.gameObject.SetActive(true);
            startGameButton.interactable = false;
            startGameButton.onClick.RemoveAllListeners();
            startGameButton.onClick.AddListener(OnClickStartGame);
        }
        else
        {
            startGameButton.gameObject.SetActive(false);
            readyButton.onClick.RemoveAllListeners();
            readyButton.onClick.AddListener(OnClickToggleReady);
            readyButton.gameObject.SetActive(true);
            isPlayerReady = false;
        }
    }

    IEnumerator AutoRefreshPlayerList(float interval)
    {
        while (true)
        {
            yield return StartCoroutine(RequestPlayerList());
            yield return new WaitForSeconds(interval);
        }
    }

    IEnumerator RequestPlayerList()
    {
        string url = $"{serverUrl}/room_players?roomName={CurrentRoomName}";
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                UpdatePlayerListUI(www.downloadHandler.text);
            }
            else
            {
                Debug.LogError("❌ 플레이어 목록 갱신 실패: " + www.error);
            }
        }
    }

    void UpdatePlayerListUI(string jsonPlayers)
    {
        foreach (Transform child in playerListParent) Destroy(child.gameObject);

        PlayerInfoData[] playerInfos = null;
        int currentPlayerCount = 0;
        int readyCount = 0;

        try
        {
            string wrappedJson = "{\"players\":" + jsonPlayers + "}";
            playerInfos = JsonUtility.FromJson<PlayerInfoDataWrapper>(wrappedJson).players;

            if (playerInfos != null)
            {
                currentPlayers = playerInfos;
                currentPlayerCount = playerInfos.Length;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("⚠️ JSON 파싱 경고: " + e.Message);
        }

        if (playerInfos != null)
        {
            foreach (var player in playerInfos)
            {
                GameObject item = Instantiate(playerItemPrefab, playerListParent);
                TMP_Text nameText = item.transform.Find("RoomName").GetComponent<TMP_Text>();
                TMP_Text statusText = item.transform.Find("UserCount").GetComponent<TMP_Text>();

                nameText.text = player.nickName;
                statusText.text = player.isReady ? "준비 완료" : "미준비";

                if (player.isReady) readyCount++;
            }
        }

        if (IsHost)
        {
            bool allReadyAndFull = (currentPlayerCount == MAX_PLAYERS) && (readyCount == MAX_PLAYERS);
            startGameButton.interactable = allReadyAndFull;
        }
    }

    public void OnClickToggleReady()
    {
        isPlayerReady = !isPlayerReady;
        StartCoroutine(RequestToggleReady(CurrentRoomName, MyNickName, isPlayerReady));
    }

    IEnumerator RequestToggleReady(string rName, string nick, bool ready)
    {
        string json = $"{{\"roomName\":\"{rName}\", \"nickName\":\"{nick}\", \"isReady\":{ready.ToString().ToLower()}}}";

        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/toggle_ready", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"✅ 준비 상태 변경 성공: {ready}");
            }
            else
            {
                Debug.LogError("❌ 준비 상태 변경 실패: " + www.downloadHandler.text);
            }
        }
    }


    // **********************************
    // 3. 게임 시작 및 씬 전환 로직
    // **********************************

    public void OnClickStartGame()
    {
        StartCoroutine(RequestStartGame(CurrentRoomName));
    }

    IEnumerator RequestStartGame(string rName)
    {
        string json = $"{{\"roomName\":\"{rName}\"}}";

        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/start_game", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                // 서버에서 게임 시작 승인 -> 씬 전환 실행
                StartGamePlay();
            }
            else
            {
                Debug.LogError("❌ 게임 시작 요청 실패: " + www.downloadHandler.text);
            }
        }
    }

    public void StartGamePlay()
    {
        Debug.Log("🎉 LobbyUI: 게임 시작 신호 수신! 씬 전환 실행.");

        StopAllCoroutines();

        // SceneDataTransfer 클래스를 사용하여 데이터 전달
        if (SceneDataTransfer.Instance != null && currentPlayers != null)
        {
            SceneDataTransfer.Instance.PlayerNicknames.Clear();

            List<string> activeNicknames = new List<string>();
            foreach (var pInfo in currentPlayers)
            {
                activeNicknames.Add(pInfo.nickName);
            }
            SceneDataTransfer.Instance.PlayerNicknames = activeNicknames;

            // 최종: GameScene으로 씬 전환!
            SceneManager.LoadScene(GameSceneName);
        }
        else
        {
            Debug.LogError("🚨 데이터 전달 실패: SceneDataTransfer 객체 또는 플레이어 정보가 없습니다.");
        }
    }
}