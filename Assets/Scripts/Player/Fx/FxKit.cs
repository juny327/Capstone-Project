using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 연출 공통 재료 (2026-11-19 상위 무기 연출 개선 1, 11-20 Kenney 입자 그림 · 더 화려하게) —
/// 불 덩어리 · 솟는 불길 · 연기 · 튀는 불꽃 · 파편 · 잔불 · 섬광 · 충격파 · 번개 줄기 · 전기 가지 · 바닥 그을음 · 바닥 그늘.
///
/// 지금까지의 연출은 거의 다 코드로 그린 선 · 고리(FxLine)라 덩어리감이 없었다. 여기서는 입자 그림(Arts/FxKit)을 쓴다.
///
/// 방식 — <b>채널마다 ParticleSystem 하나</b>를 씬에 만들어 두고, 연출이 필요할 때 그 자리에 입자를 뿌린다 (Emit).
///  · 폭발마다 오브젝트를 만들지 않는다 → 풀도, 처음 쓸 때의 끊김도 없다. 그리는 호출은 채널 수로 고정이다
///  · 비용은 입자 수에만 비례한다 (GPU 쪽). 적 수와는 무관하다
///  · 그래픽 품질 '중간 · 낮음' 에서는 입자 수를 줄이고, '낮음' 에서는 바닥 그을음 · 그늘을 그리지 않는다
///
/// 설정은 Resources/Fx/FxKitSet. 없으면 Ready 가 false — 쓰는 쪽이 예전 연출로 돌아간다.
/// </summary>
public class FxKit : MonoBehaviour
{
    /// <summary>
    /// 채널 — 적힌 순서대로 그린다 (뒤가 위). 2026-11-20 나머지 10종으로 넓히며 7개 더함 (빛 원반 · 소용돌이 · 베기 호 · 기운 · 빛기둥 · 할퀸 자국 · 반짝임).
    /// 같은 날 도깨비불 2개 (불 · 솟는 불길과 같은 그림, 색을 그대로 쓴다 — 그리는 순서는 불 · 불길 자리).
    /// </summary>
    public enum Ch { Shade, Scorch, Magic, Ring, Wave, Twirl, Slash, Smoke, Shard, Fire, Flame, Wisp, Twig, Bolt, Beam, Ember, Spark, Claw, Star, Burst, Flare, GhostFire, GhostFlame }

    const int Channels = 23;

    static FxKitSet set;
    static bool loaded;
    static FxKit host;

    /// <summary>켜면 공통 재료를 쓰지 않는다 — 전후 비교 캡처에서 '전' 을 찍을 때 (쓰는 쪽이 모두 예전 연출로 돌아간다).</summary>
    public static bool ForceOff;

    ParticleSystem[] systems;
    readonly List<Pending> pending = new List<Pending>();

    struct Pending
    {
        public float at;
        public Ch ch;
        public Vector3 pos, vel;
        public float size, life, rot, height;
        public Color color;
    }

    /// <summary>시험용 — 채널마다 뿌린 입자 수 · 폭발 · 낙뢰 횟수.</summary>
    public static readonly int[] Emitted = new int[Channels];
    public static int Explosions { get; private set; }
    public static int Strikes { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        set = null;
        loaded = false;
        host = null;
        ForceOff = false;
        System.Array.Clear(Emitted, 0, Emitted.Length);
        Explosions = Strikes = 0;
        burstStart = -99f;
        burstCount = 0;
    }

    public static FxKitSet Set
    {
        get
        {
            if (!loaded)
            {
                set = Resources.Load<FxKitSet>(FxKitSet.ResourcePath);
                loaded = true;
            }
            return set;
        }
    }

    /// <summary>공통 재료를 쓸 수 있는가 (설정 에셋과 재질이 다 있다).</summary>
    public static bool Ready => !ForceOff && Set != null && Set.Complete;

    /// <summary>보조 칸 상위 무기의 연출 배율 (FxKitSet.supportScale — 기본 1, 줄이지 않는다). 설정이 없어도 1.</summary>
    public static float SupportScale => Set != null ? Mathf.Clamp(Set.supportScale, 0.1f, 1f) : 1f;

    /// <summary>입자 수 배율 — 설정 × 그래픽 품질.</summary>
    public static float Amount
    {
        get
        {
            FxKitSet s = Set;
            if (s == null) return 0f;
            GraphicsLevel q = GraphicsQuality.Current;
            return s.amount * (q == GraphicsLevel.High ? 1f : q == GraphicsLevel.Medium ? s.mediumAmount : s.lowAmount);
        }
    }

    static bool Decals => Set != null && GraphicsQuality.Current != GraphicsLevel.Low;

    static FxKit Host
    {
        get
        {
            if (host != null) return host;
            if (!Ready) return null;

            var go = new GameObject("FxKit");
            host = go.AddComponent<FxKit>();
            host.Build();
            return host;
        }
    }

    /// <summary>
    /// 미리 만들어 둔다 — 입장 가림막 아래에서 (StageWarmup). 채널마다 보이지 않는 입자 하나를 그 자리에 뿌려
    /// 재질 · 셰이더를 한 번 그리게 한다 (처음 폭발할 때 끊기지 않게).
    /// </summary>
    public static void Prewarm(Vector3 at)
    {
        FxKit h = Host;
        if (h == null) return;
        for (int i = 0; i < Channels; i++)
        {
            var p = new ParticleSystem.EmitParams { position = at, startSize = 0.01f, startLifetime = 0.05f, startColor = new Color(1f, 1f, 1f, 0.01f) };
            h.systems[i].Emit(p, 1);
        }
    }

    // ───────── 만들기 ─────────

