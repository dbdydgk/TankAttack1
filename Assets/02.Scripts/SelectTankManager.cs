using UnityEngine;
using UnityEngine.UI;

public class SelectTankManager : MonoBehaviour
{
    [Header("탱크 프리뷰 관련")]
    public Transform previewSpawnPoint;   // 로비에 탱크가 서 있을 위치
    public GameObject[] tankPrefabs;      // PlayerTank, PlayerTank2 등 (프리팹 배열)

    [Header("UI 패널")]
    public GameObject selectPanel;        // 탱크 선택 패널 (처음에는 비활성화)


    [Header("UI 텍스트")]
    public Text tankNameTxt;
    public Text tankSpecTxt;

    GameObject currentPreview;
    int currentIndex;

    void Start()
    {
        // 저장된 값이 있으면 불러오고, 없으면 0
        currentIndex = PlayerPrefs.GetInt("SelectedTankIndex", 0);
        PlayerInfo.SelectedTankIndex = currentIndex;

        SpawnPreviewTank();
        selectPanel.SetActive(false);
    }

    // ====== UI 버튼에서 호출할 함수들 ======

    // Select Tank 버튼에 연결
    public void OpenSelectPanel()
    {
        selectPanel.SetActive(true);
    }

    // 패널 닫기(취소 버튼 등)
    public void CloseSelectPanel()
    {
        selectPanel.SetActive(false);
    }

    // 다음 탱크 버튼 →
    public void NextTank()
    {
        currentIndex++;
        if (currentIndex >= tankPrefabs.Length)
            currentIndex = 0;

        SpawnPreviewTank();
    }

    // 이전 탱크 버튼 ←
    public void PrevTank()
    {
        currentIndex--;
        if (currentIndex < 0)
            currentIndex = tankPrefabs.Length - 1;

        SpawnPreviewTank();
    }

    // 선택 확정 버튼 OK
    public void ConfirmSelect()
    {
        PlayerInfo.SelectedTankIndex = currentIndex;
        PlayerPrefs.SetInt("SelectedTankIndex", currentIndex);
        PlayerPrefs.Save();

        selectPanel.SetActive(false);
    }

    // ====== 내부에서만 사용하는 함수 ======

    void SpawnPreviewTank()
    {
        // 기존 프리뷰 삭제
        if (currentPreview != null)
            Destroy(currentPreview);

        // 새 탱크 생성
        GameObject prefab = tankPrefabs[currentIndex];
        currentPreview = Instantiate(prefab,
                                     previewSpawnPoint.position,
                                     previewSpawnPoint.rotation);

        // 깔끔하게 정리하고 싶으면 부모로 붙이기
        currentPreview.transform.SetParent(previewSpawnPoint, true);

        UpdateTankInfoUI();
    }
    void UpdateTankInfoUI()
    {
        if (currentPreview == null) return;

        // 1) 탱크 이름 (원하면 프리팹 이름을 그대로 사용하거나 직접 배열로 관리)
        string tankName = currentPreview.name.Replace("(Clone)", "");
        if (tankNameTxt != null)
            tankNameTxt.text = tankName;

        // 2) 각 컴포넌트에서 스탯 가져오기
        int hp = 0;
        float damage = 0f;
        float moveSpeed = 0f;
        float fireInterval = 0f;

        TankDamage td = currentPreview.GetComponent<TankDamage>();
        if (td != null) hp = td.initHp;

        FireCannon fc = currentPreview.GetComponent<FireCannon>();
        if (fc != null)
        {
            damage = fc.cannonDamage;      // 아까 추가해둔 포탄 데미지 변수
            fireInterval = fc.fireInterval;
        }

        TankMove tm = currentPreview.GetComponent<TankMove>();
        if (tm != null) moveSpeed = tm.moveSpeed;

        // 3) 보기 좋게 문자열로 구성
        if (tankSpecTxt != null)
        {
            string fireRateStr = fireInterval > 0f
                ? (1f / fireInterval).ToString("0.0")   // 초당 발사 수
                : "-";

            tankSpecTxt.text =
                $"HP : {hp}\n" +
                $"Damage : {damage}\n" +
                $"Move Speed : {moveSpeed}\n" +
                $"Fire Rate : {fireRateStr} shots/sec";
        }
    }
}
