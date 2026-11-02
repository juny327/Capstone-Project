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
        CombatFx.PlaySound(cfg.throwSound);
        return spear;
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

        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = cfg.material;
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
        if (nowLap != lap) { lap = nowLap; hit.Clear(); }
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
        Destroy(gameObject);
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
            FxLine.Ring(cfg.material, enemy.position, 0.1f, 0.7f, cfg.color, 0.1f, 0.14f);
            if (stage == 0) AchillesSpearEffect.ThrowHits++;
            else if (stage == 1) AchillesSpearEffect.OrbitHits++;
            else AchillesSpearEffect.ReturnHits++;
        }
    }

    Vector3 Ground(Vector3 p) => p - Vector3.up * cfg.flyHeight;

    void OnDestroy() => RestoreHand();
}
