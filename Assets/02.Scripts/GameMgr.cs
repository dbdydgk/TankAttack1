using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

public class GameMgr : MonoBehaviourPunCallbacks
{
    public Text txtConnect; // 접속 인원 수 표시
    public Text txtLogmsg; // 접속자 로그 표시
    public Text txtWave;    //웨이브 표시
    PhotonView pv;

    [Header("플레이어 탱크 프리팹 이름")]
    public string[] tanks = { "Tank", "HeavyTank" };

    [Header("PVE 전용 플레이어 스폰 위치들")]
    public Transform[] playerSpawnPoints;

    [Header("적 탱크 프리팹 이름(PVE)")]
    public string[] enemyTanks = {"EnemyTank_Basic", "EnemyTank_Heavy"};

    [Header("적 스폰 위치들(PVE)")]
    public Transform[] enemySpawnPoints;

    [Header("웨이브 설정")]
    public int maxWave = 5;
    public float timeBeforeFirstWave = 3f;   // 첫 웨이브 시작까지 대기 시간
    public float timeBetweenWaves = 5f;      // 웨이브 간 대기 시간

    public int baseEnemyCount = 2;           // Wave 1, 플레이어 1명 기준 적 수
    public int enemyIncreasePerWave = 1;     // 웨이브마다 플레이어 1명 기준으로 이만큼씩 증가

    [Header("적 태그(PVE 적 생존 여부 체크용")]
    public string enemyTag = "Enemy";

    [Header("PVP 랜덤 스폰 영역(BoxCollider Trigger")]
    public BoxCollider[] pvpSpawnAreas;

    [Header("PVP 스폰 검사 레이어")]
    public LayerMask groundLayer;    //도로/바닥 MeshCollider 레이어
    public LayerMask blockLayer;    //건물/병/장애물 레이어

    [Header("PVP 스폰 검사 옵션")]
    public float spawnCheckRadius = 2.5f;        // 탱크 크기에 맞게
    public float minDistanceFromPlayers = 8f;    // 겹스폰 방지 거리
    public int spawnTryCount = 30;               // 랜덤 재시도 횟수
    public float raycastHeight = 200f;           // 위에서 아래로 Raycast 시작 높이
    public float groundOffsetY = 0.5f;           // 바닥에 살짝 띄우기

    public bool isPvpMode = false;  //현재 방의 모드(PVP인지 아닌지)

