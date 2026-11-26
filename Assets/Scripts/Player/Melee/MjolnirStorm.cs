using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 묠니르 연속 낙뢰 (2026-11-25 사용자: "번개도 한 번 빵 찍기보다는 찍고 몇 번 쾅쾅쾅") —
/// 마무리 내려찍기의 첫 낙뢰 뒤 aftershockInterval 마다 둘레 적에게 낙뢰가 더 떨어진다 (아직 안 맞은 적부터, 적이 없으면 둘레 빈자리를 돌아가며).
/// 마지막 한 번은 더 크고 세게 (큰 순간). 무기가 사라져도 끝까지 떨어진다 (0.5초 남짓).
/// </summary>
public class MjolnirStorm : MonoBehaviour
{
    HammerEffect cfg;
    WeaponBase weapon;
    Vector3 center;
    float damage;
    float scale;
    int total;
    int index;
    float timer;
    float spiral;
    readonly List<Transform> found = new List<Transform>();
    readonly HashSet<Transform> struck = new HashSet<Transform>();

    /// <summary>center = 내려찍은 자리 (바닥), damage = 무기 공격력, scale = 연출 크기 (보조 칸은 낮다).</summary>
    public static void Begin(HammerEffect cfg, WeaponBase weapon, Vector3 center, float damage, float scale)
    {
        if (cfg == null || cfg.aftershocks <= 0) return;

        var storm = new GameObject("MjolnirStorm").AddComponent<MjolnirStorm>();
        storm.cfg = cfg;
        storm.weapon = weapon;
        storm.center = center;
        storm.damage = damage;
        storm.scale = scale;
        storm.total = cfg.aftershocks;
        storm.timer = cfg.aftershockInterval;
        storm.spiral = Random.Range(0f, 360f);
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer += cfg.aftershockInterval;
        index++;
        bool last = index >= total;
        Strike(PickPoint(), last);
        if (last) Destroy(gameObject);
    }

    /// <summary>다음 낙뢰 자리 — 둘레의 아직 안 맞은 적 (무작위) → 아무 적 → 둘레 빈자리 (나선으로 돌아가며).</summary>
    Vector3 PickPoint()
    {
        CombatFx.FindEnemies(center, cfg.aftershockRange, found);
        Transform pick = null;
        int fresh = 0;
        foreach (Transform t in found)
        {
            if (struck.Contains(t)) continue;
            fresh++;
            if (Random.Range(0, fresh) == 0) pick = t;   // 안 맞은 적 중 고르게 하나
        }
        if (pick == null && found.Count > 0) pick = found[Random.Range(0, found.Count)];
        if (pick != null) return new Vector3(pick.position.x, center.y, pick.position.z);

        spiral += 137.5f;
        float distance = Mathf.Lerp(1.6f, Mathf.Max(2f, cfg.aftershockRange * 0.6f), total > 1 ? (index - 1f) / (total - 1f) : 0.5f);
        Vector3 at = center + Quaternion.Euler(0f, spiral, 0f) * Vector3.forward * distance;
        float reach = CombatFx.ObstacleDistance(center + Vector3.up * 0.5f, at - center, distance);
        return reach < distance ? center + (at - center).normalized * Mathf.Max(0.5f, reach - 0.4f) : at;
    }

    void Strike(Vector3 at, bool last)
    {
        HammerEffect.Aftershocks++;
        float s = scale * (last ? 1.15f : 0.9f);
        float radius = cfg.aftershockRadius * (last ? 1.3f : 1f);
        Vector3 sky = at + Vector3.up * cfg.boltHeight + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));

        if (cfg.material != null)
            EvoStrikes.Lightning(cfg.material, cfg.color, sky, at, radius, damage * cfg.aftershockDamageMul, cfg.aftershockStun, null,
                last ? 0.55f : 0.42f, last ? 6 : 4, 0f, s,
                source: DamageSource.Of(weapon));   // 무기 콜백 밖(Update)에서 치므로 출처를 직접 — 안 주면 판 결과 화면 '기타' (2026-11-27)

        if (cfg.UsesKit)
        {
            FxKit.LightningStrike(at, radius * 0.9f, cfg.color, s, cfg.boltHeight);
            // 구름 번쩍은 첫 낙뢰보다 작게 — 이어서 터지면 화면이 하얗게 덮인다 (2026-11-25 캡처)
            FxKit.CloudFlash(sky - Vector3.up * 2f, last ? 3.4f : 2.6f, cfg.color, last ? 0.8f : 0.55f);
            FxKit.EnergyBurst(at, radius * 0.6f, cfg.color, s, true);
            FxKit.Scorch(at, radius * 1.1f, 0.7f);
            if (last)
            {
                FxKit.Pillar(at, 0.9f * scale, 7f, cfg.color, 0.4f, 6);
                FxKit.Shockwave(at, radius * 2.2f, cfg.color, 0.32f, 0.9f);
                FxKit.Glints(at + Vector3.up, radius, Color.white, 16f * scale, 0.8f);
            }
        }
        FlashLight.Flash(at + Vector3.up, cfg.color, last ? 9f : 6f, last ? 0.2f : 0.14f, s);

        // 낙뢰가 경직시킨 적에게 전기 별
        CombatFx.FindEnemies(at, radius, found);
        foreach (Transform t in found)
        {
            struck.Add(t);
            StunMarks.Show(t, cfg.aftershockStun, cfg.color, StunMarks.Kind.Sparks);
        }

        // 쾅 · 쾅 · 쾅! — 앞의 둘은 흔들림만, 마지막은 큰 순간 (번쩍 · 멈칫 — 같은 순간이 겹치면 ImpactFx 가 한 번만)
        if (last) ImpactFx.Play(ImpactMoment.MjolnirStrike, weapon, cfg.color, 0.45f);
        else WeaponBase.ShakeCamera(weapon, 0.28f);
        CombatFx.PlaySound(cfg.strikeSound);
    }
}
