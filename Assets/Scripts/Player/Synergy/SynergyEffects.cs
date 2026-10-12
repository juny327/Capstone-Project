using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 작은 조합의 효과 (상위무기-계획.md 7장) — 능력 코드가 동작하는 순간 여기를 부른다. 조합이 꺼져 있으면 아무것도 안 한다.
///
///  · 유탄 착탄 → 네이팜(불바다) · EMP(긴 경직)        · 총검 돌격 끝 → 폭파 돌격
///  · 충격 방출기 → 테슬라 노바(전격) · 화염 고리(화상)  · 그랩으로 끌어온 적 → 방패 받기
///  · 패링으로 되돌린 탄 → 거울 패링
/// 이미 있는 효과(화상 · 전격 · 폭발 · 경직)를 다시 쓴다. 수치는 조합 에셋(SYN_*)에 있다.
/// </summary>
public static class SynergyEffects
{
    static readonly List<Transform> found = new List<Transform>();

    /// <summary>발동 횟수 (테스트).</summary>
    public static int Napalms { get; private set; }
    public static int Emps { get; private set; }
    public static int BlastCharges { get; private set; }
    public static int TeslaNovas { get; private set; }
    public static int FlameRings { get; private set; }
    public static int ShieldCatches { get; private set; }
    public static int MirrorParries { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Napalms = Emps = BlastCharges = TeslaNovas = FlameRings = ShieldCatches = MirrorParries = 0;
    }

    // ───────── 사수 — 유탄 발사기 ─────────

    /// <summary>유탄이 터졌다 — 네이팜 유탄 · EMP 유탄.</summary>
    public static void GrenadeImpact(IWeapon gun, Vector3 at, List<Transform> hit, float weaponDamage)
    {
        SynergyData napalm = SynergyCatalog.On(gun, SynergyKind.NapalmGrenade);
        if (napalm != null)
        {
            FireField.Spawn(napalm, at, weaponDamage * napalm.damageRatio);
            CombatFx.PlaySound(napalm.sound);
            Napalms++;
        }

        SynergyData emp = SynergyCatalog.On(gun, SynergyKind.EmpGrenade);
        if (emp != null)
        {
            Vector3 top = at + Vector3.up * 0.6f;
            foreach (Transform t in hit)
            {
                if (!CombatFx.IsAlive(t)) continue;
                CombatFx.Stun(t, emp.stun);
                FxLine.Bolt(emp.lineMaterial, top, CombatFx.Body(t), emp.color, 0.1f, 0.3f, 0.2f);
            }

            FxLine.Ring(emp.lineMaterial, at, 0.3f, emp.radius, emp.color, 0.2f, 0.4f);
            FxLine.Ring(emp.lineMaterial, at, emp.radius, 0.3f, Color.white, 0.1f, 0.3f);
            EvoStrikes.Spawn(emp.vfxPrefab, at, emp.color, 0.9f);
            CombatFx.PlaySound(emp.sound);
            Emps++;
        }
    }

    // ───────── 사수 — 총검 돌격 ─────────

    /// <summary>총검 돌진이 끝났다 — 폭파 돌격 (몸 앞에서 폭발).</summary>
    public static void ChargeEnd(MonoBehaviour runner, IWeapon gun, Vector3 at, float weaponDamage)
    {
        SynergyData s = SynergyCatalog.On(gun, SynergyKind.BlastCharge);
        if (s == null) return;

        EvoStrikes.Blast(runner, s.lineMaterial, s.color, at, s.radius, weaponDamage * s.damageRatio, 1.5f, null, s.vfxPrefab, 1.1f, 0.35f);
        CombatFx.PlaySound(s.sound);
        BlastCharges++;
    }

    // ───────── 사수 — 충격 방출기 ─────────

