using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 프리즘 노바 한 발의 실행 (PrismBurstSettings) — ChargeBeamWeapon 이 쏠 때 시작하고, 끝나면 알린다.
///
///  ① 분산: 빛마다 전방 부채꼴의 가까운 적을 찾아 날아가 맞히고, 맞힌 자리에서 다음 적으로 튕긴다.
///          적이 없으면 앞쪽을 흔들리며 떠돈다. 무기 둘레를 돌던 결정 3개가 이 빛이 된다 (그동안 숨긴다)
///  ② 수렴: 빛이 총구로 빨려 들어와 합쳐진다 — 흰 섬광 · 소리
///  ③ 방사: 세 색이 꼬인 굵은 빔. 방향은 플레이어가 보는 쪽(커서)을 turnRate 로 따라간다. tickInterval 마다 빔 위의 적 전부에 피해
/// 무기를 바꿔 들거나 사망하면 그 자리에서 끝난다.
/// </summary>
public class PrismNovaBurst : MonoBehaviour
{
    enum Phase { Idle, Scatter, Converge, Sustain }

    class Mote
    {
        public Transform body;
        public MeshRenderer renderer;
        public TrailRenderer trail;
        public Color color;
        public Vector3 position;
        public Vector3 heading;
        public Transform target;
        public readonly HashSet<Transform> visited = new HashSet<Transform>();
        public int hits;
        public float wobble;
        public float seekTimer;
        public Vector3 convergeFrom;
    }

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    ChargeBeamWeapon weapon;
    PrismBurstSettings cfg;
    Transform muzzle;
    Transform owner;
    Action finished;

    Phase phase = Phase.Idle;
    float phaseTime;
    float tickTimer;
    float range;
    Vector3 beamDir;

    readonly List<Mote> motes = new List<Mote>();
    readonly List<Transform> found = new List<Transform>();
    LineRenderer core;
    readonly List<LineRenderer> strands = new List<LineRenderer>();
    MaterialPropertyBlock block;
    EvolvedOrbit crystals;

    public bool Running => phase != Phase.Idle;

    // 시험용 기록
    public int MoteHits { get; private set; }
    public int BeamTicks { get; private set; }
    public int BeamHits { get; private set; }
    public int Converges { get; private set; }
    public Vector3 BeamDirection => beamDir;
    public bool Sustaining => phase == Phase.Sustain;

    void OnEnable() => GameEvents.OnPlayerDeadStart += Cancel;
    void OnDisable() => GameEvents.OnPlayerDeadStart -= Cancel;

    public void Begin(ChargeBeamWeapon source, PrismBurstSettings settings, Transform muzzlePoint, Transform ownerRoot, float beamRange, Action onFinished)
    {
        Cancel();

        weapon = source;
        cfg = settings;
        muzzle = muzzlePoint;
        owner = ownerRoot;
        range = beamRange;
        finished = onFinished;
        block ??= new MaterialPropertyBlock();
        if (crystals == null) crystals = GetComponentInChildren<EvolvedOrbit>(true);

        SpawnMotes();
        SetCrystalsVisible(false);
        CombatFx.PlaySound(cfg.scatterSound);

        phase = Phase.Scatter;
        phaseTime = 0f;
    }

    /// <summary>즉시 끝낸다 (무기 바꿈 · 사망 · 파괴). 끝 알림은 보내지 않는다.</summary>
    public void Cancel()
    {
        foreach (Mote m in motes)
            if (m.body != null) Destroy(m.body.gameObject);
        motes.Clear();

        ShowBeam(false);
        SetCrystalsVisible(true);
        phase = Phase.Idle;
    }

    void OnDestroy() => Cancel();

    void Update()
    {
        if (phase == Phase.Idle) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;   // 시간이 멈춘 동안(카드 · 정비 창) 그대로

        phaseTime += dt;

        switch (phase)
        {
            case Phase.Scatter:
                foreach (Mote m in motes) TickScatter(m, dt);
                if (phaseTime >= cfg.scatterTime) BeginConverge();
                break;

            case Phase.Converge:
                TickConverge();
                if (phaseTime >= cfg.convergeTime) BeginSustain();
                break;

            case Phase.Sustain:
                TickSustain(dt);
                if (phaseTime >= cfg.sustainTime) Finish();
                break;
        }
    }

