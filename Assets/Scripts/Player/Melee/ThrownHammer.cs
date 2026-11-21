using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 던진 묠니르 (2026-11-20) — 마무리 내려찍기(낙뢰) 뒤 곧게 날아갔다가 손으로 돌아온다.
///  · 가는 길 · 오는 길의 적을 모두 친다 (같은 적도 오는 길에 한 번 더) · 맞은 적은 잠깐 기절
///  · 날아가다 벽 · 엄폐물에 닿으면 거기서 돌아선다 (탄과 같은 규칙)
///  · 도는 망치 둘레에 전기가 튀고 번개 꼬리가 남는다. 돌아서는 자리에서 한 번 번쩍
/// 모양은 손에 든 망치의 메시를 복제하고, 던진 동안 손의 망치를 숨기고 공격을 막는다 (아킬레우스의 창과 같다 — WeaponBase.HeldAway).
/// 떠 있는 보조 망치도 같다 — 제자리(보조 칸)로 돌아온다.
/// </summary>
public class ThrownHammer : MonoBehaviour
{
    enum Phase { Wait, Fly, Return }

    HammerEffect cfg;
    WeaponBase weapon;
    float baseDamage;
    Phase phase;
    float timer;
    Vector3 dir;
    Vector3 target;
    Transform spin;
    float spinAngle;
    float boltTimer;
    TrailRenderer trail;
    readonly HashSet<Transform> hit = new HashSet<Transform>();
    readonly List<Transform> found = new List<Transform>();
    readonly List<Renderer> hidden = new List<Renderer>();

    const float MaxReturnTime = 3f;

    /// <summary>날아가는 중인가 · 돌아오는 중인가 (시험).</summary>
    public bool Flying => phase == Phase.Fly;
    public bool Returning => phase == Phase.Return;

    public static ThrownHammer Launch(HammerEffect cfg, WeaponBase weapon, Vector3 dir, float damage)
    {
        var go = new GameObject("ThrownMjolnir");
        var h = go.AddComponent<ThrownHammer>();
        h.cfg = cfg;
        h.weapon = weapon;
        h.baseDamage = damage;
        h.dir = dir;
        h.phase = Phase.Wait;
        h.timer = cfg.throwDelay;
        go.transform.position = weapon.transform.position;

        // 낙뢰가 번쩍이는 동안 잠깐 들고 있다가 던진다 — 그동안도 다음 공격은 막는다
        weapon.HeldAway = true;
        return h;
    }

    float Scale => weapon != null ? weapon.EvoFxScale : 1f;

    float GroundY => weapon != null && weapon.Controller != null ? weapon.Controller.transform.position.y : transform.position.y - cfg.flyHeight;

    /// <summary>돌아올 자리 — 손(무기 루트 = 손 소켓) 또는 떠 있는 자리. 움직이면 따라간다.</summary>
    Vector3 Home => weapon != null ? weapon.transform.position : transform.position;

    void Begin()
    {
        Vector3 start = weapon is MeleeWeapon melee ? melee.HeadWorld : weapon.transform.position;
        start.y = GroundY + cfg.flyHeight;
        float reach = CombatFx.ObstacleDistance(start, dir, cfg.throwDistance);
        target = start + dir * Mathf.Max(1.5f, reach - 0.3f);

        transform.SetPositionAndRotation(start, Quaternion.LookRotation(dir, Vector3.up));
        BuildModel();   // 숨기기 전에 복제한다 (켜진 렌더러만 복제)
        BuildTrail();
        HideHand();
        ThrowFx(start);
        CombatFx.PlaySound(cfg.throwSound);
        phase = Phase.Fly;
    }

    /// <summary>손의 망치를 숨긴다 — 받을 때 · 지워질 때 되돌린다.</summary>
    void HideHand()
    {
        if (weapon == null) return;
        foreach (Renderer r in weapon.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            r.enabled = false;
            hidden.Add(r);
        }
    }

    void RestoreHand()
    {
        foreach (Renderer r in hidden)
            if (r != null) r.enabled = true;
        hidden.Clear();
        if (weapon != null) weapon.HeldAway = false;
    }

