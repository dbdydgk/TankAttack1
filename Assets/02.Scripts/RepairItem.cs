using Photon.Pun;
using UnityEngine;

public class RepairItem : MonoBehaviourPun
{
    [Header("회복량")]
    public int healAmount = 30;

    [Header("먹으면 사라지기")]
    public bool destroyOnPickup = true;

    private bool picked = false;

    private void OnTriggerEnter(Collider other)
    {
        if (picked) return;

        // 플레이어 탱크(또는 자식 콜라이더) 감지
        var td = other.GetComponentInParent<TankDamage>();
        if (td == null) return;

        // 플레이어만 먹게 하고 싶으면 Tag 검사(필요시)
        // if (!td.CompareTag("Player")) return;

        // 네트워크 방이 아니면 로컬 처리
        if (!PhotonNetwork.InRoom || photonView == null)
        {
            picked = true;
            td.Heal(healAmount);
            if (destroyOnPickup) Destroy(gameObject);
            else gameObject.SetActive(false);
            return;
        }

        // 내 탱크일 때만 "먹기 요청" (중복 방지)
        PhotonView playerPv = td.GetComponent<PhotonView>();
        if (playerPv != null && !playerPv.IsMine) return;

        picked = true;
        int viewId = (playerPv != null) ? playerPv.ViewID : -1;

        // 마스터에게 처리 맡기기 → 전체에게 동기화
        photonView.RPC(nameof(RPC_RequestPickup), RpcTarget.MasterClient, viewId);
    }

    [PunRPC]
    private void RPC_RequestPickup(int playerViewId)
    {
        // 마스터만 확정 처리
        if (!PhotonNetwork.IsMasterClient) return;

        photonView.RPC(nameof(RPC_ApplyPickup), RpcTarget.AllBuffered, playerViewId);
    }

    [PunRPC]
    private void RPC_ApplyPickup(int playerViewId)
    {
        // 플레이어 찾아서 회복
        if (playerViewId != -1)
        {
            PhotonView pv = PhotonView.Find(playerViewId);
            if (pv != null)
            {
                TankDamage td = pv.GetComponent<TankDamage>();
                if (td != null) td.Heal(healAmount);
            }
        }

        // 아이템 제거/비활성 (AllBuffered라 새로 들어온 사람도 사라진 상태 유지)
        if (destroyOnPickup) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}