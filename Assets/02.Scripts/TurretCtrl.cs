using Photon.Pun;
using UnityEngine;

public class TurretCtrl : MonoBehaviourPun, IPunObservable
{
    private Transform tr;
    private RaycastHit hit;
    public float rotSpeed = 5.0f;
    PhotonView pv = null; // 포톤뷰 컴포넌트 변수
    Quaternion currRot = Quaternion.identity; //원격 탱크 회전 값
    // Start is called once before the first execution of Update after the MonoBehaviour is created
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
            //메인 카메라에서 마우스 커서의 위치로 캐스팅되는 Ray를 생성
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Debug.DrawRay(ray.origin, ray.direction * 100.0f, Color.green);
            //레이가 8번 째 레이어와 부딪혔다면(TERRAIN)
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, 1 << 8))
            {
                //Ray에 맞은 위치를 로컬좌표로 변환
                Vector3 relative = tr.InverseTransformPoint(hit.point);
                //역탄젠트 함수로 두 점간 각도를 계산
                float angle = Mathf.Atan2(relative.x, relative.z) * Mathf.Rad2Deg;
                //rotSpeed에 지정된 속도로 회전
                tr.Rotate(0, angle * Time.deltaTime * rotSpeed, 0);

            }
        }
        else //원격 탱크의 회전 값 
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
