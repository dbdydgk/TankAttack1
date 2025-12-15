using System;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
#endif

public class EndingScoreboardUI : MonoBehaviour
{
    [Header("UI (Legacy Text 기준)")]
    public Text scoreTxt;   // ScoreTxt(Text Legacy) 연결

    [Header("Photon Custom Property Key")]
    public string killsKey = "kills";

    void Start()
    {
        Refresh();
    }

    // Photon에서 킬값이 갱신되면 엔딩에서도 갱신되게(룸에 남아있을 때만 의미 있음)
#if PHOTON_UNITY_NETWORKING
    public void OnEnable()
    {
        PhotonNetwork.NetworkingClient.EventReceived += OnPhotonEvent;
    }

    public void OnDisable()
    {
        PhotonNetwork.NetworkingClient.EventReceived -= OnPhotonEvent;
    }

    private void OnPhotonEvent(ExitGames.Client.Photon.EventData obj)
    {
        // 프로퍼티 업데이트 이벤트까지 전부 잡을 필요는 없고, 그냥 주기적으로 갱신해도 됨.
        // 간단하게는 Enable/Start에서 한번만 해도 OK.
    }
#endif

    public void Refresh()
    {
        if (scoreTxt == null) return;

        // Photon 룸에 남아있다면 방의 PlayerList 기반으로 정렬 출력
#if PHOTON_UNITY_NETWORKING
        if (PhotonNetwork.InRoom && PhotonNetwork.PlayerList != null && PhotonNetwork.PlayerList.Length > 0)
        {
            var players = PhotonNetwork.PlayerList
                .OrderByDescending(p => GetKills(p))
                .ThenBy(p => p.ActorNumber)
                .ToArray();

            var sb = new StringBuilder();
            sb.AppendLine("RANKING (Kills)");

            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                int kills = GetKills(p);

                string name = string.IsNullOrWhiteSpace(p.NickName)
                    ? $"Player {p.ActorNumber}"
                    : p.NickName;

                bool isMe = (p == PhotonNetwork.LocalPlayer);
                sb.AppendLine($"{i + 1}. {(isMe ? "[ME] " : "")}{name}  :  {kills}");
            }

            scoreTxt.text = sb.ToString();
            return;
        }
#endif

        // 룸 정보가 없으면(LeaveRoom 했거나 싱글 테스트) 임시 문구
        scoreTxt.text = "RANKING (Kills)\n(Photon room data not available)";
    }

#if PHOTON_UNITY_NETWORKING
    private int GetKills(Player p)
    {
        if (p == null || p.CustomProperties == null) return 0;

        if (!p.CustomProperties.TryGetValue(killsKey, out object v) || v == null) return 0;

        // Photon CustomProperties 타입이 int/byte/short 등으로 올 수 있어서 안전 변환
        try
        {
            return Convert.ToInt32(v);
        }
        catch
        {
            return 0;
        }
    }
#endif
}
