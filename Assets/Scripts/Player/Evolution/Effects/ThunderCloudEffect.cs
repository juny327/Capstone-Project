using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상위 무기 "뇌운" (전격 소총 + 방전) — 장전하는 동안 플레이어 머리 위에 먹구름이 끼고,
/// 장전이 끝날 때까지 주변 range 안의 적에게 낙뢰가 초당 strikesPerSecond 번 떨어진다.
/// 장전 시작 순간의 방전은 이어받은 개조(방전)가 그대로 맡는다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Thunder Cloud", fileName = "AE_EVO_ThunderCloud")]
public class ThunderCloudEffect : AttachmentEffect
{
    [Min(1f)] public float range = 8f;
    [Min(0.5f)] public float strikesPerSecond = 4f;
    [Min(0.3f)] public float radius = 1.2f;
    [Tooltip("낙뢰 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float damageMul = 1.5f;
    [Min(0f)] public float stun = 0.3f;

    [Header("먹구름")]
    [Min(1f)] public float cloudHeight = 5f;
    [Min(0.5f)] public float cloudRadius = 2.2f;
    [Tooltip("구름 입자 재질 (반투명 · 부드러운 점). 비우면 구름 없이 낙뢰만")]
    public Material cloudMaterial;
    public Color cloudColor = new Color(0.16f, 0.14f, 0.32f, 0.85f);

    [Header("낙뢰")]
    public Material material;
    public Color color = new Color(0.55f, 0.55f, 1f, 1f);
    public GameObject impactPrefab;
    public GameSoundSet.Entry sound = new GameSoundSet.Entry();

    public static int Strikes { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Strikes = 0;

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly ThunderCloudEffect cfg;
        readonly List<Transform> found = new List<Transform>();
        float timer;
        float soundTimer;
        ParticleSystem cloud;

        public Runtime(ThunderCloudEffect cfg) => this.cfg = cfg;

        public override void OnReloadStarted() => timer = 0.15f;

        public override void Tick(float deltaTime)
        {
            bool storming = Weapon != null && Weapon.IsActive && Weapon.IsReloading;
            Transform player = Weapon != null && Weapon.Controller != null ? Weapon.Controller.transform : null;

            UpdateCloud(storming, player);
            if (!storming || player == null) return;

            soundTimer -= deltaTime;
            timer -= deltaTime;
            if (timer > 0f) return;
            timer += 1f / cfg.strikesPerSecond;

            CombatFx.FindEnemies(player.position, cfg.range, found);
            if (found.Count == 0) return;

            Transform target = found[Random.Range(0, found.Count)];
            Vector3 sky = player.position + Vector3.up * cfg.cloudHeight
                          + new Vector3(Random.Range(-cfg.cloudRadius, cfg.cloudRadius), 0f, Random.Range(-cfg.cloudRadius, cfg.cloudRadius)) * 0.6f;

            EvoStrikes.Lightning(cfg.material, cfg.color, sky, target.position, cfg.radius, Weapon.Stats.Damage * cfg.damageMul, cfg.stun, cfg.impactPrefab);
            Strikes++;

            if (soundTimer <= 0f)
            {
                soundTimer = 0.12f;
                CombatFx.PlaySound(cfg.sound);
            }
        }

        public override void Dispose()
        {
            if (cloud != null) Object.Destroy(cloud.gameObject);
            cloud = null;
        }

        void UpdateCloud(bool storming, Transform player)
        {
            if (cfg.cloudMaterial == null) return;

            if (cloud == null)
            {
                if (!storming) return;
                cloud = MakeCloud();
            }

            if (player != null) cloud.transform.position = player.position + Vector3.up * cfg.cloudHeight;

            ParticleSystem.EmissionModule emission = cloud.emission;
            emission.enabled = storming;
        }

        ParticleSystem MakeCloud()
        {
            var go = new GameObject("ThunderCloud");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
            main.startColor = cfg.cloudColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            main.playOnAwake = true;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 40f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = cfg.cloudRadius;
            shape.scale = new Vector3(1f, 0.25f, 1f);

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = cfg.cloudMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ps.Play();
            return ps;
        }
    }
}