    /// <summary>
    /// 손에 든 망치의 메시를 복제 — 손잡이가 수평으로 눕고, 망치 무게 중심(머리 쪽)을 축으로 세로축(위)을 따라 돈다.
    /// 위에서 내려다보는 카메라에 도는 망치로 또렷이 보인다.
    /// </summary>
    void BuildModel()
    {
        spin = new GameObject("Spin").transform;
        spin.SetParent(transform, false);
        var model = new GameObject("Model").transform;
        model.SetParent(spin, false);
        if (weapon == null) return;

        Transform src = weapon.transform;
        MeleeWeapon melee = weapon as MeleeWeapon;
        Vector3 axis = melee != null ? melee.BladeAxisLocal : Vector3.forward;
        float length = melee != null ? melee.BladeLocalLength : 1f;

        foreach (MeshFilter mf in src.GetComponentsInChildren<MeshFilter>())
        {
            var renderer = mf.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled || mf.sharedMesh == null) continue;

            Matrix4x4 rel = src.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            var part = new GameObject(mf.name).transform;
            part.SetParent(model, false);
            part.localPosition = rel.GetColumn(3);
            part.localRotation = rel.rotation;
            part.localScale = rel.lossyScale;

            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var copy = part.gameObject.AddComponent<MeshRenderer>();
            copy.sharedMaterials = renderer.sharedMaterials;
            copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        float scale = src.lossyScale.x;
        model.localScale = src.lossyScale;
        model.localRotation = Quaternion.FromToRotation(axis, Vector3.forward);
        model.localPosition = -Vector3.forward * (length * scale * 0.62f);   // 머리 쪽을 도는 축에
    }

