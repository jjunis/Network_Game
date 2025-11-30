using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;
using System;

[System.Serializable]
public class PlayerInfoData
{
    public string nickName;
    public bool isReady;
}

[System.Serializable]
public class PlayerInfoDataWrapper
{
    public PlayerInfoData[] players;
    public bool isStarted;
}

public class LobbyUI : MonoBehaviour
{
    private const string GameSceneName = "GameScene";
    private const int MAX_PLAYERS = 3;
    private string serverUrl = "http://localhost:3000";

    public static string CurrentRoomName;
    public static string MyNickName;
    public static bool IsHost = false;
    private bool isPlayerReady = false;
    private PlayerInfoData[] currentPlayers;

    [Header("--- 1. 씬 패널 관리 ---")]
    public GameObject lobbyPanel;
    public GameObject waitingRoomPanel;
    public GameObject createRoomPanel;

    [Header("--- 2. 로비 UI 연결 ---")]
    public TMP_InputField roomNameInput;
    public Transform contentParent;
    public GameObject roomItemPrefab;

    [Header("--- 3. 대기실 UI 연결 ---")]
    public TMP_Text roomNameText_WR;
    public Button startGameButton;
    public Transform playerListParent;
    public GameObject playerItemPrefab;
    public Button readyButton;

    void Start()
    {
        lobbyPanel.SetActive(true);
        waitingRoomPanel.SetActive(false);
        createRoomPanel.SetActive(false);

        if (startGameButton != null) startGameButton.onClick.RemoveAllListeners();
        if (readyButton != null) readyButton.onClick.RemoveAllListeners();

        Debug.Log("🚀 로비 진입: 방 목록 자동 갱신 시작");
        StartCoroutine(AutoRefreshRoomList(5.0f));
    }

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
        StartCoroutine(RequestRoomList());
    }

    // =========================================================
    // 1. 방 목록 로직 (Room List)
    // =========================================================

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
        catch (Exception e) { Debug.LogError(e.Message); }
    }

    // =========================================================
    // 2. 방 생성 및 입장 요청
    // =========================================================

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
                CurrentRoomName = rName;
                IsHost = true;
                SwitchToWaitingRoomUI();
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
                SwitchToWaitingRoomUI();
            }
        }
    }

    // =========================================================
    // 3. 대기실 로직 (Player List) + ★ PING 추가됨
    // =========================================================

    void SwitchToWaitingRoomUI()
    {
        StopAllCoroutines();

        lobbyPanel.SetActive(false);
        waitingRoomPanel.SetActive(true);
        roomNameText_WR.text = "Room: " + CurrentRoomName;

        StartCoroutine(AutoRefreshPlayerList(1.0f));

        // ★★★ [중요] 생존 신고(Ping) 시작! 이거 없어서 사라졌던 거임 ★★★
        StartCoroutine(SendHeartbeat());

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

    // ★ 1초마다 서버에 "나 살아있음" 신호 보내기
    IEnumerator SendHeartbeat()
    {
        while (true)
        {
            // 방을 나갔거나 패널이 꺼지면 중단
            if (!waitingRoomPanel.activeSelf) yield break;

            string json = "{\"nickName\":\"" + MyNickName + "\"}";
            using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/ping", "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();
            }
            yield return new WaitForSeconds(1.0f);
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
        }
    }

    void UpdatePlayerListUI(string jsonResponse)
    {
        // 1. 기존 목록 초기화
        foreach (Transform child in playerListParent) Destroy(child.gameObject);

        PlayerInfoData[] playerInfos = null;
        bool gameStarted = false;
        int readyCount = 0;

        try
        {
            PlayerInfoDataWrapper wrapper = JsonUtility.FromJson<PlayerInfoDataWrapper>(jsonResponse);
            if (wrapper != null)
            {
                playerInfos = wrapper.players;
                gameStarted = wrapper.isStarted;
            }
        }
        catch (Exception e) { Debug.LogWarning(e.Message); }

        // 게임 시작 신호 처리
        if (gameStarted)
        {
            if (IsHost == false)
            {
                StartGamePlay();
                return;
            }
        }

        if (playerInfos != null)
        {
            currentPlayers = playerInfos;

            foreach (var player in playerInfos)
            {
                GameObject item = Instantiate(playerItemPrefab, playerListParent);

                // 위치/크기 초기화
                item.transform.localPosition = Vector3.zero;
                item.transform.localScale = Vector3.one;

                // ---------------------------------------------------------
                // 1. 닉네임 설정 (오브젝트 이름: "Text")
                // ---------------------------------------------------------
                Transform nameObj = item.transform.Find("Text");
                if (nameObj != null)
                {
                    nameObj.GetComponent<TMP_Text>().text = player.nickName;
                }

                // ---------------------------------------------------------
                // 2. 준비 상태 설정 (오브젝트 이름: "Text (1)")
                // ---------------------------------------------------------
                Transform statusObj = item.transform.Find("Text (1)");
                if (statusObj != null)
                {
                    TMP_Text tmpStatus = statusObj.GetComponent<TMP_Text>();

                    if (player.isReady)
                    {
                        tmpStatus.text = "준비 완료";
                        tmpStatus.color = Color.green; // 준비하면 초록색
                    }
                    else
                    {
                        tmpStatus.text = "대기 중";
                        tmpStatus.color = Color.black; // 대기 중엔 검은색
                    }
                }

                if (player.isReady) readyCount++;
            }

            // 방장 시작 버튼 활성화 (테스트용: 접속자 전원 준비 시 활성화)
            if (IsHost)
            {
                bool isFull = (playerInfos.Length == MAX_PLAYERS);

                bool allReady = (readyCount == playerInfos.Length);

                startGameButton.interactable = (isFull && allReady);

                startGameButton.gameObject.SetActive(isFull && allReady);
            }
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
        }
    }

    public void OnClickStartGame()
    {
        Debug.Log("🖱️ [클릭] 시작 버튼 눌림! 서버로 요청 보냄..."); // ★ 이 로그 추가
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
                StartGamePlay();
            }
        }
    }

    public void StartGamePlay()
    {
        StopAllCoroutines();

        if (SceneDataTransfer.Instance != null && currentPlayers != null)
        {
            SceneDataTransfer.Instance.PlayerNicknames.Clear();

            List<string> activeNicknames = new List<string>();
            foreach (var pInfo in currentPlayers)
            {
                activeNicknames.Add(pInfo.nickName);
            }
            SceneDataTransfer.Instance.PlayerNicknames = activeNicknames;

            SceneManager.LoadScene(GameSceneName);
        }
    }
}