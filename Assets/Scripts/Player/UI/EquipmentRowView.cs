using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 장비 한 줄 — [부위 · 키] [그림] 이름 (+ 상세: 효과 한 줄). 장비 HUD 와 정비 창 내 장비 판이 같이 쓴다.
/// 부착물 · 서브 능력 · 빈 칸 세 가지로 보여 준다 (커스터마이징-구현계획.md 8장).
/// </summary>
public class EquipmentRowView : MonoBehaviour
{
    [SerializeField] TMP_Text label;        // 부위 이름 또는 키 글자
    [SerializeField] Image frame;           // 등급 색 테두리
    [SerializeField] Image icon;
    [SerializeField] TMP_Text title;
    [SerializeField] TMP_Text detail;       // Z 상세 — 효과 한 줄
    [SerializeField] Image gauge;           // 방패 내구도 (Filled)
    [SerializeField] LayoutElement layout;
    [SerializeField] Image flash;           // 얻었을 때 잠깐 반짝

    [SerializeField] float compactHeight = 40f;
    [SerializeField] float detailHeight = 62f;
    [SerializeField] Color emptyColor = new Color(1f, 1f, 1f, 0.28f);
    [SerializeField] Color abilityColor = new Color(1f, 0.62f, 0.30f);

    float flashLeft;
    SubAbility ability;

    public void ShowAttachment(AttachmentSlot slot, AttachmentData attachment, bool showDetail)
    {
        ability = null;
        gameObject.SetActive(true);

        if (label != null) label.text = AttachmentData.SlotName(slot);

        if (attachment == null)
        {
            SetEmpty("─");
            SetDetail(null, false);
            return;
        }

        SetPicture(attachment.icon, AttachmentData.TierColor(attachment.tier));
        if (title != null) { title.text = attachment.displayName; title.color = Color.white; }
        SetDetail(attachment.EffectText, showDetail);
        SetGauge(-1f);
    }

    public void ShowAbility(string key, SubAbility value, bool showDetail)
    {
        ability = value;
        gameObject.SetActive(true);

        if (label != null) label.text = key;

        if (value == null || value.Data == null)
        {
            SetEmpty("─  정비에서 얻는다");
            SetDetail(null, false);
            SetGauge(-1f);
            return;
        }

        SetPicture(value.Data.icon, abilityColor);
        if (title != null) { title.text = $"{value.Data.displayName}  Lv{value.Level}"; title.color = Color.white; }
        SetDetail(value.Data.description, showDetail);
        SetGauge(value.Gauge);
    }

    public void Hide() => gameObject.SetActive(false);

    /// <summary>얻었을 때 잠깐 반짝인다.</summary>
    public void Pulse(float seconds = 0.6f) => flashLeft = seconds;

    void SetEmpty(string text)
    {
        if (frame != null) frame.color = emptyColor;
        if (icon != null) { icon.sprite = null; icon.enabled = false; }
        if (title != null) { title.text = text; title.color = emptyColor; }
    }

    void SetPicture(Sprite sprite, Color border)
    {
        if (frame != null) frame.color = border;
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }
    }

    void SetDetail(string text, bool show)
    {
        bool on = show && !string.IsNullOrEmpty(text);
        if (detail != null)
        {
            detail.gameObject.SetActive(on);
            if (on) detail.text = text;
        }

        if (layout != null) layout.preferredHeight = on ? detailHeight : compactHeight;
    }

    void SetGauge(float value)
    {
        if (gauge == null) return;

        // 바탕(GaugeBg)째 켜고 끈다 — 게이지가 없는 줄에 빈 바가 남지 않게
        GameObject bar = gauge.transform.parent != null && gauge.transform.parent != transform ? gauge.transform.parent.gameObject : gauge.gameObject;
        bar.SetActive(value >= 0f);
        gauge.gameObject.SetActive(true);
        if (value >= 0f) gauge.fillAmount = value;
    }

    void Update()
    {
        // 방패 내구도는 계속 변한다
        if (ability != null && gauge != null && ability.Gauge >= 0f) gauge.fillAmount = ability.Gauge;

        if (flash == null) return;
        if (flashLeft > 0f)
        {
            flashLeft -= Time.unscaledDeltaTime;
            flash.enabled = true;
            flash.color = new Color(0.6f, 1f, 0.9f, Mathf.Clamp01(flashLeft / 0.6f) * 0.22f);
        }
        else if (flash.enabled)
        {
            flash.enabled = false;
        }
    }
}