    void Build()
    {
        FxKitSet s = Set;
        systems = new ParticleSystem[Channels];
        const ParticleSystemRenderMode Lying = ParticleSystemRenderMode.HorizontalBillboard;   // 바닥에 눕는다
        const ParticleSystemRenderMode Facing = ParticleSystemRenderMode.Billboard;            // 카메라를 본다

        // 바닥 그늘 — 먹구름 아래가 어두워진다
        Put(Ch.Shade, Make(Ch.Shade, s.shade, 16, Lying, 0f, 0f, 1, 1, 1f, Fade((0f, 0f), (0.25f, 1f), (0.7f, 1f), (1f, 0f)), null, 0f));

        // 바닥 그을음 — 천천히 나타났다 오래 남고 사라진다
        Put(Ch.Scorch, Make(Ch.Scorch, s.scorch, 128, Lying, 0f, 0f, 1, 1, 1f, Fade((0f, 0f), (0.04f, 1f), (0.65f, 1f), (1f, 0f)), null, 0f));

        // 바닥의 빛 원반 — 천천히 돈다 (모으기 · 중력장 · 폭주)
        Put(Ch.Magic, Make(Ch.Magic, s.magic, 48, Lying, 0f, 0f, 2, 1, s.magicGlow,
            Fade((0f, 0f), (0.15f, 1f), (0.7f, 0.8f), (1f, 0f)), Curve((0f, 0.6f), (0.3f, 1f), (1f, 1.06f)), -50f));

        // 충격파 — 또렷한 고리 · 일렁이는 고리
        Put(Ch.Ring, Make(Ch.Ring, s.ring, 128, Lying, 0f, 0f, 1, 1, s.ringGlow,
            Fade((0f, 1f), (0.35f, 0.75f), (1f, 0f)), Curve((0f, 0.1f), (0.3f, 0.68f), (1f, 1f)), 0f));
        Put(Ch.Wave, Make(Ch.Wave, s.wave, 72, Lying, 0f, 0f, 1, 1, s.waveGlow,
            Fade((0f, 0.9f), (0.5f, 0.55f), (1f, 0f)), Curve((0f, 0.18f), (0.4f, 0.7f), (1f, 1f)), 0f));

        // 소용돌이 — 바닥에서 한쪽으로 빠르게 돌며 퍼진다 (바람 · 중력)
        Put(Ch.Twirl, Make(Ch.Twirl, s.twirl, 128, Lying, 0f, 0f, 2, 2, s.twirlGlow,
            Fade((0f, 0f), (0.2f, 1f), (0.6f, 0.7f), (1f, 0f)), Curve((0f, 0.55f), (1f, 1.15f)), -480f));

        // 베기 호 — 볼록한 쪽으로 밀려 나가며 커진다
        Put(Ch.Slash, Make(Ch.Slash, s.slash, 128, Lying, 0f, 2.5f, 2, 2, s.slashGlow,
            Fade((0f, 1f), (0.4f, 0.85f), (1f, 0f)), Curve((0f, 0.65f), (0.3f, 1f), (1f, 1.14f)), 0f));

        // 연기 — 불보다 늦게 올라와 천천히 퍼진다
        Put(Ch.Smoke, Make(Ch.Smoke, s.smoke, 560, Facing, -0.05f, 1.4f, 2, 2, 1f,
            Fade((0f, 0f), (0.18f, 1f), (0.6f, 0.75f), (1f, 0f)), Curve((0f, 0.55f), (1f, 1.7f)), 26f));

        // 파편 뭉치 — 튀어 오르며 흩어진다
        Put(Ch.Shard, Make(Ch.Shard, s.shard, 160, Facing, 1.6f, 0.8f, 2, 2, 1f,
            Fade((0f, 1f), (0.6f, 1f), (1f, 0f)), Curve((0f, 0.55f), (1f, 1.35f)), 110f));

        // 불 — 흰 노랑 → 주황 → 검붉게 식는다
        var hot = new Gradient();
        hot.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.97f, 0.85f), 0f), new GradientColorKey(new Color(1f, 0.78f, 0.38f), 0.28f),
                new GradientColorKey(new Color(1f, 0.4f, 0.12f), 0.62f), new GradientColorKey(new Color(0.42f, 0.09f, 0.05f), 1f),
            },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.75f, 0.5f), new GradientAlphaKey(0.16f, 0.82f), new GradientAlphaKey(0f, 1f) });
        Put(Ch.Fire, Make(Ch.Fire, s.fire, 900, Facing, -0.12f, 3f, 2, 2, s.fireGlow, hot, Curve((0f, 0.45f), (0.25f, 0.95f), (1f, 1.2f)), 55f));

        // 솟는 불길 — 바닥에서 위로 선다 (돌지 않는다)
        Put(Ch.Flame, Make(Ch.Flame, s.flame, 320, Facing, -0.25f, 1f, 2, 2, s.flameGlow, hot, Curve((0f, 0.45f), (0.3f, 1f), (1f, 1.15f)), 0f, true));

        // 도깨비불 (2026-11-20 도깨비 방망이) — 불 · 솟는 불길과 같은 그림, 색을 그대로 쓴다 (불은 주황 그라디언트라 연두로 칠할 수 없다)
        Gradient ghost = Fade((0f, 0f), (0.1f, 1f), (0.55f, 0.75f), (1f, 0f));
        ParticleSystem ghostFire = Make(Ch.GhostFire, s.fire, 600, Facing, -0.2f, 2.5f, 2, 2, s.fireGlow, ghost, Curve((0f, 0.45f), (0.25f, 0.95f), (1f, 1.2f)), 55f);
        ghostFire.GetComponent<ParticleSystemRenderer>().sortingOrder = (int)Ch.Fire;
        Put(Ch.GhostFire, ghostFire);
        ParticleSystem ghostFlame = Make(Ch.GhostFlame, s.flame, 320, Facing, -0.3f, 1f, 2, 2, s.flameGlow, ghost, Curve((0f, 0.45f), (0.3f, 1f), (1f, 1.15f)), 0f, true);
        ghostFlame.GetComponent<ParticleSystemRenderer>().sortingOrder = (int)Ch.Flame;
        Put(Ch.GhostFlame, ghostFlame);

        // 일렁이는 기운 — 색을 그대로 쓰는 불 (피 · 바람 · 중력 · 빛). 흔들리며 오른다
        Put(Ch.Wisp, Make(Ch.Wisp, s.wisp, 480, Facing, -0.18f, 1.2f, 2, 2, s.wispGlow,
            Fade((0f, 0f), (0.15f, 1f), (0.55f, 0.7f), (1f, 0f)), Curve((0f, 0.5f), (1f, 1.3f)), 70f));

        // 전기 가지 — 바닥(또는 구름)에 눕는다. 아주 짧게 번쩍인다
        Put(Ch.Twig, Make(Ch.Twig, s.twig, 240, Lying, 0f, 0f, 2, 2, s.twigGlow, Fade((0f, 1f), (0.3f, 0.45f), (0.5f, 1f), (1f, 0f)), null, 0f));

        // 번개 줄기 — 맞은 자리에서 위로 선다. 깜박이며 사라진다
        Put(Ch.Bolt, Make(Ch.Bolt, s.bolt, 48, Facing, 0f, 0f, 2, 1, s.boltGlow, Fade((0f, 1f), (0.25f, 0.5f), (0.45f, 1f), (1f, 0f)), null, 0f, true));

        // 빛기둥 — 바닥에서 위로 서는 빛줄기
        Put(Ch.Beam, Make(Ch.Beam, s.beam, 160, Facing, 0f, 0f, 2, 2, s.beamGlow, Fade((0f, 0f), (0.12f, 1f), (0.5f, 0.8f), (1f, 0f)), null, 0f, true));

        // 잔불 · 불티 — 천천히 흔들리며 오른다
        ParticleSystem ember = Make(Ch.Ember, s.glow, 1200, Facing, -0.06f, 0.6f, 1, 1, s.emberGlow,
            Fade((0f, 0f), (0.1f, 1f), (0.45f, 0.55f), (0.6f, 1f), (1f, 0f)), Curve((0f, 1f), (1f, 0.35f)), 0f);
        ParticleSystem.NoiseModule noise = ember.noise;
        noise.enabled = true;
        noise.strength = 0.6f;
        noise.frequency = 0.9f;
        noise.scrollSpeed = 0.4f;
        Put(Ch.Ember, ember);

        // 튀는 불꽃 — 날아가는 방향으로 늘여 그린다
        Put(Ch.Spark, Make(Ch.Spark, s.spark, 1800, ParticleSystemRenderMode.Stretch, 1.1f, 1.1f, 1, 1, s.sparkGlow,
            Fade((0f, 1f), (0.5f, 0.9f), (1f, 0f)), Curve((0f, 1f), (1f, 0.35f)), 0f));

        // 할퀸 자국 — 맞은 자리에 비스듬한 세 줄
        Put(Ch.Claw, Make(Ch.Claw, s.claw, 96, Facing, 0f, 0f, 1, 1, s.clawGlow,
            Fade((0f, 1f), (0.5f, 0.9f), (1f, 0f)), Curve((0f, 0.6f), (0.2f, 1f), (1f, 1.1f)), 0f));

        // 반짝임 — 깜박이며 사라지는 별빛
        Put(Ch.Star, Make(Ch.Star, s.star, 420, Facing, 0f, 0f, 2, 2, s.starGlow,
            Fade((0f, 0f), (0.2f, 1f), (0.5f, 0.5f), (0.7f, 1f), (1f, 0f)), Curve((0f, 0.3f), (0.3f, 1f), (1f, 0.2f)), 120f));

        // 터지는 순간의 뾰족한 섬광 · 별 모양 섬광
        Put(Ch.Burst, Make(Ch.Burst, s.burst, 128, Facing, 0f, 0f, 2, 2, s.burstGlow,
            Fade((0f, 1f), (0.4f, 0.7f), (1f, 0f)), Curve((0f, 0.5f), (0.3f, 1f), (1f, 1.12f)), 0f));
        Put(Ch.Flare, Make(Ch.Flare, s.flare, 128, Facing, 0f, 0f, 1, 1, s.flareGlow,
            Fade((0f, 1f), (0.35f, 0.6f), (1f, 0f)), Curve((0f, 1f), (1f, 0.45f)), 0f));
    }

    void Put(Ch ch, ParticleSystem ps) => systems[(int)ch] = ps;

    static Gradient Fade(params (float t, float a)[] keys)
    {
        var g = new Gradient();
        var alpha = new GradientAlphaKey[keys.Length];
        for (int i = 0; i < keys.Length; i++) alpha[i] = new GradientAlphaKey(keys[i].a, keys[i].t);
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alpha);
        return g;
    }

    static AnimationCurve Curve(params (float t, float v)[] keys)
    {
        var c = new AnimationCurve();
        foreach ((float t, float v) in keys) c.AddKey(t, v);
        for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
        return c;
    }

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    /// <param name="upright">바닥에서 위로 서는 입자 (번개 줄기 · 불길) — 아래 끝이 뿌린 자리에 오고, 폭 · 높이를 따로 준다.</param>
    ParticleSystem Make(Ch ch, Material material, int max, ParticleSystemRenderMode mode, float gravity, float drag, int tilesX, int tilesY, float glow,
        Gradient color, AnimationCurve size, float spin, bool upright = false)
    {
        var go = new GameObject(ch.ToString());
        go.transform.SetParent(transform, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        main.gravityModifier = gravity;
        main.startSpeed = 0f;
        main.startLifetime = 1f;
        main.startSize = 1f;
        if (upright) main.startSize3D = true;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;   // 스스로는 내지 않는다 — Emit 으로만
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;

        if (drag > 0f)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 1000f;
            limit.dampen = 0f;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
        }

        if (tilesX > 1 || tilesY > 1)
        {
            // 그림 묶음에서 입자마다 한 칸을 고른다 (움직이지는 않는다)
            ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = tilesX;
            sheet.numTilesY = tilesY;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.9999f);
            sheet.cycleCount = 1;
        }

        if (color != null)
        {
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = color;
        }

        if (size != null)
        {
            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, size);
        }

        if (spin != 0f)
        {
            // 양수 = 입자마다 아무 쪽으로 (−spin ~ +spin), 음수 = 모두 한쪽으로 그 빠르기로 (소용돌이 · 빛 원반)
            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = spin > 0f ? new ParticleSystem.MinMaxCurve(-spin * Mathf.Deg2Rad, spin * Mathf.Deg2Rad)
                              : new ParticleSystem.MinMaxCurve(-spin * Mathf.Deg2Rad);
        }

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = mode;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = (int)ch;
        renderer.maxParticleSize = 3f;
        if (mode == ParticleSystemRenderMode.Stretch)
        {
            renderer.velocityScale = 0.045f;
            renderer.lengthScale = 1.3f;
            renderer.cameraVelocityScale = 0f;
        }
        if (upright)
        {
            renderer.pivot = new Vector3(0f, 0.5f, 0f);   // 아래 끝이 뿌린 자리
            renderer.flip = new Vector3(0.5f, 0f, 0f);    // 절반은 좌우를 뒤집는다
        }

        if (!Mathf.Approximately(glow, 1f) && material != null && material.HasProperty(BaseColor))
        {
            var block = new MaterialPropertyBlock();
            Color c = material.GetColor(BaseColor);
            block.SetColor(BaseColor, new Color(c.r * glow, c.g * glow, c.b * glow, c.a));
            renderer.SetPropertyBlock(block);
        }

        ps.Play();
        return ps;
    }

    // ───────── 뿌리기 ─────────

    /// <param name="rot">도는 각도(도). NaN 이면 아무 각도.</param>
    /// <param name="height">0 보다 크면 폭(size) · 높이를 따로 — 위로 서는 채널(번개 줄기 · 불길)용.</param>
    static void Emit(Ch ch, Vector3 pos, Vector3 vel, float size, float life, Color color, float delay = 0f, float rot = float.NaN, float height = 0f)
    {
        FxKit h = Host;
        if (h == null || size <= 0f || life <= 0f || color.a <= 0f) return;

        if (delay > 0f)
        {
            h.pending.Add(new Pending { at = Time.time + delay, ch = ch, pos = pos, vel = vel, size = size, life = life, color = color, rot = rot, height = height });
            return;
        }

        h.EmitNow(ch, pos, vel, size, life, color, rot, height);
    }

    void EmitNow(Ch ch, Vector3 pos, Vector3 vel, float size, float life, Color color, float rot, float height)
    {
        var p = new ParticleSystem.EmitParams
        {
            position = pos,
            velocity = vel,
            startLifetime = life,
            startColor = new Color(color.r, color.g, color.b, color.a * FxCrowd.Alpha),   // 적이 많으면 살짝 옅게 (2026-11-27, 남은 작업 7-4 — FxCrowd)
            rotation = float.IsNaN(rot) ? Random.Range(0f, 360f) : rot,
        };
        if (height > 0f) p.startSize3D = new Vector3(size, height, 1f);
        else p.startSize = size;

        systems[(int)ch].Emit(p, 1);
        Emitted[(int)ch]++;
    }

    void Update()
    {
        if (pending.Count == 0) return;
        float now = Time.time;
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            Pending p = pending[i];
            if (p.at > now) continue;
            pending.RemoveAt(i);
            EmitNow(p.ch, p.pos, p.vel, p.size, p.life, p.color, p.rot, p.height);
        }
    }

    void OnDestroy()
    {
        if (host == this) host = null;
    }

    /// <summary>수 × 양 배율 — 소수 부분은 확률로 (0.35 배에서도 가끔은 나온다).</summary>
    static int Count(float n)
    {
        float v = n * Amount;
        int k = Mathf.FloorToInt(v);
        return k + (Random.value < v - k ? 1 : 0);
    }

    static Vector3 Around(float radius) => Random.insideUnitSphere * radius;

    /// <summary>바닥 위에서 흩어지는 자리 (높이는 그대로).</summary>
    static Vector3 Flat(float radius)
    {
        Vector2 c = Random.insideUnitCircle * radius;
        return new Vector3(c.x, 0f, c.y);
    }

    /// <summary>위쪽 반구로 흩어지는 방향 (up 이 클수록 위로 모인다).</summary>
    static Vector3 Burst(float up)
    {
        Vector3 d = Random.onUnitSphere;
        d.y = Mathf.Abs(d.y) * up + 0.15f;
        return d.normalized;
    }

    static Vector3 Ringward() => Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * Vector3.forward;

    static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    static float Scale => Set != null ? Set.scale : 1f;

    // ───────── 연출 ─────────

    /// <summary>바닥에 퍼지는 충격파 고리 하나.</summary>
    public static void Shockwave(Vector3 ground, float diameter, Color color, float life = 0.34f, float alpha = 0.9f)
    {
        if (!Ready) return;
        Emit(Ch.Ring, ground + Vector3.up * 0.06f, Vector3.zero, diameter, life, A(Color.Lerp(color, Color.white, 0.35f), alpha));
    }

    /// <summary>바닥에 퍼지는 일렁이는 충격파 (고리보다 느리고 넓다).</summary>
    public static void Wave(Vector3 ground, float diameter, Color color, float life = 0.5f, float alpha = 0.6f)
    {
        if (!Ready) return;
        Emit(Ch.Wave, ground + Vector3.up * 0.05f, Vector3.zero, diameter, life, A(Color.Lerp(color, Color.white, 0.25f), alpha));
    }

    /// <summary>바닥 그을음 하나 (그래픽 품질 '낮음' 에서는 없다).</summary>
    public static void Scorch(Vector3 ground, float diameter, float lifeScale = 1f)
    {
        if (!Ready || !Decals || Set.scorchAlpha <= 0f || Set.scorchLife <= 0f) return;
        Emit(Ch.Scorch, ground + Vector3.up * 0.035f, Vector3.zero, diameter, Set.scorchLife * lifeScale, new Color(0.03f, 0.02f, 0.02f, Set.scorchAlpha));
    }

    /// <summary>바닥 그늘 — 먹구름 아래처럼 바닥이 잠깐 어두워진다 (그래픽 품질 '낮음' 에서는 없다).</summary>
    public static void Shade(Vector3 ground, float diameter, float alpha, float life)
    {
        if (!Ready || !Decals) return;
        Emit(Ch.Shade, ground + Vector3.up * 0.03f, Vector3.zero, diameter, life, new Color(0.02f, 0.02f, 0.06f, alpha));
    }

    /// <summary>
    /// 폭발 — 뾰족한 섬광 · 불 덩어리 · 솟는 불길 · 2차 폭발 · 검붉은 연기 · 튀는 불꽃 · 파편 · 잔불 · 충격파 두 겹 · 바닥 그을음.
    /// radius 는 폭발 판정 반경(m), theme 는 무기 색 (불은 주황으로 두고 테마 색은 살짝만 입힌다), grow 는 크기 · 양 배율.
    /// 피해와는 무관하다 — 모습만.
    /// </summary>
    public static void Explosion(Vector3 at, float radius, Color theme, float grow = 1f)
    {
        if (!Ready) return;
        FxKitSet s = Set;
        Explosions++;

        float crowd = Crowd();

        Vector3 center = at + Vector3.up * 0.6f;
        float r = radius * grow * s.scale;
        Color fire = A(Color.Lerp(Color.white, theme, 0.2f), 0.9f);
        Color white = new Color(1f, 0.93f, 0.8f, 1f);

        // 터지는 순간 — 뾰족한 섬광, 앞의 넷은 별 모양 섬광까지
        Emit(Ch.Burst, center, Vector3.zero, r * (burstCount <= 4 ? 2.3f : 1.5f), 0.16f, A(white, burstCount <= 4 ? 1f : 0.8f));
        if (burstCount <= 4) Emit(Ch.Flare, center, Vector3.zero, r * 2.6f, 0.12f, A(white, 0.9f));

        // 불 덩어리 — 크게 부풀어 반경을 거의 채운다
        int n = Count((9f + 3f * grow) * crowd);
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Burst(0.9f);
            Emit(Ch.Fire, center + d * r * Random.Range(0.05f, 0.3f), d * r * Random.Range(1f, 2.4f) + Vector3.up * 0.9f,
                r * Random.Range(0.45f, 0.8f), Random.Range(0.42f, 0.68f), fire);
        }

        // 솟는 불길 — 가운데서 위로
        n = Count(4f * crowd);
        for (int i = 0; i < n; i++)
            Emit(Ch.Flame, at + Vector3.up * 0.1f + Flat(r * 0.35f), Vector3.up * Random.Range(0.5f, 1.2f),
                r * Random.Range(0.5f, 0.8f), Random.Range(0.32f, 0.5f), fire, Random.Range(0f, 0.06f), 0f, r * Random.Range(1.1f, 1.8f));

        // 2차 폭발 — 조금 늦게, 둘레에서 작게 몇 번 더
        n = Count(3f * crowd);
        for (int i = 0; i < n; i++)
        {
            Vector3 p = center + Flat(r * 0.7f);
            float delay = Random.Range(0.07f, 0.24f);
            Emit(Ch.Burst, p, Vector3.zero, r * Random.Range(0.8f, 1.1f), 0.11f, A(white, 0.85f), delay);
            Emit(Ch.Fire, p, Vector3.up * 1.2f, r * Random.Range(0.35f, 0.55f), Random.Range(0.3f, 0.42f), fire, delay);
        }

        // 검붉은 연기 — 불이 식으며 올라온다
        if (s.smokeAlpha > 0f)
        {
            Color smoke = A(Color.Lerp(new Color(0.12f, 0.07f, 0.07f), theme, 0.12f), s.smokeAlpha);
            n = Count((5f + 2f * grow) * crowd);
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Burst(1.2f);
                Emit(Ch.Smoke, center + d * r * 0.2f, d * r * Random.Range(0.3f, 0.8f) + Vector3.up * Random.Range(0.8f, 1.6f),
                    r * Random.Range(0.7f, 1.15f), Random.Range(1f, 1.5f) * s.smokeLife, smoke, Random.Range(0.05f, 0.15f));
            }
        }

        // 튀는 불꽃 — 반경 밖까지 날아간다
        Color spark = Color.Lerp(theme, new Color(1f, 0.82f, 0.45f), 0.6f);
        n = Count(26f * grow * Mathf.Max(0.6f, crowd));
        for (int i = 0; i < n; i++)
            Emit(Ch.Spark, center, Burst(0.8f) * r * Random.Range(3f, 8.5f), Random.Range(0.07f, 0.13f), Random.Range(0.3f, 0.75f), spark);

        // 파편 뭉치
        n = Count(4f * crowd);
        for (int i = 0; i < n; i++)
            Emit(Ch.Shard, center, Burst(1.2f) * r * Random.Range(1.5f, 3f), r * Random.Range(0.35f, 0.6f), Random.Range(0.5f, 0.8f), new Color(0.2f, 0.16f, 0.14f, 0.95f));

        // 잔불 — 폭발 뒤에 남아 떠오른다
        Color ember = new Color(1f, 0.55f, 0.2f, 1f);
        n = Count(14f * grow * crowd);
        for (int i = 0; i < n; i++)
            Emit(Ch.Ember, center + Around(r * 0.6f), Vector3.up * Random.Range(0.4f, 1.3f) + Around(0.6f),
                Random.Range(0.07f, 0.14f), Random.Range(0.8f, 1.6f), ember, Random.Range(0.05f, 0.3f));

        Shockwave(at, r * 2.7f, theme, 0.36f, 0.95f * Mathf.Max(0.6f, crowd));
        Wave(at, r * 3.4f, theme, 0.5f, 0.6f * crowd);
        Scorch(at, r * 1.6f);
    }

    /// <summary>같이 터지는 폭발로 치는 시간(초) · 그 창이 시작된 때 · 그 안에서 몇 번째인가.</summary>
    const float BurstWindow = 0.12f;
    static float burstStart = -99f;
    static int burstCount;

    /// <summary>
    /// 여럿이 한꺼번에 터질 때 (연쇄 폭발 · 산탄 — 한 프레임에 여덟 군데까지) 뒤에 터지는 것일수록 조금씩 줄이는 배율.
    /// 화려해도 좋지만, 그대로 다 겹치면 화면이 통째로 하얗게 떠 무슨 일인지 안 보인다.   1 · 0.89 · 0.81 · 0.74 · 0.68 …
    /// </summary>
    static float Crowd()
    {
        float now = Time.time;
        if (now - burstStart > BurstWindow || now < burstStart) { burstStart = now; burstCount = 0; }
        burstCount++;
        return 1f / (1f + 0.12f * (burstCount - 1));
    }

    /// <summary>
    /// 큰 연쇄의 마지막 — 화면을 가로지르는 충격파 세 겹 · 큰 섬광 · 바닥을 훑는 불꽃 · 불의 고리 (처형자 큰 연쇄).
    /// </summary>
    public static void BigShockwave(Vector3 at, float radius, Color theme)
    {
        if (!Ready) return;
        float r = radius * Scale;
        Color white = new Color(1f, 0.93f, 0.82f, 1f);
        Vector3 center = at + Vector3.up * 0.6f;

        Shockwave(at, r * 5f, Color.white, 0.34f, 1f);
        Shockwave(at, r * 6.5f, theme, 0.6f, 0.8f);
        Wave(at, r * 7.5f, theme, 0.75f, 0.7f);
        Emit(Ch.Burst, center, Vector3.zero, r * 4.2f, 0.2f, white);
        Emit(Ch.Flare, center, Vector3.zero, r * 5f, 0.16f, A(white, 0.95f));

        // 바닥을 훑고 나가는 불꽃
        int n = Count(40f);
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Ringward();
            Emit(Ch.Spark, center, (d + Vector3.up * Random.Range(0.02f, 0.25f)) * r * Random.Range(5f, 10f), Random.Range(0.08f, 0.14f), Random.Range(0.4f, 0.8f),
                Color.Lerp(theme, new Color(1f, 0.85f, 0.5f), 0.6f));
        }

        // 불의 고리 — 둘레에서 밖으로 밀려 나간다
        Color fire = A(Color.Lerp(Color.white, theme, 0.2f), 0.9f);
        n = Count(12f);
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Quaternion.AngleAxis(i * 360f / Mathf.Max(1, n) + Random.Range(-10f, 10f), Vector3.up) * Vector3.forward;
            Emit(Ch.Fire, center + d * r * 1.1f, d * r * Random.Range(2f, 3.2f), r * Random.Range(0.5f, 0.75f), Random.Range(0.4f, 0.6f), fire, 0.04f);
        }

        n = Count(26f);
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Ringward();
            Emit(Ch.Ember, at + Vector3.up * 0.3f + d * r * Random.Range(0.4f, 2f), d * Random.Range(0.5f, 1.8f) + Vector3.up * Random.Range(0.5f, 1.6f),
                Random.Range(0.08f, 0.15f), Random.Range(0.9f, 1.8f), new Color(1f, 0.5f, 0.2f, 1f), Random.Range(0f, 0.2f));
        }
        Scorch(at, r * 3f);
    }

    /// <summary>
    /// 낙뢰가 맞은 자리 — 번개 줄기 그림 · 뾰족한 섬광 · 바닥을 타는 전기 가지 · 튀는 불꽃 · 충격파 · 그을음 · 잠깐 남는 잔전류.
    /// height 는 줄기의 높이(m) — 0 이면 줄기 그림은 그리지 않는다 (선으로 그린 줄기만). scale 은 크기 · 밝기 배율 (보조 칸은 낮춘다).
    /// </summary>
    public static void LightningStrike(Vector3 at, float radius, Color color, float scale = 1f, float height = 0f)
    {
        if (!Ready) return;
        FxKitSet s = Set;
        Strikes++;

        Vector3 ground = at + Vector3.up * 0.08f;
        Color white = Color.Lerp(color, Color.white, 0.75f);
        float r = radius * scale * s.scale;

        // 번개 줄기 그림 — 굵게 한 번, 조금 비껴 가늘게 한 번 더
        if (height > 1f)
        {
            Emit(Ch.Bolt, at, Vector3.zero, r * 2.6f, 0.17f, A(white, 1f), 0f, 0f, height);
            Emit(Ch.Bolt, at + Flat(0.35f), Vector3.zero, r * 1.7f, 0.11f, A(color, 0.9f), 0.09f, 0f, height);
        }

        Emit(Ch.Burst, at + Vector3.up * 0.4f, Vector3.zero, r * 3.2f, 0.13f, A(white, 1f));
        Emit(Ch.Flare, at + Vector3.up * 0.4f, Vector3.zero, r * 4f, 0.12f, A(white, 0.95f));

        // 바닥을 타고 퍼지는 전기 가지 — 맞는 순간 크게, 그 뒤 0.75초 동안 작게 여러 번 (잔전류)
        int n = Mathf.Max(2, Count(3f));
        for (int i = 0; i < n; i++)
            Emit(Ch.Twig, ground, Vector3.zero, r * Random.Range(3f, 4.2f), Random.Range(0.12f, 0.2f), A(white, 1f));
        n = Count(7f);
        for (int i = 0; i < n; i++)
            Emit(Ch.Twig, ground + Flat(r * 0.6f), Vector3.zero, r * Random.Range(1.2f, 2.2f), Random.Range(0.07f, 0.13f), A(color, 0.85f),
                0.1f + 0.095f * i + Random.Range(0f, 0.05f));

        // 튀는 불꽃 — 위로 · 밖으로
        n = Count(24f * scale);
        for (int i = 0; i < n; i++)
            Emit(Ch.Spark, at + Vector3.up * 0.2f, Burst(1.4f) * Random.Range(3f, 8f), Random.Range(0.06f, 0.1f), Random.Range(0.2f, 0.5f), white);

        // 남아 떠오르는 전기 알갱이
        n = Count(10f * scale);
        for (int i = 0; i < n; i++)
            Emit(Ch.Ember, at + Vector3.up * 0.2f + Around(r * 0.7f), Vector3.up * Random.Range(0.5f, 1.5f), Random.Range(0.06f, 0.11f), Random.Range(0.4f, 0.9f), A(color, 1f), Random.Range(0f, 0.2f));

        Shockwave(at, r * 2.8f, color, 0.26f, 0.8f);
        Wave(at, r * 3.6f, color, 0.4f, 0.5f);
        Scorch(at, r * 1.9f, 0.8f);

        if (s.smokeAlpha > 0f)
        {
            n = Count(2f);
            for (int i = 0; i < n; i++)
                Emit(Ch.Smoke, at + Vector3.up * 0.3f + Flat(r * 0.3f), Vector3.up * Random.Range(0.8f, 1.4f), r * 0.8f, 0.8f * s.smokeLife,
                    new Color(0.3f, 0.3f, 0.38f, s.smokeAlpha * 0.5f), 0.08f);
        }
    }

    /// <summary>
    /// 구름 안이 번쩍인다 — 구름 높이에 섬광과 옆으로 퍼지는 전기 가지 (위에서 내려다보는 카메라에 구름 속 번개로 보인다).
    /// strength 1 = 낙뢰 때의 큰 번쩍임, 0.3 쯤 = 사이사이 깜박임.
    /// </summary>
    public static void CloudFlash(Vector3 sky, float cloudRadius, Color color, float strength = 1f)
    {
        if (!Ready) return;
        Color white = Color.Lerp(color, Color.white, 0.6f);
        float r = cloudRadius * Scale;
        if (strength >= 0.7f) Emit(Ch.Flare, sky, Vector3.zero, r * 2.6f, 0.13f, A(white, 0.8f * strength));
        Emit(Ch.Twig, sky + Flat(r * 0.3f), Vector3.zero, r * Random.Range(1.6f, 2.3f) * Mathf.Lerp(0.6f, 1f, strength), 0.12f, A(white, Mathf.Lerp(0.6f, 1f, strength)));
        if (strength >= 0.7f)
            Emit(Ch.Twig, sky + Flat(r * 0.5f), Vector3.zero, r * 1.5f, 0.1f, A(color, 0.9f), 0.07f);
    }

    // ───────── 연출 2 — 나머지 10종으로 넓히며 더한 것 (2026-11-20) ─────────
    // 기준은 같다: 과하다 싶을 만큼. 자주 불리는 것(탄마다 · 프레임마다)은 count 를 받아 부르는 쪽이 양을 정한다.

    static readonly Color WarmWhite = new Color(1f, 0.93f, 0.8f, 1f);
    static readonly Color EmberColor = new Color(1f, 0.55f, 0.2f, 1f);
    static readonly Color SparkYellow = new Color(1f, 0.84f, 0.48f, 1f);
    static readonly Color SootColor = new Color(0.12f, 0.07f, 0.07f, 1f);

    /// <summary>
    /// 빛 폭발 — 불이 아닌 것(빛 · 마력 · 금속)이 터질 때: 뾰족한 섬광 · 별 섬광 · 색 불꽃 · 색 알갱이 · 일렁이는 기운 · 반짝임 · 충격파 두 겹.
    /// at 은 바닥 자리, radius 는 크기의 기준(m). decal 이면 바닥 그을음도 남긴다.
    /// </summary>
    public static void EnergyBurst(Vector3 at, float radius, Color color, float scale = 1f, bool decal = false)
    {
        if (!Ready) return;
        float crowd = Crowd();
        float r = radius * scale * Scale;
        Vector3 center = at + Vector3.up * 0.6f;
        Color tint = A(color, 1f);
        Color white = A(Color.Lerp(color, Color.white, 0.7f), 1f);

        Emit(Ch.Burst, center, Vector3.zero, r * 2.4f, 0.15f, white);
        if (burstCount <= 4)
        {
            Emit(Ch.Flare, center, Vector3.zero, r * 3.2f, 0.14f, A(white, 0.95f));
            Emit(Ch.Star, center, Vector3.zero, r * 2.8f, 0.3f, tint, 0f, 0f);
        }

        int n = Count(22f * scale * Mathf.Max(0.6f, crowd));
        for (int i = 0; i < n; i++)
            Emit(Ch.Spark, center, Burst(0.7f) * r * Random.Range(3f, 8f), Random.Range(0.08f, 0.14f), Random.Range(0.25f, 0.6f), Color.Lerp(tint, white, Random.value * 0.6f));

        n = Count(12f * scale * crowd);
        for (int i = 0; i < n; i++)
            Emit(Ch.Ember, center + Around(r * 0.6f), Around(1.2f) + Vector3.up * Random.Range(0.3f, 1.2f), Random.Range(0.12f, 0.22f), Random.Range(0.5f, 1.1f), tint, Random.Range(0f, 0.15f));

        n = Count(5f * crowd);
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Burst(1f);
            Emit(Ch.Wisp, center + d * r * 0.2f, d * r * Random.Range(0.8f, 2f), r * Random.Range(0.7f, 1.2f), Random.Range(0.35f, 0.6f), A(color, 0.85f));
        }

        n = Count(6f * crowd);
        for (int i = 0; i < n; i++)
            Emit(Ch.Star, center + Around(r), Vector3.zero, r * Random.Range(0.3f, 0.6f), Random.Range(0.3f, 0.6f), white, Random.Range(0.05f, 0.35f));

        Shockwave(at, r * 2.6f, color, 0.3f, 0.9f * Mathf.Max(0.6f, crowd));
        Wave(at, r * 3.4f, color, 0.45f, 0.55f * crowd);
        if (decal) Scorch(at, r * 1.4f, 0.6f);
    }

    /// <summary>
    /// 작은 폭발 — 자주 터지는 것(폭발탄 · 스웜 카빈)용: 섬광 · 불 덩어리 몇 · 솟는 불길 · 불꽃 · 연기 조금 · 충격파 · 그을음.
    /// </summary>
    public static void Pop(Vector3 at, float radius, Color theme, float scale = 1f)
    {
        if (!Ready) return;
        FxKitSet s = Set;
        Explosions++;
        float crowd = Crowd();
        float r = radius * scale * s.scale;
        Vector3 center = at + Vector3.up * 0.5f;
        Color fire = A(Color.Lerp(Color.white, theme, 0.2f), 0.9f);

        Emit(Ch.Burst, center, Vector3.zero, r * 2.4f, 0.14f, A(WarmWhite, burstCount <= 4 ? 1f : 0.75f));
        if (burstCount <= 3) Emit(Ch.Flare, center, Vector3.zero, r * 2.6f, 0.1f, A(WarmWhite, 0.85f));

        int n = Count(8f * crowd);
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Burst(0.9f);
            Emit(Ch.Fire, center + d * r * 0.2f, d * r * Random.Range(1f, 2.2f) + Vector3.up * 0.8f, r * Random.Range(0.5f, 0.85f), Random.Range(0.3f, 0.5f), fire);
        }

        n = Count(2.2f * crowd);
        for (int i = 0; i < n; i++)
            Emit(Ch.Flame, at + Vector3.up * 0.1f + Flat(r * 0.3f), Vector3.up * Random.Range(0.4f, 1f), r * Random.Range(0.5f, 0.75f), Random.Range(0.28f, 0.42f), fire,
                0f, 0f, r * Random.Range(1.1f, 1.7f));

        Color spark = Color.Lerp(theme, SparkYellow, 0.6f);
        n = Count(16f * scale * Mathf.Max(0.6f, crowd));
        for (int i = 0; i < n; i++)
            Emit(Ch.Spark, center, Burst(0.8f) * r * Random.Range(3f, 8f), Random.Range(0.06f, 0.11f), Random.Range(0.25f, 0.6f), spark);

        if (s.smokeAlpha > 0f)
        {
            Color smoke = A(Color.Lerp(SootColor, theme, 0.12f), s.smokeAlpha * 0.8f);
            n = Count(2f * crowd);
            for (int i = 0; i < n; i++)
                Emit(Ch.Smoke, center, Burst(1.2f) * r * Random.Range(0.3f, 0.7f) + Vector3.up * Random.Range(0.6f, 1.2f), r * Random.Range(0.7f, 1f),
                    Random.Range(0.7f, 1f) * s.smokeLife, smoke, Random.Range(0.04f, 0.12f));
        }

        n = Count(5f * crowd);
        for (int i = 0; i < n; i++)
            Emit(Ch.Ember, center + Around(r * 0.5f), Vector3.up * Random.Range(0.4f, 1.2f) + Around(0.5f), Random.Range(0.06f, 0.12f), Random.Range(0.6f, 1.2f), EmberColor, Random.Range(0.05f, 0.25f));

        Shockwave(at, r * 2.5f, theme, 0.28f, 0.9f * Mathf.Max(0.6f, crowd));
        Wave(at, r * 3.2f, theme, 0.4f, 0.5f * crowd);
        Scorch(at, r * 1.3f, 0.6f);
    }

    /// <summary>
    /// 총구 폭풍 — 쏘는 쪽으로 뿜는 불 · 부채꼴 불꽃 · 양옆으로 밀려 나는 연기 · 발밑 충격파 (공성포 · 태양창).
    /// size 는 크기의 기준(m), groundY 는 바닥 높이.
    /// </summary>
    public static void MuzzleBlast(Vector3 muzzle, Vector3 dir, float size, Color theme, float groundY)
    {
        if (!Ready) return;
        FxKitSet s = Set;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        float z = size * s.scale;
        Color fire = A(Color.Lerp(Color.white, theme, 0.2f), 0.9f);

        Emit(Ch.Burst, muzzle + dir * z * 0.4f, Vector3.zero, z * 2.4f, 0.12f, WarmWhite);
        Emit(Ch.Flare, muzzle + dir * z * 0.2f, Vector3.zero, z * 3f, 0.1f, A(WarmWhite, 0.9f));

        int n = Count(9f);
        for (int i = 0; i < n; i++)
            Emit(Ch.Fire, muzzle + dir * z * Random.Range(0.1f, 0.6f), dir * z * Random.Range(5f, 11f) + Around(z * 0.9f), z * Random.Range(0.6f, 1.05f), Random.Range(0.25f, 0.42f), fire);

        Color spark = Color.Lerp(theme, SparkYellow, 0.6f);
        n = Count(28f);
        for (int i = 0; i < n; i++)
            Emit(Ch.Spark, muzzle, (dir + Around(0.45f)).normalized * z * Random.Range(9f, 20f), Random.Range(0.06f, 0.11f), Random.Range(0.2f, 0.5f), spark);

        if (s.smokeAlpha > 0f)
        {
            n = Count(4f);
            for (int i = 0; i < n; i++)
            {
                float sign = (i & 1) == 0 ? 1f : -1f;
                Emit(Ch.Smoke, muzzle + dir * z * 0.3f, side * sign * z * Random.Range(1.2f, 2.6f) + dir * z + Vector3.up * Random.Range(0.4f, 1f), z * Random.Range(0.9f, 1.3f),
                    Random.Range(0.7f, 1f) * s.smokeLife, new Color(0.3f, 0.28f, 0.26f, s.smokeAlpha * 0.7f), 0.03f);
            }
        }

        Vector3 ground = new Vector3(muzzle.x, groundY, muzzle.z);
        Shockwave(ground, z * 4.2f, theme, 0.26f, 0.8f);
        Wave(ground, z * 5.4f, theme, 0.4f, 0.5f);
    }

    /// <summary>
    /// 총구 불꽃 (연사용, 가볍게) — 쏠 때마다 앞으로 뿜는 불 한두 덩이 + 불꽃 몇 줄. heat 0~1 — 달아오를수록 크고 많다 (회전 기관포).
    /// </summary>
    public static void MuzzleJet(Vector3 muzzle, Vector3 dir, float heat, Color theme, float scale = 1f)
    {
        if (!Ready) return;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        heat = Mathf.Clamp01(heat);
        // 화면에서 1m 가 33px 쯤이다 — 총구 불은 길이 2 ~ 3m 는 돼야 보인다 (처음엔 0.5m 라 점으로 보였다)
        float z = Mathf.Lerp(1.2f, 2.6f, heat) * scale * Scale;
        Color fire = A(Color.Lerp(Color.white, theme, 0.25f), 0.9f);

        Emit(Ch.Burst, muzzle + dir * z * 0.25f, Vector3.zero, z * Random.Range(0.9f, 1.3f), 0.07f, A(WarmWhite, 0.9f));

        // 앞으로 뻗는 불의 혀
        int n = Count(4f + 5f * heat);
        for (int i = 0; i < n; i++)
            Emit(Ch.Fire, muzzle + dir * z * Random.Range(0.1f, 0.7f), dir * z * Random.Range(4f, 9f) + Around(0.5f), z * Random.Range(0.4f, 0.65f), Random.Range(0.2f, 0.34f), fire);

        // 탄막 — 표적 쪽으로 빠르게 날아가는 불꽃 줄기
        Color spark = Color.Lerp(theme, SparkYellow, 0.55f);
        n = Count((4f + 10f * heat) * scale);
        for (int i = 0; i < n; i++)
            Emit(Ch.Spark, muzzle, (dir + Around(0.16f)).normalized * Random.Range(22f, 44f), Random.Range(0.07f, 0.12f), Random.Range(0.12f, 0.3f), spark);

        // 옆으로 튀는 불꽃
        n = Count((2f + 5f * heat) * scale);
        for (int i = 0; i < n; i++)
            Emit(Ch.Spark, muzzle, (dir + Around(0.6f)).normalized * Random.Range(6f, 14f), Random.Range(0.05f, 0.09f), Random.Range(0.15f, 0.35f), spark);
    }

    /// <summary>
    /// 불의 길 — from 에서 to 까지 바닥을 따라 불길이 차례로 솟는다 (태양창): 솟는 불길 · 불 덩어리 · 옆으로 튀는 불꽃 · 잔불 · 연기 · 그을음.
    /// speed 는 불길이 번져 가는 빠르기(m/s) — 가까운 데부터 솟는다.
    /// </summary>
    public static void FireLine(Vector3 from, Vector3 to, float width, Color theme, float speed = 70f)
    {
        if (!Ready) return;
        FxKitSet s = Set;
        Vector3 span = to - from;
        span.y = 0f;
        float length = span.magnitude;
        if (length < 0.2f) return;

        Vector3 dir = span / length;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        float w = width * s.scale;
        Color fire = A(Color.Lerp(Color.white, theme, 0.2f), 0.9f);
        Color spark = Color.Lerp(theme, SparkYellow, 0.5f);
        Color smoke = A(Color.Lerp(SootColor, theme, 0.12f), s.smokeAlpha * 0.8f);

        int steps = Mathf.Clamp(Mathf.CeilToInt(length / Mathf.Max(0.5f, w * 0.45f)), 2, 28);
        for (int i = 0; i < steps; i++)
        {
            float t = (i + Random.Range(0.2f, 0.8f)) / steps;
            Vector3 p = from + dir * length * t;
            float delay = length * t / speed;

            int n = Count(2f);
            for (int k = 0; k < n; k++)
                Emit(Ch.Flame, p + side * Random.Range(-0.35f, 0.35f) * w + Vector3.up * 0.05f, Vector3.up * Random.Range(0.5f, 1.4f), w * Random.Range(0.5f, 0.8f),
                    Random.Range(0.4f, 0.62f), fire, delay + Random.Range(0f, 0.06f), 0f, w * Random.Range(1.2f, 2f));

            n = Count(2f);
            for (int k = 0; k < n; k++)
                Emit(Ch.Fire, p + Vector3.up * 0.5f + Around(w * 0.25f), Vector3.up * Random.Range(1f, 2.4f) + side * Random.Range(-1f, 1f) * w, w * Random.Range(0.45f, 0.75f),
                    Random.Range(0.4f, 0.62f), fire, delay);

            n = Count(5f);
            for (int k = 0; k < n; k++)
            {
                float sign = Random.value < 0.5f ? -1f : 1f;
                Emit(Ch.Spark, p + Vector3.up * 0.3f, (side * sign * Random.Range(0.5f, 1f) + Vector3.up * Random.Range(0.3f, 1.2f) + dir * Random.Range(0f, 0.8f)) * Random.Range(4f, 9f),
                    Random.Range(0.06f, 0.11f), Random.Range(0.3f, 0.65f), spark, delay);
            }

            n = Count(2f);
            for (int k = 0; k < n; k++)
                Emit(Ch.Ember, p + Around(w * 0.4f) + Vector3.up * 0.4f, Vector3.up * Random.Range(0.5f, 1.6f) + Around(0.5f), Random.Range(0.07f, 0.13f), Random.Range(0.8f, 1.6f),
                    EmberColor, delay + Random.Range(0.1f, 0.4f));

            if (s.smokeAlpha > 0f && (i & 1) == 0)
                Emit(Ch.Smoke, p + Vector3.up * 0.6f, Vector3.up * Random.Range(0.8f, 1.5f) + side * Random.Range(-0.5f, 0.5f), w * Random.Range(0.6f, 0.9f),
                    Random.Range(1f, 1.4f) * s.smokeLife, smoke, delay + 0.15f);

            if (Decals && s.scorchAlpha > 0f && s.scorchLife > 0f)
                Emit(Ch.Scorch, p + Vector3.up * 0.035f, Vector3.zero, w * Random.Range(0.9f, 1.3f), s.scorchLife, new Color(0.03f, 0.02f, 0.02f, s.scorchAlpha), delay);
        }
    }

    /// <summary>
    /// 빨려드는 기운 — 둘레에서 가운데로 모이는 빛줄기 · 알갱이 · 기운 (중력장 · 모으기). count 는 이번에 낼 수 (초당 수 × dt).
    /// </summary>
    public static void Inflow(Vector3 center, float radius, Color color, float count)
    {
        if (!Ready) return;
        Vector3 core = center + Vector3.up * 0.6f;

        int n = Count(count);
        for (int i = 0; i < n; i++)
        {
            Vector3 from = center + Ringward() * radius * Random.Range(0.65f, 1f) + Vector3.up * Random.Range(0.1f, 1.4f);
            float life = Random.Range(0.22f, 0.4f);
            Emit(Ch.Spark, from, (core - from) / life, Random.Range(0.08f, 0.14f), life, A(color, 1f));
        }

        n = Count(count * 0.5f);
        for (int i = 0; i < n; i++)
        {
            Vector3 from = center + Ringward() * radius * Random.Range(0.65f, 1f) + Vector3.up * Random.Range(0.1f, 1.4f);
            float life = Random.Range(0.35f, 0.6f);
            Emit(Ch.Ember, from, (core - from) / life, Random.Range(0.14f, 0.24f), life, A(Color.Lerp(color, Color.white, 0.3f), 1f));
        }

        n = Count(count * 0.2f);
        for (int i = 0; i < n; i++)
        {
            Vector3 from = center + Ringward() * radius * Random.Range(0.5f, 0.9f) + Vector3.up * Random.Range(0.2f, 1f);
            float life = Random.Range(0.35f, 0.55f);
            Emit(Ch.Wisp, from, (core - from) / life, radius * Random.Range(0.25f, 0.4f), life, A(color, 0.7f));
        }
    }

    /// <summary>바닥 소용돌이 — 돌면서 퍼지는 호 여럿 (바람 · 중력장 · 회전 베기).</summary>
    public static void Swirl(Vector3 ground, float diameter, Color color, float life = 0.5f, int arms = 3, float alpha = 0.85f, float delay = 0f)
    {
        if (!Ready) return;
        float turn = Random.Range(0f, 360f);
        for (int i = 0; i < arms; i++)
            Emit(Ch.Twirl, ground + Vector3.up * 0.07f, Vector3.zero, diameter * Scale * Random.Range(0.8f, 1.05f), life * Random.Range(0.85f, 1.15f), A(color, alpha),
                delay, turn + i * 360f / arms);
    }

    /// <summary>
    /// 베기 호 — dir 쪽으로 볼록한 초승달이 그 높이에 누워 앞으로 밀려 나간다 (색 호 + 흰 심지). size 는 호의 폭(m).
    /// </summary>
    public static void SlashArc(Vector3 at, Vector3 dir, float size, Color color, float push = 2f, float life = 0.22f, float delay = 0f)
    {
        if (!Ready) return;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        // 그림의 위쪽(볼록한 쪽)이 dir 을 보게 — 바닥에 눕는 입자는 돌리지 않으면 그림의 위쪽이 +Z 를 보고, 각도가 늘면 시계 방향으로 돈다
        // (2026-11-20 캡처 도구의 WEAPONFX_CAL 표시로 확인. 그림 묶음의 호는 모두 볼록한 쪽이 위여야 한다)
        float rot = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float z = size * Scale;
        Emit(Ch.Slash, at, dir * push, z, life, A(color, 1f), delay, rot);
        Emit(Ch.Slash, at - dir * z * 0.04f, dir * push * 0.9f, z * 0.92f, life * 0.9f, A(color, 0.9f), delay, rot);
        Emit(Ch.Slash, at - dir * z * 0.08f, dir * push * 0.8f, z * 0.8f, life * 0.75f, A(Color.Lerp(color, Color.white, 0.75f), 0.95f), delay, rot);
    }

    /// <summary>반짝임 — 둘레에 흩어져 깜박이는 별빛.</summary>
    public static void Glints(Vector3 at, float radius, Color color, float count, float size = 0.7f, float spread = 0.35f)
    {
        if (!Ready) return;
        int n = Count(count);
        for (int i = 0; i < n; i++)
            Emit(Ch.Star, at + Around(radius), Around(0.3f), size * Scale * Random.Range(0.6f, 1.3f), Random.Range(0.25f, 0.6f),
                A(Color.Lerp(color, Color.white, Random.value * 0.7f), 1f), Random.Range(0f, spread));
    }

    /// <summary>빛기둥 — 바닥에서 위로 솟는 빛줄기 묶음 + 바닥의 빛 원반 + 치솟는 알갱이 (받기 · 폭주 · 붕괴).</summary>
    public static void Pillar(Vector3 ground, float width, float height, Color color, float life = 0.45f, int strands = 6)
    {
        if (!Ready) return;
        float w = width * Scale;

        // 빛줄기 그림은 칸 가운데의 좁은 띠만 빛난다 — 폭을 넉넉히 준다
        for (int i = 0; i < strands; i++)
            Emit(Ch.Beam, ground + Flat(w * 0.4f), Vector3.up * Random.Range(0f, 1.5f), w * Random.Range(3f, 5f), life * Random.Range(0.7f, 1.2f),
                A(Color.Lerp(color, Color.white, Random.value * 0.6f), 1f), Random.Range(0f, 0.08f), 0f, height * Random.Range(0.7f, 1.1f));

        Emit(Ch.Magic, ground + Vector3.up * 0.06f, Vector3.zero, w * 4f, life * 1.5f, A(color, 0.9f));

        int n = Count(12f);
        for (int i = 0; i < n; i++)
            Emit(Ch.Ember, ground + Flat(w * 0.6f) + Vector3.up * Random.Range(0f, 0.5f), Vector3.up * Random.Range(2f, 6f), Random.Range(0.07f, 0.13f), Random.Range(0.4f, 0.9f),
                A(color, 1f), Random.Range(0f, 0.15f));
    }

    /// <summary>색 안개 — 색을 입힌 연기 뭉치 (피 안개 · 어두운 기운 · 먼지). 가리는 것이라 설정의 연기 진하기 0 이면 없다.</summary>
    public static void Mist(Vector3 at, float radius, Color color, float alpha = 0.5f, float count = 4f, float life = 1f)
    {
        if (!Ready || Set.smokeAlpha <= 0f) return;
        int n = Count(count);
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Burst(0.6f);
            Emit(Ch.Smoke, at + Vector3.up * 0.5f + Around(radius * 0.4f), d * radius * Random.Range(0.3f, 0.9f), radius * Random.Range(0.7f, 1.2f) * Scale,
                life * Random.Range(0.8f, 1.3f) * Set.smokeLife, A(color, alpha));
        }
    }

    /// <summary>지나간 자리에 남는 빛 알갱이 — 날아가는 것(창 · 검기 · 빛)이 프레임마다 부른다. count 는 이번에 낼 수 (초당 수 × dt).</summary>
    public static void TrailDust(Vector3 at, Color color, float count, float size = 0.12f, bool stars = false, float wisp = 0f)
    {
        if (!Ready) return;
        int n = Count(count);
        for (int i = 0; i < n; i++)
            Emit(Ch.Ember, at + Around(0.18f), Around(0.6f) + Vector3.up * Random.Range(0f, 0.6f), size * Random.Range(0.7f, 1.4f), Random.Range(0.3f, 0.7f), A(color, 1f));

        // wisp > 0 — 일렁이는 기운도 남긴다 (그 크기로)
        if (wisp > 0f)
        {
            n = Count(count * 0.25f);
            for (int i = 0; i < n; i++)
                Emit(Ch.Wisp, at + Around(0.15f), Around(0.4f) + Vector3.up * Random.Range(0.2f, 0.8f), wisp * Scale * Random.Range(0.7f, 1.3f), Random.Range(0.3f, 0.55f), A(color, 0.8f));
        }

        if (!stars) return;
        n = Count(count * 0.3f);
        for (int i = 0; i < n; i++)
            Emit(Ch.Star, at + Around(0.25f), Vector3.zero, size * Random.Range(3f, 5f), Random.Range(0.2f, 0.4f), A(Color.Lerp(color, Color.white, 0.5f), 1f));
    }

    /// <summary>바람 줄기 — 길(from → dir 쪽 length) 위에서 dir 쪽으로 날아가는 길쭉한 빛줄기 (돌풍 · 던지기).</summary>
    public static void WindStreaks(Vector3 from, Vector3 dir, float length, float halfWidth, Color color, float count, float speed = 24f)
    {
        if (!Ready) return;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, dir);

        int n = Count(count);
        for (int i = 0; i < n; i++)
        {
            Vector3 p = from + dir * Random.Range(0f, length * 0.85f) + side * Random.Range(-halfWidth, halfWidth) + Vector3.up * Random.Range(0.2f, 1.5f);
            Emit(Ch.Spark, p, dir * speed * Random.Range(0.7f, 1.3f) + Vector3.up * Random.Range(0.5f, 2.5f), Random.Range(0.09f, 0.17f), Random.Range(0.2f, 0.42f),
                A(Color.Lerp(color, Color.white, Random.value * 0.5f), 1f));
        }
    }

    /// <summary>바닥의 빛 원반 하나 — 천천히 돈다 (모으기 · 중력장 · 폭주).</summary>
    public static void Disc(Vector3 ground, float diameter, Color color, float life, float alpha = 0.8f)
    {
        if (!Ready) return;
        Emit(Ch.Magic, ground + Vector3.up * 0.05f, Vector3.zero, diameter * Scale, life, A(color, alpha));
    }

    /// <summary>할퀸 자국 — 맞은 자리에 비스듬한 세 줄 (색 + 흰 심지).</summary>
    public static void ClawMark(Vector3 at, float size, Color color, float life = 0.2f)
    {
        if (!Ready) return;
        float rot = Random.Range(0f, 360f);
        Emit(Ch.Claw, at, Vector3.zero, size * Scale, life, A(color, 1f), 0f, rot);
        Emit(Ch.Claw, at, Vector3.zero, size * Scale * 0.8f, life * 0.7f, A(Color.Lerp(color, Color.white, 0.8f), 0.9f), 0f, rot);
    }

    /// <summary>한쪽으로 튀는 불꽃 줄기 (피 방울 · 금속 불꽃). cone 이 클수록 넓게 퍼진다.</summary>
    public static void Spray(Vector3 at, Vector3 dir, Color color, float count, float speed = 6f, float cone = 0.55f)
    {
        if (!Ready) return;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.up;
        int n = Count(count);
        for (int i = 0; i < n; i++)
            Emit(Ch.Spark, at, (dir + Around(cone)).normalized * speed * Random.Range(0.5f, 1.4f) + Vector3.up * Random.Range(0f, 1.5f), Random.Range(0.08f, 0.14f),
                Random.Range(0.2f, 0.5f), A(color, 1f));
    }

    /// <summary>지나가며 맞힌 자리 — 작은 섬광 · 별빛 · 간 쪽으로 튀는 불꽃 · 알갱이.</summary>
    public static void Hit(Vector3 at, Vector3 dir, Color color, float scale = 1f)
    {
        if (!Ready) return;
        float z = scale * Scale;
        Emit(Ch.Burst, at, Vector3.zero, 1.5f * z, 0.11f, A(Color.Lerp(color, Color.white, 0.7f), 1f));
        Emit(Ch.Star, at, Vector3.zero, 1.7f * z, 0.22f, A(color, 1f), 0f, 0f);
        Spray(at, dir, Color.Lerp(color, Color.white, 0.3f), 10f * scale, 7f);

        int n = Count(3f * scale);
        for (int i = 0; i < n; i++)
            Emit(Ch.Ember, at + Around(0.3f), Around(1f) + Vector3.up * Random.Range(0.3f, 1f), Random.Range(0.07f, 0.12f), Random.Range(0.4f, 0.8f), A(color, 1f));
    }

    /// <summary>둘레에서 솟는 불길 — 원을 그리며 서는 불길 (폭주 · 붉은 계열에 어울린다 — 불 색 그라디언트를 탄다).</summary>
    public static void FlameRing(Vector3 ground, float radius, Color color, float count, float height, float life = 0.45f)
    {
        if (!Ready) return;
        int n = Count(count);
        float turn = Random.Range(0f, 360f);
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Quaternion.AngleAxis(turn + i * 360f / Mathf.Max(1, n) + Random.Range(-12f, 12f), Vector3.up) * Vector3.forward;
            Emit(Ch.Flame, ground + d * radius * Random.Range(0.85f, 1.1f) + Vector3.up * 0.05f, Vector3.up * Random.Range(0.4f, 1.2f), height * Random.Range(0.38f, 0.55f),
                life * Random.Range(0.8f, 1.2f), A(color, 0.9f), Random.Range(0f, 0.08f), 0f, height * Random.Range(0.8f, 1.2f));
        }
    }

    /// <summary>피어오르는 기운 — 둘레에서 일렁이며 오르는 색 불 · 알갱이 (폭주 중 · 달아오른 총열). count 는 이번에 낼 수 (초당 수 × dt).</summary>
    public static void Aura(Vector3 ground, float radius, Color color, float count, float size = 0.9f)
    {
        if (!Ready) return;
        int n = Count(count);
        for (int i = 0; i < n; i++)
        {
            Vector3 p = ground + Ringward() * radius * Random.Range(0.3f, 1f) + Vector3.up * Random.Range(0.1f, 0.8f);
            Emit(Ch.Wisp, p, Vector3.up * Random.Range(1.2f, 2.8f) + Around(0.4f), size * Scale * Random.Range(0.7f, 1.3f), Random.Range(0.4f, 0.75f), A(color, 0.85f));
        }

        n = Count(count * 1.5f);
        for (int i = 0; i < n; i++)
            Emit(Ch.Ember, ground + Ringward() * radius * Random.Range(0.2f, 1f) + Vector3.up * Random.Range(0f, 0.6f), Vector3.up * Random.Range(1f, 3f) + Around(0.5f),
                Random.Range(0.06f, 0.12f), Random.Range(0.5f, 1.1f), A(color, 1f));
    }

    /// <summary>from 에서 to 로 날아가는 빛 알갱이 (흡혈 · 모으기).</summary>
    public static void Fly(Vector3 from, Vector3 to, Color color, float count, float life = 0.3f)
    {
        if (!Ready) return;
        int n = Count(count);
        for (int i = 0; i < n; i++)
        {
            float t = life * Random.Range(0.8f, 1.3f);
            Vector3 start = from + Around(0.25f);
            if ((i & 1) == 0) Emit(Ch.Spark, start, (to - start) / t + Vector3.up * 1.5f, Random.Range(0.07f, 0.11f), t, A(color, 1f));
            else Emit(Ch.Ember, start, (to - start) / t, Random.Range(0.09f, 0.15f), t, A(Color.Lerp(color, Color.white, 0.3f), 1f));
        }
    }

    // ───────── 도깨비불 (2026-11-20 도깨비 방망이) — 색을 그대로 쓰는 불 ─────────

    /// <summary>도깨비불 한 덩이 — 날아가는 타구 · 맴도는 불 · 홀린 기운이 프레임마다 부른다.</summary>
    public static void GhostPuff(Vector3 at, Vector3 vel, float size, float life, Color color)
    {
        if (!Ready) return;
        Emit(Ch.GhostFire, at, vel, size * Scale, life, color);
    }

    /// <summary>솟는 도깨비불 하나 — 아래 끝이 at (바닥 · 무기 머리).</summary>
    public static void GhostLick(Vector3 at, float width, float height, float life, Color color)
    {
        if (!Ready) return;
        Emit(Ch.GhostFlame, at, Vector3.up * Random.Range(0.3f, 0.9f), width * Scale, life, color, 0f, 0f, height * Scale);
    }

    /// <summary>둘레에서 솟는 도깨비불 (FlameRing 의 도깨비불판).</summary>
    public static void GhostFlameRing(Vector3 ground, float radius, Color color, float count, float height, float life = 0.45f)
    {
        if (!Ready) return;
        int n = Count(count);
        float turn = Random.Range(0f, 360f);
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Quaternion.AngleAxis(turn + i * 360f / Mathf.Max(1, n) + Random.Range(-12f, 12f), Vector3.up) * Vector3.forward;
            Emit(Ch.GhostFlame, ground + d * radius * Random.Range(0.85f, 1.1f) + Vector3.up * 0.05f, Vector3.up * Random.Range(0.4f, 1.2f), height * Random.Range(0.38f, 0.55f),
                life * Random.Range(0.8f, 1.2f), A(color, 0.9f), Random.Range(0f, 0.08f), 0f, height * Random.Range(0.8f, 1.2f));
        }
    }

    /// <summary>
    /// 도깨비불 폭발 — 섬광 · 도깨비불 덩어리 · 솟는 도깨비불 고리 · 혼불 기운 · 불티 · 반짝임 · 충격파 두 겹 · 그을음.
    /// at 은 바닥 자리, radius 는 폭발 판정 반경(m). 모습만 — 피해와 무관.
    /// </summary>
    public static void GhostBlast(Vector3 at, float radius, Color color, float scale = 1f)
    {
        if (!Ready) return;
        Explosions++;
        float crowd = Crowd();
        float r = radius * scale * Scale;
        Vector3 center = at + Vector3.up * 0.6f;
        Color white = A(Color.Lerp(color, Color.white, 0.65f), 1f);
        Color tint = A(color, 0.95f);

        Emit(Ch.Burst, center, Vector3.zero, r * 2.2f, 0.16f, white);
        if (burstCount <= 4) Emit(Ch.Flare, center, Vector3.zero, r * 2.8f, 0.14f, A(white, 0.9f));

        int n = Count(16f * crowd);
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Burst(0.8f);
            Emit(Ch.GhostFire, center + d * r * Random.Range(0.05f, 0.3f), d * r * Random.Range(1f, 2.4f) + Vector3.up * 1.1f, r * Random.Range(0.45f, 0.8f),
                Random.Range(0.4f, 0.65f), tint, Random.Range(0f, 0.05f));
        }
        GhostFlameRing(at, r * 0.8f, color, 14f * crowd, r * 1.2f, 0.55f);

        n = Count(6f * crowd);
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Burst(1f);
            Emit(Ch.Wisp, center + d * r * 0.2f, d * r * Random.Range(0.8f, 2f) + Vector3.up, r * Random.Range(0.7f, 1.2f), Random.Range(0.5f, 0.8f), A(color, 0.8f));
        }

        n = Count(26f * scale);
        for (int i = 0; i < n; i++)
            Emit(Ch.Spark, center, Burst(0.9f) * r * Random.Range(3f, 7f), Random.Range(0.08f, 0.13f), Random.Range(0.3f, 0.6f), Color.Lerp(tint, white, Random.value * 0.5f));

        n = Count(20f * scale);
        for (int i = 0; i < n; i++)
            Emit(Ch.Ember, center + Around(r * 0.7f), Around(1.2f) + Vector3.up * Random.Range(0.6f, 2f), Random.Range(0.1f, 0.18f), Random.Range(0.7f, 1.4f), tint,
                Random.Range(0f, 0.25f));

        n = Count(8f);
        for (int i = 0; i < n; i++)
            Emit(Ch.Star, center + Around(r), Vector3.zero, r * Random.Range(0.3f, 0.55f), Random.Range(0.3f, 0.6f), white, Random.Range(0.05f, 0.35f));

        Shockwave(at, r * 2.6f, color, 0.32f, 0.95f);
        Wave(at, r * 3.4f, color, 0.5f, 0.6f);
        Scorch(at, r * 1.5f, 0.7f);
    }

    /// <summary>별 하나 — 그 자리에 잠깐 (기절 표시 · 반짝임). 돌지 않게 0도로 시작한다.</summary>
    public static void Spot(Vector3 at, Color color, float size, float life)
    {
        if (!Ready) return;
        Emit(Ch.Star, at, Vector3.zero, size * Scale, life, A(color, 1f), 0f, 0f);
    }

    /// <summary>튀어 오르는 파편 뭉치 (붕괴 · 큰 충격).</summary>
    public static void Debris(Vector3 at, float radius, float count)
    {
        if (!Ready) return;
        int n = Count(count);
        for (int i = 0; i < n; i++)
            Emit(Ch.Shard, at + Vector3.up * 0.4f, Burst(1.2f) * radius * Random.Range(1.5f, 3f), radius * Random.Range(0.3f, 0.55f) * Scale, Random.Range(0.5f, 0.85f),
                new Color(0.2f, 0.16f, 0.14f, 0.95f));
    }

    /// <summary>빛줄기를 따라 흩어지는 알갱이 · 반짝임 (지속 빔 · 예고선). count 는 이번에 낼 수.</summary>
    public static void BeamDust(Vector3 from, Vector3 to, Color color, float count, float spread = 0.5f, bool stars = true, float wisp = 0f)
    {
        if (!Ready) return;
        int n = Count(count);
        for (int i = 0; i < n; i++)
            Emit(Ch.Ember, Vector3.Lerp(from, to, Random.value) + Around(spread * 0.5f), Around(spread * 2.5f) + Vector3.up * Random.Range(0f, 1f), Random.Range(0.14f, 0.3f),
                Random.Range(0.3f, 0.7f), A(color, 1f));

        // wisp > 0 — 줄기를 따라 일렁이는 기운도 (그 크기로)
        if (wisp > 0f)
        {
            n = Count(count * 0.2f);
            for (int i = 0; i < n; i++)
                Emit(Ch.Wisp, Vector3.Lerp(from, to, Random.value) + Around(spread * 0.6f), Around(spread) + Vector3.up * Random.Range(0.3f, 1.2f), wisp * Scale * Random.Range(0.7f, 1.3f),
                    Random.Range(0.35f, 0.6f), A(color, 0.8f));
        }

        if (!stars) return;
        n = Count(count * 0.35f);
        for (int i = 0; i < n; i++)
            Emit(Ch.Star, Vector3.Lerp(from, to, Random.value) + Around(spread), Vector3.zero, Random.Range(0.5f, 1.1f) * Scale, Random.Range(0.2f, 0.45f),
                A(Color.Lerp(color, Color.white, 0.6f), 1f));
    }
}
