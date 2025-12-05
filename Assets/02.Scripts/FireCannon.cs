using Photon.Pun;
using UnityEngine;

public class FireCannon : MonoBehaviour
{
    private GameObject cannon = null;
    public Transform firePos;
    AudioClip fireSfx = null;
    AudioSource sfx = null;
    PhotonView pv = null; //포톤뷰 컴포넌트
    float lastFireTime = 0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        //Cannon 프리팹을  Resources폴더에서 불러와 로드
        cannon = (GameObject)Resources.Load("Cannon");
        fireSfx = Resources.Load<AudioClip>("CannonFire");
        sfx = GetComponent<AudioSource>();
        pv = GetComponent<PhotonView>();//포톤뷰 컴포넌트 할당
    }
    // Update is called once per frame
    void Update()
    {
        if (pv.IsMine && Input.GetMouseButtonDown(0) &&
            Time.time > lastFireTime + 1.0f)
        {
            lastFireTime = Time.time;
            Fire(); //마우스 왼쪽 버튼 누르면 cannon 프리팹 생성
            pv.RPC("Fire", RpcTarget.Others, null);
        }
    }
    [PunRPC]
    void Fire()
    {
        sfx.PlayOneShot(fireSfx, 1.0f);// 발사 사운드 재생
        Instantiate(cannon, firePos.position, firePos.rotation);
    }
}
