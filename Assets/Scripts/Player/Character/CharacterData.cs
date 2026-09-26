using UnityEngine;

/// <summary>회피 수단. 같은 키(구르기 키)를 누르지만 캐릭터마다 동작이 다르다.</summary>
public enum DodgeType
{
    /// <summary>구르기 — 자리를 벗어난다 (사수).</summary>
    Roll,

    /// <summary>패링 — 제자리에서 적을 밀쳐내고 투사체를 쳐낸다 (검사).</summary>
    Parry,
}

/// <summary>
/// 캐릭터 한 명의 설정 (캐릭터-2종-확장-설계.md 4-3).
///
/// 플레이어 프리팹은 하나다. 사수와 검사는 **같은 몸에 다른 설정**이므로,
/// 차이는 전부 이 데이터에 두고 <see cref="CharacterLoadout"/> 이 생성 시점에 적용한다.
/// 캐릭터를 하나 더 만들 때는 이 `.asset` 하나만 추가하면 된다.
/// </summary>
[CreateAssetMenu(menuName = "Character/Character Data", order = 0)]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "New Character";

    [Tooltip("로비 캐릭터 영역의 한 줄 요약")]
    [TextArea(1, 3)] public string summary;

    [Tooltip("로비에서 이 캐릭터 영역을 구분하는 색")]
    public Color accentColor = Color.white;

    [Header("Stats")]
    [Min(1)] public int maxHp = 100;

    [Tooltip("받는 피해 배율. 0.85 면 15% 덜 받는다")]
    [Range(0.1f, 2f)] public float damageTakenMultiplier = 1f;

    [Tooltip("이동 속도 배율. 슬로우 · 업그레이드 배율과 곱해진다")]
    [Range(0.5f, 2f)] public float moveSpeedMultiplier = 1f;

    [Min(1f)] public float maxStamina = 100f;

    [Header("Dodge")]
    public DodgeType dodge = DodgeType.Roll;

    [Tooltip("HUD 회피 아이콘. 비우면 HUD 에 원래 있던 구르기 아이콘을 쓴다")]
    public Sprite dodgeIcon;

    [Header("Weapons")]
    [Tooltip("로비에서 고를 수 있는 시작 무기. 첫 번째가 기본값이다")]
    public WeaponData[] startWeapons;

    [Tooltip("스테이지 보상 · 획득으로 얻을 수 있는 손 무기 종류. 서브유닛(드론)은 항상 허용된다")]
    public WeaponKind[] allowedHeldKinds = { WeaponKind.Projectile };

    [Header("Look")]
    [Tooltip("몸 머티리얼. 비우면 프리팹 기본값을 그대로 쓴다")]
    public Material bodyMaterial;

    /// <summary>시작 무기 첫 번째. 로비를 거치지 않았을 때의 기본 무기다.</summary>
    public WeaponData FirstStartWeapon =>
        startWeapons != null && startWeapons.Length > 0 ? startWeapons[0] : null;

    /// <summary>이 캐릭터의 시작 무기 목록에 있는지.</summary>
    public bool HasStartWeapon(WeaponData weapon)
    {
        if (weapon == null || startWeapons == null) return false;

        for (int i = 0; i < startWeapons.Length; i++)
            if (startWeapons[i] == weapon) return true;

        return false;
    }
}