    // ───────── ① 분산 ─────────

    void SpawnMotes()
    {
        Color[] colors = cfg.colors != null && cfg.colors.Length > 0 ? cfg.colors : new[] { Color.white };
        Vector3 forward = Forward();

        for (int i = 0; i < colors.Length; i++)
        {
            var mote = new Mote { color = colors[i], wobble = UnityEngine.Random.value * 10f };

            // 무기 둘레의 결정에서 나간다 (없으면 총구)
            Transform from = crystals != null && i < crystals.transform.childCount ? crystals.transform.GetChild(i) : muzzle;
            mote.position = from.position;
            float spread = colors.Length > 1 ? Mathf.Lerp(-35f, 35f, i / (float)(colors.Length - 1)) : 0f;
            mote.heading = Quaternion.Euler(0f, spread, 0f) * forward;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.name = "PrismMote";
            go.transform.position = mote.position;
            go.transform.localScale = Vector3.one * cfg.moteScale;

            mote.body = go.transform;
            mote.renderer = go.GetComponent<MeshRenderer>();
            mote.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (cfg.moteMaterial != null) mote.renderer.sharedMaterial = cfg.moteMaterial;
            block.Clear();
            block.SetColor(BaseColorId, mote.color);
            block.SetColor(EmissionId, mote.color * 2.2f);   // 너무 세면 톤매핑에서 흰색으로 날아가 색이 안 보인다
            mote.renderer.SetPropertyBlock(block);

            mote.trail = go.AddComponent<TrailRenderer>();
            mote.trail.sharedMaterial = cfg.lineMaterial;
            mote.trail.time = 0.35f;
            mote.trail.minVertexDistance = 0.05f;
            mote.trail.widthCurve = new AnimationCurve(new Keyframe(0f, cfg.moteScale * 1.3f), new Keyframe(1f, 0f));
            mote.trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.Lerp(mote.color, Color.white, 0.3f), 0f), new GradientColorKey(mote.color, 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            mote.trail.colorGradient = g;

            mote.target = FindTarget(OwnerPosition(), cfg.seekRange, mote);
            motes.Add(mote);
        }
    }

    void TickScatter(Mote m, float dt)
    {
        if (m.body == null) return;

        float height = OwnerPosition().y + 1f;

        if (m.target != null && !CombatFx.IsAlive(m.target)) m.target = null;

        if (m.target == null && m.hits < cfg.moteMaxHits)
        {
            m.seekTimer -= dt;
            if (m.seekTimer <= 0f)
            {
                m.seekTimer = 0.12f;
                m.target = FindTarget(m.hits > 0 ? m.position : OwnerPosition(), m.hits > 0 ? cfg.hopRange : cfg.seekRange, m);
            }
        }

        Vector3 desired;
        if (m.target != null)
        {
            Vector3 aim = CombatFx.Body(m.target);
            desired = aim - m.position;
            desired.y = 0f;

            if (desired.magnitude < 0.45f + cfg.moteSpeed * dt)
            {
                Strike(m);
                return;
            }
        }
        else
        {
            // 떠돌기 — 앞쪽을 흔들리며, 플레이어에서 너무 멀어지지 않게
            Vector3 home = OwnerPosition() + Forward() * 5f;
            Vector3 back = home - m.position;
            back.y = 0f;
            desired = Quaternion.Euler(0f, Mathf.Sin(Time.time * 5f + m.wobble) * 70f, 0f) * m.heading + back * 0.15f;
        }

        if (desired.sqrMagnitude > 0.0001f)
            m.heading = Vector3.Slerp(m.heading, desired.normalized, Mathf.Clamp01(dt * 10f)).normalized;

        m.position += m.heading * cfg.moteSpeed * dt;
        m.position.y = Mathf.Lerp(m.position.y, height, Mathf.Clamp01(dt * 8f));

        // 보이는 자리는 옆으로 조금 흔들린다 — "무작위로 움직이는 빛"
        Vector3 side = Vector3.Cross(Vector3.up, m.heading);
        m.body.position = m.position + side * Mathf.Sin(Time.time * 18f + m.wobble) * 0.18f;
    }

