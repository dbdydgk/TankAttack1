using System.Collections;
using UnityEngine;

public class Cannon : MonoBehaviour
{
    public GameObject expEffect; //폭발 효과 프리팹
    private CapsuleCollider _collider;
    private Rigidbody _ridbody;
    public int damage = 20; //포탄의 데미지
    // Start is called once before the first execution of Update after the MonoBehaviour is created
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
        //대상이 누구든 부딪히면 파괴
        StartCoroutine(this.ExplosionCannon(0.0f));
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
}
