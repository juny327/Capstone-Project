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
        // 2026-11-25 사용자: "'~ 짝' 글 없이" → "노란 테두리가 카드 모양과 맞지 않는다 — 카드 모양 그대로 + 작은 별":
        // 카드 그림(가장 큰 그림 — 깎인 모서리 · 위 탭)을 따라 뒤에서 금빛이 번지고, 스킬 아이콘 오른쪽 위에 작은 별
        PairHighlight.Mark((RectTransform)transform, !string.IsNullOrEmpty(label), null, icon != null ? icon.rectTransform : null);
    }

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
