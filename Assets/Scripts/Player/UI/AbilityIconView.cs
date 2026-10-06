using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 액션 아이콘 하나 — 서브 능력 그림 · 쿨타임 덮개 · 키 글자 · 게이지(방패 내구도).
/// 왼쪽 아래 회피 아이콘 옆에 놓는다 (커스터마이징-구현계획.md 5-1).
/// </summary>
public class AbilityIconView : MonoBehaviour
{
    [SerializeField] Image icon;
    [SerializeField] Image cooldownFill;   // Filled · Radial360 — 스프라이트가 있어야 동작한다
    [SerializeField] TMP_Text keyText;
    [SerializeField] TMP_Text timerText;
    [SerializeField] Image gauge;          // Filled · Horizontal

    [SerializeField] Color readyColor = Color.white;
    [SerializeField] Color coolingColor = new Color(1f, 1f, 1f, 0.4f);
    [SerializeField] Color activeColor = new Color(0.55f, 0.95f, 1f, 1f);

    SubAbility ability;

    public void Show(SubAbility value, string key)
    {
        ability = value;
        gameObject.SetActive(value != null);
        if (value == null) return;

        if (icon != null)
        {
            icon.sprite = value.Data != null ? value.Data.icon : null;
            icon.enabled = icon.sprite != null;
        }

        if (keyText != null) keyText.text = key;
        Tick();
    }

    public void Hide()
    {
        ability = null;
        gameObject.SetActive(false);
    }

    void Update() => Tick();

    void Tick()
    {
        if (ability == null) return;

        float total = ability.CooldownTotal;
        float left = ability.CooldownRemaining;
        bool cooling = left > 0.01f && total > 0.01f;

        if (cooldownFill != null) cooldownFill.fillAmount = cooling ? Mathf.Clamp01(left / total) : 0f;

        if (timerText != null)
        {
            timerText.gameObject.SetActive(cooling);
            if (cooling) timerText.text = left >= 1f ? Mathf.CeilToInt(left).ToString() : left.ToString("0.0");
        }

        if (icon != null) icon.color = ability.IsBusy ? activeColor : cooling ? coolingColor : readyColor;

        if (gauge != null)
        {
            float g = ability.Gauge;
            gauge.transform.parent.gameObject.SetActive(g >= 0f);
            if (g >= 0f) gauge.fillAmount = g;
        }
    }
}
