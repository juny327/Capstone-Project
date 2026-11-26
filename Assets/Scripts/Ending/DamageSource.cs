/// <summary>
/// 피해의 출처 (2026-11-26, 판 결과 화면 — 무기 · 스킬별 피해 몫).
///  · 무기: 무기 데이터 에셋 이름 (WD_Rifle · WD_EVO_Executioner …) — 결과 화면이 진화 전 무기를 상위 무기 카드에 합친다
///  · 스킬: 레벨업 카드 에셋 이름 (FireBallUpgrade · TornadoUpgrade …) — 카드의 그림 · 이름 · 레벨을 그대로 쓴다
///  · 그 밖: "sub:…" (패링 · 서브 능력)
/// <see cref="Current"/> 는 무기가 자기 효과(부착물 · 상위 무기 동작)를 돌리는 동안의 출처다 (WeaponBase 가 정한다).
/// 효과가 만든 투사체 · 장판은 만들어질 때 이 값을 기억해 두었다가 피해를 줄 때 쓴다.
/// </summary>
public static class DamageSource
{
    public const string Parry = "sub:Parry";
    public const string Grapple = "sub:Grapple";
    public const string Shield = "sub:Shield";
    /// <summary>보스가 쓰러질 때 남은 부하를 치우는 피해 — 기록하지 않는다 (최고 한 방 999,999 가 되지 않게).</summary>
    public const string Sweep = "sweep";

    /// <summary>지금 효과를 돌리는 무기 (없으면 null).</summary>
    public static string Current;

    public static string Of(IWeapon weapon) => weapon != null && weapon.Data != null ? weapon.Data.name : null;
    public static string Of(WeaponData data) => data != null ? data.name : null;

    /// <summary>이 무기의 효과를 돌리는 동안 Current 를 바꾼다 — 돌려받은 이전 값을 끝날 때 Restore 로.</summary>
    public static string Enter(IWeapon weapon)
    {
        string previous = Current;
        string key = Of(weapon);
        if (key != null) Current = key;
        return previous;
    }

    public static void Restore(string previous) => Current = previous;
}