    /// <summary>충격파로 밀쳐냈다 — 테슬라 노바 (전격) · 화염 고리 (화상). baseStun = 충격 방출기가 이미 건 경직.</summary>
    public static void ShockWave(IWeapon gun, Vector3 center, List<Transform> pushed, float weaponDamage, float baseStun)
    {
        SynergyData tesla = SynergyCatalog.On(gun, SynergyKind.TeslaNova);
        if (tesla != null)
        {
            Vector3 from = center + Vector3.up * 1.1f;
            foreach (Transform t in pushed)
            {
                if (!CombatFx.IsAlive(t)) continue;
                Vector3 to = CombatFx.Body(t);
                Vector3 dir = to - from;
                dir.y = 0f;
                CombatFx.Damage(t, weaponDamage * tesla.damageRatio, to, dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward);
                // 경직은 새로 거는 값으로 덮인다 — 충격 방출기의 경직에 더해 건다 (줄어들지 않게)
                CombatFx.Stun(t, baseStun + tesla.stun);
                FxLine.Bolt(tesla.lineMaterial, from, to, tesla.color, 0.12f, 0.3f, 0.25f);
                FxLine.Bolt(tesla.lineMaterial, from, to, Color.white, 0.05f, 0.2f, 0.25f);
            }

            FxLine.Ring(tesla.lineMaterial, center, 0.5f, tesla.radius, tesla.color, 0.18f, 0.35f);
            CombatFx.PlaySound(tesla.sound);
            TeslaNovas++;
        }

        SynergyData flame = SynergyCatalog.On(gun, SynergyKind.FlameRing);
        if (flame != null)
        {
            foreach (Transform t in pushed)
                BurnTicker.Apply(t, weaponDamage * flame.damageRatio, flame.duration);

            // 바깥으로 번지는 불 고리 세 겹
            FxLine.Ring(flame.lineMaterial, center, 0.6f, flame.radius, flame.color, 0.35f, 0.45f);
            FxLine.Ring(flame.lineMaterial, center, 0.4f, flame.radius * 0.8f, new Color(1f, 0.85f, 0.4f, 1f), 0.2f, 0.35f);
            FxLine.Ring(flame.lineMaterial, center, flame.radius * 0.95f, flame.radius * 1.1f, flame.color, 0.12f, 0.6f);
            CombatFx.PlaySound(flame.sound);
            FlameRings++;
        }
    }

    // ───────── 검사 — 그랩 훅 + 에너지 방패 ─────────

    /// <summary>방패를 든 채 그랩과 방패를 같이 쓸 수 있는가 — 하나를 누를 때 다른 하나를 끊지 않는다 (SubAbilitySlot).</summary>
    public static bool KeepsTogether(GameObject player, SubAbility running, SubAbility pressed)
    {
        bool pair = (running is ShieldAbility && pressed is GrappleAbility) || (running is GrappleAbility && pressed is ShieldAbility);
        return pair && SynergyCatalog.For(player, SynergyKind.ShieldCatch) != null;
    }

    /// <summary>그랩으로 끌어온 적이 도착했다 — 방패를 들고 있으면 방패에 부딪힌다 (방패 받기). 일어났으면 true.</summary>
    public static bool ShieldCatch(GameObject player, Enemy enemy, float weaponDamage)
    {
        if (player == null || enemy == null || !CombatFx.IsAlive(enemy.transform)) return false;

        SynergyData s = SynergyCatalog.For(player, SynergyKind.ShieldCatch);
        if (s == null) return false;

        var slot = player.GetComponent<SubAbilitySlot>();
        var shield = SynergyData.FindAbility(slot, s.abilityB) as ShieldAbility ?? SynergyData.FindAbility(slot, s.abilityA) as ShieldAbility;
        if (shield == null || !shield.IsHolding) return false;

        Vector3 dir = enemy.transform.position - player.transform.position;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : player.transform.forward;

        // 방패는 앞을 막는다 — 넘기기(그랩 변형)로 뒤에 떨어진 적은 부딪히지 않는다
        Vector3 facing = player.transform.forward;
        facing.y = 0f;
        if (Vector3.Dot(dir, facing.normalized) < 0.2f) return false;

        Vector3 at = player.transform.position + dir * 0.9f + Vector3.up * 1.1f;
        CombatFx.Damage(enemy.transform, weaponDamage * s.damageRatio, at, dir, false, 0.2f);
        // 그랩이 건 끌기 뒤 경직(1초)에 더한다 — 경직은 새로 거는 값으로 덮이므로 합을 건다
        CombatFx.Stun(enemy.transform, 1f + s.stun);

        FxLine.Ring(s.lineMaterial, enemy.transform.position, 0.2f, 1.4f, s.color, 0.25f, 0.3f);
        FxLine.Ring(s.lineMaterial, at - Vector3.up * 1.05f, 0.1f, 0.9f, Color.white, 0.15f, 0.2f);
        EvoStrikes.Spawn(s.vfxPrefab, at - Vector3.up * 0.8f, s.color, 0.7f);
        CombatFx.PlaySound(s.sound);
        ShieldCatches++;
        return true;
    }

    // ───────── 검사 — 패링 + 반사판 ─────────

    /// <summary>패링으로 되돌리는 탄의 피해 — 거울 패링이 켜져 있으면 배율을 곱하고 그 자리에 고리를 띄운다.</summary>
    public static float MirrorParry(GameObject player, Vector3 at, float damage)
    {
        SynergyData s = SynergyCatalog.For(player, SynergyKind.MirrorParry);
        if (s == null) return damage;

        FxLine.Ring(s.lineMaterial, at - Vector3.up * 0.9f, 0.1f, 1.1f, s.color, 0.18f, 0.3f);
        CombatFx.PlaySound(s.sound);
        MirrorParries++;
        return damage * s.damageRatio;
    }
}
