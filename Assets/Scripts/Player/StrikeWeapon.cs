using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 궤도 폭격 위성 (서브유닛).
/// 주기마다 감지 범위 안 무작위 적 N명의 발밑에 예고 마커를 띄우고, delay 뒤에 그 자리를 폭발시킨다.
///
/// 예고 지점은 마커를 띄우는 순간 고정된다. 적이 걸어 나가면 빗나가는데, 이것이 이 무기의 성격이다.
///
/// ⚠ 대기 중인 폭격은 코루틴으로 돈다. 사망(SetActive(false))이나 파괴 시 반드시 멈춘다.
///    멈추지 않으면 죽은 뒤에도 폭격이 떨어지고, 씬이 바뀐 뒤 사라진 풀에 접근한다.
/// </summary>
public class StrikeWeapon : WeaponBase
{
    private StrikeWeaponData Config => (StrikeWeaponData)data;

    private readonly List<Transform> candidates = new List<Transform>();
    private readonly List<Vector3> points = new List<Vector3>();
    private readonly List<Coroutine> pending = new List<Coroutine>();

    public override bool HasTargetInReach(in WeaponFireContext context)
    {
        if (context.Targets == null || context.OwnerRoot == null) return false;

        StrikeWeaponData cfg = Config;
        if (cfg == null) return false;

        return context.Targets.GetNearest(context.OwnerRoot.position, cfg.searchRange) != null;
    }

    protected override void OnFire(in WeaponRuntimeStats stats, in WeaponFireContext context)
    {
        StrikeWeaponData cfg = Config;
        if (cfg == null || context.Targets == null || context.OwnerRoot == null) return;

        int found = context.Targets.GetTargets(context.OwnerRoot.position, 16, candidates);
        if (found == 0) return;

        // 사거리 밖은 버리고, 남은 후보에서 무작위로 고른다
        points.Clear();

        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (candidates[i] == null) { candidates.RemoveAt(i); continue; }

            float sqr = (candidates[i].position - context.OwnerRoot.position).sqrMagnitude;
            if (sqr > cfg.searchRange * cfg.searchRange) candidates.RemoveAt(i);
        }

        int want = Mathf.Min(stats.ProjectileCount, candidates.Count);

        for (int i = 0; i < want; i++)
        {
            int index = Random.Range(0, candidates.Count);
            points.Add(candidates[index].position);
            candidates.RemoveAt(index);
        }

        for (int i = 0; i < points.Count; i++)
            pending.Add(StartCoroutine(StrikeRoutine(points[i], stats)));
    }

    IEnumerator StrikeRoutine(Vector3 point, WeaponRuntimeStats stats)
    {
        StrikeWeaponData cfg = Config;

        // 예고
        if (cfg.markerFxPrefab != null)
        {
            FxUtil.SpawnScaledToRadius(
                cfg.markerFxPrefab, point, Quaternion.identity, stats.Range, cfg.markerFxBaseRadius);
        }

        if (cfg.delay > 0f)
            yield return new WaitForSeconds(cfg.delay);

        // 타격
        if (cfg.beamFxPrefab != null)
        {
            FxUtil.SpawnScaledToRadius(
                cfg.beamFxPrefab, point, Quaternion.identity, stats.Range, cfg.beamFxBaseRadius);
        }

        if (cfg.explosionFxPrefab != null)
        {
            FxUtil.SpawnScaledToRadius(
                cfg.explosionFxPrefab, point, Quaternion.identity, stats.Range, cfg.explosionFxBaseRadius);
        }

        AreaDamage.Apply(point, stats.Range, in stats, cfg.hitLayers,
            null, 0f, cfg.cameraShake);
    }

    public override void SetActive(bool active)
    {
        base.SetActive(active);

        if (active) return;

        CancelPending();
    }

    void OnDestroy()
    {
        CancelPending();
    }

    void CancelPending()
    {
        for (int i = 0; i < pending.Count; i++)
            if (pending[i] != null) StopCoroutine(pending[i]);

        pending.Clear();
    }
}
