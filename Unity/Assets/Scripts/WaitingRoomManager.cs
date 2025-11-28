using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;

[System.Serializable]
public class PlayerInfo
{
    // 서버 JSON의 키 이름과 정확히 일치해야 합니다.
    public string nickName;
    public bool isReady;
}

[System.Serializable]
public class PlayerInfoWrapper // JSON 배열 ([...]) 파싱을 위한 래퍼 클래스
{
    // Unity의 JsonUtility는 JSON 배열을 직접 파싱하지 못하기 때문에,
    // 이 'players'라는 필드에 JSON 배열 전체를 감싸서 처리합니다.
    public PlayerInfo[] players;
}
public class WaitingRoomManager : MonoBehaviour
{
    [Header("게임 컨트롤러")]
    public GameManager gameManager;

    [Header("UI 연결")]
    public TMP_Text roomNameText;
    public Button startGameButton; // 호스트 전용 게임 시작 버튼
    public Transform playerListParent; // 플레이어 목록 표시 부모
    public GameObject playerItemPrefab; // 플레이어 아이템 프리팹 (이름, 준비 상태 등 표시)
    public Button readyButton; // 일반 플레이어 준비 버튼

    private string serverUrl = "http://localhost:3000";
    private const int MAX_PLAYERS = 3;
    private bool isPlayerReady = false;
    
    private PlayerInfo[] currentPlayers;

