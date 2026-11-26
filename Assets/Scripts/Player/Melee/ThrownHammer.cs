using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 던진 묠니르 (2026-11-20 · 11-25 사용자: "단순히 던지기보다는 마블처럼 휭휭 돌면서 난리치는 느낌").
///  · 휘돌리기 — 낙뢰 뒤 망치가 손을 떠나 몸 둘레를 한 바퀴 남짓 휭 돈다 (손과 망치 사이에 번개 끈). 둘레 적을 친다
///  · 튕기기 — 앞쪽 적에게 휘어 날아가 맞히고, 가까운 다음 적으로 꺾여 튄다 (bounces 마리까지). 튈 때마다 하늘에서 번개가 같이 떨어진다
///    앞쪽에 적이 없으면 예전처럼 던진 거리까지(조금 휘어) 갔다가 돌아선다 (벽 · 엄폐물에서 멈춤 — 탄과 같은 규칙)
///  · 돌아오기 — 반대쪽으로 휘며 손으로 빨려 온다. 가는 길 · 오는 길의 적을 모두 친다 (같은 적도 오는 길에 한 번 더) · 맞은 적은 잠깐 기절
///  · 날아가는 내내 빙글빙글 돌며 위아래로 뒤집힌다 · 둘레에 전기가 튀고 번개 꼬리가 남는다 (손에 든 것보다 크게 — 위에서 잘 보이게)
/// 모양은 손에 든 망치의 메시를 복제하고, 던진 동안 손의 망치를 숨기고 공격을 막는다 (아킬레우스의 창과 같다 — WeaponBase.HeldAway).
/// 떠 있는 보조 망치도 같다 — 플레이어 둘레를 돌고, 제자리(보조 칸)로 돌아온다.
/// </summary>
public class ThrownHammer : MonoBehaviour
{
    // 판 결과 화면 — 이 오브젝트를 만든 무기 (만들 때의 DamageSource.Current, 2026-11-26)
    readonly string damageSource = DamageSource.Current;
    enum Phase { Wait, Whirl, Fly, Return }

    HammerEffect cfg;
    WeaponBase weapon;
    float baseDamage;
    Phase phase;
    float timer;
    Vector3 dir;
    Transform spin;
    float spinAngle;
    float boltTimer;
    float tetherTimer;
    TrailRenderer trail;

    // 휘돌리기 — 몸 둘레 각도 (도, 위에서 보아 시계 방향)
    float whirlFrom, whirlTotal, whirlDuration, whirlStartRadius;

    // 지금 나는 길 — from → (옆으로 휜 control) → 끝 (적이면 적을 따라간다). 돌아오는 길도 같은 식 (끝 = 손)
    Vector3 segFrom, segControl, segPoint;
    Transform segTarget;
    float segT, segLength;
    float side = 1f;
    int targetsLeft;
    float flyTime;

    readonly HashSet<Transform> hit = new HashSet<Transform>();
    readonly HashSet<Transform> whirlHit = new HashSet<Transform>();
    readonly List<Transform> found = new List<Transform>();
    readonly List<Renderer> hidden = new List<Renderer>();

    const float MaxFlyTime = 2.4f;
    const float MaxReturnTime = 3f;

    /// <summary>몸 둘레를 도는 중 · 날아가는 중 · 돌아오는 중인가 (시험).</summary>
    public bool Whirling => phase == Phase.Whirl;
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

    float FlyY => GroundY + cfg.flyHeight;

    /// <summary>돌아올 자리 — 손(무기 루트 = 손 소켓) 또는 떠 있는 자리. 움직이면 따라간다.</summary>
    Vector3 Home => weapon != null ? weapon.transform.position : transform.position;

    /// <summary>휘돌리는 축 — 플레이어 (보조 망치도 플레이어 둘레를 돈다).</summary>
    Vector3 Center
    {
        get
        {
            Vector3 c = weapon != null && weapon.Controller != null ? weapon.Controller.transform.position : Home;
            c.y = FlyY;
            return c;
        }
    }

