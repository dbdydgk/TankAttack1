using Photon.Pun;
using UnityEngine;

public class ItemSpawner : MonoBehaviourPun
{
    [Header("Resources 프리팹 이름 (Assets/Resources/RepairItem.prefab)")]
    public string prefabName = "RepairItem";

    [Header("스폰 옵션")]
    public int maxAliveCount = 3;
    public float spawnInterval = 10f;

    [Header("테스트")]
    public bool spawnOneOnStart = true;   // 에디터에서 바로 확인용

    [Header("랜덤 범위(BoxCollider)")]
    public Collider spawnArea;
    public LayerMask groundMask;
    public float raycastHeight = 50f;
    public float groundOffset = 0.2f;

    float timer;

    void Start()
    {
        timer = spawnInterval;

        // 에디터에서 시작하자마자 1개 찍어보기
        if (spawnOneOnStart && PhotonNetwork.InRoom)
        {
            TrySpawnRequest();
        }
    }

    void Update()
    {
        // "무조건 네트워크 생성"이 목적이면, 방 밖에서는 아무것도 안 함
        if (!PhotonNetwork.InRoom) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = spawnInterval;

        TrySpawnRequest();
    }

    void TrySpawnRequest()
    {
        // 현재 맵에 살아있는 RepairItem 개수 체크
        int alive = FindObjectsOfType<RepairItem>(true).Length;
        if (alive >= maxAliveCount) return;

        if (!TryGetRandomGroundPoint(out Vector3 spawnPos))
        {
            Debug.LogWarning("[ItemSpawner] 바닥 레이캐스트 실패. groundMask/바닥 레이어/콜라이더 확인 필요");
            return;
        }

        // 마스터면 바로 생성, 아니면 마스터에게 요청
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.InstantiateRoomObject(prefabName, spawnPos, Quaternion.identity);
            Debug.Log("[ItemSpawner] (Master) 수리 아이템 생성!");
        }
        else
        {
            photonView.RPC(nameof(RPC_RequestSpawn), RpcTarget.MasterClient, spawnPos.x, spawnPos.y, spawnPos.z);
        }
    }

    [PunRPC]
    void RPC_RequestSpawn(float x, float y, float z)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Vector3 pos = new Vector3(x, y, z);

        int alive = FindObjectsOfType<RepairItem>(true).Length;
        if (alive >= maxAliveCount) return;

        PhotonNetwork.InstantiateRoomObject(prefabName, pos, Quaternion.identity);
        Debug.Log("[ItemSpawner] (RPC->Master) 수리 아이템 생성!");
    }

    bool TryGetRandomGroundPoint(out Vector3 result)
    {
        result = Vector3.zero;
        if (spawnArea == null) return false;

        Bounds b = spawnArea.bounds;

        // 충분히 큰 값으로 고정 (맵 크기 커져도 안전)
        float startY = b.max.y + 200f;
        float dist = 2000f;

        for (int i = 0; i < 30; i++)
        {
            float x = Random.Range(b.min.x, b.max.x);
            float z = Random.Range(b.min.z, b.max.z);

            Vector3 origin = new Vector3(x, startY, z);
            Debug.DrawRay(origin, Vector3.down * dist, Color.green, 1f);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, dist, groundMask))
            {
                result = hit.point + Vector3.up * groundOffset;
                return true;
            }
        }
        return false;
    }
}
