
using System.Collections;
using UnityEngine;
using TMPro;

public class UpgradeSuccessUI : MonoBehaviour
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
        GameEvents.OnUpgradeSuccess += Open;
    }

    private void OnDisable()
    {
        GameEvents.OnUpgradeSuccess -= Open;

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

        // Inspector에서 설정한 TMP 문구를 그대로 사용
        panel.SetActive(true);

        // 연속으로 성공하면 표시 시간 초기화
        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        hideCoroutine = StartCoroutine(AutoHide());
    }

    private IEnumerator AutoHide()
    {
        // 게임이 일시정지 상태여도 정상 작동
        yield return new WaitForSecondsRealtime(displayDuration);

        if (panel != null)
            panel.SetActive(false);

        hideCoroutine = null;
    }

    // 기존 버튼 이벤트 호환용
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
