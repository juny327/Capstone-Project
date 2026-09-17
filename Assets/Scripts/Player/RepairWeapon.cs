using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 수리 나노봇 (서브유닛). 주기마다 플레이어 체력을 회복한다.
///
/// 판정 코드가 없다. PlayerStats.AddHP 를 부르는 것이 전부다.
///
/// 두 가지 규칙이 있다.
///   · 회복량이 정수(AddHP(int))라서 소수 회복량을 누적했다가 1 이 넘을 때 회복한다.
///   · HasTargetInReach 를 "체력이 깎였는가"로 재정의한다.
///     체력이 가득한 동안에는 쿨다운을 쓰지 않고 기다렸다가 맞는 즉시 회복한다.
///     가득 찬 상태에서 회복 효과만 반복되는 것도 막는다.
/// </summary>
public class RepairWeapon : WeaponBase
{
    private RepairWeaponData Config => (RepairWeaponData)data;

    private readonly List<OrbitUnit> units = new List<OrbitUnit>();

    private PlayerStats playerStats;
    private bool statsChecked;

    // 소수 회복량 누적분
    private float healBuffer;

    public override void Initialize(WeaponData weaponData, WeaponController owner)
    {
        base.Initialize(weaponData, owner);
        SyncUnits();
    }

    protected override void OnStatsChanged()
    {
        SyncUnits();
    }

    /// <summary>체력이 가득하면 회복하지 않는다 (쿨다운도 쓰지 않는다).</summary>
    public override bool HasTargetInReach(in WeaponFireContext context)
    {
        PlayerStats stats = ResolveStats();
        if (stats == null) return false;

        return stats.currentHp < stats.maxHp;
    }

    protected override void OnFire(in WeaponRuntimeStats stats, in WeaponFireContext context)
    {
        RepairWeaponData cfg = Config;
        if (cfg == null) return;

        PlayerStats target = ResolveStats();
        if (target == null) return;

        healBuffer += Mathf.Max(0f, cfg.healAmount);

        int amount = Mathf.FloorToInt(healBuffer);
        if (amount <= 0) return;      // 아직 1 이 안 됐다. 다음 주기에 이어서 쌓는다.

        healBuffer -= amount;

        int before = target.currentHp;
        target.AddHP(amount);

        // 이미 가득 차서 실제로 회복되지 않았으면 효과도 내지 않는다
        if (target.currentHp <= before) return;

        SpawnEffects(cfg, context);
    }

    void SpawnEffects(RepairWeaponData cfg, in WeaponFireContext context)
    {
        Transform ownerRoot = context.OwnerRoot != null
            ? context.OwnerRoot
            : (Owner != null ? Owner.transform : null);

        if (ownerRoot == null) return;

        // 풀 효과를 플레이어 자식으로 붙이지 않는다. 위치만 따라가게 한다 (11번 B5).
        float scale = Mathf.Max(0.01f, cfg.healFxScale);

        if (cfg.healFxPrefab != null)
            FxUtil.SpawnFollow(cfg.healFxPrefab, ownerRoot, cfg.healFxOffset, scale);

        if (cfg.groundFxPrefab != null)
            FxUtil.SpawnFollow(cfg.groundFxPrefab, ownerRoot, Vector3.zero, scale);
    }

    PlayerStats ResolveStats()
    {
        if (statsChecked) return playerStats;

        statsChecked = true;

        if (Owner == null) return null;

        playerStats = Owner.GetComponent<PlayerStats>();
        if (playerStats == null) playerStats = Owner.GetComponentInParent<PlayerStats>();

        if (playerStats == null)
            Debug.LogError("[RepairWeapon] PlayerStats 를 찾지 못했습니다. 회복이 동작하지 않습니다.", this);

        return playerStats;
    }

    /// <summary>나노봇 개체를 스탯에 맞춰 생성·배치한다. 연출 전용이라 판정은 하지 않는다.</summary>
    void SyncUnits()
    {
        RepairWeaponData cfg = Config;
        if (cfg == null || cfg.nanoPrefab == null) return;   // 모델 없이 회복만 해도 된다

        WeaponRuntimeStats stats = Stats;
        int want = Mathf.Max(0, stats.SubUnitCount);

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
            GameObject obj = Instantiate(cfg.nanoPrefab, ownerRoot);
            OrbitUnit unit = obj.GetComponent<OrbitUnit>();

            if (unit == null)
            {
                Debug.LogError("[RepairWeapon] nanoPrefab 에 OrbitUnit 컴포넌트가 없습니다.", this);
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

        DestroyUnits();
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
