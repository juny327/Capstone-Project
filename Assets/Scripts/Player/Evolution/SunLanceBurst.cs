using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 태양창 한 발의 실행 (SunLanceSettings) — ChargeBeamWeapon 이 쏠 때 시작하고, 끝나면 알린다.
///
///  ① 모으기: 총구에 빛 고리가 안으로 모이고, 바닥 예고선이 minRange → maxRange 로 앞으로 뻗는다. 방향은 커서를 따라간다
///  ② 발사: 예고선 끝까지 굵은 기둥 한 방 — 기둥 위 적 전부 피해 + 화상, 바닥 그을음, 큰 흔들림
/// 무기를 바꿔 들거나 사망하면 그 자리에서 끝난다 (쏘지 않는다).
/// </summary>
public class SunLanceBurst : MonoBehaviour
{
    enum Phase { Idle, WindUp, Beam }

    ChargeBeamWeapon weapon;
    SunLanceSettings cfg;
    Transform muzzle;
    Transform owner;
    Action finished;

    Phase phase = Phase.Idle;
    float phaseTime;
    float ringTimer;
    Vector3 aim;
    Vector3 beamFrom;
    Vector3 beamTo;

    readonly List<Transform> found = new List<Transform>();
    LineRenderer telegraph;
    LineRenderer outer;
    LineRenderer inner;

    public bool Running => phase != Phase.Idle;

    // 시험용 기록
    public int Shots { get; private set; }
    public int Hits { get; private set; }
    public float LastRange { get; private set; }
    public float Charge => phase == Phase.WindUp && cfg != null ? Mathf.Clamp01(phaseTime / cfg.windUpTime) : 0f;

    void OnEnable() => GameEvents.OnPlayerDeadStart += Cancel;
    void OnDisable() => GameEvents.OnPlayerDeadStart -= Cancel;

    public void Begin(ChargeBeamWeapon source, SunLanceSettings settings, Transform muzzlePoint, Transform ownerRoot, Action onFinished)
    {
        Cancel();

        weapon = source;
        cfg = settings;
        muzzle = muzzlePoint;
        owner = ownerRoot;
        finished = onFinished;
        aim = Forward();

        EnsureLines();
        CombatFx.PlaySound(cfg.gatherSound);

        phase = Phase.WindUp;
        phaseTime = 0f;
        ringTimer = 0f;
    }

    /// <summary>즉시 끝낸다 (무기 바꿈 · 사망 · 파괴). 끝 알림은 보내지 않는다.</summary>
    public void Cancel()
    {
        if (telegraph != null) telegraph.enabled = false;
        if (outer != null) outer.enabled = false;
        if (inner != null) inner.enabled = false;
        phase = Phase.Idle;
    }

    void OnDestroy() => Cancel();

    void Update()
    {
        if (phase == Phase.Idle) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;   // 시간이 멈춘 동안(카드 · 정비 창) 그대로

        phaseTime += dt;

        if (phase == Phase.WindUp)
        {
            TickWindUp(dt);
            if (phaseTime >= cfg.windUpTime) Fire(1f);
            else if (ShouldReleaseEarly()) Fire(Charge);
        }
        else
        {
            TickBeam();
            if (phaseTime >= cfg.beamTime) Finish();
        }
    }

    // ───────── ① 모으기 ─────────

    void TickWindUp(float dt)
    {
        aim = Vector3.RotateTowards(aim, Forward(), cfg.turnRate * Mathf.Deg2Rad * dt, 0f).normalized;

        float charge = Mathf.Clamp01(phaseTime / cfg.windUpTime);
        float range = cfg.RangeAt(charge);

        // 바닥 예고선 — 끝이 앞으로 뻗어 나간다. 다 모아 갈수록 진하고 굵다
        Vector3 from = Ground(Origin());
        Vector3 to = from + aim * range;
        Color c = cfg.color;
        c.a = Mathf.Lerp(0.25f, 0.85f, charge);
        Draw(telegraph, from + Vector3.up * 0.06f, to + Vector3.up * 0.06f, c, c, Mathf.Lerp(0.12f, 0.4f, charge));

        // 총구에 빛 고리가 모인다
        ringTimer -= dt;
        if (ringTimer <= 0f)
        {
            ringTimer = 0.12f;
            Color r = Color.Lerp(cfg.color, Color.white, charge * 0.6f);
            FxLine.Ring(cfg.lineMaterial, Origin() - Vector3.up * 0.2f, 1.6f - charge * 0.6f, 0.15f, r, 0.08f + charge * 0.1f, 0.14f);
        }

        // 끝점 표시 — "여기까지 닿는다"
        if (Mathf.Repeat(phaseTime, 0.2f) < dt)
            FxLine.Ring(cfg.lineMaterial, to, 0.1f, 0.9f, c, 0.08f, 0.18f);
    }

