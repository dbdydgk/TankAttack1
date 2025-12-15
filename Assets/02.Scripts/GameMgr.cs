using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

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

    [Header("PVE 패트롤 경로들(씬 오브젝트)")]
    public PatrolRoute[] patrolRoutes;

    public bool isPvpMode = false;  //현재 방의 모드(PVP인지 아닌지)

    int currentWave = 0;

    [Header("PVE Start Button")]
    public Button btnStart; // Canvas에 만든 START 버튼 연결

    const string ROOMPROP_PVE_STARTED = "PVE_STARTED";
    bool pveStarted = false;
    Coroutine waveCo;

    //PVE 모드 결과
    const string ROOMPROP_END_RESULT = "END_RESULT"; // "CLEAR" / "FAIL"
    const string ROOMPROP_END_WAVE = "END_WAVE";
    const string PLAYERPROP_READY = "READY";
    // 로그 최대 줄 수
    const int MAX_LOG_LINES = 12;
    readonly Queue<string> _logLines = new Queue<string>();
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        #if PHOTON_UNITY_NETWORKING
if (PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
{
    var ht = new ExitGames.Client.Photon.Hashtable();
    if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("MAP"))
        ht["MAP"] = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

    if (ht.Count > 0) PhotonNetwork.CurrentRoom.SetCustomProperties(ht);
}
#endif
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

        PhotonNetwork.IsMessageQueueRunning = true;

        if (txtWave != null)
            txtWave.gameObject.SetActive(!isPvpMode);
    }
    IEnumerator Start()
    {
        // 룸 들어갈 때까지 대기 (여기가 핵심)
        yield return new WaitUntil(() => PhotonNetwork.InRoom);

        pv = GetComponent<PhotonView>();
        GetConnectPlayerCount();

        // 이제부터는 룸 안이라 Instantiate/RPC 정상
        CreateTank();

        string msg = "\n<color=#00ff00>[" + PhotonNetwork.NickName + "] Connected</color>";
        pv.RPC("LogMsg", RpcTarget.AllBuffered, msg);

        //if (!isPvpMode && PhotonNetwork.IsMasterClient)
        //    StartCoroutine(WaveRoutine());

        if (!isPvpMode && !GetRoomBool(ROOMPROP_PVE_STARTED))
        {
            SetLocalReady(false);
        }

        RefreshStartButtonUI();

        // 만약 “이미 시작된 방”에 늦게 들어온 경우: 마스터만 웨이브 코루틴 켜주기
        if (!isPvpMode && PhotonNetwork.IsMasterClient && GetRoomBool(ROOMPROP_PVE_STARTED))
        {
            if (waveCo == null)
                waveCo = StartCoroutine(WaveRoutine());
        }
    }
    // START 버튼에 연결할 함수
    public void OnClickStartPve()
    {
        if (isPvpMode) return;

        // 이미 시작된 방이면 아무것도 안함
        if (GetRoomBool(ROOMPROP_PVE_STARTED)) return;

        // ===== 클라이언트: READY 토글 =====
        if (!PhotonNetwork.IsMasterClient)
        {
            bool next = !GetPlayerReady(PhotonNetwork.LocalPlayer);
            SetLocalReady(next);

            if (pv != null)
            {
                pv.RPC("LogMsg", RpcTarget.All,
                    $"[READY] {PlayerName(PhotonNetwork.LocalPlayer)} : {(next ? "READY" : "NOT READY")}");
            }

            RefreshStartButtonUI();
            return;
        }

        // ===== 마스터: 모두 READY면 START =====
        if (!AreAllNonMasterReady(out var notReady))
        {
            if (pv != null)
            {
                pv.RPC("LogMsg", RpcTarget.All,
                    $"[PVE] 시작 불가 - READY 안한 사람: {BuildNameList(notReady)}");
            }
            RefreshStartButtonUI();
            return;
        }

        if (pv != null)
        {
            pv.RPC("LogMsg", RpcTarget.All,
                $"[PVE] 모두 READY 확인. {Mathf.CeilToInt(timeBeforeFirstWave)}초 후 시작합니다.");
        }

        SetRoomBool(ROOMPROP_PVE_STARTED, true);
        pv.RPC(nameof(RpcPveStarted), RpcTarget.All);

        if (waveCo == null)
            waveCo = StartCoroutine(WaveRoutine());
    }
    [PunRPC]
    void RpcPveStarted()
    {
        pveStarted = true;
        if (btnStart != null)
            btnStart.gameObject.SetActive(false);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        // 방장이 바뀌면 START 버튼 interactable 갱신
        RefreshStartButtonUI();

        // 시작된 방이면 새 방장이 웨이브 코루틴을 이어서 담당
        if (!isPvpMode && PhotonNetwork.IsMasterClient && GetRoomBool(ROOMPROP_PVE_STARTED))
        {
            if (waveCo == null)
                waveCo = StartCoroutine(WaveRoutine());
        }
    }
    bool GetRoomBool(string key)
    {
        if (PhotonNetwork.CurrentRoom == null) return false;
        if (PhotonNetwork.CurrentRoom.CustomProperties == null) return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object v) && v is bool b)
            return b;

        return false;
    }
    void SetRoomBool(string key, bool value)
    {
        if (PhotonNetwork.CurrentRoom == null) return;
        var ht = new Hashtable { { key, value } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(ht);
    }
    void RefreshStartButtonUI()
    {
        if (btnStart == null) return;

        // PVP면 숨김
        if (isPvpMode)
        {
            btnStart.gameObject.SetActive(false);

            if (txtWave != null)
                txtWave.gameObject.SetActive(false);

            return;
        }

        pveStarted = GetRoomBool(ROOMPROP_PVE_STARTED);

        // 이미 시작됨 -> 숨김
        if (pveStarted)
        {
            btnStart.gameObject.SetActive(false);
            if (txtWave != null && currentWave == 0)
                txtWave.text = $"Wave {currentWave}/{maxWave}";
            return;
        }

        btnStart.gameObject.SetActive(true);

        // 버튼 라벨
        var label = btnStart.GetComponentInChildren<UnityEngine.UI.Text>();

        if (PhotonNetwork.IsMasterClient)
        {
            if (label != null) label.text = "START";

            bool allReady = AreAllNonMasterReady(out var notReady);
            btnStart.interactable = allReady;

            if (txtWave != null)
                txtWave.text = allReady ? "All READY. (Host press START)" : "Waiting... (Players press READY)";
        }
        else
        {
            bool ready = GetPlayerReady(PhotonNetwork.LocalPlayer);
            if (label != null) label.text = ready ? "UNREADY" : "READY";

            btnStart.interactable = true;

            if (txtWave != null)
                txtWave.text = "Waiting... (Press READY)";
        }
    }
    // =========================
    //  PVE: 웨이브 & 적 스폰
    // =========================
    void StartNextWave()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (currentWave >= maxWave) return;   // 추가: 여기 중요

        currentWave++;

        if (pv != null)
            pv.RPC(nameof(RpcSetWave), RpcTarget.AllBuffered, currentWave, maxWave);

        SpawnWave(currentWave);
    }
    System.Collections.IEnumerator WaveRoutine()
    {
        if (!PhotonNetwork.IsMasterClient) yield break;

        // (A) 첫 웨이브 시작 전(아직 0웨이브)일 때만 카운트다운
        if (currentWave == 0 && !AreEnemiesAlive())
        {
            int t = Mathf.CeilToInt(timeBeforeFirstWave);
            while (t > 0)
            {
                if (txtWave != null)
                    txtWave.text = $"[PVE] Starting in {t}...";

                yield return new WaitForSeconds(1f);
                t--;
            }

            StartNextWave(); // 여기서만 0->1 증가
        }

        // (B) 이후 루프: 현재 웨이브가 끝날 때까지 기다렸다가 다음 웨이브 시작
        while (true)
        {
            // 적이 살아있는 동안 계속 체크
            while (AreEnemiesAlive())
            {
                if (!AreAnyPlayersAlive())
                {
                    EndGameToEnding(false);
                    yield break;
                }

                // 도중에 마스터 권한을 잃었으면 즉시 종료(중복 진행 방지)
                if (!PhotonNetwork.IsMasterClient) yield break;

                yield return null;
            }

            // 적이 더 이상 없다 = 웨이브 종료 상태
            if (currentWave >= maxWave)
                break;

            // 웨이브 간 대기
            yield return new WaitForSeconds(timeBetweenWaves);

            // 대기 중 마스터가 바뀌었으면 종료
            if (!PhotonNetwork.IsMasterClient) yield break;

            StartNextWave();
        }

        // 여기까지 왔으면 maxWave까지 끝난 상태
        EndGameToEnding(AreAnyPlayersAlive());
    }

    void UpdateWaveUI()
    {
        if (isPvpMode) return;

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

            // 추가: 바닥으로 스냅
            TrySnapToGround(ref spawnPos);
        }
        else
        {
            // 스폰 포인트를 안 넣어줬을 경우 임시 랜덤 위치
            float pos = Random.Range(-100.0f, 100.0f);
            spawnPos = new Vector3(pos, 20.0f, pos);

            // 추가: 바닥으로 스냅
            TrySnapToGround(ref spawnPos);
        }

        int routeIndex = PickRouteIndex(); // 0 ~ patrolRoutes.Length-1, 없으면 -1
        object[] instData = new object[] { routeIndex };

        PhotonNetwork.Instantiate(enemyName, spawnPos, Quaternion.identity, 0, instData);
    }
    int PickRouteIndex()
    {
        if (patrolRoutes == null || patrolRoutes.Length == 0) return -1;
        return Random.Range(0, patrolRoutes.Length);
    }

    bool AreEnemiesAlive()
    {
        var enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        return enemies != null && enemies.Length > 0;
    }
    [PunRPC]
    void RpcSetWave(int wave, int max)
    {
        if (isPvpMode) return;

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
        if (txtLogmsg == null || string.IsNullOrEmpty(msg)) return;

        // 메시지 정규화 (앞/뒤 줄바꿈 정리)
        msg = msg.Replace("\r", "").Trim('\n');

        // 여러 줄 들어오면 줄 단위로 처리
        var lines = msg.Split('\n');
        foreach (var line in lines)
        {
            var s = line.TrimEnd();
            if (string.IsNullOrWhiteSpace(s)) continue;

            _logLines.Enqueue(s);
            while (_logLines.Count > MAX_LOG_LINES)
                _logLines.Dequeue();
        }

        txtLogmsg.text = string.Join("\n", _logLines);
    }
    void GetConnectPlayerCount() //룸 접속자 수 표시 함수
    {
        Room currRoom = PhotonNetwork.CurrentRoom;

        if (currRoom == null || txtConnect == null) return;

        int displayMax = isPvpMode ? currRoom.MaxPlayers : 4;   // PVE는 4로 고정 표시

        txtConnect.text = $"{currRoom.PlayerCount}/{displayMax}";
    }
    public override void OnPlayerEnteredRoom(Player newPlayer) //새로운 플레이어가 룸에 접속했을 때
    {
        GetConnectPlayerCount();
        RefreshStartButtonUI();
    }
    public override void OnPlayerLeftRoom(Player otherPlayer) //플레이어가 룸에서 나갔을 때
    {
        GetConnectPlayerCount();
        RefreshStartButtonUI();
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
        PhotonNetwork.DestroyPlayerObjects(PhotonNetwork.LocalPlayer);

        Vector3 spawnPos;
        Quaternion spawnRot = Quaternion.identity;

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

            Transform sp = playerSpawnPoints[idx];
            spawnPos = sp.position;
            // 스폰 포인트 회전을 그대로 쓰기
            spawnRot = sp.rotation;

            Debug.Log($"[CreateTank] PVE SpawnPoint idx={idx}, " +
                $"name={playerSpawnPoints[idx].name}, pos={spawnPos}");
        }

        int tankIndex = Mathf.Clamp(PlayerInfo.SelectedTankIndex, 0, tanks.Length - 1);
        var go = PhotonNetwork.Instantiate(tanks[tankIndex], spawnPos, spawnRot, 0);

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
    
    bool TrySnapToGround(ref Vector3 pos)
    {
        // 위에서 아래로 쏴서 지면을 찾는다
        Vector3 origin = pos + Vector3.up * raycastHeight;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
            raycastHeight * 2f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            pos.y = hit.point.y + groundOffsetY; // 바닥에 살짝 띄우기
            return true;
        }
        return false;
    }
    //PVE 시작 상태(Room Property) 변경을 모든 클라가 즉시 반영하도록
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged == null) return;

        if (propertiesThatChanged.ContainsKey(ROOMPROP_PVE_STARTED))
        {
            RefreshStartButtonUI();

            // 시작된 방인데 방장이면 웨이브 담당
            if (!isPvpMode && PhotonNetwork.IsMasterClient && GetRoomBool(ROOMPROP_PVE_STARTED))
            {
                if (waveCo == null)
                    waveCo = StartCoroutine(WaveRoutine());
            }
        }
    }
    bool AreAnyPlayersAlive()
    {
        var players = GameObject.FindGameObjectsWithTag("Player");
        if (players == null || players.Length == 0) return false;

        foreach (var go in players)
        {
            var td = go.GetComponentInParent<TankDamage>();
            if (td != null && td.IsAlive) return true;
        }
        return false;
    }
    //LoadLevel이 빨라 프로퍼티가 저장 안됨 => 될 때까지 기다렸다가 씬 로딩
    void EndGameToEnding(bool isClear)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (PhotonNetwork.CurrentRoom == null) return;

        var ht = new Hashtable
    {
        { ROOMPROP_END_RESULT, isClear ? "CLEAR" : "FAIL" },
        { ROOMPROP_END_WAVE, currentWave }
    };
        PhotonNetwork.CurrentRoom.SetCustomProperties(ht);

        StartCoroutine(CoLoadEndingAfterShortDelay());
    }

    IEnumerator CoLoadEndingAfterShortDelay()
    {
        yield return new WaitForSeconds(0.2f);
        PhotonNetwork.LoadLevel("Ending");
    }

    bool GetPlayerReady(Player p)
    {
        if (p == null || p.CustomProperties == null) return false;
        return p.CustomProperties.TryGetValue(PLAYERPROP_READY, out object v) && v is bool b && b;
    }

    void SetLocalReady(bool ready)
    {
        var ht = new ExitGames.Client.Photon.Hashtable { { PLAYERPROP_READY, ready } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(ht);
    }

    string PlayerName(Player p)
    {
        if (p == null) return "Unknown";
        return string.IsNullOrEmpty(p.NickName) ? $"Player{p.ActorNumber}" : p.NickName;
    }

    bool AreAllNonMasterReady(out List<Player> notReady)
    {
        notReady = new List<Player>();
        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (p == PhotonNetwork.MasterClient) continue;   // 방장은 제외
            if (!GetPlayerReady(p)) notReady.Add(p);
        }
        return notReady.Count == 0;
    }

    string BuildNameList(List<Player> list)
    {
        if (list == null || list.Count == 0) return "";
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < list.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(PlayerName(list[i]));
        }
        return sb.ToString();
    }
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (changedProps != null && changedProps.ContainsKey(PLAYERPROP_READY))
        {
            RefreshStartButtonUI();
        }
    }

}
