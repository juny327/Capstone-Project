using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 드론(서브유닛) 무기.
///
/// 무기 슬롯으로 취급하는 이유 (W1): 슬롯 관리·업그레이드·WeaponModifier 파이프라인을
/// 그대로 재사용할 수 있다. 별도 시스템으로 만들면 그 경로를 전부 다시 만들어야 한다.
///
/// 단 WeaponKind.SubUnit 이므로 **스왑 대상이 아니고 항상 동작**한다.
/// 손에 드는 무기를 바꿔도 드론은 계속 적을 공격한다.
///
/// 레벨 = 드론 수 규칙을 쓴다. 레벨업 업그레이드가 곧 드론 추가가 된다.
/// </summary>
public class DroneWeapon : WeaponBase
{
    private DroneWeaponData Config => (DroneWeaponData)data;

    private readonly List<DroneUnit> units = new List<DroneUnit>();

    // 드론을 번갈아 쏘게 해서 발사가 한 기에 몰리지 않게 한다
    private int nextUnit;

    public override void Initialize(WeaponData weaponData, WeaponController owner)
    {
        base.Initialize(weaponData, owner);
        SyncUnitCount();
    }

    public override void SetLevel(int newLevel)
    {
        base.SetLevel(newLevel);
        // base 가 OnStatsChanged 를 호출하므로 SyncUnitCount 가 따라온다
    }

    protected override void OnStatsChanged()
    {
        SyncUnitCount();
    }

    protected override void OnFire(in WeaponRuntimeStats stats, in WeaponFireContext context)
    {
        if (units.Count == 0) return;

        // 쏠 수 있는 드론을 하나 찾아 발사시킨다.
        // 전부 사거리 밖이면 아무것도 하지 않고 쿨다운만 돌아간다.
        for (int i = 0; i < units.Count; i++)
        {
            int index = (nextUnit + i) % units.Count;
            DroneUnit unit = units[index];

            if (unit == null) continue;
            if (!unit.TryFire(in stats, context.Targets)) continue;

            nextUnit = (index + 1) % units.Count;
            return;
        }
    }

    /// <summary>스탯이 요구하는 드론 수에 맞춰 생성/제거한다.</summary>
    void SyncUnitCount()
    {
        DroneWeaponData cfg = Config;
        if (cfg == null) return;

        if (cfg.dronePrefab == null)
        {
            Debug.LogError($"[DroneWeapon] dronePrefab 이 비어 있습니다. ({data.weaponName})", this);
            return;
        }

        int want = Mathf.Max(0, Stats.SubUnitCount);

        // 파괴된 항목 정리 (t == null 로 Unity 의 == 오버로드를 거친다)
        for (int i = units.Count - 1; i >= 0; i--)
            if (units[i] == null) units.RemoveAt(i);

        while (units.Count > want)
        {
            int last = units.Count - 1;
            DroneUnit unit = units[last];
            units.RemoveAt(last);

            if (unit != null)
                Destroy(unit.gameObject);
        }

        while (units.Count < want)
        {
            // 플레이어의 자식으로 둔다 — 플레이어가 DontDestroyOnLoad 이므로
            // 씬 전환 시 드론도 함께 유지되고, 플레이어 파괴 시 함께 정리된다.
            Transform ownerRoot = Owner != null ? Owner.transform : transform.root;

            GameObject obj = Instantiate(cfg.dronePrefab, ownerRoot);
            DroneUnit unit = obj.GetComponent<DroneUnit>();

            if (unit == null)
            {
                Debug.LogError("[DroneWeapon] dronePrefab 에 DroneUnit 컴포넌트가 없습니다.", this);
                Destroy(obj);
                return;
            }

            units.Add(unit);
        }

        // 궤도를 균등 분배한다
        for (int i = 0; i < units.Count; i++)
            units[i].Configure(Owner != null ? Owner.transform : transform.root, cfg, i, units.Count);
    }

    public override void SetActive(bool active)
    {
        base.SetActive(active);

        // 사망 시 WeaponController 가 SetActive(false) 를 호출한다 → 드론을 치운다.
        // 코루틴이 아니라 동기 메서드로 처리하는 이유: 컴포넌트가 비활성화되면 코루틴이
        // 중단되어 드론이 유령처럼 남는다 (H-1 과 같은 유형의 사고).
        if (active) return;

        for (int i = 0; i < units.Count; i++)
            if (units[i] != null) Destroy(units[i].gameObject);

        units.Clear();
    }

    void OnDestroy()
    {
        for (int i = 0; i < units.Count; i++)
            if (units[i] != null) Destroy(units[i].gameObject);

        units.Clear();
    }
}
