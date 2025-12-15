using UnityEngine;

#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
#endif

/// <summary>
/// 적 탱크의 포탑(Yaw) / 포신(Pitch) 회전을 네트워크로 동기화.
/// - 오너(보통 마스터)가 값을 전송
/// - 다른 클라는 보간해서 적용
/// </summary>
public class TurretCannonSync : MonoBehaviour
#if PHOTON_UNITY_NETWORKING
    , IPunObservable
#endif
{
    [Header("참조(EnemyAI와 동일 대상)")]
    public Transform turret;        // Yaw 회전하는 포탑
    public Transform cannonPitch;   // Pitch 회전하는 포신(캐논)

    [Header("보간")]
    public float turretLerp = 14f;
    public float cannonLerp = 14f;

    private Quaternion netTurretLocalRot;
    private Quaternion netCannonLocalRot;
    private bool hasNet;

#if PHOTON_UNITY_NETWORKING
    private PhotonView pv;
#endif

    void Awake()
    {
#if PHOTON_UNITY_NETWORKING
        pv = GetComponent<PhotonView>();
#endif
        if (turret != null) netTurretLocalRot = turret.localRotation;
        if (cannonPitch != null) netCannonLocalRot = cannonPitch.localRotation;
        hasNet = false;
    }

    void Update()
    {
#if PHOTON_UNITY_NETWORKING
        if (!PhotonNetwork.InRoom) return;

        // 오너(대개 마스터)가 계산한 회전을 전송하고,
        // 비오너는 수신값으로 보간 적용
        if (pv != null && !pv.IsMine)
        {
            if (!hasNet) return;

            if (turret != null)
                turret.localRotation = Quaternion.Slerp(
                    turret.localRotation, netTurretLocalRot, Time.deltaTime * turretLerp);

            if (cannonPitch != null)
                cannonPitch.localRotation = Quaternion.Slerp(
                    cannonPitch.localRotation, netCannonLocalRot, Time.deltaTime * cannonLerp);
        }
#endif
    }

#if PHOTON_UNITY_NETWORKING
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // 이 오브젝트의 소유자만 Write 가능 (PhotonView.IsMine 기준)
        if (stream.IsWriting)
        {
            // turret / cannonPitch가 비어있어도 크래시 안 나게 방어
            stream.SendNext(turret != null ? turret.localRotation : Quaternion.identity);
            stream.SendNext(cannonPitch != null ? cannonPitch.localRotation : Quaternion.identity);
        }
        else
        {
            netTurretLocalRot = (Quaternion)stream.ReceiveNext();
            netCannonLocalRot = (Quaternion)stream.ReceiveNext();
            hasNet = true;
        }
    }
#endif
}