using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 나노 산성 장판 (서브유닛). 주기마다 적 발밑에 장판을 깐다.
///
/// 이 무기는 장판을 꺼내 수치를 넘기는 일만 한다.
/// 틱 데미지 · 수명 · 풀 반납은 전부 장판 프리팹의 DamageZone 이 처리한다 (11번 B2).
///
/// 장판은 무기보다 오래 살아남으므로, 깔리는 순간의 스탯을 복사해서 넘긴다.
/// 나중에 무기 레벨이 올라도 이미 깔린 장판의 수치는 바뀌지 않는다.
/// </summary>
public class ZoneWeapon : WeaponBase
{
    private ZoneWeaponData Config => (ZoneWeaponData)data;

    private readonly List<Transform> candidates = new List<Transform>();

    public override bool HasTargetInReach(in WeaponFireContext context)
    {
        ZoneWeaponData cfg = Config;
        if (cfg == null) return false;

        // 적이 없어도 까는 설정이면 항상 발동한다
        if (cfg.fallbackSpread > 0f) return true;

        if (context.Targets == null || context.OwnerRoot == null) return false;

        return context.Targets.GetNearest(context.OwnerRoot.position, cfg.searchRange) != null;
    }

    protected override void OnFire(in WeaponRuntimeStats stats, in WeaponFireContext context)
    {
        ZoneWeaponData cfg = Config;
        if (cfg == null || context.OwnerRoot == null) return;

        if (cfg.zonePrefab == null)
        {
            Debug.LogError($"[ZoneWeapon] zonePrefab 이 비어 있습니다. ({data.weaponName})", this);
            return;
        }

        int found = context.Targets != null
            ? context.Targets.GetTargets(context.OwnerRoot.position, 16, candidates)
            : 0;

        // 사거리 밖 후보 제거
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (candidates[i] == null) { candidates.RemoveAt(i); continue; }

            float sqr = (candidates[i].position - context.OwnerRoot.position).sqrMagnitude;
            if (sqr > cfg.searchRange * cfg.searchRange) candidates.RemoveAt(i);
        }

        int want = Mathf.Max(1, stats.ProjectileCount);

        for (int i = 0; i < want; i++)
        {
            Vector3 point;

            if (candidates.Count > 0)
            {
                int index = Random.Range(0, candidates.Count);
                point = candidates[index].position;
                candidates.RemoveAt(index);
            }
            else if (cfg.fallbackSpread > 0f)
            {
                Vector2 offset = Random.insideUnitCircle * cfg.fallbackSpread;
                point = context.OwnerRoot.position + new Vector3(offset.x, 0f, offset.y);
            }
            else
            {
                break;   // 적도 없고 대체 배치도 안 하면 그만둔다
            }

            SpawnZone(cfg, point, in stats);
        }

        _ = found;
    }

    void SpawnZone(ZoneWeaponData cfg, Vector3 point, in WeaponRuntimeStats stats)
    {
        GameObject obj = FxUtil.Spawn(cfg.zonePrefab, point, Quaternion.identity);
        if (obj == null) return;

        DamageZone zone = obj.GetComponent<DamageZone>();

        if (zone == null)
        {
            Debug.LogError("[ZoneWeapon] zonePrefab 에 DamageZone 컴포넌트가 없습니다.", this);
            return;
        }

        zone.Init(point, stats.Range, stats.Duration, cfg.tickInterval,
            stats.Damage, stats.CritChance, stats.CritMultiplier);
    }
}
