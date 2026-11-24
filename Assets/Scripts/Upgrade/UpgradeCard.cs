using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeCard : MonoBehaviour
{
    [Header("UI")]
    public Image icon;
    public TextMeshProUGUI title;
    public TextMeshProUGUI description;
    public TextMeshProUGUI cost;

    [Header("Visual")]
    public Animator animator; // unscale 모드로 해서 timeScale 0 이여도 재생

    [Header("Sound")]
    public AudioSource audioSource;
    public AudioClip successSound;
    public AudioClip failSound;

    UpgradeData data;
    bool clicked;

    /// <summary>이 카드가 보여 주는 업그레이드 (UpgradeUI 가 산 뒤 다시 그린다).</summary>
    public UpgradeData Data => data;

    public void Setup(UpgradeData upgrade)
    {
        Setup(upgrade, 1);
    }

    public void Setup(UpgradeData upgrade, int nextLevel)
    {
        Setup(upgrade, nextLevel, upgrade != null ? upgrade.CostFor(nextLevel) : 0);
    }

    public void Setup(UpgradeData upgrade, int nextLevel, int costExp)
    {
        data = upgrade;
        clicked = false;

        if (upgrade == null)
            return;

        if (icon != null)
            icon.sprite = upgrade.icon;

        if (title != null)
            title.text = $"{upgrade.upgradeName} Lv.{Mathf.Max(1, nextLevel)}";

        if (description != null)
            description.text = UiTextFit.KeepWords(DescText.Skill(upgrade));   // 간단 / 상세 — '상세 설명' 토글 · 낱말 가운데에서 꺾이지 않게 (2026-11-22)

        if (cost != null)
        {
            cost.gameObject.SetActive(true);
            cost.text = costExp.ToString();
        }
    }

    void OnEnable()
    {
        GameEvents.OnUpgradeSuccess += OnSuccess;
        GameEvents.OnUpgradeFailed += OnFailed;
        DescriptionMode.Changed += OnDescriptionMode;
    }

    void OnDisable()
    {
        GameEvents.OnUpgradeSuccess -= OnSuccess;
        GameEvents.OnUpgradeFailed -= OnFailed;
        DescriptionMode.Changed -= OnDescriptionMode;
        ResetFlash();
    }

    // ───────── 성공 · 실패 번쩍임이 남지 않게 (2026-11-24) ─────────
    // 사용자 제보: "카드를 고를 때 이따금 나오는 초록 창이 사라지지 않고 계속 남는다".
    // 원인: 성공(초록) · 실패(빨강) 애니메이션이 카드 안 'Image' 판의 색과 카드 크기를 0.33초 동안 바꾸는데, 그 도중에 창이 닫히면
    // (사자마자 '닫기') 애니메이터가 다음에 켜질 때 그 순간의 색 · 크기를 '기본값'으로 다시 잡는다 (Write Defaults · 끌 때 상태 안 남김).
    // 그 뒤로는 빈 상태가 초록 · 1.06배를 계속 써서 세 카드가 초록인 채 굳었다 (시험 kept_UpgradeFlashCheck — a 0.094).
    // → 처음 색 · 크기를 기억해 두고, 꺼질 때 되돌린다. 다음에 켜질 때 깨끗한 값이 기본값이 된다.

    Image flash;
    Color flashColor;
    Vector3 baseScale = Vector3.one;
    bool flashCaptured;

    void Awake() => CaptureFlash();

    void CaptureFlash()
    {
        if (flashCaptured || animator == null) return;
        flashCaptured = true;
        Transform t = animator.transform.Find("Image");
        if (t != null && t.TryGetComponent(out flash)) flashColor = flash.color;
        baseScale = animator.transform.localScale;
    }

    void ResetFlash()
    {
        if (animator == null) return;
        CaptureFlash();
        if (flash != null) flash.color = flashColor;
        animator.transform.localScale = baseScale;
    }

    // '상세 설명' 토글이 바뀌면 설명만 다시 (2026-11-22)
    void OnDescriptionMode(bool detail)
    {
        if (data != null && description != null)
            description.text = UiTextFit.KeepWords(DescText.Skill(data));
    }

    /// <summary>
    /// 상위 무기 짝 표시 (2026-11-17 밸런스 결정 6) — 지금 가진 무기의 짝 스킬이면 금색 테두리와 위쪽 꼬리표("★ 혈검 짝").
    /// label 이 null 이면 끈다. 테두리는 카드 아래에 스스로 만든다 (PairHighlight — 프리팹은 그대로).
    /// </summary>
    public void ShowPairMark(string label)
    {
        // 꼬리표는 위쪽 '스킬 강화' 탭 자리 — 탭은 몸통 위 가장자리보다 조금 아래(몸통 높이의 2.6%)가 가운데, 너비 58%
        PairHighlight.Set((RectTransform)transform, label, true, title, 0.58f, CardArtInsets, 0.026f);
    }

    // 카드 그림(Space_Exploration_GUI_Kit/Containers/Small/UpgradeCard.png)의 투명한 여백 — 왼쪽 · 위(탭 아래 몸통) · 오른쪽 · 아래.
    // 그림을 바꾸면 이 값도 다시 잰다 (알파 40 넘는 칸이 줄의 70% 이상인 곳)
    static readonly Vector4 CardArtInsets = new Vector4(0.058f, 0.103f, 0.052f, 0.026f);

    /// <summary>최대 레벨 — 더 살 수 없다 (2026-11-04).</summary>
    public void ShowMaxed()
    {
        clicked = true;

        if (data != null && title != null)
            title.text = $"{data.upgradeName} MAX";

        if (cost != null)
        {
            cost.gameObject.SetActive(true);
            cost.text = "최대 레벨";
        }
    }

    public void OnClick()
    {
        if (data == null || clicked)
            return;

        clicked = true;
        GameEvents.OnUpgradeSelected?.Invoke(data);
    }

    void OnSuccess(UpgradeData d)
    {
        if (d != data)
            return;

        if (animator != null)
            animator.Play("Success");

        if (audioSource && successSound)
            audioSource.PlayOneShot(successSound);
    }

    void OnFailed(UpgradeData d)
    {
        if (d != data)
            return;

        // 경험치 부족 등으로 실패하면 다시 선택할 수 있게 한다.
        clicked = false;

        if (animator != null)
            animator.Play("Fail");

        if (audioSource && failSound)
            audioSource.PlayOneShot(failSound);
    }
}
