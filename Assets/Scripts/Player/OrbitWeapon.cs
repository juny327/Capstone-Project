using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플라즈마 오브 링 (서브유닛). 플레이어 주위를 도는 오브가 닿는 적을 태운다.
///
/// 구조는 DroneWeapon 과 같다. 무기가 개체(OrbitUnit)를 만들고 관리하며,
/// 판정은 무기가 오브 위치를 기준으로 직접 한다.
///
/// **재타격 기록(HitCooldowns)을 무기 하나가 들고 오브들이 공유한다.**
/// 오브가 5개여도 한 적은 FireInterval 마다 한 번만 맞아야
/// 10번 4-3에서 정한 "적 1명당 최대 DPS"가 유지되기 때문이다.
/// 기록을 오브마다 두면 오브 수만큼 DPS가 곱해진다.
///
/// 쿨다운(Fire)은 쓰지 않는다. 접촉 판정은 매 프레임 OnTick 에서 돈다.
/// </summary>
public class OrbitWeapon : WeaponBase
{
    private OrbitWeaponData Config => (OrbitWeaponData)data;

    private readonly List<OrbitUnit> units = new List<OrbitUnit>();

    // 링 전체가 공유하는 재타격 기록
    private readonly HitCooldowns cooldowns = new HitCooldowns();

    // 타격 효과를 낼 대상을 받아 오는 재사용 버퍼
    private readonly List<Transform> hitTargets = new List<Transform>();

    public override void Initialize(WeaponData weaponData, WeaponController owner)
    {
        base.Initialize(weaponData, owner);
        SyncUnits();
    }

    protected override void OnStatsChanged()
    {
        // 레벨업으로 오브 수 · 궤도 반지름 · 회전 속도가 바뀐다
        SyncUnits();
    }

    protected override void OnTick(float deltaTime)
    {
        if (!IsActive || units.Count == 0) return;

        OrbitWeaponData cfg = Config;
        if (cfg == null) return;

        WeaponRuntimeStats stats = Stats;
        int fxBudget = Mathf.Max(1, cfg.maxHitFx);

        for (int i = 0; i < units.Count; i++)
        {
            OrbitUnit unit = units[i];
            if (unit == null) continue;

            int hits = AreaDamage.Apply(
                unit.Position, cfg.contactRadius, in stats, cfg.hitLayers,
                cooldowns, stats.FireInterval, cfg.cameraShake, hitTargets);

            if (hits <= 0 || cfg.hitFxPrefab == null) continue;

            for (int t = 0; t < hitTargets.Count && fxBudget > 0; t++)
            {
                if (hitTargets[t] == null) continue;

                FxUtil.Spawn(cfg.hitFxPrefab, hitTargets[t].position + Vector3.up * 0.8f, Quaternion.identity);
                fxBudget--;
            }
        }
    }

    /// <summary>접촉 판정은 OnTick 이 처리한다. 쿨다운으로 도는 동작은 없다.</summary>
    protected override void OnFire(in WeaponRuntimeStats stats, in WeaponFireContext context) { }

    /// <summary>스탯이 요구하는 오브 수 · 궤도에 맞춰 개체를 만들고 배치한다.</summary>
    void SyncUnits()
    {
        OrbitWeaponData cfg = Config;
        if (cfg == null) return;

        if (cfg.orbPrefab == null)
        {
            Debug.LogError($"[OrbitWeapon] orbPrefab 이 비어 있습니다. ({data.weaponName})", this);
            return;
        }

        WeaponRuntimeStats stats = Stats;
        int want = Mathf.Max(0, stats.SubUnitCount);

        // 파괴된 항목 정리 (t == null 로 Unity 의 == 오버로드를 거친다)
        for (int i = units.Count - 1; i >= 0; i--)
            if (units[i] == null) units.RemoveAt(i);

        while (units.Count > want)
        {
            int last = units.Count - 1;
            OrbitUnit unit = units[last];
            units.RemoveAt(last);

            if (unit != null)
                Destroy(unit.gameObject);
        }

        Transform ownerRoot = Owner != null ? Owner.transform : transform.root;

        while (units.Count < want)
        {
            // 플레이어의 자식으로 둔다 — 플레이어가 DontDestroyOnLoad 이므로
            // 씬 전환 시 함께 유지되고 플레이어 파괴 시 함께 정리된다.
            GameObject obj = Instantiate(cfg.orbPrefab, ownerRoot);
            OrbitUnit unit = obj.GetComponent<OrbitUnit>();

            if (unit == null)
            {
                Debug.LogError("[OrbitWeapon] orbPrefab 에 OrbitUnit 컴포넌트가 없습니다.", this);
                Destroy(obj);
                return;
            }

            units.Add(unit);
        }

        for (int i = 0; i < units.Count; i++)
        {
            units[i].Configure(ownerRoot, stats.Range, cfg.orbitHeight,
                stats.ProjectileSpeed, cfg.followLerp, i, units.Count);
        }
    }

    public override void SetActive(bool active)
    {
        base.SetActive(active);

        if (active) return;

        // 사망 시 WeaponController 가 SetActive(false) 를 호출한다 → 오브를 치운다.
        // 코루틴이 아니라 동기 메서드로 처리한다 (DroneWeapon 과 같은 이유).
        DestroyUnits();
        cooldowns.Clear();
    }

    void OnDestroy()
    {
        DestroyUnits();
    }

    void DestroyUnits()
    {
        for (int i = 0; i < units.Count; i++)
            if (units[i] != null) Destroy(units[i].gameObject);

        units.Clear();
    }
}
