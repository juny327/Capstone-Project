using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상위 무기들이 같이 쓰는 한 방 연출 + 피해 — 낙뢰(폭풍 코일 · 뇌운) · 폭발(공성포 · 처치 연쇄 밖의 폭발).
/// 한 프레임에 너무 많이 터지지 않게 Take 로 수를 막는다.
/// </summary>
public static class EvoStrikes
{
    static readonly List<Transform> found = new List<Transform>();
    static int frameStamp = -1;
    static int frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        frameStamp = -1;
        frameCount = 0;
    }

    /// <summary>이번 프레임에 하나 더 터뜨려도 되는가 (max 개까지).</summary>
    public static bool Take(int max)
    {
        if (frameStamp != Time.frameCount)
        {
            frameStamp = Time.frameCount;
            frameCount = 0;
        }

        if (frameCount >= max) return false;
        frameCount++;
        return true;
    }

    /// <summary>
    /// from(하늘 · 구름)에서 at 으로 떨어지는 번개 — 반경 안 적에게 피해 · 경직.
    /// 모습만 정하는 값: width 줄기 굵기 · branches 맞은 자리에서 바닥을 타고 퍼지는 전기 가지 수(1 이상이면 줄기 중간에서 곁가지도 하나) ·
    /// glowRadius 바닥 빛 반경(0 이면 radius × 1.3) · scale 연출 크기와 밝기(보조 칸은 낮춘다).
    /// </summary>
    public static int Lightning(Material material, Color color, Vector3 from, Vector3 at, float radius, float damage, float stun, GameObject vfx = null,
        float width = 0.24f, int branches = 0, float glowRadius = 0f, float scale = 1f)
    {
        Vector3 end = at + Vector3.up * 0.2f;
        FxLine.Bolt(material, from, end, color, width * scale, 0.2f, 0.55f);
        FxLine.Bolt(material, from, end, Color.white, width * 0.4f * scale, 0.14f, 0.35f);
        FxLine.Ring(material, at, 0.2f, radius, color, 0.2f, 0.22f);
        GroundGlow.Flash(at, (glowRadius > 0f ? glowRadius : radius * 1.3f) * scale, color, 0.2f, scale);
        FlashLight.Flash(at, color, 5f, 0.12f, 0.6f * scale);
        Spawn(vfx, at, color, 0.8f, 0.7f);

        if (branches > 0)
        {
            // 줄기 중간에서 갈라지는 곁가지 하나 — 한 줄로만 떨어지면 광선처럼 보인다
            Vector3 mid = Vector3.Lerp(from, end, Random.Range(0.35f, 0.6f));
            Vector3 out2 = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * Vector3.forward * Random.Range(0.8f, 1.6f);
            FxLine.Bolt(material, mid, Vector3.Lerp(mid, end, 0.55f) + out2, color, Mathf.Max(0.05f, width * 0.4f * scale), 0.14f, 0.3f);

            // 맞은 자리에서 바닥을 타고 퍼지는 전기 가지
            Vector3 low = at + Vector3.up * 0.15f;
            for (int i = 0; i < branches; i++)
            {
                Vector3 d = Quaternion.AngleAxis(i * 360f / branches + Random.Range(-25f, 25f), Vector3.up) * Vector3.forward;
                FxLine.Bolt(material, low, low + d * radius * Random.Range(0.7f, 1.15f) * scale, color, Mathf.Max(0.05f, width * 0.3f), 0.16f, 0.22f);
            }
        }

        CombatFx.FindEnemies(at, radius, found);
        foreach (Transform t in found)
        {
            CombatFx.Damage(t, damage, CombatFx.Body(t), Vector3.down, sound: HitSound.Electric);
            CombatFx.Stun(t, stun);
        }

        return found.Count;
    }

    /// <summary>at 에서 터지는 폭발 — 반경 안 적에게 피해 · 바깥으로 밀기. exclude 는 빼고 센다.</summary>
    public static int Blast(MonoBehaviour runner, Material material, Color color, Vector3 at, float radius, float damage,
        float push, Transform exclude = null, GameObject vfx = null, float vfxScale = 1f, float shake = 0f)
    {
        FxLine.Ring(material, at, 0.3f, radius, color, 0.32f, 0.3f);
        FxLine.Ring(material, at, 0.1f, radius * 0.55f, Color.white, 0.16f, 0.2f);
        GroundGlow.Flash(at, radius * 1.2f, color, 0.26f, 0.5f);   // 포탄 여러 발이 겹쳐 터진다 — 약하게
        FlashLight.Flash(at, color, Mathf.Max(4f, radius * 2.2f), 0.18f);
        Spawn(vfx, at, color, vfxScale);

        CombatFx.FindEnemies(at, radius, found, exclude);
        foreach (Transform t in found)
        {
            Vector3 dir = t.position - at;
            dir.y = 0f;
            Vector3 away = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            CombatFx.Damage(t, damage, CombatFx.Body(t), away);
            if (push > 0f && runner != null) CombatFx.Push(runner, t, away, push, 0.2f);
        }

        WeaponBase.ShakeCamera(runner as WeaponBase, shake);   // 보조 무기는 약하게 (WeaponBase.ShakeCamera)
        return found.Count;
    }

    /// <summary>point 까지 duration 초에 끌어온다 — 적은 내비메시 위로(EnemyShove), 테스트 룸 무리 표적은 IPullable 로. 보스는 끌지 않는다.</summary>
    // 지금 쓰는 곳 없음 — 보관한 천공창 · 사슬검이 쓰던 끌어오기. 되살릴 때를 위해 남긴다 (2026-10-13)
    public static bool PullTo(MonoBehaviour runner, Transform target, Vector3 point, float duration)
    {
        if (runner == null || target == null) return false;

        if (target.TryGetComponent(out Enemy enemy)) return EnemyShove.MoveTo(runner, enemy, point, duration) != null;
        if (target.TryGetComponent(out IPullable pullable))
        {
            pullable.PullTo(point, duration);
            return true;
        }

        return false;
    }

    /// <summary>바닥에서 본 수평 방향 (길이 0 이면 fallback).</summary>
    public static Vector3 Flat(Vector3 v, Vector3 fallback)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : fallback;
    }

    /// <summary>파티클 효과를 잠깐 띄운다 (색을 맞춰서). 없으면 아무것도 안 한다.</summary>
    public static void Spawn(GameObject prefab, Vector3 at, Color color, float scale, float life = 2.5f)
    {
        if (prefab == null) return;

        GameObject fx = Object.Instantiate(prefab, at + Vector3.up * 0.3f, Quaternion.identity);
        fx.transform.localScale = Vector3.one * scale;
        Tint(fx, color);
        ThinSmoke(fx, 0.45f, 0.6f);
        Object.Destroy(fx, life);
    }

    /// <summary>
    /// 파티클 효과에 무기 색을 입힌다 — <b>원래 밝기는 지킨다</b> (2026-11-14).
    /// 폭발 프리팹(vfx_Explosion_01)은 재질 색이 ×4 라서, 검은 연기 · 어두운 불꽃까지 밝은 색으로 덮으면 통째로 흰 덩어리가 됐다.
    /// 어두운 것(연기)은 그대로 두고, 불꽃은 원래 밝기에 색만 바꾸고, 원래 밝던 것(전기 · 섬광)은 예전처럼 흰색을 조금 섞는다.
    /// </summary>
    public static void Tint(GameObject fx, Color color)
    {
        if (fx == null) return;

        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            Color original = main.startColor.mode == ParticleSystemGradientMode.Color ? main.startColor.color : Color.white;
            float bright = Mathf.Max(original.r, Mathf.Max(original.g, original.b));
            if (bright < DarkTint) continue;   // 검은 연기는 그대로

            // 원래 밝던 것 — 재질이 색을 몇 배로 키우면(전기 효과 ×4) 그만큼 낮춰야 무기 색이 남는다. 아니면 예전처럼 흰색을 조금 섞는다
            float boost = MaterialBoost(ps);
            Color tinted = bright <= 0.9f ? color * bright
                : boost > 1.5f ? color * Mathf.Min(1f, 2.2f / boost)
                : Color.Lerp(color, Color.white, 0.25f);
            tinted.a = original.a;
            main.startColor = new ParticleSystem.MinMaxGradient(tinted);
        }
    }

    static readonly int LegacyColor = Shader.PropertyToID("_Color");

    /// <summary>파티클 재질이 색에 곱하는 배율 (_Color 의 가장 큰 성분). 궤적으로 그리는 파티클은 궤적 재질을 본다.</summary>
    static float MaterialBoost(ParticleSystem ps)
    {
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer == null) return 1f;

        Material material = ps.trails.enabled && renderer.trailMaterial != null ? renderer.trailMaterial : renderer.sharedMaterial;
        if (material == null || !material.HasProperty(LegacyColor)) return 1f;

        Color c = material.GetColor(LegacyColor);
        return Mathf.Max(c.r, Mathf.Max(c.g, c.b));
    }

    /// <summary>이보다 어두운 파티클(연기)은 색을 입히지 않는다.</summary>
    const float DarkTint = 0.08f;

    /// <summary>
    /// 폭발 효과의 검은 연기를 옅고 짧게 — 그대로 두면 지름 4m 짜리 검은 덩어리가 1.5초 동안 적을 가린다.
    /// alpha 는 연기 진하기(0~1), life 는 수명 배율. Tint 다음에 부른다 (어두운 파티클만 건드린다).
    /// </summary>
    public static void ThinSmoke(GameObject fx, float alpha, float life)
    {
        if (fx == null) return;

        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            if (main.startColor.mode != ParticleSystemGradientMode.Color) continue;

            Color c = main.startColor.color;
            if (Mathf.Max(c.r, Mathf.Max(c.g, c.b)) >= DarkTint) continue;

            c.a *= alpha;
            main.startColor = new ParticleSystem.MinMaxGradient(c);
            main.startLifetimeMultiplier *= life;
        }
    }
}
