using UnityEngine;
using TMPro;

/// <summary>
/// 업그레이드 확인창.
///
/// 확인 버튼을 누르면 카드 창까지 닫히고 게임이 재개된다 (13번 11-4).
/// 예전에는 카드를 고르면 잠깐 뒤 자동으로 닫혀, 확인 버튼을 누를 틈이 없었다.
/// </summary>
public class UpgradeSuccessUI : MonoBehaviour
{
    public GameObject panel;
    public TextMeshProUGUI text;

    void OnEnable()
    {
        GameEvents.OnUpgradeSuccess += Open;
    }

    void OnDisable()
    {
        GameEvents.OnUpgradeSuccess -= Open;
    }

    void Open(UpgradeData data)
    {
        if (panel != null)
            panel.SetActive(true);

        if (text == null || data == null) return;

        // 카드에 표시된 이름과 같은 값을 쓴다 — 서브유닛 카드는 무기 이름이 나온다
        string title = data.effect != null ? data.effect.GetTitle(data) : data.upgradeName;

        text.text = $"{title} 획득!";
    }

    /// <summary>확인 버튼. 씬의 버튼 OnClick 에 연결돼 있다.</summary>
    public void OnClose()
    {
        if (panel != null)
            panel.SetActive(false);

        // 카드 창을 닫고 게임을 재개시킨다
        GameEvents.OnUpgradeConfirmed?.Invoke();
    }
}
