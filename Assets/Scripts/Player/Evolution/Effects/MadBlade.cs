using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 광란의 검 (2026-11-24 혈검 — 사용자: "주위를 베면서도 검이 스스로 미친 컨셉으로 여기저기 들쑤시고 다니거나 적들을 막 자동으로 휘젓는 연출,
/// 이 무기가 뭘 하는지 눈에 확 보이게").
/// 혈검의 분신 하나가 손을 떠나 둘레(madSeek) 적 사이를 날아다닌다:
///  · 노린 적에게 칼끝을 겨누고 굽은 길로 쏜살같이 꽂힌다(찌르기 — 무기 공격력 × madDamageMul) → 그대로 몸을 꿰뚫고 지나가며(찢기)
///    프로펠러처럼 미친 듯 돌다가 → 다음 적을 고른다 (가까운 셋 중 무작위 · 방금 찌른 적은 madRehit 초 쉰다)
///  · 찌를 적이 없으면 주인 둘레 · 적 둘레를 돌며 어슬렁거린다
///  · 시간이 다 되면(폭주 검은 폭주가 끝나면) 주인 손으로 돌아와 사라진다
/// 모습은 손에 든 검의 메시를 복제해 크게(madScale) — 칼날을 따라 빛나는 붉은 심지 · 가운데 궤적 · 칼끝 궤적(돌 때 톱날 원이 그려진다).
/// 손의 검은 그대로 둔다 (분신).
/// </summary>
public class MadBlade : MonoBehaviour
{
    // 판 결과 화면 — 이 오브젝트를 만든 무기 (만들 때의 DamageSource.Current, 2026-11-26)
    readonly string damageSource = DamageSource.Current;
    enum Phase { Dash, Rip, Prowl, Return }

    BloodFrenzyEffect cfg;
    WeaponBase weapon;
    Transform owner;
    Func<bool> keepAlive;
    Action<Transform, float, Vector3, bool> onStab;
    float lifeLeft;
    bool timed;
    Phase phase;
    Transform target;
    Vector3 p0, p1, p2;
    float u, uSpeed;
    float yawSpin, yawSpeed;
    float roll;
    Vector3 lastTangent = Vector3.forward;
    Transform model;
    float bladeLength = 1.2f;
    float dustCarry;
    readonly Dictionary<Transform, float> lastStab = new Dictionary<Transform, float>();
    readonly List<Transform> found = new List<Transform>();

    const float Height = 1.05f;   // 몸통 높이에서 난다

    /// <summary>시험 집계 — 날린 수 · 찌른 수 · 돌아온 수 · 지금 떠 있는 수.</summary>
    public static int Launched, Stabs, Returned, Alive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Launched = Stabs = Returned = Alive = 0; }

    float S => weapon != null ? weapon.EvoFxScale : 1f;
    bool Kit => cfg.UsesKit;
    float Ground => owner != null ? owner.position.y : transform.position.y - Height;

    /// <param name="seconds">0 보다 크면 그 시간 뒤 돌아온다. 0 이면 keepAlive 가 false 가 될 때까지 (폭주 검)</param>
    /// <param name="onStab">찌른 적 · 피해 · 자리 · 찌르기 전에 살아 있었나 — 혈검이 흡혈 · 폭주 연장을 맡는다</param>
    public static MadBlade Launch(BloodFrenzyEffect cfg, WeaponBase weapon, Transform owner, Vector3 from, float seconds, Func<bool> keepAlive,
        Action<Transform, float, Vector3, bool> onStab)
    {
        var go = new GameObject("MadBlade");
        go.transform.position = from;
        var blade = go.AddComponent<MadBlade>();
        blade.cfg = cfg;
        blade.weapon = weapon;
        blade.owner = owner;
        blade.timed = seconds > 0f;
        blade.lifeLeft = seconds;
        blade.keepAlive = keepAlive;
        blade.onStab = onStab;
        blade.yawSpeed = (Random.value < 0.5f ? -1f : 1f) * Random.Range(900f, 1300f);
        blade.BuildModel();
        blade.BuildTrails();
        blade.LaunchFx(from);
        blade.NextLeg();
        Launched++;
        return blade;
    }

    /// <summary>바로 돌아오게 한다 (무기를 바꾸거나 뗄 때).</summary>
    public void Recall()
    {
        timed = true;
        lifeLeft = 0f;
        keepAlive = null;
    }

    void OnEnable() => Alive++;
    void OnDestroy() => Alive--;

    // ───────── 움직임 ─────────

    void Update()
    {
        float dt = Time.deltaTime;
        if (owner == null) { Destroy(gameObject); return; }

        if (phase != Phase.Return && Expired(dt)) StartReturn();

        // 노리던 적이 쓰러지면 그 자리에서 다시 고른다
        if (phase == Phase.Dash && !CombatFx.IsAlive(target)) NextLeg();

        // 돌아오는 길은 주인을 따라간다
        if (phase == Phase.Return) p2 = Hand();

        u = Mathf.Min(1f, u + uSpeed * dt);
        Vector3 pos = Bezier(u);
        Vector3 tangent = 2f * (1f - u) * (p1 - p0) + 2f * u * (p2 - p1);
        if (tangent.sqrMagnitude > 0.0001f) lastTangent = tangent.normalized;
        transform.position = pos;
        Orient(dt);
        DustFx(dt);

        if (u < 1f) return;

        switch (phase)
        {
            case Phase.Dash: Stab(); break;
            case Phase.Rip:
            case Phase.Prowl: NextLeg(); break;
            case Phase.Return: ReturnFx(); Returned++; Destroy(gameObject); break;
        }
    }

    bool Expired(float dt)
    {
        if (weapon == null || !weapon.isActiveAndEnabled) return true;
        if (timed) return (lifeLeft -= dt) <= 0f;
        return keepAlive == null || !keepAlive();
    }

    Vector3 Bezier(float t)
    {
        float a = 1f - t;
        return a * a * p0 + 2f * a * t * p1 + t * t * p2;
    }

    Vector3 Hand() => owner.position + Vector3.up * 1.2f;

    /// <summary>end 까지 굽은 길 하나 — 옆으로 크게 휘어 '들쑤시는' 느낌. speedMul 은 빠르기 배율.</summary>
    void Leg(Vector3 end, Phase next, float speedMul, float bend)
    {
        phase = next;
        p0 = transform.position;
        p2 = end;
        Vector3 d = p2 - p0;
        Vector3 flat = new Vector3(d.x, 0f, d.z);
        Vector3 side = flat.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;
        float offset = Random.Range(-1f, 1f) * Mathf.Min(bend, d.magnitude * 0.6f);
        p1 = (p0 + p2) * 0.5f + side * offset + Vector3.up * Random.Range(0.1f, 0.9f);
        float length = d.magnitude + Mathf.Abs(offset) * 0.5f;
        uSpeed = cfg.madSpeed * speedMul / Mathf.Max(0.3f, length);
        u = 0f;
    }

    /// <summary>다음 할 일 — 찌를 적(가까운 셋 중 무작위, 쉬는 적은 뒤로) · 없으면 어슬렁.</summary>
    void NextLeg()
    {
        Vector3 here = transform.position;
        CombatFx.FindEnemies(owner.position, cfg.madSeek, found);
        found.RemoveAll(Dead);
        found.Sort((a, b) => Flat(a.position - here).sqrMagnitude.CompareTo(Flat(b.position - here).sqrMagnitude));

        Transform pick = null;
        int ready = 0;
        for (int i = 0; i < found.Count && ready < 3; i++)
        {
            if (Resting(found[i])) continue;
            ready++;
            if (pick == null || Random.value < 1f / ready) pick = found[i];   // 쉬지 않는 가까운 셋 중 고르게
        }

        if (pick != null)
        {
            target = pick;
            Vector3 aim = Aim(pick, here);
            Leg(aim, Phase.Dash, 1f, 2.6f);
            return;
        }

        // 찌를 적이 쉬는 중이면 그 둘레를 휙 돈다 · 적이 없으면 주인 둘레를 어슬렁
        target = null;
        Transform near = found.Count > 0 ? found[0] : null;
        Vector3 center = near != null ? Aim(near, here) : owner.position;
        Vector2 r = Random.insideUnitCircle.normalized * (near != null ? Random.Range(2f, 3.2f) : Random.Range(1.6f, 3f));
        Vector3 spot = new Vector3(center.x + r.x, Ground + Height + Random.Range(-0.2f, 0.5f), center.z + r.y);
        Leg(spot, Phase.Prowl, 0.75f, 2f);
    }

    static readonly Predicate<Transform> Dead = t => t == null || !CombatFx.IsAlive(t);

    bool Resting(Transform t) => lastStab.TryGetValue(t, out float at) && Time.time - at < cfg.madRehit;

    Vector3 Aim(Transform t, Vector3 from)
    {
        Vector3 p = CombatFx.TargetPoint(t, from);
        p.y = t.position.y + Height;
        return p;
    }

    /// <summary>꽂힌 순간 — 피해 · 피 · 할퀸 자국 → 몸을 꿰뚫고 지나간다.</summary>
    void Stab()
    {
        Transform t = target;
        Vector3 dir = Flat(lastTangent);
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();

        if (t != null && CombatFx.IsAlive(t))
        {
            lastStab[t] = Time.time;
            float damage = weapon != null ? weapon.Stats.Damage * cfg.madDamageMul : 0f;
            Vector3 point = CombatFx.Body(t);
            if (damage > 0f)
            {
                CombatFx.Damage(t, damage, point, dir, source: damageSource);
                Stabs++;
                onStab?.Invoke(t, damage, point, true);
            }
            StabFx(transform.position, dir);
        }

        Vector3 side = Vector3.Cross(Vector3.up, dir);
        Vector3 end = transform.position + dir * Random.Range(1.4f, 2.4f) + side * Random.Range(-0.9f, 0.9f);
        end.y = Ground + Height + Random.Range(-0.1f, 0.6f);
        Leg(end, Phase.Rip, 0.8f, 1.2f);
    }

    void StartReturn()
    {
        Leg(Hand(), Phase.Return, 1.25f, 1.5f);
    }

    /// <summary>
    /// 찌르러 갈 때는 칼끝이 가는 쪽을 본다(찌르기). 꿰뚫고 지나가거나 어슬렁거릴 때는 바닥과 나란히 누워 프로펠러처럼 미친 듯 돈다 —
    /// 위에서 내려다보는 카메라에서 가장 잘 보이는 움직임.
    /// </summary>
    void Orient(float dt)
    {
        Vector3 look = lastTangent;
        roll += dt * 720f;
        if (phase == Phase.Dash || phase == Phase.Return)
        {
            transform.rotation = Quaternion.LookRotation(look, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
            yawSpin = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            return;
        }

        yawSpin += yawSpeed * dt;
        transform.rotation = Quaternion.Euler(0f, yawSpin, 0f) * Quaternion.Euler(8f, 0f, 0f);
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    // ───────── 모습 ─────────

    /// <summary>손에 든 검의 메시를 복제 — 칼끝(BladeAxisLocal)이 앞(+Z)을 보게, madScale 배로.</summary>
    void BuildModel()
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

        model.localScale = src.lossyScale * cfg.madScale;
        model.localRotation = Quaternion.FromToRotation(axis, Vector3.forward);

        // 칼날 길이 — 손잡이 끝을 가운데에 두지 않고 칼날 가운데가 가운데에 오게 민다 (돌 때 칼 전체가 원을 그린다)
        var renderers = model.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) return;
        Bounds b = renderers[0].bounds;
        foreach (MeshRenderer r in renderers) b.Encapsulate(r.bounds);
        Vector3 f = transform.forward;
        float centre = Vector3.Dot(b.center - transform.position, f);
        float half = Mathf.Abs(f.x) * b.extents.x + Mathf.Abs(f.y) * b.extents.y + Mathf.Abs(f.z) * b.extents.z;
        bladeLength = Mathf.Max(0.4f, half * 2f);
        model.localPosition = -f * centre;
    }

    /// <summary>칼날을 따라 빛나는 붉은 심지 · 가운데 궤적(지나간 길) · 칼끝 궤적(돌 때 톱날 원).</summary>
    void BuildTrails()
    {
        if (cfg.material == null) return;
        float halfLen = bladeLength * 0.5f;

        var coreGo = new GameObject("Core");
        coreGo.transform.SetParent(transform, false);
        var core = coreGo.AddComponent<LineRenderer>();
        core.useWorldSpace = false;
        core.sharedMaterial = cfg.material;
        FxGlow.Apply(core, FxGlow.Core);
        core.positionCount = 2;
        core.SetPosition(0, new Vector3(0f, 0f, -halfLen));
        core.SetPosition(1, new Vector3(0f, 0f, halfLen));
        core.widthCurve = new AnimationCurve(new Keyframe(0f, 0.12f), new Keyframe(0.75f, 0.32f), new Keyframe(1f, 0.06f));
        core.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        core.receiveShadows = false;
        core.colorGradient = Grad(cfg.color, cfg.slashCoreColor, 0.55f, 0.95f);

        var path = gameObject.AddComponent<TrailRenderer>();
        Trail(path, 0.26f, 0.75f * S, Grad(cfg.slashCoreColor, cfg.color, 0.85f, 0f));

        var tipGo = new GameObject("Tip");
        tipGo.transform.SetParent(transform, false);
        tipGo.transform.localPosition = new Vector3(0f, 0f, halfLen * 0.95f);
        Trail(tipGo.AddComponent<TrailRenderer>(), 0.12f, 0.32f * S, Grad(Color.white, cfg.color, 0.95f, 0f));
    }

    void Trail(TrailRenderer trail, float time, float width, Gradient colors)
    {
        trail.sharedMaterial = cfg.material;
        FxGlow.Apply(trail, FxGlow.Body);
        trail.time = time;
        trail.minVertexDistance = 0.04f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, width), new Keyframe(1f, 0f));
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.colorGradient = colors;
    }

    static Gradient Grad(Color head, Color tail, float headAlpha, float tailAlpha)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(head, 0f), new GradientColorKey(tail, 0.4f), new GradientColorKey(tail, 1f) },
                  new[] { new GradientAlphaKey(headAlpha, 0f), new GradientAlphaKey(Mathf.Max(tailAlpha, headAlpha * 0.6f), 0.4f), new GradientAlphaKey(tailAlpha, 1f) });
        return g;
    }

    /// <summary>손을 떠나는 순간 — 붉은 빛 · 고리 · 피.</summary>
    void LaunchFx(Vector3 from)
    {
        Vector3 ground = new Vector3(from.x, Ground, from.z);
        if (cfg.material != null) FxLine.Ring(cfg.material, ground, 0.2f, 1.4f, cfg.color, 0.12f, 0.22f);
        GroundGlow.Flash(ground, 1.6f, cfg.color, 0.2f);
        if (Kit)
        {
            FxKit.EnergyBurst(from, 0.7f, cfg.color, 0.8f * S);
            FxKit.Spray(from, Vector3.up, new Color(cfg.color.r * 0.85f, 0.02f, 0.04f, 1f), 10f * S, 6f, 0.9f);
        }
        CombatFx.PlaySound(cfg.slashSound);
    }

    /// <summary>꽂힌 자리 — 피가 튀고 · 할퀸 자국 · 가로지르는 붉은 초승달 · 흰 베기 줄.</summary>
    void StabFx(Vector3 at, Vector3 dir)
    {
        float s = S;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        if (cfg.material != null)
        {
            FxLine.Line(cfg.material, at - dir * 1.1f + side * 0.35f, at + dir * 1.1f - side * 0.35f, cfg.slashCoreColor, 0.14f * s, 0.14f);
            FxLine.Line(cfg.material, at - dir * 0.9f - side * 0.5f, at + dir * 0.9f + side * 0.5f, cfg.color, 0.1f * s, 0.18f);
        }
        GroundGlow.Flash(new Vector3(at.x, Ground, at.z), 1.4f, cfg.color, 0.2f);
        if (Kit && EvoStrikes.Take(10))
        {
            Color drop = new Color(cfg.color.r * 0.85f, 0.02f, 0.04f, 1f);
            FxKit.ClawMark(at, 1.9f * s, cfg.color, 0.24f);
            FxKit.SlashArc(at, side * (Random.value < 0.5f ? 1f : -1f), 2.4f * s, cfg.color, 3f, 0.2f);
            FxKit.Spray(at, dir, drop, 12f * s, 8f, 0.7f);
            FxKit.Hit(at, dir, cfg.color, 0.8f * s);
        }
        CombatFx.PlaySound(cfg.slashSound);
        if (Random.value < 0.5f) CombatFx.PlaySound(cfg.bloodSound);
    }

    /// <summary>날아다니는 동안 — 붉은 불티 · 기운이 흩날린다.</summary>
    void DustFx(float dt)
    {
        if (!Kit) return;
        dustCarry += dt;
        if (dustCarry < 0.02f) return;
        FxKit.TrailDust(transform.position, cfg.color, 45f * dustCarry * S, 0.13f, false, 0.55f);
        dustCarry = 0f;
    }

    /// <summary>손으로 돌아온 순간 — 붉은 빛이 스민다.</summary>
    void ReturnFx()
    {
        if (owner == null) return;
        if (cfg.material != null) FxLine.Ring(cfg.material, owner.position, 1.2f, 0.2f, cfg.color, 0.1f, 0.18f);
        if (Kit) FxKit.Glints(Hand(), 0.6f, cfg.color, 8f * S, 0.5f);
    }
}
