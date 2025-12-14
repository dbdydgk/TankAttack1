using UnityEngine;

public class EnemyIncomingFireSensor : MonoBehaviour
{
    public EnemyAI enemyAI;

    [Header("플레이어 포탄 판별")]
    public LayerMask playerProjectileMask;   // PlayerProjectile 레이어 추천
    public string projectileTag = "CANNON"; // 태그로 쓰고 싶으면

    private void Reset()
    {
        enemyAI = GetComponentInParent<EnemyAI>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (enemyAI == null) return;

        // 레이어 체크(권장)
        bool layerOk = (playerProjectileMask.value != 0) &&
                       ((playerProjectileMask.value & (1 << other.gameObject.layer)) != 0);

        // 태그 체크(옵션)
        bool tagOk = !string.IsNullOrEmpty(projectileTag) && other.CompareTag(projectileTag);

        if (!layerOk && !tagOk) return;

        // 속도 방향 추출(가능하면 Rigidbody 우선)
        Vector3 velDir = Vector3.zero;
        Rigidbody rb = other.attachedRigidbody;
        if (rb != null && rb.velocity.sqrMagnitude > 0.01f)
            velDir = rb.velocity.normalized;
        else
            velDir = other.transform.forward;

        enemyAI.NotifyIncomingFire(velDir);
    }
}