    void Strike(Mote m)
    {
        Transform target = m.target;
        m.visited.Add(target);
        m.hits++;
        m.target = null;
        m.seekTimer = 0f;
        m.position = CombatFx.Body(target);

        Vector3 dir = m.heading;
        if (weapon != null)
        {
            float dealt = weapon.RollPrismDamage(cfg.moteDamageMul, out bool crit);
            weapon.DealPrismDamage(target, dealt, crit, m.position, dir);
        }
        MoteHits++;

        FxLine.Ring(cfg.lineMaterial, target.position, 0.1f, 0.8f, m.color, 0.12f, 0.18f);
    }

    Transform FindTarget(Vector3 from, float radius, Mote m)
    {
        CombatFx.FindEnemies(from, radius, found);

        Vector3 ownerPos = OwnerPosition();
        Vector3 forward = Forward();
        float half = cfg.coneAngle * 0.5f;
        Transform best = null;
        float bestSqr = float.MaxValue;

        foreach (Transform t in found)
        {
            if (m.visited.Contains(t)) continue;

            Vector3 fromOwner = t.position - ownerPos;
            fromOwner.y = 0f;
            if (fromOwner.sqrMagnitude > 0.01f && Vector3.Angle(forward, fromOwner) > half) continue;

            float sqr = (t.position - from).sqrMagnitude;
            if (sqr >= bestSqr) continue;
            bestSqr = sqr;
            best = t;
        }

        return best;
    }

    // ───────── ② 수렴 ─────────

    void BeginConverge()
    {
        phase = Phase.Converge;
        phaseTime = 0f;
        foreach (Mote m in motes) m.convergeFrom = m.body != null ? m.body.position : m.position;
        CombatFx.PlaySound(cfg.convergeSound);
    }

    void TickConverge()
    {
        float k = Mathf.Clamp01(phaseTime / cfg.convergeTime);
        float ease = k * k;   // 점점 빨라지며 빨려 들어간다
        Vector3 end = muzzle != null ? muzzle.position : OwnerPosition() + Vector3.up;

        foreach (Mote m in motes)
        {
            if (m.body == null) continue;
            m.body.position = Vector3.Lerp(m.convergeFrom, end, ease);
            m.body.localScale = Vector3.one * cfg.moteScale * Mathf.Lerp(1f, 0.5f, ease);
        }
    }

    // ───────── ③ 방사 ─────────

    void BeginSustain()
    {
        foreach (Mote m in motes)
            if (m.body != null) Destroy(m.body.gameObject);
        motes.Clear();

        Converges++;
        Vector3 at = muzzle != null ? muzzle.position : OwnerPosition();
        FxLine.Ring(cfg.lineMaterial, at, 0.1f, 1.4f, Color.white, 0.3f, 0.25f);
        FxLine.Ring(cfg.lineMaterial, at, 1.6f, 0.1f, Color.white, 0.15f, 0.2f);
        CombatFx.PlaySound(cfg.beamSound);
        CombatFx.PlaySound(cfg.beamBassSound);
        CombatFx.PlaySound(cfg.beamHumSound);
        if (cfg.cameraShake > 0f && CameraShakeManager.Instance != null) CameraShakeManager.Instance.Shake(cfg.cameraShake);

        phase = Phase.Sustain;
        phaseTime = 0f;
        tickTimer = 0f;
        beamDir = Forward();
        EnsureBeam();
        ShowBeam(true);
    }

