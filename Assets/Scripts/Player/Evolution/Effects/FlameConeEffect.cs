using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상위 무기 "용의 포효" (샷건 + 용의 숨결) — 산탄 대신 화염 방사.
///  · 쏠 때마다(연사 간격 = 틱) 정면 range · angle 부채꼴의 적 전부에 피해 + 화상
///  · 화상 중에 죽은 적은 주변 spreadRadius 의 적에게 불을 옮긴다
/// 상위 무기 데이터는 spawnProjectiles 를 끄고 연사를 틱 간격(0.1초 안팎)으로 둔다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Flame Cone", fileName = "AE_EVO_FlameCone")]
public class FlameConeEffect : AttachmentEffect
{
    [Min(1f)] public float range = 6f;
    [Range(10f, 180f)] public float angle = 50f;
    [Tooltip("한 틱 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float tickDamageMul = 1f;

    [Header("화상")]
    [Tooltip("화상 초당 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float burnRatio = 2f;
    [Min(0f)] public float burnDuration = 3f;
    [Tooltip("화상 중 죽으면 이 반경에 불이 옮는다")]
    [Min(0f)] public float spreadRadius = 2f;

    [Header("연출")]
    [Tooltip("화염 입자 재질 (반투명 · 부드러운 점)")]
    public Material flameMaterial;
    public Material lineMaterial;
    public Color color = new Color(1f, 0.45f, 0.12f, 1f);
    [Range(1, 40)] public int particlesPerTick = 18;

    public static int Ticks { get; private set; }
    public static int Spreads { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Ticks = 0; Spreads = 0; }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly FlameConeEffect cfg;
        readonly List<Transform> found = new List<Transform>();
        readonly List<Transform> near = new List<Transform>();

        // 불붙인 적 — 화상이 끝나기 전에 죽으면 옮긴다. 죽으면 풀로 돌아가니 마지막 자리를 들고 있는다
        readonly Dictionary<Transform, float> ignited = new Dictionary<Transform, float>();
        readonly Dictionary<Transform, Vector3> lastSeen = new Dictionary<Transform, Vector3>();
        readonly List<Transform> scratch = new List<Transform>();

        ParticleSystem jet;

        public Runtime(FlameConeEffect cfg) => this.cfg = cfg;

        public override void OnAttack(in WeaponAttack attack)
        {
            if (Weapon == null) return;

            Transform player = Weapon.Controller != null ? Weapon.Controller.transform : Weapon.transform;
            Vector3 dir = EvoStrikes.Flat(attack.Direction, player.forward);
            Vector3 origin = player.position;

            float damage = Weapon.Stats.Damage * cfg.tickDamageMul;
            float burnDps = Weapon.Stats.Damage * cfg.burnRatio;
            float until = Time.time + cfg.burnDuration;

            CombatFx.FindEnemies(origin, cfg.range, found);
            foreach (Transform t in found)
            {
                Vector3 to = EvoStrikes.Flat(t.position - origin, dir);
                if (Vector3.Angle(dir, to) > cfg.angle * 0.5f && (t.position - origin).sqrMagnitude > 1f) continue;

                CombatFx.Damage(t, damage, CombatFx.Body(t), to);
                BurnTicker.Apply(t, burnDps, cfg.burnDuration);
                ignited[t] = until;
                lastSeen[t] = t.position;
            }

            Ticks++;
            Emit(attack.Origin, dir);
        }

        public override void Tick(float deltaTime)
        {
            if (ignited.Count == 0) return;

            scratch.Clear();
            scratch.AddRange(ignited.Keys);

            foreach (Transform t in scratch)
            {
                if (t != null && CombatFx.IsAlive(t))
                {
                    if (ignited[t] < Time.time) Forget(t);
                    else lastSeen[t] = t.position;
                    continue;
                }

                // 화상 중에 죽었다 — 둘레로 불을 옮긴다
                if (ignited[t] >= Time.time && lastSeen.TryGetValue(t, out Vector3 at)) Spread(at);
                Forget(t);
            }
        }

        void Spread(Vector3 at)
        {
            if (cfg.spreadRadius <= 0f || Weapon == null) return;

            CombatFx.FindEnemies(at, cfg.spreadRadius, near);
            if (near.Count == 0) return;

            float burnDps = Weapon.Stats.Damage * cfg.burnRatio;
            float until = Time.time + cfg.burnDuration;

            foreach (Transform n in near)
            {
                if (BurnTicker.IsBurning(n)) continue;
                BurnTicker.Apply(n, burnDps, cfg.burnDuration);
                ignited[n] = until;
                lastSeen[n] = n.position;
                FxLine.Bolt(cfg.lineMaterial, at + Vector3.up * 0.6f, CombatFx.Body(n), cfg.color, 0.12f, 0.25f, 0.2f);
                Spreads++;
            }

            FxLine.Ring(cfg.lineMaterial, at, 0.2f, cfg.spreadRadius, cfg.color, 0.16f, 0.3f);
        }

        void Forget(Transform t)
        {
            ignited.Remove(t);
            lastSeen.Remove(t);
        }

        public override void OnActiveChanged(bool active)
        {
            if (!active && jet != null) jet.Clear();
        }

        public override void Dispose()
        {
            if (jet != null) Object.Destroy(jet.gameObject);
            jet = null;
            ignited.Clear();
            lastSeen.Clear();
        }

        // ───────── 화염 입자 ─────────

        void Emit(Vector3 origin, Vector3 dir)
        {
            if (cfg.flameMaterial == null) return;
            if (jet == null) jet = MakeJet();

            jet.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(dir));
            if (!jet.isPlaying) jet.Play();
            jet.Emit(cfg.particlesPerTick);
        }

        ParticleSystem MakeJet()
        {
            var go = new GameObject("DragonFlame");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float life = 0.38f;

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.75f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(cfg.range / life * 0.85f, cfg.range / life * 1.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 1.0f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = cfg.angle * 0.42f;
            shape.radius = 0.12f;

            // 흰 노랑 → 주황 → 검붉게 꺼진다
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f),
                    new GradientColorKey(Color.Lerp(cfg.color, Color.yellow, 0.3f), 0.25f),
                    new GradientColorKey(cfg.color, 0.6f),
                    new GradientColorKey(new Color(0.45f, 0.08f, 0.04f), 1f),
                },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.85f, 0.45f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            // 멀리 갈수록 커진다 (불길이 퍼진다)
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 2.8f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = cfg.flameMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return ps;
        }
    }
}
