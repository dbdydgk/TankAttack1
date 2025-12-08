using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

public class GameMgr : MonoBehaviourPunCallbacks
{
    public Text txtConnect; // 접속 인원 수 표시
    public Text txtLogmsg; // 접속자 로그 표시
    PhotonView pv;

    public string[] tanks = { "Tank", "HeavyTank" };

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        CreateTank(); //탱크 생성
        PhotonNetwork.IsMessageQueueRunning = true;
        pv = GetComponent<PhotonView>();
        GetConnectPlayerCount();
    }
    private void Start()
    {
        string msg = "\n<color=#00ff00>["
            + PhotonNetwork.NickName+"] Connected</color>";
        pv.RPC("LogMsg", RpcTarget.AllBuffered, msg);

        
    }
    [PunRPC]
    void LogMsg(string msg)
    {
        txtLogmsg.text = txtLogmsg.text+msg;
    }
    void GetConnectPlayerCount() //룸 접속자 수 표시 함수
    {
        Room currRoom = PhotonNetwork.CurrentRoom;
        txtConnect.text = currRoom.PlayerCount.ToString()+
            "/"+currRoom.MaxPlayers.ToString();
    }
    public override void OnPlayerEnteredRoom(Player newPlayer) //새로운 플레이어가 룸에 접속했을 때
    {
        GetConnectPlayerCount();
    }
    public override void OnPlayerLeftRoom(Player otherPlayer) //플레이어가 룸에서 나갔을 때
    {
        GetConnectPlayerCount();
    }
    public void OnClickExitRoom()
    {
        string msg = "\n<color=#ff0000>[" + PhotonNetwork.NickName + "] Disconnected</color>";
        pv.RPC("LogMsg", RpcTarget.AllBuffered, msg);
        PhotonNetwork.LeaveRoom(); //룸 나가기
    }
    public override void OnLeftRoom() //룸 나가기가 완료되었을 때
    {
        PhotonNetwork.LoadLevel("scLobby"); //로비 씬으로 이동
    }
    void CreateTank()
    {
        float pos = Random.Range(-100.0f, 100.0f);
        PhotonNetwork.Instantiate(tanks[PlayerInfo.SelectedTankIndex],
            new Vector3(pos, 20.0f, pos), Quaternion.identity, 0);
    }
}
