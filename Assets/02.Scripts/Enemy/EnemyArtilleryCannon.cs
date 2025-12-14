using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class EnemyArtilleryCannon : MonoBehaviourPun
{
    [Header("피해")]
    public int maxDamage = 40;     // 중심부 최대 데미지
    public int minDamage = 15;     // 바깥 최소 데미지
    public float splashRadius = 6f;

    [Header("이동")]
    public float speed = 60f;      // Unity 6.2: linearVelocity 사용
    public float lifeTime = 6f;

    [Header("이펙트")]
    public GameObject expEffect;

    Rigidbody rb;
    Collider col;

    bool exploded;
    bool initialized;   // RpcInit을 받았는지
    bool launched;      // 실제 발사(속도 세팅) 했는지

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // 네트워크가 아닌(오프라인) 환경이면 바로 발사 가능
        if (!PhotonNetwork.IsConnected)
            initialized = true;
    }

    void Start()
    {
        // 원격 클라는 물리 계산하지 않게 (위치/회전은 PhotonRigidbodyView로 받음)
        if (PhotonNetwork.IsConnected && !photonView.IsMine)
        {
            rb.isKinematic = true;
            return;
        }

        // 오프라인이면 바로 발사
        if (!PhotonNetwork.IsConnected)
            LaunchIfNeeded();
        // 온라인(Photon)에서는 RpcInit에서 발사 시작
    }

    // EnemyAI에서 PhotonView.RPC로 호출할 초기화 함수
    [PunRPC]
    public void RpcInit(int newMax, int newMin, float newRadius, float newSpeed, float newLifeTime)
    {
        maxDamage = newMax;
        minDamage = newMin;
        splashRadius = newRadius;
        speed = newSpeed;
        lifeTime = newLifeTime;

        initialized = true;

        // 발사는 "소유자(대부분 마스터)"만 수행
        if (photonView.IsMine)
            LaunchIfNeeded();
    }

    void LaunchIfNeeded()
    {
        if (!initialized || launched) return;

        launched = true;

        rb.isKinematic = false;
        rb.useGravity = true;

        // firePoint의 forward 방향으로 초기 속도 부여 (곡사는 중력으로 자동 형성)
        rb.linearVelocity = transform.forward * speed;

        StartCoroutine(AutoExplode());
    }

    IEnumerator AutoExplode()
    {
        yield return new WaitForSeconds(lifeTime);
        Explode();
    }

    void OnTriggerEnter(Collider other)
    {
        // 폭발 트리거/물리는 소유자만 처리 (중복 방지)
        if (!photonView.IsMine || exploded) return;

        // 기존 예외 태그들
        if (other.CompareTag("SpawnArea")) return;
        if (other.CompareTag("EnemySensor")) return;
        if (other.CompareTag("Item")) return;

        Explode();
    }

    void Explode()
    {
        if (exploded) return;
        exploded = true;

        // 충돌 중복 방지
        if (col != null) col.enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        Vector3 pos = transform.position;

        // 1) 데미지 적용은 무조건 "마스터만"
        if (PhotonNetwork.IsConnected)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                ApplySplashDamage(pos);
            }
            else
            {
                // 마스터가 아닌 경우: 마스터에게 "데미지 적용 요청"만 보냄
                photonView.RPC(nameof(RPC_ApplySplashDamage), RpcTarget.MasterClient, pos);
            }
        }
        else
        {
            // 오프라인은 그냥 적용
            ApplySplashDamage(pos);
        }

        // 2) 이펙트는 모두에게
        if (PhotonNetwork.IsConnected)
            photonView.RPC(nameof(RPC_PlayFx), RpcTarget.All, pos);
        else
            RPC_PlayFx(pos);

        // 3) 삭제는 소유자(or 마스터)가 수행
        if (PhotonNetwork.IsConnected)
        {
            if (photonView.IsMine || PhotonNetwork.IsMasterClient)
                PhotonNetwork.Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [PunRPC]
    void RPC_ApplySplashDamage(Vector3 pos)
    {
        // 안전장치: 마스터만 수행
        if (!PhotonNetwork.IsMasterClient) return;
        ApplySplashDamage(pos);
    }

    void ApplySplashDamage(Vector3 center)
    {
        Collider[] hits = Physics.OverlapSphere(center, splashRadius);

        foreach (var h in hits)
        {
            TankDamage td = h.GetComponentInParent<TankDamage>();
            if (td == null) continue;

            // 아군(Enemy)은 제외
            if (td.CompareTag("Enemy")) continue;

            // 거리 기반 데미지 감소
            float d = Vector3.Distance(center, td.transform.position);
            float t = Mathf.Clamp01(1f - (d / splashRadius));
            int dmg = Mathf.RoundToInt(Mathf.Lerp(minDamage, maxDamage, t));

            td.TakeDamage(dmg); // TankDamage 안에서 이미 isDead면 무시 처리됨
        }
    }

    [PunRPC]
    void RPC_PlayFx(Vector3 pos)
    {
        if (expEffect != null)
        {
            GameObject fx = Instantiate(expEffect, pos, Quaternion.identity);
            Destroy(fx, 2f);
        }
    }
}
