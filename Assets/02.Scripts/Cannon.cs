using System.Collections;
using UnityEngine;
#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
#endif

public class Cannon : MonoBehaviour
{
    public GameObject expEffect; //폭발 효과 프리팹
    private CapsuleCollider _collider;
    private Rigidbody _ridbody;
    public float damage = 20f; //포탄의 데미지

    [Header("자기 몸통 충돌 방지")]
    public float ignoreOwnerCollisionTime = 0.25f;

    private Collider[] _ownerCols;
    private bool _exploding = false;

    public int ownerActorNumber = -1;
    void Start()
    {
        _collider = GetComponent<CapsuleCollider>();
        _ridbody = GetComponent<Rigidbody>();
        GetComponent<Rigidbody>().AddForce(transform.forward * 6000.0f);
        //포탄이 발사된 후 3초가 지나면 폭발 이펙트 후 파괴
        StartCoroutine(this.ExplosionCannon(3.0f));
    }
    private void OnTriggerEnter(Collider other)
    {
        //플레이어 스폰 영역, 적의 탐지영역, 아이템 콜리더는 제외
        if (other.CompareTag("SpawnArea")) return;
        if (other.CompareTag("EnemySensor")) return;
        if (other.CompareTag("Item")) return;

        // 안전장치: 혹시 IgnoreCollision 타이밍 전에 들어오면 발사자 콜라이더는 무시
        if (_ownerCols != null)
        {
            for (int i = 0; i < _ownerCols.Length; i++)
            {
                if (_ownerCols[i] == other) return;
            }
        }

        ExplodeNow();
    }
    void ExplodeNow()
    {
        if (_exploding) return;
        _exploding = true;
        StartCoroutine(ExplosionCannon(0.0f));
    }
    IEnumerator ExplosionCannon(float tm)
    {
        yield return new WaitForSeconds(tm);
        _collider.enabled = false; // 더이상 충돌이 안되게 콜라이더 비활성화  
        _ridbody.isKinematic = true;
        //폭발 효과 생성
        GameObject obj = (GameObject)Instantiate(expEffect,transform.position,
            Quaternion.identity);
        Destroy(obj, 1.0f); //폭발효과 파괴
        Destroy(this.gameObject, 1.0f); //포탄 파괴
    }
    // 발사 직후 호출해서 "발사자" 콜라이더를 잠깐 무시
    public void InitOwner(GameObject owner)
    {
        if (owner == null) return;

        Collider myCol = GetComponent<Collider>();
        _ownerCols = owner.GetComponentsInChildren<Collider>(true);

        foreach (var c in _ownerCols)
        {
            if (c == null || c.isTrigger) continue;
            Physics.IgnoreCollision(myCol, c, true);
        }

        // 일정 시간 후 다시 충돌 허용(원하면 이 코루틴 제거해서 영구 무시도 가능)
        StartCoroutine(ReenableOwnerCollision(myCol, _ownerCols, ignoreOwnerCollisionTime));
    }

    IEnumerator ReenableOwnerCollision(Collider myCol, Collider[] ownerCols, float t)
    {
        yield return new WaitForSeconds(t);

        if (myCol == null || ownerCols == null) yield break;

        foreach (var c in ownerCols)
        {
            if (c == null || c.isTrigger) continue;
            Physics.IgnoreCollision(myCol, c, false);
        }
    }

#if PHOTON_UNITY_NETWORKING
    [PunRPC]
    public void RpcInitOwnerActor(int actorNumber)
    {
        ownerActorNumber = actorNumber;
    }
#endif
}
