using UnityEngine;

/// <summary>
/// 상위 무기 "회전 기관포" (기관단총 + 이중 탄창) — 쏘는 동안 연사가 spinUpTime 에 걸쳐 maxBoost 배까지 오르고,
/// 멈추면 spinDownTime 에 걸쳐 내려간다. 무기의 다총열 부속(barrelName)이 연사에 맞춰 돈다.
/// 장전이 없는 것(탄창 무한)은 상위 무기 데이터의 탄창 0 이 맡는다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Spin Up", fileName = "AE_EVO_SpinUp")]
public class SpinUpEffect : AttachmentEffect
{
    [Min(1f)] public float maxBoost = 3f;
    [Min(0.1f)] public float spinUpTime = 2f;
    [Min(0.1f)] public float spinDownTime = 1f;
    [Tooltip("마지막 발 뒤 이만큼 지나면 '안 쏜다'로 본다")]
    [Min(0.05f)] public float idleGrace = 0.35f;

    [Header("다총열")]
    public string barrelName = "SpinBarrels";
    [Tooltip("최고 연사일 때 도는 속도 (도/초)")]
    public float barrelMaxSpeed = 1440f;

    [Header("회전음 (2026-11-12)")]
    [Tooltip("쏘는 동안 반복하는 낮은 모터 험 — 회전이 오를수록 피치가 오르고, 다 멈추면 빠진다. 비우면 소리 없음")]
    public AudioClip spinLoop;
    [Range(0f, 1f)] public float spinLoopVolume = 0.45f;
    [Tooltip("회전 0 → 최고일 때 피치")]
    public Vector2 spinLoopPitch = new Vector2(0.75f, 1.25f);

    [Header("달아오르는 총열 (2026-11-14) — 연사가 오를수록 붉게, 내려가면 식는다")]
    [Tooltip("총열 위에 겹쳐 그리는 빛의 재질 (FxLine.mat). 비우면 없음")]
    public Material heatMaterial;
    public Color heatColor = new Color(1f, 0.28f, 0.08f, 1f);
    [Tooltip("빛의 굵기(m) — 총열 묶음의 지름쯤")]
    [Min(0f)] public float heatWidth = 0.24f;
    [Tooltip("빛의 길이(m) — 다총열 부속의 가운데에서 앞뒤로 반씩")]
    [Min(0f)] public float heatLength = 0.4f;
    [Tooltip("회전이 이만큼 오른 뒤부터 달아오른다 (0~1)")]
    [Range(0f, 0.95f)] public float heatFrom = 0.3f;
    [Tooltip("총열 축에서 위로 띄우는 높이(m) — 축 한가운데에 그리면 둘레의 총열에 가려 안 보인다 (위에서 보는 카메라)")]
    [Min(0f)] public float heatLift = 0.13f;

    [Header("총구 연기 — 연사가 높을 때")]
    [Tooltip("연기 재질 (EVO_Cloud.mat). 비우면 없음")]
    public Material smokeMaterial;
    [Tooltip("회전이 이만큼 오른 뒤부터 연기가 난다 (0~1)")]
    [Range(0f, 0.95f)] public float smokeFrom = 0.75f;
    [Tooltip("최고 연사일 때 1초에 나오는 연기 뭉치 수")]
    [Min(0f)] public float smokeRate = 14f;

    [Header("최고 연사 흔들림")]
    [Tooltip("연사가 최고일 때 shakeEvery 발마다 한 번 흔든다. 0 이면 없음")]
    [Min(0f)] public float maxSpinShake = 0.04f;
    [Min(1)] public int shakeEvery = 4;

    [Header("공통 재료 (2026-11-20)")]
    [Tooltip("공통 재료(FxKit)로 총구 불 · 과열 기운을 더한다. 끄면 예전 연출")]
    public bool kitMuzzle = true;

