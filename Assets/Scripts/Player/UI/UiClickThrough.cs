using UnityEngine;

/// <summary>
/// 보여 주기만 하는 창(알림 문구 등)이 클릭을 받지 않게 한다 (2026-11-23) — CanvasGroup 의 blocksRaycasts 를 끈다.
/// 레벨업 창의 '업그레이드 완료' · '경험치 부족' 문구가 카드 위에 떠서, 문구가 사라질 때까지(1.5초) 그 아래 카드를 누를 수 없었다.
/// </summary>
public static class UiClickThrough
{
    public static void Apply(GameObject panel)
    {
        if (panel == null) return;
        if (!panel.TryGetComponent(out CanvasGroup group)) group = panel.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
    }
}
