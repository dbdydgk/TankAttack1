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
            float pos = Random.Range(-100.0f, 100.0f);
            spawnPos = new Vector3(pos, 20.0f, pos);
        }
        else
        {
            // PVE 모드: 플레이어 스폰 포인트에서 스폰
            if (playerSpawnPoints != null && playerSpawnPoints.Length > 0)
            {
                // 플레이어마다 고정된 위치를 주고 싶으면 ActorNumber 기반으로 인덱스 결정
                int index = 0;
                if (PhotonNetwork.LocalPlayer != null)
                {
                    index = (PhotonNetwork.LocalPlayer.ActorNumber - 1) % playerSpawnPoints.Length;
                }

                spawnPos = playerSpawnPoints[index].position;
            }
            else
            {
                // 스폰 포인트를 아직 안 넣었다면, 임시로 랜덤 스폰 (디버그용)
                float pos = Random.Range(-100.0f, 100.0f);
                spawnPos = new Vector3(pos, 20.0f, pos);
                Debug.LogWarning("[PVE] playerSpawnPoints가 비어 있어 임시 랜덤 위치에서 플레이어를 스폰합니다.");
            }
        }

        PhotonNetwork.Instantiate(
            tanks[PlayerInfo.SelectedTankIndex],
            spawnPos,
            Quaternion.identity,
            0);
    }
}
