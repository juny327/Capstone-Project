using UnityEngine;

/// <summary>
/// 몬스터 공격 경고 공통 (2026-11-27, 남은 작업 7-4 — 사용자: "몬스터들의 공격은 빨강 테두리 + 깜빡임, 적 탄에 테두리도 추가").
/// 아군 연출(불길 · 폭발 · 번개)이 주황 · 노랑이라 경고도 주황이면 묻힌다 → 경고는 모두 빨강, 테두리는 진하게 깜빡인다.
/// 깜빡임은 터질 때가 가까울수록 빨라진다 (초당 3 → 8번) — 언제 피해야 하는지 보인다.
/// </summary>
public static class DangerTelegraph
{
    /// <summary>테두리 · 화살표 — 가장 진한 빨강.</summary>
    public static readonly Color Edge = new Color(1f, 0.06f, 0.05f, 1f);
    /// <summary>차오르는 칸 (남은 시간).</summary>
    public static readonly Color Fill = new Color(1f, 0.1f, 0.07f, 0.42f);
    /// <summary>맞는 범위 바탕.</summary>
    public static readonly Color Area = new Color(0.85f, 0.03f, 0.03f, 0.16f);
    /// <summary>적 탄 테두리.</summary>
    public static readonly Color ShotRim = new Color(1f, 0.05f, 0.05f, 0.95f);

    const float StartHz = 3f, EndHz = 8f, Low = 0.35f;

    /// <summary>
    /// 깜빡임 세기 0.35 ~ 1. elapsed = 경고가 시작된 뒤 지난 시간, duration = 경고 길이.
    /// 빨라지는 깜빡임을 매끄럽게 — 진동수를 시간에 따라 적분한 위상을 쓴다.
    /// </summary>
    public static float Blink(float elapsed, float duration)
    {
        float d = Mathf.Max(0.01f, duration);
        float t = Mathf.Clamp(elapsed, 0f, d);
        float phase = 2f * Mathf.PI * (StartHz * t + (EndHz - StartHz) * t * t / (2f * d));
        return Low + (1f - Low) * (0.5f + 0.5f * Mathf.Cos(phase));
    }

    public static Color Dim(Color color, float k) => new Color(color.r, color.g, color.b, color.a * k);
}
