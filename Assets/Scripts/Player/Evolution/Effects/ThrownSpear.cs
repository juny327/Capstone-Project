using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 던진 창 하나 (아킬레우스의 창) — 날아감 → 캐릭터 둘레 궤도 → 돌아옴. 손에 닿으면 스스로 지운다.
/// 모양은 손에 든 창의 메시를 그대로 복제한다 (창끝이 앞을 보게 돌려서). 던진 동안 손의 창은 숨기고 공격을 막는다.
/// </summary>
public class ThrownSpear : MonoBehaviour
{
    enum Phase { Fly, Orbit, Return }

    AchillesSpearEffect cfg;
    Transform owner;
    WeaponBase weapon;
    float baseDamage;
    Phase phase;
    Vector3 target;
    float timer;
    Transform model;
    TrailRenderer trail;
    TrailRenderer coreTrail;
    readonly HashSet<Transform> hit = new HashSet<Transform>();
    readonly List<Transform> found = new List<Transform>();
    readonly List<Renderer> hidden = new List<Renderer>();

    // 궤도
    float radius;
    float angle;          // 라디안, 캐릭터 기준 (x = cos, z = sin)
    float travelled;      // 지금까지 돈 각 (라디안)
    int lap;
    int halfLap;

    const float MaxReturnTime = 3f;

    /// <summary>돈 바퀴 · 궤도 반경 · 궤도 중인가 (테스트).</summary>
    public float Laps => travelled / (Mathf.PI * 2f);
    public float Radius => radius;
    public bool Orbiting => phase == Phase.Orbit;

    public static ThrownSpear Launch(AchillesSpearEffect cfg, WeaponBase weapon, Transform owner, Vector3 dir, float damage)
    {
        Vector3 start = owner.position + Vector3.up * cfg.flyHeight;
        float reach = CombatFx.ObstacleDistance(start, dir, cfg.throwDistance);

        var go = new GameObject("ThrownSpear");
        go.transform.SetPositionAndRotation(start, Quaternion.LookRotation(dir, Vector3.up));

        var spear = go.AddComponent<ThrownSpear>();
        spear.cfg = cfg;
        spear.owner = owner;
        spear.weapon = weapon;
        spear.baseDamage = damage;
        spear.target = start + dir * Mathf.Max(1f, reach - 0.3f);
        spear.BuildModel(weapon);   // 숨기기 전에 복제한다 (켜진 렌더러만 복제)
        spear.BuildCore();
        spear.BuildTrail();
        spear.HideHand();
        spear.ThrowFx(start, dir);
        CombatFx.PlaySound(cfg.throwSound);
        return spear;
    }

    /// <summary>손에서 떠나는 순간 — 굵은 찌르기 줄기 한 번 + 발밑 섬광.</summary>
    void ThrowFx(Vector3 start, Vector3 dir)
    {
        if (cfg.material == null || cfg.throwStreak <= 0f) return;

        float reach = Mathf.Min(3.2f, (target - start).magnitude);
        FxLine.Line(cfg.material, start, start + dir * reach, cfg.color, cfg.throwStreak, 0.2f);
        FxLine.Line(cfg.material, start, start + dir * reach * 0.8f, Color.white, cfg.throwStreak * 0.3f, 0.14f);

        Vector3 feet = owner.position;
        FxLine.Ring(cfg.material, feet, 0.25f, 1.2f, cfg.goldColor, 0.1f, 0.18f);
        GroundGlow.Flash(feet, 1.8f, cfg.goldColor, 0.16f);
    }

    /// <summary>손의 창을 숨기고 공격을 막는다 — 받을 때 · 지워질 때 되돌린다.</summary>
    void HideHand()
    {
        if (weapon == null) return;

        foreach (Renderer r in weapon.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            r.enabled = false;
            hidden.Add(r);
        }
        weapon.HeldAway = true;
    }

    void RestoreHand()
    {
        foreach (Renderer r in hidden)
            if (r != null) r.enabled = true;
        hidden.Clear();
        if (weapon != null) weapon.HeldAway = false;
    }

