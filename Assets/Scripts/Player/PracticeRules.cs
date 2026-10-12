using UnityEngine;

/// <summary>
/// 연습 규칙 — 테스트 룸에서만 켠다 (2026-10-11 사용자 요청). 실제 판에는 영향이 없다.
///
///  · 스태미나 무한 — 달려도 줄지 않는다
///  · 능력 쿨타임을 <see cref="CooldownCap"/> 초 이하로 — 서브 능력(그랩 · 방패) · 사수 E(유탄 · 총검 · 충격 방출기) · 패링 · 구르기
/// 쿨타임을 거는 곳이 <see cref="Cooldown"/> 을 거쳐 값을 정한다. TestRoomManager 가 켜고(장면 시작) 끈다(장면을 나갈 때).
/// 팀원 쪽 자동 스킬(파이어볼 · 표창 …)은 레벨업으로 얻는 것이라 테스트 룸에 없고, 여기서 건드리지 않는다.
/// </summary>
public static class PracticeRules
{
    /// <summary>연습 규칙이 켜져 있는가.</summary>
    public static bool Active { get; private set; }

    /// <summary>쿨타임 상한 (초).</summary>
    public static float CooldownCap { get; private set; } = 1f;

    public static bool InfiniteStamina { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => End();

    public static void Begin(float cooldownCap, bool infiniteStamina)
    {
        Active = true;
        CooldownCap = Mathf.Max(0f, cooldownCap);
        InfiniteStamina = infiniteStamina;
    }

    public static void End()
    {
        Active = false;
        InfiniteStamina = false;
        CooldownCap = 1f;
    }

    /// <summary>걸 쿨타임 — 연습 중이면 상한으로 줄인다. 원래 더 짧으면 그대로.</summary>
    public static float Cooldown(float seconds) => Active && seconds > CooldownCap ? CooldownCap : seconds;
}
