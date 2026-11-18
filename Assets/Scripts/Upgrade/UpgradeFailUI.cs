
using System.Collections;
using UnityEngine;
using TMPro;

public class UpgradeFailUI : MonoBehaviour
{
    [Header("UI")]
    public GameObject panel;
    public TextMeshProUGUI text;

    [Header("Message Settings")]
    [Min(0.1f)]
    [SerializeField] private float displayDuration = 1.5f;

    private Coroutine hideCoroutine;

    private void OnEnable()
    {
        GameEvents.OnUpgradeFailed += Open;
    }

    private void OnDisable()
    {
        GameEvents.OnUpgradeFailed -= Open;

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
    }

    private void Open(UpgradeData data)
    {
        if (panel == null)
            return;

        // Inspector에 입력된 TMP 문구를 그대로 사용
        panel.SetActive(true);

        // 연속으로 실패하면 표시 시간 초기화
        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        hideCoroutine = StartCoroutine(AutoHide());
    }

    private IEnumerator AutoHide()
    {
        // 게임이 일시정지 상태여도 1.5초 후 사라짐
        yield return new WaitForSecondsRealtime(displayDuration);

        if (panel != null)
            panel.SetActive(false);

        hideCoroutine = null;
    }

    // 기존 버튼 이벤트가 연결되어 있어도 오류가 나지 않도록 유지
    public void OnClose()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        if (panel != null)
            panel.SetActive(false);
    }
}
