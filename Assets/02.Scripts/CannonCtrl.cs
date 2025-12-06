using Photon.Pun;
using UnityEngine;
using UnityEngine.UIElements;

public class CannonCtrl : MonoBehaviourPun, IPunObservable
{
    private Transform tr;
    public float rotSpeed = 100.0f;
    PhotonView pv = null; // 포톤뷰 컴포넌트 변수
    Quaternion currRot = Quaternion.identity; //원격 탱크 회전 값

    [Header("포신 상하 각도 제한")]
    public float minPitch = -25f;
    public float maxPitch = 5f;

    void Awake()
    {
        tr = GetComponent<Transform>();
        pv = GetComponent<PhotonView>();
        //데이터 전송 타입 및 동기화 속성 설정
        pv.ObservedComponents[0] = this;
        pv.Synchronization = ViewSynchronization.UnreliableOnChange;
        currRot = tr.localRotation; //초기 회전값 설정
    }
    // Update is called once per frame
    void Update()
    {
        if (pv.IsMine)
        {
            //마우스 스크롤 휠로 Cannon 각도조절
            float angle = -Input.GetAxis("Mouse ScrollWheel") * Time.deltaTime *
                rotSpeed;

            Vector3 euler = tr.localEulerAngles;
            float pitch = euler.x;
            if (pitch > 180f) pitch -= 360f;

            pitch += angle;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            euler.x = pitch;
            tr.localEulerAngles = euler;
        }
        else
        {
            tr.localRotation = Quaternion.Slerp(tr.localRotation,
               currRot, Time.deltaTime * 3.0f);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(tr.localRotation);
        }
        else
        {
            currRot = (Quaternion)stream.ReceiveNext();
        }
    }
}
