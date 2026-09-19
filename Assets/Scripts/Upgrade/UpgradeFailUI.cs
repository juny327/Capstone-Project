using UnityEngine;
using TMPro;

public class UpgradeFailUI : MonoBehaviour
{
    public GameObject panel;
    public TextMeshProUGUI text;

    void OnEnable()
    {
        GameEvents.OnUpgradeFailed += Open;
    }

    void OnDisable()
    {
        GameEvents.OnUpgradeFailed -= Open;
    }

    void Open(UpgradeData data)
    {
        panel.SetActive(true);

        // 레벨업 보상이라 EXP 비용이 없다 (13번 U7-A).
        // 이제 실패는 데이터가 비었거나 플레이어를 못 찾은 경우뿐이다.
        text.text = "업그레이드를 적용하지 못했습니다.";
    }

    public void OnClose()
    {
        panel.SetActive(false);
    }
}