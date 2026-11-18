
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PressStartBlink : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    [SerializeField] private TMP_Text startText;
    [SerializeField] private Image buttonBackground;

    [Header("Fade Settings")]
    [Tooltip("완전히 사라졌다 다시 나타나는 전체 시간")]
    [Min(0.1f)]
    [SerializeField] private float fadeDuration = 2f;

    [Range(0f, 1f)]
    [SerializeField] private float minAlpha = 0f;

    [Range(0f, 1f)]
    [SerializeField] private float maxAlpha = 1f;

    [Header("Hover")]
    [SerializeField] private bool showOnHover = true;

    private bool isHovered;

    private void Awake()
    {
        if (startText == null)
            startText = GetComponentInChildren<TMP_Text>(true);

        if (startText != null)
            startText.raycastTarget = false;

        if (buttonBackground != null)
        {
            Color bgColor = buttonBackground.color;
            bgColor.a = 0f;
            buttonBackground.color = bgColor;
            buttonBackground.raycastTarget = true;
        }

        Button button = GetComponent<Button>();

        if (button != null)
            button.transition = Selectable.Transition.None;
    }

    private void Update()
    {
        if (startText == null)
            return;

        if (showOnHover && isHovered)
        {
            startText.alpha = maxAlpha;
            return;
        }

        float duration = Mathf.Max(0.1f, fadeDuration);

        // 1 → 0 → 1 부드럽게 반복
        float wave = (
            Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / duration)
            + 1f
        ) * 0.5f;

        startText.alpha = Mathf.Lerp(minAlpha, maxAlpha, wave);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }

    private void OnDisable()
    {
        isHovered = false;

        if (startText != null)
            startText.alpha = maxAlpha;
    }
}