    /// <summary>손에 든 창의 메시를 복제 — 무기 루트 기준 자세를 그대로 옮기고, 창끝 방향(BladeAxisLocal)이 앞(+Z)을 보게 돌린다.</summary>
    void BuildModel(WeaponBase weapon)
    {
        model = new GameObject("Model").transform;
        model.SetParent(transform, false);
        if (weapon == null) return;

        Transform src = weapon.transform;
        Vector3 axis = weapon is MeleeWeapon melee ? melee.BladeAxisLocal : Vector3.forward;

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

        model.localScale = src.lossyScale;
        model.localRotation = Quaternion.FromToRotation(axis, Vector3.forward);
    }

    /// <summary>
    /// 창대를 따라 빛나는 줄기 — 창 모델은 위에서 보면 가늘어 날아가는 동안 잘 안 보였다 (2026-10-31 시험 캡처).
    /// 모델이 차지하는 앞뒤 길이를 재서 손잡이 뒤 끝 → 창끝까지 긋는다. 로컬 좌표라 돌 때 같이 돈다.
    /// </summary>
    void BuildCore()
    {
        if (cfg.material == null) return;

        float back = -0.5f, tip = 2.5f;
        var renderers = model.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (MeshRenderer r in renderers) b.Encapsulate(r.bounds);
            Vector3 f = transform.forward;
            float centre = Vector3.Dot(b.center - transform.position, f);
            float half = Mathf.Abs(f.x) * b.extents.x + Mathf.Abs(f.y) * b.extents.y + Mathf.Abs(f.z) * b.extents.z;
            back = centre - half;
            tip = centre + half;
        }