    // 바짝 붙은 적이 있으면 모은 만큼만 — 끝까지 모으다 맞지 않게 (earlyReleaseRange)
    bool ShouldReleaseEarly()
    {
        if (cfg.earlyReleaseRange <= 0f || phaseTime < cfg.minWindUp) return false;
        return CombatFx.FindNearest(OwnerPosition(), cfg.earlyReleaseRange) != null;
    }

    // ───────── ② 발사 ─────────

    void Fire(float charge)
    {
        float range = cfg.RangeAt(charge);
        float mul = cfg.damageMul * Mathf.Lerp(cfg.minChargeDamage, 1f, charge);

        Vector3 origin = Origin();
        beamFrom = origin;
        beamTo = origin + aim * range;
        LastRange = range;
        Shots++;

        if (telegraph != null) telegraph.enabled = false;

        Vector3 ground = Ground(origin);
        CombatFx.FindEnemiesAlong(ground, ground + aim * range, cfg.hitRadius, found);

        float burnDps = weapon != null ? weapon.Stats.Damage * cfg.burnRatio : 0f;
        foreach (Transform t in found)
        {
            if (weapon == null) break;

            float dealt = weapon.RollPrismDamage(mul, out bool crit);
            weapon.DealPrismDamage(t, dealt, crit, CombatFx.Body(t), aim);
            BurnTicker.Apply(t, burnDps, cfg.burnDuration);
            FxLine.Ring(cfg.lineMaterial, t.position, 0.2f, 1.2f, cfg.color, 0.16f, 0.22f);
            Hits++;
        }

        // 바닥 그을음 — 오래 남아 "여기를 지났다"
        if (cfg.scorchTime > 0f)
            FxLine.Line(cfg.lineMaterial, ground + Vector3.up * 0.04f, ground + aim * range + Vector3.up * 0.04f, cfg.scorchColor, cfg.beamWidth * 0.45f, cfg.scorchTime);

        // 총구 섬광 · 끝 폭발 고리
        FxLine.Ring(cfg.lineMaterial, origin - Vector3.up * 0.3f, 0.3f, 2.4f, Color.white, 0.35f, 0.3f);
        FxLine.Ring(cfg.lineMaterial, Ground(beamTo), 0.4f, 2.2f, cfg.color, 0.3f, 0.35f);

        if (cfg.cameraShake > 0f && CameraShakeManager.Instance != null) CameraShakeManager.Instance.Shake(cfg.cameraShake);
        CombatFx.PlaySound(cfg.fireSound);

        phase = Phase.Beam;
        phaseTime = 0f;
        TickBeam();
    }

    void TickBeam()
    {
        // 앞 30% 는 굵게, 나머지 동안 가늘어지며 사라진다
        float left = 1f - Mathf.Clamp01(phaseTime / cfg.beamTime);
        float k = Mathf.Clamp01(left / 0.7f);

        Color o = cfg.color;
        o.a = k;
        Color i = Color.white;
        i.a = k;

        Draw(outer, beamFrom, beamTo, o, o, cfg.beamWidth * k);
        Draw(inner, beamFrom, beamTo, i, i, cfg.beamWidth * 0.35f * k);
    }

    void Finish()
    {
        Cancel();
        finished?.Invoke();
    }

    // ───────── 그리기 ─────────

    void EnsureLines()
    {
        if (telegraph == null) telegraph = MakeLine("SunTelegraph");
        if (outer == null) outer = MakeLine("SunBeam");
        if (inner == null) inner = MakeLine("SunCore");
        telegraph.sharedMaterial = outer.sharedMaterial = inner.sharedMaterial = cfg.lineMaterial;
    }

    LineRenderer MakeLine(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.numCapVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.positionCount = 2;
        line.enabled = false;
        return line;
    }

    static void Draw(LineRenderer line, Vector3 from, Vector3 to, Color start, Color end, float width)
    {
        if (line == null) return;
        line.enabled = true;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.startColor = start;
        line.endColor = end;
        line.startWidth = width;
        line.endWidth = width * 0.8f;
    }

    Vector3 Origin() => muzzle != null ? muzzle.position : OwnerPosition() + Vector3.up;

    Vector3 Ground(Vector3 p) => new Vector3(p.x, OwnerPosition().y, p.z);

    Vector3 OwnerPosition() => owner != null ? owner.position : transform.position;

    Vector3 Forward()
    {
        Vector3 f = owner != null ? owner.forward : transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }
}
