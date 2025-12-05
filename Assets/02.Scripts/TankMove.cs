using UnityEngine;
using Photon.Pun;
using UnityStandardAssets.Utility;

public class TankMove : MonoBehaviourPun, IPunObservable
{
    public float moveSpeed = 20.0f;
    public float rotSpeed = 50.0f;

    private Rigidbody rbody;
    private Transform tr;
    private float h, v;
    PhotonView pv; //포톤뷰 컴포넌트
    public Transform camPivot; //카메라가 추적할 대상 camPivot

    Vector3 currPos = Vector3.zero; //원격 탱크 좌표
    Quaternion currRot = Quaternion.identity;//회전값
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        rbody = GetComponent<Rigidbody>();
        tr = GetComponent<Transform>();
        rbody.centerOfMass = new Vector3(0.0f, -0.5f, 0.0f);
        pv = GetComponent<PhotonView>();//포톤뷰 컴포넌트 할당
        //데이터 전송 타입 설정(UDP)
        pv.Synchronization = ViewSynchronization.UnreliableOnChange;
        pv.ObservedComponents[0] = this;
        if (pv.IsMine) //로컬이라면(내 탱크라면)
        {
            Camera.main.
             GetComponent<SmoothFollow>().target = camPivot;
            rbody.centerOfMass = new Vector3(0.0f,-0.5f, 0.0f);
        }
        else //원격 탱크의 물리력 사용 X
        {
            rbody.isKinematic = true;
        }
        //원격 탱크의 위치, 회전 값 설정
        currPos = tr.position;
        currRot = tr.rotation;
    }

    // Update is called once per frame
    void Update()
    {
        if (pv.IsMine)//로컬 탱크만 키 조작
        {
            h = Input.GetAxis("Horizontal");
            v = Input.GetAxis("Vertical");
            tr.Rotate(Vector3.up * rotSpeed * h * Time.deltaTime);
            tr.Translate(Vector3.forward * v * moveSpeed * Time.deltaTime);
        }
        else//수신받은 위치/회전 값으로 부드럽게 이동
        {
            tr.position = Vector3.Lerp(tr.position,
                currPos, Time.deltaTime * 3.0f);
            tr.rotation = Quaternion.Slerp(tr.rotation,
                currRot, Time.deltaTime * 3.0f);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting) //로컬 위치/회전 정보 송신
        {
            stream.SendNext(tr.position);
            stream.SendNext(tr.rotation);
        }
        else//원격 플레이어 위치/회전 정보 수신
        {
            currPos = (Vector3)stream.ReceiveNext();
            currRot = (Quaternion)stream.ReceiveNext();
        }
    }
}
