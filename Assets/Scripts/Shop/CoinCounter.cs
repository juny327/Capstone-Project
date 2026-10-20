using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 크레딧 표시 (아이콘 + 숫자). 늘어나면 잠깐 커졌다 돌아온다.
/// 전투 HUD 와 상점 창이 함께 쓴다. 시간이 멈춰 있어도(상점) 움직이도록 실제 시간을 쓴다.
/// </summary>
public class CoinCounter : MonoBehaviour
{
    [SerializeField] Image icon;
    [SerializeField] TMP_Text amount;

    [Tooltip("늘어날 때 커지는 대상. 비우면 자기 자신")]
    [SerializeField] RectTransform punchTarget;

    [SerializeField] float punchScale = 1.25f;
    [SerializeField] float punchDuration = 0.25f;

    float punchLeft;

    void Awake()
    {
        if (punchTarget == null) punchTarget = transform as RectTransform;
    }

    public void SetIcon(Sprite sprite)
    {
        if (icon == null) return;

        icon.sprite = sprite;
        icon.enabled = sprite != null;
    }

    public void Set(int value, bool punch = false)
    {
        if (amount != null) amount.text = value.ToString();
        if (punch) punchLeft = punchDuration;
    }

    void Update()
    {
        if (punchTarget == null) return;

        if (punchLeft > 0f)
        {
            punchLeft -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(punchLeft / Mathf.Max(0.01f, punchDuration));
            punchTarget.localScale = Vector3.one * Mathf.Lerp(1f, punchScale, k);
        }
        else if (punchTarget.localScale != Vector3.one)
        {
            punchTarget.localScale = Vector3.one;
        }
    }
}
