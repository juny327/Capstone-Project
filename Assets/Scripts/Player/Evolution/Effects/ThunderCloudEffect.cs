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

    [Header("낙뢰 모습 (2026-11-14) — 판정 · 피해와 무관")]
    [Tooltip("줄기 굵기(m)")]
    [Min(0.05f)] public float strikeWidth = 0.3f;
    [Tooltip("낙뢰가 시작하는 높이 — 맞는 자리 위(m). 0 이면 예전처럼 캐릭터 머리 위 구름에서 비스듬히 뻗는다")]
    [Min(0f)] public float strikeHeight = 8f;
    [Tooltip("시작 자리를 구름 쪽으로 기울이는 정도 (0 = 맞는 자리 바로 위 · 1 = 구름 바로 아래)")]
    [Range(0f, 1f)] public float strikeLean = 0.25f;
    [Tooltip("맞은 자리에서 바닥을 타고 퍼지는 전기 가지 수")]
    [Range(0, 6)] public int strikeBranches = 4;
    [Tooltip("맞은 자리 바닥 빛 반경(m). 0 이면 판정 반경 × 1.3")]
    [Min(0f)] public float strikeGlowRadius = 1.5f;
    [Tooltip("낙뢰마다 흔들림")]
    [Min(0f)] public float strikeShake = 0.05f;
    [Tooltip("낙뢰가 떨어질 때 구름 안이 번쩍이는 세기 (0 이면 없음)")]
    [Min(0f)] public float cloudFlash = 1f;

    [Header("보조 칸 (무기-개편-개발-계획)")]
    [Tooltip("보조 무기(전격 드론)는 장전하지 않는다 — 대신 이 주기마다 먹구름을 띄운다 (초)")]
    [Min(1f)] public float supportPeriod = 8f;
    [Tooltip("한 번 띄운 먹구름이 낙뢰를 쏟는 시간 (초)")]
    [Min(0.2f)] public float supportStormTime = 2f;

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

        float supportClock;
        bool supportStorming;

        public override void Tick(float deltaTime)
        {
            bool storming = Weapon != null && Weapon.IsActive && (Weapon.IsSupport ? SupportStorm(deltaTime) : Weapon.IsReloading);
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

            // 낙뢰는 맞는 자리 위 높은 곳에서 내려꽂힌다 (구름 쪽으로 조금 기울여서) — 구름에서 비스듬히 뻗으면 기본 사격 줄기와 비슷해 보였다
            Vector3 at = target.position;
            Vector3 top = cfg.strikeHeight > 0f
                ? Vector3.Lerp(at, new Vector3(sky.x, at.y, sky.z), cfg.strikeLean) + Vector3.up * cfg.strikeHeight
                : sky;
            float scale = Weapon.IsSupport ? 0.7f : 1f;   // 보조 칸은 작고 옅게

            EvoStrikes.Lightning(cfg.material, cfg.color, top, at, cfg.radius, Weapon.Stats.Damage * cfg.damageMul, cfg.stun, cfg.impactPrefab,
                cfg.strikeWidth, cfg.strikeBranches, cfg.strikeGlowRadius, scale);
            WeaponBase.ShakeCamera(Weapon, cfg.strikeShake);   // 보조 무기는 약하게 (WeaponBase.ShakeCamera)
            CloudFlash(sky, scale);
            Strikes++;

            if (soundTimer <= 0f)
            {
                soundTimer = 0.12f;
                CombatFx.PlaySound(cfg.sound);
            }
        }

        /// <summary>낙뢰가 떨어지는 순간 구름 안이 번쩍인다 — 빛 덩어리 하나 + 구름 속을 가로지르는 짧은 줄기.</summary>
        void CloudFlash(Vector3 sky, float scale)
        {
            if (cfg.cloudFlash <= 0f) return;

            Color glow = Color.Lerp(cfg.color, Color.white, 0.5f);
            GroundGlow.Flash(sky - Vector3.up * 0.3f, cfg.cloudRadius * 0.65f, glow, 0.14f, cfg.cloudFlash * 1.2f * scale);

            Vector3 d = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * Vector3.forward * cfg.cloudRadius * Random.Range(0.5f, 0.9f);
            FxLine.Bolt(cfg.material, sky - d, sky + d, glow, 0.1f, 0.12f, 0.35f);
        }

        /// <summary>보조 칸 — 장전이 없으니 supportPeriod 마다 supportStormTime 동안 먹구름 (8초마다 2초).</summary>
        bool SupportStorm(float deltaTime)
        {
            float period = Mathf.Max(cfg.supportPeriod, cfg.supportStormTime + 0.1f);
            supportClock += deltaTime;
            if (supportClock >= period) supportClock -= period;

            bool on = supportClock >= period - cfg.supportStormTime;
            if (on && !supportStorming) timer = 0.15f;   // 장전 시작과 같은 첫 낙뢰 지연
            supportStorming = on;
            return on;
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