    void TickSustain(float dt)
    {
        Vector3 want = Forward();
        beamDir = Vector3.RotateTowards(beamDir, want, cfg.turnRate * Mathf.Deg2Rad * dt, 0f).normalized;

        Vector3 origin = muzzle != null ? muzzle.position : OwnerPosition() + Vector3.up;
        Vector3 end = origin + beamDir * range;

        tickTimer -= dt;
        if (tickTimer <= 0f)
        {
            tickTimer += cfg.tickInterval;
            BeamTicks++;

            float ground = OwnerPosition().y;
            CombatFx.FindEnemiesAlong(new Vector3(origin.x, ground, origin.z), new Vector3(end.x, ground, end.z), cfg.hitRadius, found);

            bool spark = BeamTicks % 3 == 0;
            int colorIndex = BeamTicks / 3;

            foreach (Transform t in found)
            {
                if (weapon == null) break;
                float dealt = weapon.RollPrismDamage(cfg.tickDamageMul, out bool crit);
                weapon.DealPrismDamage(t, dealt, crit, CombatFx.Body(t), beamDir);
                BeamHits++;

                // 맞은 자리에 세 색이 번갈아 터지는 작은 고리 (세 틱마다)
                if (spark && cfg.colors != null && cfg.colors.Length > 0)
                    FxLine.Ring(cfg.lineMaterial, t.position, 0.2f, 1.1f, cfg.colors[colorIndex++ % cfg.colors.Length], 0.18f, 0.2f);
            }
        }

        DrawBeam(origin, end);
    }

    void Finish()
    {
        ShowBeam(false);
        SetCrystalsVisible(true);
        phase = Phase.Idle;
        finished?.Invoke();
    }

    void EnsureBeam()
    {
        if (core != null) return;

        core = MakeLine("PrismCore");
        int count = cfg.colors != null ? cfg.colors.Length : 0;
        for (int i = 0; i < count; i++) strands.Add(MakeLine($"PrismStrand{i + 1}"));
    }

    LineRenderer MakeLine(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.sharedMaterial = cfg.lineMaterial;
        line.numCapVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.positionCount = 28;
        line.enabled = false;
        return line;
    }

    void ShowBeam(bool on)
    {
        if (core != null) core.enabled = on;
        foreach (LineRenderer s in strands) if (s != null) s.enabled = on;
    }

    /// <summary>세 색이 흰 중심을 감고 도는 빔. 처음 0.12초 커지고 마지막 0.25초 가늘어진다.</summary>
    void DrawBeam(Vector3 origin, Vector3 end)
    {
        if (core == null) return;

        float fadeIn = Mathf.Clamp01(phaseTime / 0.12f);
        float fadeOut = Mathf.Clamp01((cfg.sustainTime - phaseTime) / 0.25f);
        float k = fadeIn * fadeOut;
        float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 30f);

        Vector3 dir = end - origin;
        Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
        int n = core.positionCount;

        for (int i = 0; i < n; i++) core.SetPosition(i, origin + dir * (i / (float)(n - 1)));
        core.startWidth = cfg.beamWidth * 0.55f * k * pulse;
        core.endWidth = cfg.beamWidth * 0.4f * k * pulse;
        Color white = new Color(1f, 1f, 1f, 0.95f * k);
        core.startColor = white;
        core.endColor = white;

        for (int s = 0; s < strands.Count; s++)
        {
            LineRenderer line = strands[s];
            Color c = cfg.colors[s];
            float phaseOffset = s * Mathf.PI * 2f / strands.Count;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float along = t * range;
                // 총구에서는 한 점에 모이고, 앞으로 갈수록 크게 감긴다
                float amp = cfg.beamWidth * 0.45f * Mathf.Clamp01(t * 6f);
                float angle = along * 0.9f - Time.time * 14f + phaseOffset;
                Vector3 offset = side * Mathf.Sin(angle) * amp + Vector3.up * Mathf.Cos(angle) * amp * 0.5f;
                line.SetPosition(i, origin + dir * t + offset);
            }

            line.startWidth = cfg.beamWidth * 0.28f * k;
            line.endWidth = cfg.beamWidth * 0.18f * k;
            c.a = 0.9f * k;
            line.startColor = c;
            line.endColor = c;
        }
    }

    // ───────── 도움 ─────────

    Vector3 OwnerPosition() => owner != null ? owner.position : transform.position;

    Vector3 Forward()
    {
        Vector3 f = owner != null ? owner.forward : transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }

    void SetCrystalsVisible(bool visible)
    {
        if (crystals == null) return;
        foreach (Renderer r in crystals.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
    }
}