    void Start()
    {
        roomNameText.text = "Room: " + LobbyUI.CurrentRoomName;
        StartCoroutine(AutoRefreshPlayerList(5.0f));

        // 1. 호스트 여부에 따른 UI 초기 설정
        if (LobbyUI.IsHost)
        {
            readyButton.gameObject.SetActive(false);
            // 호스트에게는 버튼을 표시하되, 조건 만족 전까지 비활성화 상태로 둡니다.
            startGameButton.gameObject.SetActive(true);
            startGameButton.interactable = false;
            startGameButton.onClick.AddListener(OnClickStartGame);
        }
        else
        {
            startGameButton.gameObject.SetActive(false);
            readyButton.onClick.AddListener(OnClickToggleReady); // ★ 준비 버튼 이벤트 연결
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
        string url = $"{serverUrl}/room_players?roomName={LobbyUI.CurrentRoomName}";
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string jsonPlayers = www.downloadHandler.text;
                UpdatePlayerListUI(jsonPlayers); // ★ 올바른 역할: 플레이어 목록 UI 갱신 호출
            }
            else
            {
                Debug.LogError("❌ 플레이어 목록 갱신 실패: " + www.error);
            }
        }
    }

    // ★ 1. 플레이어 목록 파싱 및 UI 갱신 (완벽 구현)
    void UpdatePlayerListUI(string jsonPlayers)
    {
        foreach (Transform child in playerListParent) Destroy(child.gameObject);

        PlayerInfo[] playerInfos = null;
        int currentPlayerCount = 0;
        int readyCount = 0;

        // JSON 배열 파싱 시도
        try
        {
            // Unity JsonUtility는 배열을 직접 파싱하지 못하므로 래퍼를 사용합니다.
            string wrappedJson = "{\"players\":" + jsonPlayers + "}";
            playerInfos = JsonUtility.FromJson<PlayerInfoWrapper>(wrappedJson).players;

            if (playerInfos != null)
            {
                currentPlayers = playerInfos;
                currentPlayerCount = playerInfos.Length;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("⚠️ JSON 파싱 경고 (데이터 없을 수 있음): " + e.Message);
        }

        // 2. UI 생성 및 준비 상태 계산
        if (playerInfos != null)
        {
            foreach (var player in playerInfos)
            {
                // UI 프리팹 생성 및 텍스트 설정 (RoomName과 UserCount를 TMP_Text로 가정한 로직)
                GameObject item = Instantiate(playerItemPrefab, playerListParent);
                TMP_Text nameText = item.transform.Find("RoomName").GetComponent<TMP_Text>();
                TMP_Text statusText = item.transform.Find("UserCount").GetComponent<TMP_Text>(); // 상태 표시용으로 가정

                nameText.text = player.nickName;
                statusText.text = player.isReady ? "준비 완료" : "미준비";

                if (player.isReady) readyCount++;
            }
        }

        // 3. 호스트 버튼 활성화 조건 (3명 모두 준비 완료)
        if (LobbyUI.IsHost)
        {
            // 호스트는 버튼을 항상 보이게 설정했으므로, 여기서는 상호작용 가능 여부만 변경
            bool allReadyAndFull = (currentPlayerCount == MAX_PLAYERS) && (readyCount == MAX_PLAYERS);

            // 호스트 전용: 3명 모두 준비 완료 시에만 시작 가능
            startGameButton.interactable = allReadyAndFull;

            if (!allReadyAndFull)
            {
                Debug.Log($"⚠️ 게임 시작 대기 중: 인원({currentPlayerCount}/{MAX_PLAYERS}), 준비({readyCount}/{MAX_PLAYERS})");
            }
        }
    }

    // ★ 2. 일반 플레이어 준비 토글 로직 (완벽 구현)
    public void OnClickToggleReady()
    {
        isPlayerReady = !isPlayerReady;
        // 버튼 텍스트 변경 로직 (선택 사항)
        // readyButton.GetComponentInChildren<TMP_Text>().text = isPlayerReady ? "준비 취소" : "준비";

        StartCoroutine(RequestToggleReady(isPlayerReady));
    }

    IEnumerator RequestToggleReady(bool readyStatus)
    {
        // 서버에 닉네임과 함께 준비 상태를 전송
        string json = "{\"roomName\":\"" + LobbyUI.CurrentRoomName + "\", \"nickName\":\"" + LobbyUI.MyNickName + "\", \"isReady\":" + readyStatus.ToString().ToLower() + "}";

        // 서버에 /toggle_ready 엔드포인트에 POST 요청을 보낸다고 가정
        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/toggle_ready", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"✅ 준비 상태 변경 요청 성공: {readyStatus}");
            }
            else
            {
                Debug.LogError("❌ 준비 상태 변경 요청 실패: " + www.downloadHandler.text);
            }
        }
    }

    public void OnClickStartGame()
    {
        if (!LobbyUI.IsHost) return;

        // 버튼이 활성화된 상태에서만 서버 요청
        if (startGameButton.interactable)
        {
            Debug.Log("▶️ 호스트가 게임 시작 버튼을 눌렀습니다.");
            StartCoroutine(RequestStartGame());
        }
    }

    // ★ 3. 게임 시작 POST 요청 설정 보완 (완벽 구현)
    IEnumerator RequestStartGame()
    {
        string json = "{\"roomName\":\"" + LobbyUI.CurrentRoomName + "\"}";
        using (UnityWebRequest www = new UnityWebRequest(serverUrl + "/start_game", "POST"))
        {
            // 누락된 POST 요청 설정 추가
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("✅ 서버에 게임 시작 요청 성공");

                // ★ 수정된 부분: 씬 전환 대신, 게임씬 내 플레이 로직 시작 함수 호출
                // 서버로부터 최종 게임 시작 신호를 받으면 게임 플레이를 시작합니다.
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
        Debug.Log("🎉 WaitingRoomManager: 게임 매니저에게 시작 명령 전달!");

        // 1. 🛑 대기실 UI 전체 비활성화
        roomNameText.gameObject.SetActive(false);
        startGameButton.gameObject.SetActive(false);
        readyButton.gameObject.SetActive(false);
        playerListParent.gameObject.SetActive(false);

        // 2. 🛑 서버 폴링 중지
        StopAllCoroutines();

        // 3. 닉네임 목록 생성
        if (gameManager != null && currentPlayers != null)
        {
            List<string> activeNicknames = new List<string>();
            foreach (var pInfo in currentPlayers)
            {
                activeNicknames.Add(pInfo.nickName);
            }

            // 4. GameManager 호출 (3명의 닉네임 목록 전달)
            gameManager.SetupMultiplayerGame(activeNicknames);
            Debug.Log($"✅ 3명의 닉네임({activeNicknames.Count}개)을 GameManager에 전달.");
        }
        else
        {
            Debug.LogError("🚨 GameManager가 연결되지 않았거나 플레이어 정보가 없습니다.");
        }
    }
}