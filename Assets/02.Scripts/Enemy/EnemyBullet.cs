using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
    [Header("기본 설정")]
    public int damage = 20;          // EnemyData에서 받아올 데미지
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
        // 앞으로 발사
        if (rb != null)
            rb.AddForce(transform.forward * speed, ForceMode.VelocityChange);

        // 일정 시간 후 자동 파괴
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        //적 포탄이 생성되자마자 스폰영역 콜리더에 충돌하는 문제를 해결
        if (other.CompareTag("SpawnArea")) return;

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

        // 지형/기타에 부딪혀도 폭발 이펙트 + 포탄 제거
        ExplodeAndDestroy();
    }

    void ExplodeAndDestroy()
    {
        if (col != null)
            col.enabled = false;

        if (rb != null)
            rb.isKinematic = true;

        if (expEffect != null)
        {
            GameObject fx = Instantiate(expEffect, transform.position, Quaternion.identity);
            Destroy(fx, 1f);
        }

        Destroy(gameObject);
    }
}