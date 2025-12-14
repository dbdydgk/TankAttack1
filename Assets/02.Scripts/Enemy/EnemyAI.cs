using UnityEngine;
using UnityEngine.AI;

#if PHOTON_UNITY_NETWORKING 
using Photon.Pun;
#endif

public class EnemyAI : MonoBehaviour
{
    [Header("데이터")]
    public EnemyData enemyData;

    [Header("컴포넌트")]
    public NavMeshAgent agent;
    public Transform turret;
    public Transform firePoint;

    [Header("포신 상하각(Pitch)")]
    public Transform cannonPitch;      // 포신/캐논 피치용 트랜스폼 (Cannon 또는 FirePoint 부모)
    public float pitchMin = -5f;
    public float pitchMax = 25f;

    [Header("탄도(포물선) 세팅")]
    public float muzzleSpeed = 40f;    // 포탄 초기 속도(너 포탄 스피드에 맞게)
    public bool useBallisticAim = true;
    public float aimOffsetY = 1.0f;    // 목표를 약간 위로 조준(탱크 중심부)

    [Header("피격/탄 날아옴 반응")]
    public float incomingLookDuration = 1.5f;
    private float incomingLookTimer = 0f;
    private Vector3 incomingLookDir = Vector3.forward;

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
    #if PHOTON_UNITY_NETWORKING
            if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
            {
                // 비마스터는 AI 로직/Agent 끄고, 위치 동기화만 받게
                if (agent != null) agent.enabled = false;
                enabled = false;
                return;
            }
    #endif
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (enemyData == null)
        {
            Debug.LogError("EnemyData가 설정되지 않았습니다.", this);
            enabled = false;
            return;
        }
        //탄의 속도를 enemydata와 맞춰줌
        if (enemyData != null && enemyData.bulletPrefab != null)
        {
            var eb = enemyData.bulletPrefab.GetComponent<EnemyBullet>();
            if (eb != null) muzzleSpeed = eb.speed;
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
        RotateTurretYaw(canSee);
        RotateCannonPitch(canSee);
        HandleShooting(canSee, dist);
    }
    void RotateTurretYaw(bool canSee)
    {
        if (turret == null) return;

        // 1) 탄이 날아온 반응이 우선
        if (incomingLookTimer > 0f)
        {
            incomingLookTimer -= Time.deltaTime;

            Vector3 dir = incomingLookDir;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;

            Quaternion targetRot = Quaternion.LookRotation(dir);
            turret.rotation = Quaternion.RotateTowards(
                turret.rotation,
                targetRot,
                enemyData.rotationSpeed * Time.deltaTime
            );
            return;
        }

        // 2) 평소에는 타겟이 보일 때만 회전
        if (!canSee || target == null) return;

        Vector3 toTarget = target.position - turret.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.01f) return;

        Quaternion lookRot = Quaternion.LookRotation(toTarget);
        turret.rotation = Quaternion.RotateTowards(
            turret.rotation,
            lookRot,
            enemyData.rotationSpeed * Time.deltaTime
        );
    }
    void RotateCannonPitch(bool canSee)
    {
        if (cannonPitch == null || firePoint == null) return;

        // 1) 탄 날아온 반응 중이면: 그냥 그 방향으로 "직접" 피치 맞추기(탄도 계산 X)
        if (incomingLookTimer > 0f)
        {
            Vector3 dir = incomingLookDir.normalized;
            ApplyPitchFromDirection(dir);
            return;
        }

        // 2) 타겟 못 보면 피치 조정 안 함
        if (!canSee || target == null) return;

        Vector3 targetPos = target.position + Vector3.up * aimOffsetY;

        if (useBallisticAim)
        {
            if (TryGetBallisticDirection(firePoint.position, targetPos, muzzleSpeed, out Vector3 ballisticDir))
            {
                ApplyPitchFromDirection(ballisticDir);
            }
            else
            {
                // 사거리/속도 조건 때문에 탄도 해가 없으면 그냥 직접 조준
                Vector3 directDir = (targetPos - firePoint.position).normalized;
                ApplyPitchFromDirection(directDir);
            }
        }
        else
        {
            Vector3 directDir = (targetPos - firePoint.position).normalized;
            ApplyPitchFromDirection(directDir);
        }
    }

    void ApplyPitchFromDirection(Vector3 worldDir)
    {
        // 캐논 로컬 기준으로 X축 회전(일반적으로 pitch = local X)
        // firePoint의 forward가 worldDir을 향하도록 캐논 pitch만 조정
        Quaternion worldLook = Quaternion.LookRotation(worldDir, Vector3.up);

        // 캐논Pitch의 "부모 기준" 로컬 회전으로 변환
        Transform parent = cannonPitch.parent;
        Quaternion localLook = (parent != null)
            ? Quaternion.Inverse(parent.rotation) * worldLook
            : worldLook;

        Vector3 euler = localLook.eulerAngles;

        // Unity euler 0~360 보정 → -180~180
        float pitch = euler.x;
        if (pitch > 180f) pitch -= 360f;

        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        // pitch만 적용 (y/z는 기존 유지)
        Vector3 current = cannonPitch.localEulerAngles;
        float curX = current.x;
        if (curX > 180f) curX -= 360f;

        cannonPitch.localEulerAngles = new Vector3(pitch, current.y, current.z);
    }

    // 탄도 방향 계산(낮은 각도 우선)
    bool TryGetBallisticDirection(Vector3 from, Vector3 to, float speed, out Vector3 dir)
    {
        dir = Vector3.forward;

        Vector3 diff = to - from;
        Vector3 diffXZ = new Vector3(diff.x, 0f, diff.z);
        float x = diffXZ.magnitude;     // 수평 거리
        float y = diff.y;               // 높이 차
        float g = Mathf.Abs(Physics.gravity.y);

        float v2 = speed * speed;
        float v4 = v2 * v2;

        float discriminant = v4 - g * (g * x * x + 2f * y * v2);
        if (discriminant < 0f) return false;

        float sqrt = Mathf.Sqrt(discriminant);

        // 낮은 각도(직사에 가까운) 선택
        float tan = (v2 - sqrt) / (g * x);
        float angle = Mathf.Atan(tan); // rad

        Vector3 flatDir = diffXZ.normalized;
        dir = (flatDir * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle)).normalized;
        return true;
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
    //이 함수는 포탑회전만 담당 -> 포탑회전과 포신 상하 각도를 제어하는 코드 추가.
    //void RotateTurret(bool canSee)
    //{
    //    if (!canSee || turret == null || target == null) return;

    //    Vector3 dir = target.position - turret.position;
    //    dir.y = 0f;
    //    if (dir.sqrMagnitude < 0.01f) return;

    //    Quaternion targetRot = Quaternion.LookRotation(dir);
    //    turret.rotation = Quaternion.RotateTowards(
    //        turret.rotation,
    //        targetRot,
    //        enemyData.rotationSpeed * Time.deltaTime
    //    );
    //}

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
    public void NotifyIncomingFire(Vector3 projectileVelocityDir)
    {
        // 포탄은 "발사자 -> 적" 방향으로 날아옴
        // 발사자를 바라보려면 반대 방향을 봐야 함
        incomingLookDir = (-projectileVelocityDir).normalized;
        incomingLookTimer = incomingLookDuration;
    }
}
