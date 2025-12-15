using Photon.Pun;
using System.Collections;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;
using UnityEngine.UI;
using ExitGames.Client.Photon;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Realtime;

public class TankDamage : MonoBehaviourPun
{
    public Canvas hudCanvas; //Canvas 객체
    public Image hpBar; // 체력바 이미지
    //탱크 폭파 후 투명 처리를 위한 MeshRenderer 컴포넌트 배열
    MeshRenderer[] renderers;
    GameObject expEffect = null; //탱크 폭발 효과
    public float initHp = 100f; //탱크 초기 생명치
    float currHp = 0f; // 현재 체력

    bool isDead = false;    //적 탱크 죽었는지 여부

    public Behaviour[] disableBehavioursOnDeath;

    public Collider[] disableCollidersOnDeath;

    Rigidbody rb;
    RigidbodyConstraints rbConstraintsBackup;
    bool rbKinematicBackup;
    //EnemyAI에서 참조할 프로퍼티
    public bool IsDead => isDead;
    public bool IsAlive => currHp > 0f && !isDead;

    //플레이어의 적 킬 수를 저장할 때 사용할 키 이름
    const string KILLS_KEY = "kills";

    void Awake()
    {
        renderers = GetComponentsInChildren<MeshRenderer>();
        currHp = initHp;
        //탱크 폭발 시 생성시킬 폭발효과 로드
        expEffect = Resources.Load<GameObject>("Exploson10");
        hpBar.color = Color.green; // Filled 이미지 색상을 녹색으로..
        ApplyHpUI();    //초기 UI 반영

        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rbConstraintsBackup = rb.constraints;
            rbKinematicBackup = rb.isKinematic;
        }
    }
    // 로컬 플레이어는 게임 시작 시 킬 0으로 초기화(한 번만)
    void Start()
    {
        if (!PhotonNetwork.InRoom) return;
        if (!photonView.IsMine) return;

        // 적(Enemy)이 마스터 소유로 스폰되면 여기 들어와서 kills 리셋되는 문제 방지
        if (!CompareTag("Player")) return;

        // 매번 0으로 덮어쓰지 말고, 처음 한 번만 세팅
        if (!PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(KILLS_KEY))
        {
            var ht = new ExitGames.Client.Photon.Hashtable { { KILLS_KEY, 0 } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(ht);
        }
    }
    void SetDeadState(bool dead)
    {
        // 이동/물리 멈춤
        if (rb != null)
        {
            if (dead)
            {
                rb.linearVelocity = Vector3.zero;   // Unity 6.2 기준
                rb.angularVelocity = Vector3.zero;
                rb.constraints = RigidbodyConstraints.FreezeAll;
            }
            else
            {
                rb.constraints = rbConstraintsBackup;
                rb.isKinematic = rbKinematicBackup;
            }
        }

        // 입력/이동/발사 스크립트 끄기(인스펙터로 지정)
        if (disableBehavioursOnDeath != null)
        {
            foreach (var b in disableBehavioursOnDeath)
                if (b != null) b.enabled = !dead;
        }

        // 콜라이더 꺼서 죽은 동안 맞거나 밀리는 것도 방지(선택)
        if (disableCollidersOnDeath != null)
        {
            foreach (var c in disableCollidersOnDeath)
                if (c != null) c.enabled = !dead;
        }
    }
    // EnemyBullet에서 직접 호출할 수 있도록 공개 함수로 분리
    public void TakeDamage(float amount)
    {
        if (amount <= 0) return;
        if (currHp <= 0 || isDead) return;

        // 온라인이면 "마스터 권한"으로 통일
        if (PhotonNetwork.IsConnected)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                ApplyDamageAsMaster(amount);
            }
            else
            {
                // 맞춘 쪽(클라)은 마스터에게 데미지 요청
                photonView.RPC(nameof(RpcRequestDamage), RpcTarget.MasterClient, amount);
            }
            return;
        }

        // 오프라인(싱글)
        ApplyDamageAsMaster(amount);
    }
    [PunRPC]
    void RpcRequestDamage(float amount, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        ApplyDamageAsMaster(amount);
    }
    void ApplyDamageAsMaster(float amount)
    {
        if (amount <= 0) return;
        if (currHp <= 0 || isDead) return;

        currHp -= amount;
        if (currHp < 0) currHp = 0;

        // 전원 동기화는 기존대로
        photonView.RPC(nameof(RpcSyncHp), RpcTarget.All, currHp);

        if (currHp <= 0 && !isDead)
        {
            isDead = true;
            photonView.RPC(nameof(RpcDeathVisual), RpcTarget.All);
            StartCoroutine(ExplosionTankOwner());
        }
    }
    // 기존 ApplyDamageAsMaster를 “공격자”까지 받게 오버로드/수정
    void ApplyDamageAsMaster(float amount, int attackerActor)
    {
        if (amount <= 0) return;
        if (currHp <= 0 || isDead) return;

        currHp -= amount;
        if (currHp < 0) currHp = 0;

        photonView.RPC(nameof(RpcSyncHp), RpcTarget.All, currHp);

        if (currHp <= 0 && !isDead)
        {
            isDead = true;

            // ★ 킬/팀킬 처리 (PVE만)
            HandleKillScore(attackerActor);

            photonView.RPC(nameof(RpcDeathVisual), RpcTarget.All);
            StartCoroutine(ExplosionTankOwner());
        }
    }
    void HandleKillScore(int attackerActor)
    {
        if (!IsPveKillCounting()) return;
        if (attackerActor < 0) return;

        // 적이 죽었으면: attacker +1
        if (CompareTag("Enemy"))
        {
            AddKills(attackerActor, +1);
            return;
        }

        // 플레이어가 죽었으면(팀킬 패널티): attacker -5 (자살은 제외)
        if (CompareTag("Player"))
        {
            int victimActor = photonView.OwnerActorNr;
            if (attackerActor != victimActor)
                AddKills(attackerActor, -5);
        }
    }

    void AddKills(int actorNumber, int delta)
    {
        Player p = PhotonNetwork.CurrentRoom?.GetPlayer(actorNumber);
        if (p == null) return;

        int cur = 0;
        if (p.CustomProperties != null && p.CustomProperties.ContainsKey(KILLS_KEY))
            cur = (int)p.CustomProperties[KILLS_KEY];

        var ht = new Hashtable { { KILLS_KEY, cur + delta } };
        p.SetCustomProperties(ht);
    }
    IEnumerator ExplosionTankOwner()
    {
        // Enemy면 소유자가 네트워크 파괴(지금 너 로직 유지)
        if (CompareTag("Enemy"))
        {
            PhotonView pv = GetComponent<PhotonView>();
            if (pv != null && PhotonNetwork.IsConnected && pv.IsMine)
                PhotonNetwork.Destroy(gameObject);
            else
                Destroy(gameObject);

            yield break;
        }

        float respawnDelay = GetRespawnDelay();
        yield return new WaitForSeconds(respawnDelay);

        // 부활(소유자만 HP 리셋) + 전원 동기화
        currHp = initHp;

        photonView.RPC(nameof(RpcSyncHp), RpcTarget.All, currHp); // 이 안에서 HUD/렌더러도 복구됨
    }
    //pvp용 포탄 데미지 처리
    private void OnTriggerEnter(Collider other)
    {
        if (currHp <= 0) return;
        if (!other.CompareTag("CANNON")) return;

        // ★ 마스터만 데미지/킬 판정 (중복 데미지 방지)
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient) return;

        float damage = 20;
        int attackerActor = -1;

        Cannon cn = other.GetComponent<Cannon>();
        if (cn != null)
        {
            damage = cn.damage;
            attackerActor = cn.ownerActorNumber;  // FireCannon에서 넣어둔 값
        }

        ApplyDamageAsMaster(damage, attackerActor);
    }
    //플레이어 죽음과 적 탱크 죽음 따로 나누는 ExplosionTank 함수 새로 작성함.
    //IEnumerator ExplosionTank()
    //{
    //    //폭발효과 생성
    //    GameObject effect = GameObject.Instantiate(expEffect,
    //        transform.position, Quaternion.identity);

    //    Destroy(effect, 3.0f);//3초뒤에 파괴

    //    hudCanvas.enabled = false;//HUD캔버스 안보이게
    //    SetTankVisible(false); //탱크 안보이게
    //    // 이 오브젝트가 "Enemy" 태그면 = 적 탱크 → 그냥 파괴하고 끝
    //    if (CompareTag("Enemy"))
    //    {
    //        // 네트워크 오브젝트면 PhotonNetwork.Destroy 사용
    //        PhotonView pv = GetComponent<PhotonView>();
    //        if (pv != null && PhotonNetwork.IsConnected && pv.IsMine)
    //        {
    //            PhotonNetwork.Destroy(gameObject);
    //        }
    //        else
    //        {
    //            Destroy(gameObject);
    //        }

    //        yield break;
    //    }

    //    float respawnDelay = GetRespawnDelay();
    //    yield return new WaitForSeconds(respawnDelay);

    //    // 체력/HP바/메쉬 복구 → 부활
    //    currHp = initHp;

    //    if (hpBar != null)
    //    {
    //        hpBar.fillAmount = 1.0f;
    //        hpBar.color = Color.green;
    //    }

    //    if (hudCanvas != null)
    //        hudCanvas.enabled = true;

    //    SetTankVisible(true);
    //}
    float GetRespawnDelay()
    {
        GameMgr gm = FindObjectOfType<GameMgr>();
        if (gm != null && gm.isPvpMode)
        {
            // PVP 모드
            return 3.0f;
        }
        else
        {
            // PVE 모드 (또는 GameMgr 못 찾았을 때 기본값)
            return 10.0f;
        }
    }
    // Update is called once per frame
    void Update()
    {
        
    }
    public void Heal(float amount)
    {
        if (amount <= 0) return;
        if (currHp <= 0 || isDead) return;

        // 힐도 마스터 권한으로 통일(안 그러면 클라/마스터 간 힐 desync 남)
        if (PhotonNetwork.IsConnected)
        {
            if (PhotonNetwork.IsMasterClient)
                ApplyHealAsMaster(amount);
            else
                photonView.RPC(nameof(RpcRequestHeal), RpcTarget.MasterClient, amount);

            return;
        }

        ApplyHealAsMaster(amount);
    }
    [PunRPC]
    void RpcRequestHeal(float amount)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        ApplyHealAsMaster(amount);
    }

    void ApplyHealAsMaster(float amount)
    {
        currHp = Mathf.Clamp(currHp + amount, 0, initHp);
        photonView.RPC(nameof(RpcSyncHp), RpcTarget.All, currHp);
    }
    [PunRPC]
    void RpcSyncHp(float newHp)
    {
        currHp = newHp;
        ApplyHpUI();

        // HP가 다시 생기면(부활) 원격에서도 보이게 복구
        //부활 조건 완화 => 체력 UI가 꺼져있는 오브젝트도 부활 조건에 포함
        if (currHp > 0f && (isDead || (hudCanvas != null && !hudCanvas.enabled)))
        {
            isDead = false;
            if (hudCanvas != null) hudCanvas.enabled = true;
            SetTankVisible(true);

            SetDeadState(false);
        }
    }

    [PunRPC]
    void RpcDeathVisual()
    {
        if (hudCanvas != null) hudCanvas.enabled = false;
        SetTankVisible(false);

        // 추가
        isDead = true;
        SetDeadState(true);

        // 죽음 연출은 모든 클라이언트 동일하게
        if (expEffect != null)
        {
            GameObject effect = Instantiate(expEffect, transform.position, Quaternion.identity);
            Destroy(effect, 3.0f);
        }

        if (hudCanvas != null) hudCanvas.enabled = false;
        SetTankVisible(false);
    }
    //체력바 업데이트 해주는 함수
    void ApplyHpUI()
    {
        if (hpBar == null) return;

        hpBar.fillAmount = (float)currHp / (float)initHp;

        if (hpBar.fillAmount <= 0.4f) hpBar.color = Color.red;
        else if (hpBar.fillAmount <= 0.6f) hpBar.color = Color.yellow;
        else hpBar.color = Color.green;
    }
    //플레이어가 죽으면 매쉬 랜더러 비활성화 하는 함수
    void SetTankVisible(bool isVisible)
    {
        if (renderers == null) return;
        foreach (var r in renderers) r.enabled = isVisible;
    }

    // PVE에서만 킬 집계한다고 했으니, GameMgr에서 모드값을 꺼내 쓰면 됨.
    bool IsPveKillCounting()
    {
        if (!PhotonNetwork.InRoom) return false;

        GameMgr gm = FindObjectOfType<GameMgr>();
        if (gm == null) return false;

        return !gm.isPvpMode; // PVE일 때만 true
    }
}
