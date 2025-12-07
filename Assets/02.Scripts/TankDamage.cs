using System.Collections;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;
using UnityEngine.UI;

public class TankDamage : MonoBehaviour
{
    public Canvas hudCanvas; //Canvas 객체
    public Image hpBar; // 체력바 이미지
    //탱크 폭파 후 투명 처리를 위한 MeshRenderer 컴포넌트 배열
    MeshRenderer[] renderers;
    GameObject expEffect = null; //탱크 폭발 효과
    public int initHp = 100; //탱크 초기 생명치
    int currHp = 0; // 현재 체력
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        renderers = GetComponentsInChildren<MeshRenderer>();
        currHp = initHp;
        //탱크 폭발 시 생성시킬 폭발효과 로드
        expEffect = Resources.Load<GameObject>("Exploson10");
        hpBar.color = Color.green; // Filled 이미지 색상을 녹색으로..
    }
    // EnemyBullet에서 직접 호출할 수 있도록 공개 함수로 분리
    public void TakeDamage(int amount)
    {
        if (currHp <= 0) return;

        currHp -= amount;

        hpBar.fillAmount = (float)currHp / (float)initHp;

        if (hpBar.fillAmount <= 0.4f)
            hpBar.color = Color.red;
        else if (hpBar.fillAmount <= 0.6f)
            hpBar.color = Color.yellow;

        if (currHp <= 0)
        {
            StartCoroutine(ExplosionTank());
        }
    }
    //pvp용 포탄 데미지 처리
    private void OnTriggerEnter(Collider other)
    {
        if (currHp > 0 && other.tag == "CANNON")
        {
            currHp -= 20;
            //현재 생명치 백분율 계산
            hpBar.fillAmount = (float)currHp / (float)initHp;
            //40%이하는 빨간색, 60% 이하는 노란색
            if(hpBar.fillAmount <= 0.4f) 
                hpBar.color = Color.red;
            else if(hpBar.fillAmount <=0.6f)
                hpBar.color = Color.yellow;

            if (currHp <= 0)
            {
                StartCoroutine(ExplosionTank());
            }
        }
    }
    IEnumerator ExplosionTank()
    {
        //폭발효과 생성
        GameObject effect = GameObject.Instantiate(expEffect,
            transform.position, Quaternion.identity);

        Destroy(effect, 3.0f);//3초뒤에 파괴

        hudCanvas.enabled = false;//HUD캔버스 안보이게
        SetTankVisible(false); //탱크 안보이게
        yield return new WaitForSeconds(3.0f);
        //3초뒤 탱크의 체력을 회복하고 체력바 갱신

        hpBar.fillAmount = 1.0f;
        hpBar.color = Color.green;
        hudCanvas.enabled = true;//HUD캔버스 다시 보이게

        currHp = initHp;
        SetTankVisible(true);

    }
    void SetTankVisible(bool isVisible)
    {
        //메쉬 렌더러를 활성/비활성화 하는 함수
        foreach (MeshRenderer _renderer in renderers)
        {
            _renderer.enabled = isVisible;
        }
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
