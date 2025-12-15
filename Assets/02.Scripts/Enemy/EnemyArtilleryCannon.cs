using System.Collections;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class EnemyArtilleryCannon : MonoBehaviourPun
{
    [Header("피해")]
    public float maxDamage = 40f;
    public float minDamage = 15f;
    public float splashRadius = 30f;

    [Header("이동")]
    public float speed = 60f;   // (이제는 참고값)
    public float lifeTime = 6f;

    [Header("곡사 탄도")]
    public float arcHeight = 6f;         // 최고점이 (start/target 중 높은 y) + arcHeight
    public float lifeTimePadding = 0.3f; // 비행시간 + 여유

    [Header("이펙트")]
    public GameObject expEffect;

    Rigidbody rb;
    Collider col;

    bool exploded;
    bool initialized;
    bool launched;

    // 추가: 착탄지점
    bool hasTargetPoint;
    Vector3 targetPoint;

    [Header("충돌 보정")]
    public float armDelay = 0.15f;   // 발사 직후 이 시간 동안은 충돌로 폭발 금지
    private float armUntil = 0f;

    [Header("디버그용 설정")]
    [SerializeField] bool debugDrawRadius = true;
    Vector3 lastExplodePos;
    bool hasExplodePos;

    [Header("사운드")]
    public AudioClip explodeSfx;
    [Range(0f, 1f)] public float explodeSfxVolume = 1f;
    public float explodeSfxMaxDistance = 80f;
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        if (!PhotonNetwork.IsConnected)
            initialized = true;
    }

    void Start()
    {
        if (PhotonNetwork.IsConnected && !photonView.IsMine)
        {
            rb.isKinematic = true;
            return;
        }

        if (!PhotonNetwork.IsConnected)
            LaunchIfNeeded();
    }

    // 기존 RPC 유지(호환용) - 그대로 둬도 됨
    [PunRPC]
    public void RpcInit(float newMax, float newMin, float newRadius, float newSpeed, float newLifeTime)
    {
        maxDamage = newMax;
        minDamage = newMin;
        splashRadius = newRadius;
        speed = newSpeed;
        lifeTime = newLifeTime;

        initialized = true;

        if (photonView.IsMine)
            LaunchIfNeeded();
    }

    // 신규: 착탄지점 기반 초기화
    [PunRPC]
    public void RpcInitWithTarget(float newMax, float newMin, float newRadius, float newSpeed, float newLifeTime, Vector3 newTargetPoint, float newArcHeight)
    {
        maxDamage = newMax;
        minDamage = newMin;
        splashRadius = newRadius;
        speed = newSpeed;
        lifeTime = newLifeTime;

        targetPoint = newTargetPoint;
        hasTargetPoint = true;
        arcHeight = newArcHeight;

        initialized = true;

        if (photonView.IsMine)
            LaunchIfNeeded();
    }

    // 오프라인에서 쓰기 편하게(선택)
    public void InitWithTarget(float newMax, float newMin, float newRadius, float newSpeed, float newLifeTime, Vector3 newTargetPoint, float newArcHeight)
    {
        maxDamage = newMax;
        minDamage = newMin;
        splashRadius = newRadius;
        speed = newSpeed;
        lifeTime = newLifeTime;

        targetPoint = newTargetPoint;
        hasTargetPoint = true;
        arcHeight = newArcHeight;

        initialized = true;
        LaunchIfNeeded();
    }

    void LaunchIfNeeded()
    {
        if (!initialized || launched) return;
        launched = true;

        armUntil = Time.time + armDelay;

        rb.isKinematic = false;
        rb.useGravity = true;

        // 1) 착탄지점이 있으면: 그 지점으로 떨어지도록 초기 속도 계산
        if (hasTargetPoint && TryGetVelocityToHitPoint(transform.position, targetPoint, arcHeight, out Vector3 v0, out float flightTime))
        {
            rb.linearVelocity = v0;

            // 시각적으로 방향도 맞춰주고 싶으면(선택)
            Vector3 flat = new Vector3(v0.x, 0f, v0.z);
            if (flat.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(flat);

            // 2) 비행시간보다 lifeTime이 짧아서 공중폭발 나는 걸 방지
            lifeTime = Mathf.Max(lifeTime, flightTime + lifeTimePadding);
        }
        else
        {
            // 실패 시 기존 방식(안전장치)
            rb.linearVelocity = transform.forward * speed;
        }

        StartCoroutine(AutoExplode());
    }

    bool TryGetVelocityToHitPoint(Vector3 from, Vector3 to, float extraArcHeight, out Vector3 v0, out float flightTime)
    {
        v0 = Vector3.zero;
        flightTime = 0f;

        float g = Mathf.Abs(Physics.gravity.y);
        if (g < 0.001f) return false;

        // 최고점 y를 강제로 지정 (항상 해가 존재하게 만들기)
        float apexY = Mathf.Max(from.y, to.y) + Mathf.Max(0.5f, extraArcHeight);

        float upHeight = apexY - from.y;
        float downHeight = apexY - to.y;
        if (upHeight < 0.01f || downHeight < 0.01f) return false;

        float vy = Mathf.Sqrt(2f * g * upHeight);
        float tUp = vy / g;
        float tDown = Mathf.Sqrt(2f * downHeight / g);
        float t = tUp + tDown;

        Vector3 diffXZ = new Vector3(to.x - from.x, 0f, to.z - from.z);
        Vector3 vXZ = diffXZ / t;

        v0 = vXZ + Vector3.up * vy;
        flightTime = t;
        return true;
    }

    IEnumerator AutoExplode()
    {
        yield return new WaitForSeconds(lifeTime);
        Explode();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!photonView.IsMine || exploded) return;

        // 발사 직후 겹침으로 들어오는 트리거는 무시
        if (Time.time < armUntil) return;

        if (other.CompareTag("SpawnArea")) return;
        if (other.CompareTag("EnemySensor")) return;
        if (other.CompareTag("Item")) return;

        // 추가: 아군(적 탱크)과의 겹침/충돌은 무시
        if (other.CompareTag("Enemy")) return;
        if (other.transform.root != null && other.transform.root.CompareTag("Enemy")) return;

        Explode();
    }

    void Explode()
    {
        if (exploded) return;
        exploded = true;

        if (col != null) col.enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        Vector3 pos = transform.position;
        lastExplodePos = pos;
        hasExplodePos = true;

        if (PhotonNetwork.IsConnected)
        {
            if (PhotonNetwork.IsMasterClient) ApplySplashDamage(pos);
            else photonView.RPC(nameof(RPC_ApplySplashDamage), RpcTarget.MasterClient, pos);
        }
        else
        {
            ApplySplashDamage(pos);
        }

        if (PhotonNetwork.IsConnected) photonView.RPC(nameof(RPC_PlayFx), RpcTarget.All, pos);
        else RPC_PlayFx(pos);

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
            if (td.CompareTag("Enemy")) continue;

            float d = Vector3.Distance(center, td.transform.position);
            float t = Mathf.Clamp01(1f - (d / splashRadius));
            float dmg = Mathf.Lerp(minDamage, maxDamage, t);
            td.TakeDamage(dmg);
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

        if (explodeSfx != null)
        {
            GameObject sfxObj = new GameObject("ArtilleryExplodeSFX");
            sfxObj.transform.position = pos;

            var a = sfxObj.AddComponent<AudioSource>();
            a.spatialBlend = 1f;          // 3D 사운드
            a.rolloffMode = AudioRolloffMode.Linear;
            a.maxDistance = explodeSfxMaxDistance;
            a.volume = explodeSfxVolume;
            a.PlayOneShot(explodeSfx);

            Destroy(sfxObj, explodeSfx.length + 0.2f);
        }
    }
    void OnDrawGizmos()
    {
        if (!debugDrawRadius) return;

        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);

        // 플레이 중엔 lastExplodePos, 아니면 현재 위치 기준으로 표시
        Vector3 p = (Application.isPlaying && hasExplodePos) ? lastExplodePos : transform.position;
        Gizmos.DrawWireSphere(p, splashRadius);
    }
}
