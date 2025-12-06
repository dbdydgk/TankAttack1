using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    [Header("데이터")]
    public EnemyData enemyData;

    [Header("컴포넌트")]
    public NavMeshAgent agent;
    public Transform turret;
    public Transform firePoint;

    [Header("패트롤 경로 (기본/중전차용)")]
    public Transform[] patrolPoints;
    public float patrolPointReachThreshold = 1f;

    [Header("시야 설정")]
    public float viewAngle = 180f;
    public LayerMask obstacleMask;   // 벽/지형 레이어 (없으면 0으로 두면 됨)

    [Header("플레이어 놓쳤을 때 (기본/중전차용)")]
    public float losePlayerDelay = 3f;

    // 현재 추적 중인 플레이어
    private Transform target;

    private int currentPatrolIndex = 0;
    private float fireTimer = 0f;
    private float losePlayerTimer = 0f;
    private int currentHP;

    private enum EnemyState { Patrol, Chase }
    private EnemyState state = EnemyState.Patrol;

    void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (enemyData == null)
        {
            Debug.LogError("EnemyData가 설정되지 않았습니다.", this);
            enabled = false;
            return;
        }

        // NavMeshAgent 기본 세팅
        agent.speed = enemyData.moveSpeed;
        agent.angularSpeed = enemyData.rotationSpeed;
        agent.stoppingDistance = 0f;

        currentHP = enemyData.maxHP;

        // 기본/중전차는 패트롤 시작, 자주곡사포는 스폰 위치 유지
        if (enemyData.role == EnemyRole.Artillery)
        {
            agent.SetDestination(transform.position); // 처음엔 제자리
        }
        else
        {
            SetNextPatrolDestination();
        }
    }

    void Update()
    {
        // 1) 매 프레임마다 "가장 가까운 플레이어" 갱신
        UpdateTarget();

        // 플레이어가 아무도 없으면(로비 상태 등) 기본 패트롤만 수행
        if (target == null)
        {
            if (enemyData.role == EnemyRole.Basic || enemyData.role == EnemyRole.Heavy)
            {
                if (!agent.pathPending && agent.remainingDistance < patrolPointReachThreshold)
                    SetNextPatrolDestination();
            }
            return;
        }

        // 2) 현재 타겟 기준으로 방향/거리 계산
        Vector3 toTarget = target.position - transform.position;
        float dist = toTarget.magnitude;

        bool canSee = CanSeeTarget(toTarget, dist);

        // 3) 역할별 이동 로직
        switch (enemyData.role)
        {
            case EnemyRole.Basic:
            case EnemyRole.Heavy:
                UpdateBasicAndHeavy(canSee);
                break;

            case EnemyRole.Artillery:
                UpdateArtillery(canSee, toTarget, dist);
                break;
        }

        // 4) 포탑 회전 + 사격
        HandleShooting(canSee, dist);
        RotateTurret(canSee);
    }

    // ================ 타겟 갱신 (4인 협동용 핵심) ================

    void UpdateTarget()
    {
        // 태그 "Player"가 붙은 모든 탱크를 찾는다 (최대 4명)
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        if (players == null || players.Length == 0)
        {
            target = null;
            return;
        }

        float bestDistSq = float.MaxValue;
        Transform best = null;
        Vector3 pos = transform.position;

        foreach (GameObject p in players)
        {
            if (!p.activeInHierarchy) continue; // 비활성 플레이어 무시

            Vector3 diff = p.transform.position - pos;
            float dSq = diff.sqrMagnitude;

            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                best = p.transform;
            }
        }

        target = best;
    }

    // ================ 기본전차 / 중전차 AI ================

    void UpdateBasicAndHeavy(bool canSee)
    {
        switch (state)
        {
            case EnemyState.Patrol:
                if (!agent.pathPending && agent.remainingDistance < patrolPointReachThreshold)
                {
                    SetNextPatrolDestination();
                }

                if (canSee)
                {
                    state = EnemyState.Chase;
                    losePlayerTimer = 0f;
                }
                break;

            case EnemyState.Chase:
                if (target != null)
                    agent.SetDestination(target.position);

                if (canSee)
                {
                    losePlayerTimer = 0f;
                }
                else
                {
                    losePlayerTimer += Time.deltaTime;
                    if (losePlayerTimer >= losePlayerDelay)
                    {
                        state = EnemyState.Patrol;
                        SetNextPatrolDestination();
                    }
                }
                break;
        }
    }

    void SetNextPatrolDestination()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            agent.SetDestination(patrolPoints[currentPatrolIndex].position);
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        }
        else
        {
            // 패트롤 포인트가 없으면 주변 랜덤 이동
            Vector3 randomDir = Random.insideUnitSphere * 10f;
            randomDir += transform.position;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDir, out hit, 10f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
    }

    // ================ 자주곡사포 (거리 10~18m 유지) ================

    void UpdateArtillery(bool canSee, Vector3 toTarget, float dist)
    {
        if (!canSee || target == null)
        {
            // 플레이어를 못 보면 스폰 위치 근처에서 대기
            if (!agent.hasPath)
                agent.SetDestination(transform.position);
            return;
        }

        Vector3 dir = toTarget.normalized;

        // 너무 가까우면 멀어지기
        if (dist < enemyData.preferredMinDistance)
        {
            Vector3 targetPos = transform.position - dir * 5f;
            MoveToNavmeshPoint(targetPos);
        }
        // 너무 멀면 조금 다가가기
        else if (dist > enemyData.preferredMaxDistance)
        {
            Vector3 targetPos = transform.position + dir * 5f;
            MoveToNavmeshPoint(targetPos);
        }
        else
        {
            // 적당한 거리면 제자리 유지
            if (!agent.hasPath || agent.remainingDistance > 0.5f)
                agent.SetDestination(transform.position);
        }
    }

    void MoveToNavmeshPoint(Vector3 targetPos)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, 3f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    // ================ 시야 판정 ================

    bool CanSeeTarget(Vector3 toTarget, float dist)
    {
        if (target == null) return false;

        // detectionRange 밖이면 무조건 못 봄
        if (dist > enemyData.detectionRange)
            return false;

        // 시야각 체크
        Vector3 forward = transform.forward;
        Vector3 flatDir = toTarget;
        flatDir.y = 0f;
        forward.y = 0f;

        if (flatDir.sqrMagnitude < 0.01f)
            return false;

        float angle = Vector3.Angle(forward, flatDir);
        if (angle > viewAngle * 0.5f)
            return false;

        // 장애물 레이어가 지정돼 있다면 Raycast로 막힘 체크
        if (obstacleMask.value != 0)
        {
            Vector3 origin = transform.position + Vector3.up * 1.5f;
            Vector3 dir = (target.position - origin).normalized;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, obstacleMask))
            {
                // 사이에 벽/지형이 있으면 못 본 걸로 처리
                return false;
            }
        }

        return true;
    }

    // ================ 포탑 회전 / 사격 ================

    void RotateTurret(bool canSee)
    {
        if (!canSee || turret == null || target == null) return;

        Vector3 dir = target.position - turret.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        turret.rotation = Quaternion.RotateTowards(
            turret.rotation,
            targetRot,
            enemyData.rotationSpeed * Time.deltaTime
        );
    }

    void HandleShooting(bool canSee, float dist)
    {
        fireTimer -= Time.deltaTime;
        if (!canSee || fireTimer > 0f) return;

        // 필요하면 여기서 dist로 사거리 제한도 가능
        Shoot();
        fireTimer = 1f / enemyData.fireRate;
    }

    void Shoot()
    {
        if (enemyData.bulletPrefab == null || firePoint == null) return;

        // 포탄 생성
        GameObject bulletObj = Instantiate(
            enemyData.bulletPrefab,
            firePoint.position,
            firePoint.rotation
        );

        // EnemyBullet에 데미지 전달
        EnemyBullet b = bulletObj.GetComponent<EnemyBullet>();
        if (b != null)
        {
            b.damage = enemyData.damage;   // EnemyData에 설정한 값 사용
        }
    }

    // ================ 데미지/사망 (아직 안 쓰고 있어도 됨) ================

    public void TakeDamage(int amount)
    {
        currentHP -= amount;
        if (currentHP <= 0)
        {
            Destroy(gameObject);
        }
    }
    //디버그용 적 AI의 시야범위 그리기
    void OnDrawGizmosSelected()
    {
        if (enemyData == null) return;

        // 탐지 반경
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, enemyData.detectionRange);

        // 시야각 (대략적인 시각화)
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        float halfAngle = viewAngle * 0.5f;
        Quaternion leftRot = Quaternion.AngleAxis(-halfAngle, Vector3.up);
        Quaternion rightRot = Quaternion.AngleAxis(halfAngle, Vector3.up);

        Vector3 leftDir = leftRot * forward;
        Vector3 rightDir = rightRot * forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, leftDir * enemyData.detectionRange);
        Gizmos.DrawRay(transform.position, rightDir * enemyData.detectionRange);
    }
}
