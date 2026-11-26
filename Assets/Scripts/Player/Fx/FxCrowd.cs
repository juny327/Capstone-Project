using UnityEngine;

/// <summary>
/// 적이 많을 때 아군 연출을 살짝 옅게 (2026-11-27, 남은 작업 7-4 — 사용자: "아군 연출 옅게 하는 건 괜찮을 것 같아").
/// 적 50마리부터 옅어지기 시작해 120마리에서 ×0.7 (스테이지 3 동시 최대 140). 보스전은 잡몹 최대 8 이라 걸리지 않는다.
/// FxKit 입자 · FxLine 선 · 바닥 빛 · 큰 한 방 번쩍임이 곱한다. 몬스터 경고 · 적 탄에는 곱하지 않는다.
/// </summary>
public static class FxCrowd
{
    public const int StartCount = 50;
    public const int FullCount = 120;
    public const float MinAlpha = 0.7f;

    static int frame = -1;
    static float cached = 1f;

    public static float Alpha
    {
        get
        {
            if (frame == Time.frameCount) return cached;
            frame = Time.frameCount;
            int n = EnemyManager.Instance != null ? EnemyManager.Instance.ActiveEnemies.Count : 0;
            cached = Mathf.Lerp(1f, MinAlpha, Mathf.InverseLerp(StartCount, FullCount, n));
            return cached;
        }
    }
}
