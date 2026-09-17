using UnityEngine;

/// <summary>
/// 수리 나노봇 데이터 (서브유닛).
///
/// 주위를 떠도는 나노봇이 주기적으로 체력을 회복한다 (뱀서 퓨마롤라).
/// 판정이 없어 새 코드가 거의 필요 없는 대신, 회복량 처리에 규칙이 두 개 있다.
///
/// ⚠ 회복량을 damage 필드로 쓰지 않는다.
///    damage 를 쓰면 공격력 업그레이드 카드(ApplyGlobalModifier)가 회복량까지 올린다.
///    대신 healAmount 를 두고, 성장은 fireIntervalMul(간격 단축)로 표현한다.
///
/// 권장 초기값 (10번 문서 4-3)
///   healAmount 1, fireInterval 8, maxLevel 5
///   perLevelBonus: fireIntervalMul 0.85  → 5레벨이면 약 4.2초마다 1 회복
///   requiresTarget 은 false (적이 없어도 회복해야 한다)
/// </summary>
[CreateAssetMenu(menuName = "Weapon/Repair Weapon Data", order = 5)]
public class RepairWeaponData : WeaponData
{
    [Header("Repair")]
    [Tooltip("한 번에 회복하는 양. 소수도 된다 — 1 이 넘을 때까지 누적했다가 회복한다")]
    [Min(0f)] public float healAmount = 1f;

    [Header("Nanobot Unit")]
    [Tooltip("OrbitUnit 컴포넌트를 가진 프리팹 (작은 구). 비워 두면 연출 없이 회복만 한다")]
    public GameObject nanoPrefab;

    [Min(0)] public int nanoCount = 2;
    [Min(1)] public int maxNanoCount = 4;

    [Header("Orbit")]
    [Min(0.1f)] public float orbitRadius = 0.9f;
    public float orbitHeight = 1.6f;
    public float orbitSpeed = 90f;
    [Min(0.1f)] public float followLerp = 12f;

    [Header("Effects")]
    [Tooltip("회복 순간 플레이어를 따라다니는 효과")]
    public GameObject healFxPrefab;

    [Tooltip("효과 크기 배율. 1 이면 원본 크기다. 회복은 '됐구나' 정도만 보이면 되므로 캐릭터 크기에 맞춘다")]
    [Min(0.01f)] public float healFxScale = 1f;

    public Vector3 healFxOffset = new Vector3(0f, 1f, 0f);

    [Tooltip("발밑 마법진 등 바닥 효과 (선택). 비워 두면 몸 주변 효과만 나온다")]
    public GameObject groundFxPrefab;

    public override WeaponKind Kind => WeaponKind.SubUnit;

    public override WeaponRuntimeStats Compose(in WeaponModifier modifier)
    {
        WeaponRuntimeStats s = base.Compose(modifier);
        WeaponModifier m = modifier.Sanitized();

        s.Range = Mathf.Max(0.1f, orbitRadius + m.rangeAdd);
        s.ProjectileSpeed = orbitSpeed + m.projectileSpeedAdd;
        s.SubUnitCount = Mathf.Clamp(nanoCount + m.subUnitAdd, 0, maxNanoCount);

        return s;
    }
}
