using UnityEngine;
using Photon.Pun;

public class TankDepenetration : MonoBehaviourPun
{
    [Header("겹침 방지")]
    public LayerMask tankMask;          // Tank 레이어만 체크
    public float checkRadius = 6f;      // 탱크 크기에 맞게 조절(대충 차체 폭 정도)
    public float maxPushPerFrame = 1.0f; // 1프레임에 최대 밀어낼 거리
    public bool onlyOwner = true;       // 네트워크면 내 탱크만 처리

    Rigidbody rb;
    Collider[] myCols;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        myCols = GetComponentsInChildren<Collider>(true);
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // 네트워크에서 여러 클라가 동시에 밀어내면 싸우니까,
        // 기본은 "내가 조종하는 탱크만" 겹침 해제
        if (PhotonNetwork.IsConnected && onlyOwner)
        {
            if (!photonView.IsMine) return;
        }

        // 트리거 제외
        Collider[] others = Physics.OverlapSphere(transform.position, checkRadius, tankMask, QueryTriggerInteraction.Ignore);

        foreach (var other in others)
        {
            if (other == null) continue;
            if (other.transform.root == transform) continue;

            foreach (var my in myCols)
            {
                if (my == null || my.isTrigger) continue;

                if (Physics.ComputePenetration(
                        my, my.transform.position, my.transform.rotation,
                        other, other.transform.position, other.transform.rotation,
                        out Vector3 dir, out float dist))
                {
                    Vector3 push = dir * dist;
                    push = Vector3.ClampMagnitude(push, maxPushPerFrame);

                    rb.MovePosition(rb.position + push);
                }
            }
        }
    }
}