    void Begin()
    {
        Vector3 start = weapon is MeleeWeapon melee ? melee.HeadWorld : weapon.transform.position;
        start.y = FlyY;

        transform.SetPositionAndRotation(start, Quaternion.LookRotation(dir, Vector3.up));
        BuildModel();   // 숨기기 전에 복제한다 (켜진 렌더러만 복제)
        BuildTrail();
        HideHand();
        CombatFx.PlaySound(cfg.throwSound);

        if (cfg.whirlTurns > 0.01f) StartWhirl(start);
        else Release();
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
    /// 위에서 내려다보는 카메라에 도는 망치로 또렷이 보인다. 손에 든 것보다 flyScale 배 크게.
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

        float scale = src.lossyScale.x * cfg.flyScale;
        model.localScale = src.lossyScale * cfg.flyScale;
        model.localRotation = Quaternion.FromToRotation(axis, Vector3.forward);
        model.localPosition = -Vector3.forward * (length * scale * 0.62f);   // 머리 쪽을 도는 축에
    }

    void BuildTrail()
    {
        if (cfg.material == null) return;

        trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = cfg.material;
        FxGlow.Apply(trail, FxGlow.Body);
        trail.time = 0.38f;
        trail.minVertexDistance = 0.05f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1.15f), new Keyframe(0.5f, 0.6f), new Keyframe(1f, 0f));
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(cfg.color, 0.35f), new GradientColorKey(cfg.color, 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;
    }

    // ───────── 휘돌리기 ─────────

    static float Yaw(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;

    /// <summary>손에서 출발해 몸 둘레를 돌다 던질 방향에서 놓는다 — 반 바퀴 ~ 한 바퀴 반 (평균 whirlTurns 바퀴), 도는 빠르기는 같다.</summary>
    void StartWhirl(Vector3 start)
    {
        Vector3 c = Center;
        Vector3 offset = start - c;
        offset.y = 0f;
        whirlStartRadius = Mathf.Clamp(offset.magnitude, 0.4f, cfg.whirlRadius);
        whirlFrom = offset.sqrMagnitude > 0.01f ? Yaw(offset) : Yaw(-dir);

        float toRelease = Mathf.Repeat(Yaw(dir) - whirlFrom, 360f);
        if (toRelease < 180f) toRelease += 360f;
        whirlTotal = toRelease + 360f * Mathf.Max(0f, cfg.whirlTurns - 1f);
        whirlDuration = cfg.whirlTime * whirlTotal / 360f;

        timer = 0f;
        whirlHit.Clear();
        phase = Phase.Whirl;
        HammerEffect.Whirls++;

        Vector3 feet = new Vector3(c.x, GroundY, c.z);
        if (cfg.material != null) FxLine.Ring(cfg.material, feet, 0.4f, cfg.whirlRadius * 1.15f, cfg.color, 0.12f, whirlDuration);
        GroundGlow.Flash(feet, cfg.whirlRadius * 1.3f, cfg.color, whirlDuration);
        if (cfg.UsesKit) FxKit.Swirl(feet, cfg.whirlRadius * 2.6f, cfg.color, whirlDuration + 0.15f, 3, 0.85f);
    }

    void Whirl(float dt)
    {
        timer += dt;
        float u = Mathf.Clamp01(timer / Mathf.Max(0.01f, whirlDuration));
        float yaw = whirlFrom + whirlTotal * u;
        float radius = Mathf.Lerp(whirlStartRadius, cfg.whirlRadius, Mathf.Clamp01(u * 4f));

        Vector3 from = transform.position;
        Vector3 to = Center + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * radius;
        transform.position = to;
        Face(to - from);
        Sweep(from, to, cfg.whirlDamageMul, whirlHit, HitKind.Whirl);

        // 손과 망치 사이의 번개 끈 · 바람
        tetherTimer -= dt;
        if (tetherTimer <= 0f)
        {
            tetherTimer = 0.035f;
            if (cfg.material != null)
            {
                Vector3 hand = Home;
                FxLine.Bolt(cfg.material, hand, to, cfg.color, 0.1f * Scale, 0.07f, 0.3f);
                FxLine.Bolt(cfg.material, hand, to, Color.white, 0.035f * Scale, 0.05f, 0.18f);
            }
            if (cfg.UsesKit)
            {
                Vector3 tangent = Quaternion.Euler(0f, yaw + 90f, 0f) * Vector3.forward;
                FxKit.WindStreaks(new Vector3(to.x, GroundY, to.z), tangent, 2.2f, 0.4f, cfg.color, 3f * Scale, 26f);
            }
        }

        if (u >= 1f) Release();
    }

    // ───────── 날아가기 · 튕기기 ─────────

    /// <summary>손(몸 둘레)을 떠나는 순간 — 앞쪽 적에게, 없으면 던진 거리까지.</summary>
    void Release()
    {
        Vector3 start = transform.position;
        ThrowFx(start);
        CombatFx.PlaySound(cfg.throwSound);

        hit.Clear();
        flyTime = 0f;
        targetsLeft = cfg.bounces;
        phase = Phase.Fly;

        Transform first = targetsLeft > 0 ? FindFirst(start) : null;
        if (first != null)
        {
            StartSegment(first, default);
            return;
        }

        float reach = CombatFx.ObstacleDistance(start, dir, cfg.throwDistance);
        StartSegment(null, start + dir * Mathf.Max(1.5f, reach - 0.3f));
    }

    /// <summary>던진 쪽(앞 ±55°) 던진 거리 안에서 가장 가까운 적 — 벽 너머는 뺀다.</summary>
    Transform FindFirst(Vector3 from)
    {
        CombatFx.FindEnemies(Ground(from), cfg.throwDistance, found);
        Transform best = null;
        float bestScore = float.MaxValue;
        foreach (Transform t in found)
        {
            Vector3 d = t.position - from;
            d.y = 0f;
            float distance = d.magnitude;
            if (distance < 0.3f) continue;
            float angle = Vector3.Angle(dir, d);
            if (angle > 55f) continue;
            if (!CombatFx.HasLineOfSight(from, FlyPoint(t))) continue;

            float score = distance * (1f + angle / 90f);
            if (score < bestScore) { bestScore = score; best = t; }
        }
        return best;
    }

    /// <summary>here 에서 가장 가까운, 이번에 아직 안 맞은 적 (튈 범위 · 벽 너머 제외).</summary>
    Transform FindNext(Vector3 here)
    {
        CombatFx.FindEnemies(Ground(here), cfg.bounceRange, found);
        Transform best = null;
        float bestSqr = float.MaxValue;
        foreach (Transform t in found)
        {
            if (hit.Contains(t)) continue;
            Vector3 d = t.position - here;
            d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr >= bestSqr) continue;
            if (!CombatFx.HasLineOfSight(here, FlyPoint(t))) continue;
            bestSqr = sqr;
            best = t;
        }
        return best;
    }

    Vector3 FlyPoint(Transform t) => new Vector3(t.position.x, FlyY, t.position.z);

    /// <summary>새 구간 — 지금 자리에서 끝까지, 옆으로 휘어 (구간마다 휘는 쪽이 바뀐다 — 지그재그).</summary>
    void StartSegment(Transform target, Vector3 point)
    {
        segFrom = transform.position;
        segTarget = target;
        segPoint = target != null ? FlyPoint(target) : point;
        segT = 0f;

        Vector3 d = segPoint - segFrom;
        d.y = 0f;
        segLength = Mathf.Max(0.5f, d.magnitude);
        Vector3 across = d.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, d.normalized) : Vector3.zero;
        segControl = (segFrom + segPoint) * 0.5f + across * (segLength * cfg.curve * side);
        segControl.y = FlyY;
        side = -side;
    }

    static Vector3 Bezier(Vector3 a, Vector3 c, Vector3 b, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * c + t * t * b;
    }

    /// <summary>휜 길을 따라 speed 로 한 걸음 — 끝이 움직여도(적 · 손) 따라간다. 끝에 닿으면 true.</summary>
    bool Advance(Vector3 end, float speed, float dt, float damageMul, HitKind kind)
    {
        segPoint = end;
        float approx = segLength * (1f + cfg.curve * 0.6f);
        segT = Mathf.Min(1f, segT + speed * dt / Mathf.Max(0.3f, approx));

        Vector3 from = transform.position;
        Vector3 to = Bezier(segFrom, segControl, segPoint, segT);
        transform.position = to;
        Face(to - from);
        Sweep(from, to, damageMul, hit, kind);
        return segT >= 1f;
    }

    void Fly(float dt)
    {
        flyTime += dt;
        Vector3 end = segTarget != null && CombatFx.IsAlive(segTarget) ? FlyPoint(segTarget) : segPoint;
        bool arrived = Advance(end, cfg.flySpeed, dt, cfg.throwDamageMul, HitKind.Out);
        if (!arrived && flyTime < MaxFlyTime) return;

        Vector3 at = transform.position;
        if (arrived && segTarget != null)
        {
            // 적에 닿았다 — 꼭 맞히고 (길에서 이미 맞았으면 넘어간다) 하늘에서 번개 · 다음 적으로 꺾인다
            if (CombatFx.IsAlive(segTarget) && hit.Add(segTarget)) HitEnemy(segTarget, cfg.throwDamageMul, HitKind.Out);
            BounceFx(at);
            HammerEffect.Bounces++;
            targetsLeft--;

            if (targetsLeft > 0 && flyTime < MaxFlyTime)
            {
                Transform next = FindNext(at);
                if (next != null)
                {
                    StartSegment(next, default);
                    return;
                }
            }
        }

        StartReturn(at);
    }

    /// <summary>끝 — 번쩍이며 돌아선다. 오는 길에는 같은 적도 다시 친다.</summary>
    void StartReturn(Vector3 at)
    {
        phase = Phase.Return;
        timer = 0f;
        hit.Clear();
        TurnFx(at);
        StartSegment(null, Home);
    }

    void Return(float dt)
    {
        timer += dt;
        // 처음엔 천천히, 점점 빠르게 손으로 빨려 온다
        float speed = cfg.returnSpeed * Mathf.Lerp(0.6f, 1.5f, Mathf.Clamp01(timer / 0.4f));
        Vector3 home = Home;
        bool arrived = Advance(home, speed, dt, cfg.returnDamageMul, HitKind.Back);

        if (!arrived && (transform.position - home).sqrMagnitude > 0.36f && timer < MaxReturnTime) return;

        HammerEffect.Caught++;
        CombatFx.PlaySound(cfg.catchSound);
        CatchFx();
        Destroy(gameObject);
    }

    void Face(Vector3 delta)
    {
        delta.y = 0f;
        if (delta.sqrMagnitude > 0.000001f) transform.rotation = Quaternion.LookRotation(delta, Vector3.up);
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
            case Phase.Whirl: Whirl(dt); break;
            case Phase.Fly: Fly(dt); break;
            case Phase.Return: Return(dt); break;
        }
        if (spin == null) return;

        // 빙글빙글 돌며 위아래로 뒤집힌다 (휘돌릴 때 더 빠르게)
        spinAngle += cfg.spinSpeed * dt * (phase == Phase.Whirl ? 1.25f : 1f);
        float wobble = Time.time * 11f;
        spin.localRotation = Quaternion.Euler(Mathf.Sin(wobble) * cfg.tumbleTilt, spinAngle, Mathf.Cos(wobble * 0.73f) * cfg.tumbleTilt * 0.6f);

        // 전기가 튄다 · 번개 꼬리
        Vector3 p = transform.position;
        if (cfg.UsesKit) FxKit.TrailDust(p, cfg.color, 170f * dt * Scale, 0.15f, true, 0.6f);

        boltTimer -= dt;
        if (boltTimer <= 0f && cfg.material != null)
        {
            boltTimer = 0.04f;
            FxLine.Bolt(cfg.material, p + Random.insideUnitSphere * 0.5f, p + Random.insideUnitSphere * 1.3f, Color.Lerp(cfg.color, Color.white, 0.4f), 0.09f * Scale, 0.07f, 0.3f);
            if (Random.value < 0.35f)
            {
                Vector2 c = Random.insideUnitCircle * 1.4f;
                FxLine.Bolt(cfg.material, p, new Vector3(p.x + c.x, GroundY + 0.05f, p.z + c.y), cfg.color, 0.07f * Scale, 0.08f, 0.35f);
            }
        }
    }

    // ───────── 연출 ─────────

    /// <summary>손(몸 둘레)을 떠나는 순간 — 앞으로 쏠리는 번개 줄기 · 발밑 빛.</summary>
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

    /// <summary>적에 닿아 꺾이는 순간 — 하늘에서 번개 · 고리 · 빛 · 작은 흔들림.</summary>
    void BounceFx(Vector3 at)
    {
        Vector3 ground = Ground(at);
        float s = Scale;
        if (cfg.material != null)
        {
            Vector3 sky = ground + Vector3.up * cfg.boltHeight * 0.8f + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            FxLine.Bolt(cfg.material, sky, ground + Vector3.up * 0.1f, cfg.color, 0.2f * s, 0.14f, 0.5f);
            FxLine.Bolt(cfg.material, sky, ground + Vector3.up * 0.1f, Color.white, 0.07f * s, 0.1f, 0.3f);
            FxLine.Ring(cfg.material, ground, 0.2f, 1.9f, cfg.color, 0.13f, 0.24f);
        }
        GroundGlow.Flash(ground, 2.3f, cfg.color, 0.2f);
        FlashLight.Flash(at, cfg.color, 5f, 0.12f, 0.7f * s);
        if (cfg.UsesKit)
        {
            FxKit.LightningStrike(ground, 1f, cfg.color, 0.85f * s, cfg.boltHeight * 0.8f);
            FxKit.EnergyBurst(ground, 1.1f, cfg.color, s);
            FxKit.Glints(at, 1f, Color.white, 10f * s, 0.6f);
        }
        WeaponBase.ShakeCamera(weapon, 0.18f);
        CombatFx.PlaySound(cfg.chainSound);
    }

    void TurnFx(Vector3 at)
    {
        Vector3 ground = Ground(at);
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

    // ───────── 맞히기 ─────────

    enum HitKind { Whirl, Out, Back }

    /// <summary>지나간 구간의 적을 한 번씩 친다 (같은 단계 안에서 한 번 — 오는 길 · 휘돌리기는 따로 센다).</summary>
    void Sweep(Vector3 from, Vector3 to, float damageMul, HashSet<Transform> once, HitKind kind)
    {
        CombatFx.FindEnemiesAlong(Ground(from), Ground(to), cfg.hitRadius, found);
        foreach (Transform enemy in found)
        {
            if (!once.Add(enemy)) continue;
            HitEnemy(enemy, damageMul, kind, to - from);
        }
    }

    void HitEnemy(Transform enemy, float damageMul, HitKind kind, Vector3 travel = default)
    {
        Vector3 p = CombatFx.Body(enemy);
        Vector3 d = EvoStrikes.Flat(travel.sqrMagnitude > 0.0001f ? travel : enemy.position - transform.position, dir);
        CombatFx.Damage(enemy, baseDamage * damageMul, p, d, false, 0f, HitSound.Electric, source: damageSource);
        cfg.StunTarget(enemy, cfg.throwStun);
        HitFx(p, d, kind != HitKind.Back);
        switch (kind)
        {
            case HitKind.Whirl: HammerEffect.WhirlHits++; break;
            case HitKind.Out: HammerEffect.ThrowHits++; break;
            default: HammerEffect.ReturnHits++; break;
        }
    }

    void HitFx(Vector3 p, Vector3 d, bool strong)
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
        if (cfg.UsesKit && EvoStrikes.Take(6)) FxKit.Hit(p, d, cfg.color, (strong ? 1.3f : 1.1f) * Scale);
    }

    Vector3 Ground(Vector3 p) => new Vector3(p.x, GroundY, p.z);

    void OnDestroy() => RestoreHand();
}
