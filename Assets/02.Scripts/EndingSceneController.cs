using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;
#endif

public class EndingSceneController : MonoBehaviourPunCallbacks
{
    [Header("UI (Legacy Text 기준)")]
    public Text resultTxt;   // ResultTxt 연결
    public Text waveTxt;     // WaveTxt 연결

    [Header("Buttons")]
    public Button replayBtn; // ReplayBtn 연결
    public Button lobbyBtn;  // LobbyBtn 연결

    [Header("Scene Names")]
    public string endingSceneName = "Ending";
    public string lobbySceneName = "scLobby";

    // 룸 커스텀 프로퍼티 키 (GameMgr에서 넣어줄 것)
    const string KEY_END_RESULT = "END_RESULT"; // "CLEAR" or "FAIL"
    const string KEY_END_WAVE = "END_WAVE";     // int
    const string KEY_MAP = "MAP";               // PhotonInit에서 쓰는 맵 키

    void Start()
    {
        ApplyEndingTexts();
        HookButtons();
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
                else if (w is byte wb) endWave = wb;
                else if (w is short ws) endWave = ws;
                else if (w is long wl) endWave = (int)wl;
                else if (w is float wf) endWave = Mathf.RoundToInt(wf);
                else if (w is double wd) endWave = (int)System.Math.Round(wd);
                else
                {
                    int.TryParse(w.ToString(), out endWave);
                }
            }
        }

        bool isClear = (result == "CLEAR");

        if (resultTxt != null)
            resultTxt.text = isClear ? "Mission Clear!" : "Mission Failed!";

        if (waveTxt != null)
            waveTxt.text = $"End Wave : {endWave}";
#else
        // Photon 없는 상태(단독 테스트) 대비
        if (resultTxt != null) resultTxt.text = "Mission Clear!";
        if (waveTxt != null) waveTxt.text = "End Wave : 0";
#endif
    }

    public void OnClickReplay()
    {
#if PHOTON_UNITY_NETWORKING
        string mapName = "scBattleField";

        if (PhotonNetwork.InRoom &&
            PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(KEY_MAP, out object v) &&
            v != null)
        {
            mapName = v.ToString();
        }

        SceneManager.LoadScene(mapName);
#else
    SceneManager.LoadScene(endingSceneName);
#endif
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

#if PHOTON_UNITY_NETWORKING
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged == null) return;

        if (propertiesThatChanged.ContainsKey("END_RESULT") ||
            propertiesThatChanged.ContainsKey("END_WAVE"))
        {
            ApplyEndingTexts();
        }
    }
#endif
}