using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PhotonView))]
public class EnemyBullet : MonoBehaviourPun
{
    [Header("기본 설정")]
    public float damage = 20;          // EnemyData에서 받아올 데미지
    public float speed = 60f;        // 발사 속도
    public float lifeTime = 3f;      // 자동 파괴 시간
    public GameObject expEffect;     // 폭발 이펙트 (있으면 사용)

    private Rigidbody rb;
    private Collider col;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    void Start()
    {
        // 네트워크 오브젝트는 소유자(=마스터가 Instantiate 했으면 마스터)만 실제 물리로 발사한다.
        if (PhotonNetwork.IsConnected)
        {
            if (photonView.IsMine)
            {
                LaunchPhysics();
            }
            // 원격은 LaunchPhysics() 호출하지 않음.
            // PhotonRigidbodyView가 rb.velocity 등을 동기화해서 알아서 날아가게 됨.
        }
        else
        {
            // 오프라인(싱글)일 때는 그냥 로컬 물리로 발사
            LaunchPhysics();
        }

        Destroy(gameObject, lifeTime);
    }
    private void LaunchPhysics()
    {
        if (rb == null) return;

        // AddForce도 가능하지만, 네트워크 동기화는 velocity로 “한 번에” 세팅하는 게 더 안정적임
        rb.linearVelocity = transform.forward * speed;
    }
    private void OnTriggerEnter(Collider other)
    {
        // 충돌 처리도 중복 방지: 소유자만 처리
        if (PhotonNetwork.IsConnected && !photonView.IsMine)
            return;

        // 적 포탄이 생성되자마자 스폰영역 콜리더에 충돌하는 문제 방지
        if (other.CompareTag("SpawnArea")) return;
        if (other.CompareTag("EnemySensor")) return;
        if (other.CompareTag("Item")) return;

        TankDamage td = other.GetComponentInParent<TankDamage>();

        if (td != null)
        {
            // 아군(Enemy 탱크)면 데미지 없이 폭발만
            if (td.gameObject.CompareTag("Enemy"))
            {
                ExplodeAndDestroy();
                return;
            }

            // 플레이어 탱크면 데미지 적용
            if (td.gameObject.CompareTag("Player"))
            {
                td.TakeDamage(damage);
                ExplodeAndDestroy();
                return;
            }
        }

        ExplodeAndDestroy();
    }

    void ExplodeAndDestroy()
    {
        // 이펙트/비주얼은 전원에게 보이게 RPC로 뿌리는 게 좋음
        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC(nameof(RpcExplodeVisual), RpcTarget.All, transform.position);
            if (photonView.IsMine)
                PhotonNetwork.Destroy(gameObject);   // 네트워크 전체 삭제
        }
        else
        {
            RpcExplodeVisual(transform.position);
            Destroy(gameObject);
        }
    }
    [PunRPC]
    void RpcExplodeVisual(Vector3 pos)
    {
        if (col != null) col.enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (expEffect != null)
        {
            GameObject fx = Instantiate(expEffect, pos, Quaternion.identity);
            Destroy(fx, 1f);
        }
    }

    // EnemyAI에서 데미지 세팅하려고 쓰는 초기화 RPC는 유지
    [PunRPC]
    public void RpcInit(float newDamage)
    {
        damage = newDamage;
    }
    
}