    /// <summary>마지막으로 걸린 연사 배율 (테스트).</summary>
    public static float LastBoost { get; private set; } = 1f;
    /// <summary>총열 빛의 진하기 0~1 · 지금 떠 있는 총구 연기 뭉치 수 (테스트).</summary>
    public static float LastHeat { get; private set; }
    public static int SmokeAlive { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { LastBoost = 1f; LastHeat = 0f; SmokeAlive = 0; }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly SpinUpEffect cfg;
        float spin;
        float sinceAttack = 99f;
        Transform barrels;
        LoopVoice hum;
        LineRenderer heat;
        ParticleSystem smoke;
        int hotShots;

        public Runtime(SpinUpEffect cfg) => this.cfg = cfg;

        /// <summary>회전음 (테스트).</summary>
        public LoopVoice Hum => hum;

        protected override void OnBind() => barrels = FindDeep(Weapon.transform, cfg.barrelName);

        public override void OnAttack(in WeaponAttack attack)
        {
            sinceAttack = 0f;
            if (cfg.kitMuzzle && FxKit.Ready) KitShot(attack);

            // 최고 연사 — 몇 발에 한 번 가볍게 흔든다. 보조 칸에서는 흔들지 않는다 (스스로 계속 쏘니 끊이지 않는 잔떨림이 된다)
            if (spin < 0.95f || cfg.maxSpinShake <= 0f || Weapon.IsSupport) return;
            if (++hotShots % Mathf.Max(1, cfg.shakeEvery) == 0) WeaponBase.ShakeCamera(Weapon, cfg.maxSpinShake);
        }

        bool wasHot;

        /// <summary>공통 재료 (2026-11-20) — 쏠 때마다 총구에서 불을 뿜는다. 달아오를수록 크고, 최고 연사에 들어서는 순간 한 번 크게 터진다.</summary>
        void KitShot(in WeaponAttack attack)
        {
            float scale = Weapon.EvoFxScale;
            FxKit.MuzzleJet(attack.Origin, attack.Direction, spin, cfg.heatColor, scale);

            bool hot = spin >= 0.95f;
            if (hot && !wasHot)
            {
                Vector3 floor = attack.Origin;
                floor.y = Weapon.Controller != null ? Weapon.Controller.transform.position.y : floor.y - 1.2f;
                FxKit.EnergyBurst(floor, 1.8f, cfg.heatColor, scale);
                FxKit.MuzzleBlast(attack.Origin, attack.Direction, 1f * scale, cfg.heatColor, floor.y);
            }
            wasHot = hot;
        }

        public override void OnHit(in WeaponHit hit)
        {
            // 공통 재료 (2026-11-20) — 맞은 자리마다 섬광과 불꽃이 튄다 (달아오를수록 크게). 총알 세례가 보이게
            if (!cfg.kitMuzzle || !FxKit.Ready || !EvoStrikes.Take(6)) return;
            FxKit.Hit(hit.Point, -hit.Direction, cfg.heatColor, (1f + 0.8f * spin) * Weapon.EvoFxScale);
        }

        public override void Tick(float deltaTime)
        {
            sinceAttack += deltaTime;

            bool firing = Weapon.IsActive && sinceAttack < cfg.idleGrace;
            spin = Mathf.Clamp01(spin + (firing ? deltaTime / cfg.spinUpTime : -deltaTime / cfg.spinDownTime));

            float boost = 1f + (cfg.maxBoost - 1f) * spin;
            Weapon.FireRateBoost = boost;
            if (Weapon.IsActive) LastBoost = boost;

            if (barrels != null) barrels.Rotate(Vector3.forward, (90f + cfg.barrelMaxSpeed * spin) * deltaTime, Space.Self);

            // 쏘는 모습(섬광 크기 · 궤적 길이와 색)이 연사에 따라 달라진다 — ShotFx 의 hot 값
            Weapon.ShotHeat = Weapon.IsActive ? spin : 0f;
            UpdateHeat();
            UpdateSmoke();

            // 공통 재료 (2026-11-20) — 달아오른 총열에서 불티가 계속 날린다
            if (cfg.kitMuzzle && Weapon.IsActive && spin > 0.6f && FxKit.Ready && Weapon.MuzzlePoint != null)
            {
                Transform muzzle = Weapon.MuzzlePoint;
                FxKit.TrailDust(muzzle.position - muzzle.forward * 0.3f, cfg.heatColor, 40f * spin * deltaTime * Weapon.EvoFxScale, 0.09f);
            }

            UpdateHum(firing);
        }

        /// <summary>총열 위에 겹쳐 그리는 붉은 빛 — 회전이 heatFrom 을 넘으면 서서히 달아오르고, 회전이 내려가면 같이 식는다.</summary>
        void UpdateHeat()
        {
            if (cfg.heatMaterial == null || barrels == null || cfg.heatWidth <= 0f || cfg.heatLength <= 0f) return;

            float glow = Weapon.IsActive ? Mathf.InverseLerp(cfg.heatFrom, 1f, spin) : 0f;
            if (Weapon.IsActive) LastHeat = glow;

            if (heat == null)
            {
                if (glow <= 0f) return;
                heat = MakeHeat();
            }

            heat.enabled = glow > 0.001f;
            if (!heat.enabled) return;

            // 달아오를수록 붉은색 → 주황 기운
            Color c = Color.Lerp(cfg.heatColor, new Color(1f, 0.45f, 0.15f, 1f), glow * glow * 0.35f);
            c.a = Mathf.Lerp(0.12f, 0.95f, glow);
            heat.startColor = c;
            heat.endColor = new Color(c.r, c.g, c.b, c.a * 0.7f);

            // 총열 묶음 바로 위에 — 총이 도는 대로 따라간다
            Vector3 axis = barrels.forward;
            Vector3 centre = barrels.position + Vector3.up * cfg.heatLift;
            heat.SetPosition(0, centre - axis * cfg.heatLength * 0.5f);
            heat.SetPosition(1, centre + axis * cfg.heatLength * 0.5f);
        }

        LineRenderer MakeHeat()
        {
            // 무기에 붙여 둔다 (무기가 꺼지면 같이 꺼진다) — 자리는 UpdateHeat 가 프레임마다 잡는다
            var go = new GameObject("BarrelHeat");
            go.transform.SetParent(Weapon.transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = cfg.heatMaterial;
            line.positionCount = 2;
            line.startWidth = cfg.heatWidth;
            line.endWidth = cfg.heatWidth;
            line.numCapVertices = 2;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = 2;   // 총구 섬광 위에
            return line;
        }

        /// <summary>총구 연기 — 회전이 smokeFrom 을 넘으면 피어오른다. 연사가 내려가는 동안에도 조금 남는다.</summary>
        void UpdateSmoke()
        {
            if (cfg.smokeMaterial == null || cfg.smokeRate <= 0f) return;

            float amount = Weapon.IsActive ? Mathf.InverseLerp(cfg.smokeFrom, 1f, spin) : 0f;
            if (smoke == null)
            {
                if (amount <= 0f) return;
                smoke = MakeSmoke();
            }

            Transform muzzle = Weapon.MuzzlePoint;
            smoke.transform.position = muzzle.position + muzzle.forward * 0.3f;

            ParticleSystem.EmissionModule emission = smoke.emission;
            emission.rateOverTime = cfg.smokeRate * amount * Weapon.EvoFxScale;
            if (Weapon.IsActive) SmokeAlive = smoke.particleCount;
        }

        ParticleSystem MakeSmoke()
        {
            var go = new GameObject("MinigunSmoke");
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // 위로 피어오른다
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.62f, 0.6f, 0.58f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            main.playOnAwake = true;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.05f;

            ParticleSystem.SizeOverLifetimeModule grow = ps.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 2.2f)));

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = cfg.smokeMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ps.Play();
            return ps;
        }

        /// <summary>쏘기 시작하면 들어오고 · 회전에 따라 피치가 오르내리고 · 회전이 다 멈추면 빠진다 (보조 무기는 ×0.5).</summary>
        void UpdateHum(bool firing)
        {
            if (cfg.spinLoop == null || Weapon == null) return;

            // 쏘는 동안은 매 프레임 '켜 둠' — 빠지는 중에 다시 쏘면 그 자리에서 다시 들어온다 (이미 재생 중이면 처음부터 다시 틀지 않는다)
            if (firing)
            {
                if (hum == null) hum = LoopVoice.On(Weapon.transform, "SpinHum");
                hum.Play(cfg.spinLoop, cfg.spinLoopVolume * Weapon.SoundScale, cfg.spinLoopPitch.x, 0.3f);
            }

            if (hum == null || !hum.Playing) return;

            hum.SetPitch(Mathf.Lerp(cfg.spinLoopPitch.x, cfg.spinLoopPitch.y, spin));
            if (!firing && spin <= 0f) hum.Stop(0.4f);
        }

        public override void OnActiveChanged(bool active)
        {
            if (active) return;
            spin = 0f;
            hotShots = 0;
            Weapon.FireRateBoost = 1f;
            Weapon.ShotHeat = 0f;
            if (heat != null) heat.enabled = false;
            if (smoke != null)
            {
                ParticleSystem.EmissionModule emission = smoke.emission;
                emission.rateOverTime = 0f;
            }
            if (hum != null) hum.Stop(0.2f);
        }

        public override void Dispose()
        {
            if (hum != null) Object.Destroy(hum.gameObject);
            hum = null;
            if (heat != null) Object.Destroy(heat.gameObject);
            heat = null;
            if (smoke != null) Object.Destroy(smoke.gameObject);
            smoke = null;
            if (Weapon != null) Weapon.ShotHeat = 0f;
        }

        static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name)) return null;
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