        // 궤적(TrailRenderer)과 한 오브젝트에 렌더러 둘을 둘 수 없어 자식에 둔다
        var coreGo = new GameObject("Core");
        coreGo.transform.SetParent(transform, false);
        var core = coreGo.AddComponent<LineRenderer>();
        core.useWorldSpace = false;
        core.sharedMaterial = cfg.material;
        FxGlow.Apply(core, FxGlow.Core);
        core.positionCount = 2;
        core.SetPosition(0, new Vector3(0f, 0f, back));
        core.SetPosition(1, new Vector3(0f, 0f, tip));
        core.widthCurve = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.8f, 0.55f), new Keyframe(1f, 0.1f));
        core.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        core.receiveShadows = false;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(cfg.color, 0f), new GradientColorKey(Color.Lerp(cfg.color, Color.white, 0.6f), 1f) },
            new[] { new GradientAlphaKey(0.35f, 0f), new GradientAlphaKey(0.95f, 0.8f), new GradientAlphaKey(0.8f, 1f) });
        core.colorGradient = gradient;
    }

    void BuildTrail()
    {
        if (cfg.material == null) return;

        trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = cfg.material;
        FxGlow.Apply(trail, FxGlow.Body);
        trail.time = 0.24f;
        trail.minVertexDistance = 0.05f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 0f));
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.Lerp(cfg.color, Color.white, 0.4f), 0f), new GradientColorKey(cfg.color, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;

        // 둘째 바퀴부터 켜는 흰 심지 — 한 오브젝트에 궤적 둘을 둘 수 없어 자식에 둔다
        var coreGo = new GameObject("TrailCore");
        coreGo.transform.SetParent(transform, false);
        coreTrail = coreGo.AddComponent<TrailRenderer>();
        coreTrail.sharedMaterial = cfg.material;
        FxGlow.Apply(coreTrail, FxGlow.Core);
        coreTrail.minVertexDistance = 0.05f;
        coreTrail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(1f, 0f));
        coreTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        coreTrail.receiveShadows = false;
        coreTrail.sortingOrder = 1;
        var white = new Gradient();
        white.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(cfg.goldColor, 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
        coreTrail.colorGradient = white;
        coreTrail.emitting = false;
    }

    /// <summary>
    /// 궤도에 들어서면 궤적을 넓은 띠로 — 머리는 금색, 꼬리는 청동색, 한 바퀴의 orbitTrailLap 만큼 남는다.
    /// 둘째 바퀴(lapIndex 1)부터는 더 넓어지고 흰 심지가 켜진다.
    /// </summary>
    void SetOrbitTrail(int lapIndex)
    {
        if (trail == null) return;

        float width = lapIndex == 0 ? cfg.orbitTrailWidth.x : cfg.orbitTrailWidth.y;
        float time = cfg.orbitDuration / Mathf.Max(0.5f, cfg.orbitLaps) * cfg.orbitTrailLap;

        trail.time = time;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, width), new Keyframe(0.6f, width * 0.8f), new Keyframe(1f, 0f));
        FxGlow.Apply(trail, 1f);   // 넓은 띠는 밝기 배율을 걸지 않는다 — 걸면 색이 하얗게 날아간다
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(cfg.goldColor, 0f), new GradientColorKey(cfg.color, 0.55f), new GradientColorKey(cfg.color, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.55f, 0.55f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;

        if (coreTrail == null) return;
        coreTrail.time = time * 0.8f;
        coreTrail.emitting = lapIndex >= 1;
    }

    void Update()
    {
        if (owner == null || !owner.gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;
        switch (phase)
        {
            case Phase.Fly: Fly(dt); break;
            case Phase.Orbit: Orbit(dt); break;
            case Phase.Return: Return(dt); break;
        }
    }

    void Fly(float dt)
    {
        Vector3 from = transform.position;
        Vector3 to = Vector3.MoveTowards(from, target, cfg.flySpeed * dt);
        transform.position = to;
        Sweep(from, to, cfg.throwDamageMul, 0);

        if ((to - target).sqrMagnitude > 0.0001f) return;

        // 던진 거리 끝 — 거기서 캐릭터 둘레 궤도로 (반경 = 지금 캐릭터와의 거리)
        Vector3 offset = to - owner.position;
        offset.y = 0f;
        radius = Mathf.Max(1f, offset.magnitude);
        angle = Mathf.Atan2(offset.z, offset.x);
        travelled = 0f;
        lap = 0;
        halfLap = 0;
        hit.Clear();
        phase = Phase.Orbit;
        SetOrbitTrail(0);
        CombatFx.PlaySound(cfg.landSound);
        FxLine.Ring(cfg.material, owner.position, radius * 0.9f, radius, cfg.color, 0.12f, 0.35f);
    }

    /// <summary>캐릭터를 중심으로 원을 그리며 돈다 — 캐릭터가 움직이면 원도 따라간다. 창끝은 도는 방향.</summary>
    void Orbit(float dt)
    {
        float total = cfg.orbitLaps * Mathf.PI * 2f;
        float speed = total / cfg.orbitDuration;
        float sign = cfg.clockwise ? -1f : 1f;

        Vector3 from = transform.position;
        float step = Mathf.Min(speed * dt, total - travelled);
        travelled += step;
        angle += sign * step;

        Vector3 centre = owner.position + Vector3.up * cfg.flyHeight;
        Vector3 to = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        transform.position = to;

        Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * sign;
        transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);

        // 바퀴마다 원 위의 적을 한 번씩
        int nowLap = Mathf.FloorToInt(travelled / (Mathf.PI * 2f) + 0.0001f);
        if (nowLap != lap) { lap = nowLap; hit.Clear(); SetOrbitTrail(lap); }
        Sweep(from, to, cfg.orbitDamageMul, 1);

        int nowHalf = Mathf.FloorToInt(travelled / Mathf.PI + 0.0001f);
        if (nowHalf != halfLap) { halfLap = nowHalf; CombatFx.PlaySound(cfg.orbitSound); }

        if (travelled < total - 0.0001f) return;

        AchillesSpearEffect.LastLaps = Laps;
        phase = Phase.Return;
        timer = 0f;
        hit.Clear();   // 돌아오는 길에는 같은 적도 다시 벤다
    }

    void Return(float dt)
    {
        timer += dt;
        Vector3 home = owner.position + Vector3.up * cfg.flyHeight;
        Vector3 from = transform.position;
        Vector3 to = Vector3.MoveTowards(from, home, cfg.returnSpeed * dt);
        transform.position = to;

        // 창끝이 날아가는 쪽(손 쪽)을 본다
        Vector3 travel = EvoStrikes.Flat(home - to, transform.forward);
        transform.rotation = Quaternion.LookRotation(travel, Vector3.up);
        Sweep(from, to, cfg.returnDamageMul, 2);

        if ((to - home).sqrMagnitude > 0.36f && timer < MaxReturnTime) return;

        AchillesSpearEffect.Caught++;
        CombatFx.PlaySound(cfg.catchSound);
        CatchFx();
        Destroy(gameObject);
    }

    /// <summary>손에 받는 순간 — 손으로 모이는 금색 고리 + 발밑 빛 + 흔들림.</summary>
    void CatchFx()
    {
        Vector3 feet = owner.position;
        FxLine.Ring(cfg.material, feet, 1.7f, 0.3f, cfg.goldColor, 0.14f, 0.2f);
        FxLine.Ring(cfg.material, feet, 1.1f, 0.2f, Color.white, 0.07f, 0.16f);
        GroundGlow.Flash(feet, 1.6f, cfg.goldColor, 0.2f);
        ImpactFx.Play(ImpactMoment.SpearCatch, weapon, cfg.goldColor, cfg.catchShake);   // 쿵 · 살짝 줌 · 금색 번쩍 (보조 무기는 약한 흔들림만)
    }

    /// <summary>지나간 구간의 적을 한 번씩 벤다 (stage 0 날아감 · 1 궤도 · 2 돌아옴 — 테스트 집계).</summary>
    void Sweep(Vector3 from, Vector3 to, float damageMul, int stage)
    {
        Vector3 dir = EvoStrikes.Flat(to - from, transform.forward);
        CombatFx.FindEnemiesAlong(Ground(from), Ground(to), cfg.hitRadius, found);
        foreach (Transform enemy in found)
        {
            if (!hit.Add(enemy)) continue;

            CombatFx.Damage(enemy, baseDamage * damageMul, CombatFx.Body(enemy), dir);
            HitFx(enemy, dir, stage);
            if (stage == 0) AchillesSpearEffect.ThrowHits++;
            else if (stage == 1) AchillesSpearEffect.OrbitHits++;
            else AchillesSpearEffect.ReturnHits++;
        }
    }

    /// <summary>지나가며 맞힌 자리 — 창이 간 쪽으로 튀는 금색 불꽃 + 바닥 빛. 던질 때 꿰뚫은 적은 고리도 하나.</summary>
    void HitFx(Transform enemy, Vector3 dir, int stage)
    {
        if (cfg.material == null) return;

        Vector3 p = CombatFx.Body(enemy);
        for (int i = 0; i < 3; i++)
        {
            Vector3 d = Quaternion.AngleAxis(Random.Range(-55f, 55f), Vector3.up) * dir;
            FxLine.Line(cfg.material, p, p + d * Random.Range(0.6f, 1.1f) + Vector3.up * Random.Range(0f, 0.3f), cfg.goldColor, 0.12f, 0.15f);
        }
        FxLine.Line(cfg.material, p - dir * 0.2f, p + dir * 0.5f, Color.white, 0.08f, 0.1f);
        GroundGlow.Flash(enemy.position, 1.2f, cfg.goldColor, 0.14f, 0.8f);
        if (stage == 0) FxLine.Ring(cfg.material, enemy.position, 0.1f, 0.7f, cfg.color, 0.1f, 0.14f);
    }

    Vector3 Ground(Vector3 p) => p - Vector3.up * cfg.flyHeight;

    void OnDestroy() => RestoreHand();
}