    int currentWave = 0;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        // 방 모드 읽기
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("MODE"))
        {
            object modeObj = PhotonNetwork.CurrentRoom.CustomProperties["MODE"];
            if (modeObj is string modeStr)
            {
                isPvpMode = (modeStr == "PVP");
            }

        }

        CreateTank(); //탱크 생성
        PhotonNetwork.IsMessageQueueRunning = true;
        pv = GetComponent<PhotonView>();
        GetConnectPlayerCount();
    }
    private void Start()
    {
        string msg = "\n<color=#00ff00>["
            + PhotonNetwork.NickName+"] Connected</color>";
        pv.RPC("LogMsg", RpcTarget.AllBuffered, msg);

        // PVE 모드 + 마스터 클라이언트만 웨이브 스폰 담당
        if (!isPvpMode && PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(WaveRoutine());
        }
    }
    // =========================
    //  PVE: 웨이브 & 적 스폰
    // =========================
    System.Collections.IEnumerator WaveRoutine()
    {
        // 첫 웨이브 시작 전 대기
        yield return new WaitForSeconds(timeBeforeFirstWave);

        while (currentWave < maxWave)
        {
            currentWave++;
            UpdateWaveUI();

            SpawnWave(currentWave);

            yield return new UnityEngine.WaitUntil(() => !AreEnemiesAlive());

            yield return new WaitForSeconds(timeBetweenWaves);
        }

        // TODO: maxWave 도달 후 클리어 패널, 보스 웨이브 등 추가 가능
    }
    void UpdateWaveUI()
    {
        if (txtWave != null)
        {
            txtWave.text = $"Wave {currentWave}/{maxWave}";
        }
    }
    void SpawnWave(int wave)
    {
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient) return;

        // 현재 방 플레이어 수
        int playerCount = (PhotonNetwork.CurrentRoom != null)
            ? PhotonNetwork.CurrentRoom.PlayerCount
            : 1;

        // 플레이어 1명 기준 적 수: baseEnemyCount + (wave-1)*enemyIncreasePerWave
        int enemyPerPlayer = baseEnemyCount + (wave - 1) * enemyIncreasePerWave;

        // 전체 적 수 = 위 값 × 플레이어 수
        int totalEnemy = enemyPerPlayer * playerCount;

        Debug.Log($"[PVE] Wave {wave} / Players {playerCount} / Spawn Enemies {totalEnemy}");

        for (int i = 0; i < totalEnemy; i++)
        {
            SpawnEnemy();
        }
    }
    void SpawnEnemy()
    {
        if (enemyTanks == null || enemyTanks.Length == 0)
        {
            Debug.LogWarning("[PVE] enemyTanks 배열에 적 탱크 프리팹 이름을 넣어주세요.");
            return;
        }

        // 1) 적 탱크 종류 랜덤 선택
        string enemyName = enemyTanks[Random.Range(0, enemyTanks.Length)];

        // 2) 적 스폰 포인트 중 하나 랜덤 선택
        Vector3 spawnPos;
        if (enemySpawnPoints != null && enemySpawnPoints.Length > 0)
        {
            Transform sp = enemySpawnPoints[Random.Range(0, enemySpawnPoints.Length)];
            spawnPos = sp.position;
        }
        else
        {
            // 스폰 포인트를 안 넣어줬을 경우 임시 랜덤 위치
            float pos = Random.Range(-100.0f, 100.0f);
            spawnPos = new Vector3(pos, 20.0f, pos);
        }

        // 3) 적 탱크 네트워크 생성 (마스터 클라이언트만 호출해야 함)
        PhotonNetwork.Instantiate(enemyName, spawnPos, Quaternion.identity, 0);
    }
    bool AreEnemiesAlive()
    {
        var enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        return enemies != null && enemies.Length > 0;
    }
    [PunRPC]
    void RpcSetWave(int wave, int max)
    {
        currentWave = wave;
        if (txtWave != null)
            txtWave.text = $"Wave {wave}/{max}";
    }
    // =========================
    //  기존 UI / 룸 관련 코드
    // =========================
    [PunRPC]
    void LogMsg(string msg)
    {
        txtLogmsg.text = txtLogmsg.text+msg;
    }
    void GetConnectPlayerCount() //룸 접속자 수 표시 함수
    {
        Room currRoom = PhotonNetwork.CurrentRoom;
        txtConnect.text = currRoom.PlayerCount.ToString()+
            "/"+currRoom.MaxPlayers.ToString();
    }
    public override void OnPlayerEnteredRoom(Player newPlayer) //새로운 플레이어가 룸에 접속했을 때
    {
        GetConnectPlayerCount();
    }
    public override void OnPlayerLeftRoom(Player otherPlayer) //플레이어가 룸에서 나갔을 때
    {
        GetConnectPlayerCount();
    }
    public void OnClickExitRoom()
    {
        string msg = "\n<color=#ff0000>[" + PhotonNetwork.NickName + "] Disconnected</color>";
        pv.RPC("LogMsg", RpcTarget.AllBuffered, msg);
        PhotonNetwork.LeaveRoom(); //룸 나가기
    }
    public override void OnLeftRoom() //룸 나가기가 완료되었을 때
    {
        PhotonNetwork.LoadLevel("scLobby"); //로비 씬으로 이동
    }
    // =========================
    //  플레이어 탱크 생성 (PVE/PVP 분리)
    // =========================
    void CreateTank()
    {
        Vector3 spawnPos;

        if (isPvpMode)
        {
            // PVP 모드: 기존처럼 맵 안 랜덤 스폰
            //float pos = Random.Range(-100.0f, 100.0f);
            //spawnPos = new Vector3(pos, 20.0f, pos);

            // PVP: 랜덤 스폰 (SpawnArea 내부 + 도로 위 + 장애물/겹스폰 방지)
            spawnPos = GetRandomPvpSpawnPos();
        }
        else
        {
            // PVE 모드: 플레이어 스폰 포인트에서 스폰
            // ★ PVE는 스폰포인트 필수로 강제
            if (playerSpawnPoints == null || playerSpawnPoints.Length == 0 || playerSpawnPoints[0] == null)
            {
                Debug.LogError("[CreateTank] PVE인데 PlayerSpawnPoints가 " +
                    "비어있거나 Missing입니다. 랜덤 스폰 금지.");
                return;
            }

            int idx = (PhotonNetwork.LocalPlayer.ActorNumber - 1) 
                % playerSpawnPoints.Length;
            spawnPos = playerSpawnPoints[idx].position;
            Debug.Log($"[CreateTank] PVE SpawnPoint idx={idx}, " +
                $"name={playerSpawnPoints[idx].name}, pos={spawnPos}");
        }

        int tankIndex = Mathf.Clamp(PlayerInfo.SelectedTankIndex, 0, tanks.Length - 1);
        var go = PhotonNetwork.Instantiate(tanks[tankIndex], spawnPos, Quaternion.identity, 0);

        Debug.Log($"[CreateTank] Spawned={go.name}, finalPos={go.transform.position}");
    }
    Vector3 GetRandomPvpSpawnPos()
    {
        // SpawnArea 미설정 시 fallback (디버그용)
        if (pvpSpawnAreas == null || pvpSpawnAreas.Length == 0 || pvpSpawnAreas[0] == null)
        {
            Debug.LogWarning("[PVP Spawn] pvpSpawnAreas 비어있음 → fallback 랜덤");
            float pos = Random.Range(-100f, 100f);
            return new Vector3(pos, 20f, pos);
        }

        for (int t = 0; t < spawnTryCount; t++)
        {
            BoxCollider area = pvpSpawnAreas[Random.Range(0, pvpSpawnAreas.Length)];
            if (area == null) continue;

            Bounds b = area.bounds;

            float x = Random.Range(b.min.x, b.max.x);
            float z = Random.Range(b.min.z, b.max.z);

            // Ground(도로 MeshCollider) 위에 붙이기
            Vector3 rayOrigin = new Vector3(x, b.max.y + raycastHeight, z);
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastHeight * 2f, groundLayer))
                continue;

            Vector3 p = hit.point;
            p.y += groundOffsetY;

            // 장애물/벽 겹침 방지
            if (Physics.CheckSphere(p, spawnCheckRadius, blockLayer))
                continue;

            // 다른 플레이어와 너무 가까우면 스폰 금지
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            bool tooClose = false;
            foreach (GameObject pl in players)
            {
                if (pl == null) continue;
                if (Vector3.Distance(pl.transform.position, p) < minDistanceFromPlayers)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            Debug.Log($"[PVP Spawn] success try={t}, pos={p}");
            return p;
        }

        // 전부 실패 시: 첫 영역 중앙 fallback
        Vector3 c = pvpSpawnAreas[0].bounds.center;
        Vector3 fallback = new Vector3(c.x, c.y + 5f, c.z);
        Debug.LogWarning($"[PVP Spawn] failed all tries → fallback={fallback}");
        return fallback;
    }
}
