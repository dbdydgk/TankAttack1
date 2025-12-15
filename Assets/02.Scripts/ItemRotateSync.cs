using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class ItemRotateSync : MonoBehaviourPun, IPunObservable
{
    [Header("회전 연출")]
    public Vector3 rotateAxis = Vector3.up;   // 보통 Y축
    public float rotateSpeed = 90f;           // deg/sec

    [Header("동기화 보간")]
    public float posLerp = 12f;
    public float rotLerp = 12f;

    // 수신 타겟(다른 클라에서 보간용)
    private Vector3 netPos;
    private Quaternion netRot;

    void Awake()
    {
        netPos = transform.position;
        netRot = transform.rotation;
    }

    void Update()
    {
        // 오프라인(싱글)에서는 그냥 로컬 회전
        if (!PhotonNetwork.InRoom)
        {
            RotateLocal();
            return;
        }

        // 네트워크에서는 "마스터가 회전/좌표 확정" -> 나머지는 수신 보간
        if (PhotonNetwork.IsMasterClient)
        {
            RotateLocal();
        }
        else
        {
            // 부드럽게 따라가게 보간
            transform.position = Vector3.Lerp(transform.position, netPos, Time.deltaTime * posLerp);
            transform.rotation = Quaternion.Slerp(transform.rotation, netRot, Time.deltaTime * rotLerp);
        }
    }

    private void RotateLocal()
    {
        transform.Rotate(rotateAxis.normalized, rotateSpeed * Time.deltaTime, Space.World);
    }

    // PhotonView가 이 컴포넌트를 Observe 할 때 자동 호출됨
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // 마스터가 현재 상태 송신
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
        }
        else
        {
            // 다른 클라: 타겟 값 수신
            netPos = (Vector3)stream.ReceiveNext();
            netRot = (Quaternion)stream.ReceiveNext();
        }
    }
}