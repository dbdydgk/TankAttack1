using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;


#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;
#endif

public class EndingSceneController : MonoBehaviourPunCallbacks
{
    [Header("UI (Legacy Text 기준)")]
    public Text resultTxt;
    public Text waveTxt;

    [Header("Buttons")]
    public Button replayBtn;
    public Button lobbyBtn;

    [Header("Scene Names")]
    public string lobbySceneName = "scLobby";

    [Header("Room Property Key (맵 이름 저장 키)")]
    // 방 만들 때/게임씬 진입 때 저장해둔 맵 씬 이름 키
    public string mapRoomPropertyKey = "MAP";

    const string KEY_END_RESULT = "END_RESULT"; // "CLEAR" or "FAIL"
    const string KEY_END_WAVE = "END_WAVE";   // int
    const string KEY_PVE_STARTED = "PVE_STARTED";
    const string KEY_READY = "READY";

    void Start()
    {
        ApplyEndingTexts();
        HookButtons();
        RefreshReplayButton();
    }

    void HookButtons()
    {
        if (replayBtn != null)
        {
            replayBtn.onClick.RemoveAllListeners();
            replayBtn.onClick.AddListener(OnClickReplay);
        }

        if (lobbyBtn != null)
        {
            lobbyBtn.onClick.RemoveAllListeners();
            lobbyBtn.onClick.AddListener(OnClickLobby);
        }
    }

    void RefreshReplayButton()
    {
#if PHOTON_UNITY_NETWORKING
        if (replayBtn != null)
            replayBtn.gameObject.SetActive(PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient);
#endif
    }

    void ApplyEndingTexts()
    {
#if PHOTON_UNITY_NETWORKING
        string result = "";
        int endWave = 0;

        var room = PhotonNetwork.CurrentRoom;
        if (room != null && room.CustomProperties != null)
        {
            if (room.CustomProperties.TryGetValue(KEY_END_RESULT, out object r) && r != null)
                result = r.ToString();

            if (room.CustomProperties.TryGetValue(KEY_END_WAVE, out object w) && w != null)
            {
                if (w is int wi) endWave = wi;
                else int.TryParse(w.ToString(), out endWave);
            }
        }

        bool isClear = (result == "CLEAR");
        if (resultTxt != null) resultTxt.text = isClear ? "Mission Clear!" : "Mission Failed!";
        if (waveTxt != null) waveTxt.text = $"End Wave : {endWave}";
#else
        if (resultTxt != null) resultTxt.text = "Mission Clear!";
        if (waveTxt != null) waveTxt.text = "End Wave : 0";
#endif
    }

#if PHOTON_UNITY_NETWORKING
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        RefreshReplayButton();
    }
#endif

    public void OnClickReplay()
    {
#if PHOTON_UNITY_NETWORKING
        // 마스터만 재시작 가능
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            return;

        // 방/프로퍼티 null 방어 (NullReference 방지)
        var room = PhotonNetwork.CurrentRoom;
        if (room == null || room.CustomProperties == null)
        {
            Debug.LogError("[Ending] CurrentRoom or CustomProperties is null.");
            return;
        }

        // 맵 씬 이름 가져오기 (하드코딩 제거)
        if (!TryGetRoomString(room, mapRoomPropertyKey, out string mapName))
        {
            Debug.LogError($"[Ending] Room CustomProperties에 '{mapRoomPropertyKey}' 맵 정보가 없습니다. (맵 재시작 불가)");
            return;
        }

        // 다음 판을 위해 최소한의 룸 상태 초기화
        room.SetCustomProperties(new Hashtable
        {
            { KEY_END_RESULT, "" },
            { KEY_END_WAVE, 0 },
            { KEY_PVE_STARTED, false }, // PVE라면 다시 Start/Ready 흐름으로
        });

        // 씬 동기화 로드 (반드시 LoadLevel)
        StartCoroutine(CoReplayAfterReset(mapName));
#endif
    }
    IEnumerator CoReplayAfterReset(string mapName)
    {
        // 프로퍼티/Destroy 요청을 최대한 빨리 보내기
        PhotonNetwork.SendAllOutgoingCommands();

        // 너무 길 필요 없음. 0.2초면 보통 충분
        yield return new WaitForSeconds(0.2f);

        PhotonNetwork.LoadLevel(mapName);
    }
    static bool TryGetRoomString(Room room, string key, out string value)
    {
        value = null;
        if (room == null || room.CustomProperties == null) return false;

        if (room.CustomProperties.TryGetValue(key, out object v) && v != null)
        {
            value = v.ToString();
            return !string.IsNullOrEmpty(value);
        }
        return false;
    }

    public void OnClickLobby()
    {
#if PHOTON_UNITY_NETWORKING
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            return;
        }
#endif
        SceneManager.LoadScene(lobbySceneName);
    }

#if PHOTON_UNITY_NETWORKING
    public override void OnLeftRoom()
    {
        SceneManager.LoadScene(lobbySceneName);
    }
#endif
}