    void BuildTrail()
    {
        if (cfg.material == null) return;

        trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = cfg.material;
        FxGlow.Apply(trail, FxGlow.Body);
        trail.time = 0.3f;
        trail.minVertexDistance = 0.05f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.9f), new Keyframe(0.5f, 0.5f), new Keyframe(1f, 0f));
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(cfg.color, 0.35f), new GradientColorKey(cfg.color, 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;
    }

    /// <summary>손을 떠나는 순간 — 앞으로 쏠리는 번개 줄기 · 발밑 빛.</summary>
    void ThrowFx(Vector3 start)
    {
        Vector3 feet = new Vector3(start.x, GroundY, start.z);
        if (cfg.material != null)
        {
            FxLine.Bolt(cfg.material, start, start + dir * 3.5f, cfg.color, 0.3f * Scale, 0.16f, 0.4f);
            FxLine.Ring(cfg.material, feet, 0.3f, 1.4f, cfg.color, 0.1f, 0.2f);
        }
        GroundGlow.Flash(feet, 2f, cfg.color, 0.2f);
        if (cfg.UsesKit)
        {
            float s = Scale;
            FxKit.EnergyBurst(feet, 0.9f, cfg.color, s);
            FxKit.WindStreaks(feet, dir, 7f, 0.6f, cfg.color, 22f * s, 36f);
        }
    }

    void Update()
    {
        if (weapon == null || !weapon.gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;
        switch (phase)
        {
            case Phase.Wait:
                timer -= dt;
                transform.position = weapon.transform.position;
                if (timer <= 0f) Begin();
                return;
            case Phase.Fly: Fly(dt); break;
            case Phase.Return: Return(dt); break;
        }

        // 돈다 · 전기가 튄다 · 번개 꼬리
        spinAngle += cfg.spinSpeed * dt;
        if (spin != null) spin.localRotation = Quaternion.Euler(0f, spinAngle, 0f);

        Vector3 p = transform.position;
        if (cfg.UsesKit) FxKit.TrailDust(p, cfg.color, 140f * dt * Scale, 0.14f, true, 0.6f);

        boltTimer -= dt;
        if (boltTimer <= 0f && cfg.material != null)
        {
            boltTimer = 0.05f;
            FxLine.Bolt(cfg.material, p + Random.insideUnitSphere * 0.5f, p + Random.insideUnitSphere * 1.1f, Color.Lerp(cfg.color, Color.white, 0.4f), 0.08f * Scale, 0.07f, 0.3f);
            if (Random.value < 0.3f)
            {
                Vector2 c = Random.insideUnitCircle * 1.2f;
                FxLine.Bolt(cfg.material, p, new Vector3(p.x + c.x, GroundY + 0.05f, p.z + c.y), cfg.color, 0.06f * Scale, 0.08f, 0.35f);
            }
        }
    }

    void Fly(float dt)
    {
        Vector3 from = transform.position;
        Vector3 to = Vector3.MoveTowards(from, target, cfg.flySpeed * dt);
        transform.position = to;
        Sweep(from, to, cfg.throwDamageMul, true);

        if ((to - target).sqrMagnitude > 0.0001f) return;

        // 던진 거리 끝 — 번쩍이며 돌아선다
        phase = Phase.Return;
        timer = 0f;
        hit.Clear();   // 오는 길에는 같은 적도 다시 친다
        TurnFx(to);
    }

    void TurnFx(Vector3 at)
    {
        Vector3 ground = new Vector3(at.x, GroundY, at.z);
        if (cfg.material != null)
        {
            FxLine.Bolt(cfg.material, at, ground + Vector3.up * 0.1f, Color.white, 0.12f * Scale, 0.12f, 0.3f);
            FxLine.Ring(cfg.material, ground, 0.2f, 1.6f, cfg.color, 0.12f, 0.24f);
        }
        GroundGlow.Flash(ground, 1.8f, cfg.color, 0.2f);
        if (cfg.UsesKit)
        {
            FxKit.EnergyBurst(ground, 1f, cfg.color, Scale);
            FxKit.LightningStrike(ground, 0.8f, cfg.color, 0.8f * Scale);
        }
    }

    void Return(float dt)
    {
        timer += dt;
        Vector3 home = Home;
        Vector3 from = transform.position;
        // 처음엔 천천히, 점점 빠르게 손으로 빨려 온다
        float speed = cfg.returnSpeed * Mathf.Lerp(0.6f, 1.4f, Mathf.Clamp01(timer / 0.4f));
        Vector3 to = Vector3.MoveTowards(from, home, speed * dt);
        transform.position = to;
        Sweep(from, to, cfg.returnDamageMul, false);

        if ((to - home).sqrMagnitude > 0.36f && timer < MaxReturnTime) return;

        HammerEffect.Caught++;
        CombatFx.PlaySound(cfg.catchSound);
        CatchFx();
        Destroy(gameObject);
    }

    /// <summary>손에 받는 순간 — 손으로 모이는 고리 · 빛 · 쿵 (보조 망치는 약한 흔들림만).</summary>
    void CatchFx()
    {
        Vector3 home = Home;
        Vector3 feet = new Vector3(home.x, GroundY, home.z);
        if (cfg.material != null)
        {
            FxLine.Ring(cfg.material, feet, 1.8f, 0.3f, cfg.color, 0.14f, 0.2f);
            FxLine.Ring(cfg.material, feet, 1.1f, 0.2f, Color.white, 0.07f, 0.16f);
        }
        GroundGlow.Flash(feet, 1.8f, cfg.color, 0.2f);
        ImpactFx.Play(ImpactMoment.MjolnirCatch, weapon, cfg.color, 0.12f);
        if (cfg.UsesKit)
        {
            float s = Scale;
            FxKit.EnergyBurst(feet, 1.3f, cfg.color, s);
            FxKit.Glints(home, 1.2f, Color.white, 12f * s, 0.6f);
            FxKit.TrailDust(home, cfg.color, 20f * s, 0.16f, true);
        }
    }

    /// <summary>지나간 구간의 적을 한 번씩 친다 (outbound = 가는 길).</summary>
    void Sweep(Vector3 from, Vector3 to, float damageMul, bool outbound)
    {
        Vector3 d = EvoStrikes.Flat(to - from, dir);
        CombatFx.FindEnemiesAlong(Ground(from), Ground(to), cfg.hitRadius, found);
        foreach (Transform enemy in found)
        {
            if (!hit.Add(enemy)) continue;

            Vector3 p = CombatFx.Body(enemy);
            CombatFx.Damage(enemy, baseDamage * damageMul, p, d, false, 0f, HitSound.Electric);
            cfg.StunTarget(enemy, cfg.throwStun);
            HitFx(p, d, outbound);
            if (outbound) HammerEffect.ThrowHits++;
            else HammerEffect.ReturnHits++;
        }
    }

    void HitFx(Vector3 p, Vector3 d, bool outbound)
    {
        if (cfg.material != null)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 s = Quaternion.AngleAxis(Random.Range(-60f, 60f), Vector3.up) * d;
                FxLine.Bolt(cfg.material, p, p + s * Random.Range(0.7f, 1.3f) + Vector3.up * Random.Range(0f, 0.4f), cfg.color, 0.1f, 0.12f, 0.3f);
            }
        }
        GroundGlow.Flash(new Vector3(p.x, GroundY, p.z), 1.2f, cfg.color, 0.14f, 0.8f);
        if (cfg.UsesKit && EvoStrikes.Take(6)) FxKit.Hit(p, d, cfg.color, (outbound ? 1.3f : 1.1f) * Scale);
    }

    Vector3 Ground(Vector3 p) => new Vector3(p.x, GroundY, p.z);

    void OnDestroy() => RestoreHand();
}
