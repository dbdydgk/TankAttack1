using UnityEngine;

public enum EnemyRole
{
    Basic,      //기본 전차
    Heavy,      //중전차
    Artillery   //자주곡사포
}

public enum FireMode
{
    Direct,     //직선 사격
    Parabolic   //포물선 사격(자주곡사포용)
}
[CreateAssetMenu(fileName = "EnemyData", menuName = "TankGame/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("기본 정보")]
    public EnemyRole role = EnemyRole.Basic;
    public string enemyName;

    [Header("능력치")]
    public float moveSpeed = 3f;        //이동속도
    public float rotationSpeed = 120f;  //회전 속도
    public float fireRate = 5f;         //초당 발사속도
    public int damage = 10;
    public int maxHP = 100;
    public float detectionRange = 80f;  // 플레이어 인식 거리
    public GameObject bulletPrefab;     // 발사할 탄환 프리팹

    //곡사포용 데이터 일정거리를 유지하도록 100~108
    public float preferredMinDistance = 100f;
    public float preferredMaxDistance = 108f;
}
