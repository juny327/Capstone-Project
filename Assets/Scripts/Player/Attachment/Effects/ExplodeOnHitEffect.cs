using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 폭발탄 (개조) — 명중 지점 반경 안의 <b>다른</b> 적에게 피해 (커스터마이징-구현계획.md 4-4).
/// 맞은 적은 이미 탄 피해를 받았으니 빼고, 둘레의 적만 맞는다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Attachment Effects/Explode On Hit", fileName = "AE_Explode")]
public class ExplodeOnHitEffect : AttachmentEffect
{
    [Min(0.3f)] public float radius = 1.5f;
    [Tooltip("폭발 피해 = 맞힌 피해 × 이 값")]
    [Min(0f)] public float damageRatio = 0.4f;

    public Material material;
    public Color color = new Color(1f, 0.6f, 0.2f, 0.9f);
    public GameSoundSet.Entry sound = new GameSoundSet.Entry();
    [Tooltip("작은 폭발 이펙트 (비우면 고리만) — 2026-11-02")]
    public GameObject vfxPrefab;
    [Min(0f)] public float vfxScale = 0.6f;

    [Header("폭발 모습 더하기 (2026-11-14 — 스웜 카빈) · 값이 0 이면 예전 그대로 (고리 하나)")]
    [Tooltip("가운데 불꽃 덩어리의 반경(m) — 0.15초 번쩍인다")]
    [Min(0f)] public float fireball;
    [Tooltip("안쪽에 흰 고리를 하나 더")]
    public bool innerRing;
    [Tooltip("밖으로 튀는 파편 불꽃 줄 수")]
    [Range(0, 10)] public int shards;
    [Tooltip("터질 때 흔들림 — 여러 발이 이어 터져도 0.12초에 한 번만")]
    [Min(0f)] public float cameraShake;

    // 산탄 8발이 한 프레임에 터지면 소리가 겹쳐 터진다 — 짧게 한 번만
    static float lastSound;
    static float lastShake;

    /// <summary>시험용 — 불꽃 덩어리 · 파편을 그린 폭발 수.</summary>
    public static int RichBlasts { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { lastSound = 0f; lastShake = 0f; RichBlasts = 0; }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly ExplodeOnHitEffect cfg;
        readonly List<Transform> targets = new List<Transform>();

        public Runtime(ExplodeOnHitEffect cfg) => this.cfg = cfg;

        public override void OnHit(in WeaponHit hit)
        {
            Vector3 at = hit.Point;
            at.y = hit.Target != null ? hit.Target.position.y : at.y;

            CombatFx.FindEnemies(at, cfg.radius, targets, hit.Target);
            foreach (Transform t in targets)
            {
                Vector3 dir = t.position - at;
                dir.y = 0f;
                CombatFx.Damage(t, hit.EffectDamage * cfg.damageRatio, CombatFx.Body(t), dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward);
            }

            FxLine.Ring(cfg.material, at, cfg.radius * 0.25f, cfg.radius, cfg.color, 0.3f, 0.28f);
            GroundGlow.Flash(new Vector3(at.x, hit.Target != null ? hit.Target.position.y : at.y - 1f, at.z), cfg.radius * 1.3f, cfg.color, 0.22f);
            if (cfg.vfxPrefab != null && EvoStrikes.Take(6)) EvoStrikes.Spawn(cfg.vfxPrefab, at, cfg.color, cfg.vfxScale);
            Enrich(at);

            if (Time.time - lastSound > 0.08f)
            {
                lastSound = Time.time;
                CombatFx.PlaySound(cfg.sound, SoundKind.Explosion);
            }
        }

        /// <summary>불꽃 덩어리 · 안쪽 고리 · 파편 불꽃 · 흔들림 (값이 있는 것만). 보조 칸은 작고 옅게.</summary>
        void Enrich(Vector3 at)
        {
            if (cfg.fireball <= 0f && !cfg.innerRing && cfg.shards <= 0 && cfg.cameraShake <= 0f) return;

            float s = Weapon != null && Weapon.IsSupport ? 0.7f : 1f;
            Color hot = Color.Lerp(cfg.color, Color.white, 0.45f);

            if (cfg.innerRing) FxLine.Ring(cfg.material, at, cfg.radius * 0.1f, cfg.radius * 0.55f, Color.white, 0.14f, 0.16f);
            if (cfg.fireball > 0f)
            {
                GroundGlow.Flash(at + Vector3.up * 0.5f, cfg.fireball * s, hot, 0.15f, 2f * s);
                GroundGlow.Flash(at + Vector3.up * 0.55f, cfg.fireball * 0.45f * s, Color.white, 0.1f, 2f * s);   // 가운데 흰 심지
            }

            float turn = Random.Range(0f, 360f);
            for (int i = 0; i < cfg.shards; i++)
            {
                Vector3 d = Quaternion.AngleAxis(turn + i * 360f / cfg.shards + Random.Range(-18f, 18f), Vector3.up) * Vector3.forward;
                Vector3 from = at + Vector3.up * 0.4f + d * cfg.radius * 0.2f;
                Vector3 to = from + (d + Vector3.up * Random.Range(0f, 0.35f)) * cfg.radius * Random.Range(0.55f, 0.95f) * s;
                FxLine.Line(cfg.material, from, to, Color.Lerp(cfg.color, Color.white, 0.3f), 0.17f * s, 0.18f);
            }

            // 보조 칸에서는 흔들지 않는다 — 스스로 계속 쏘는 무기라 약한 흔들림이 끊이지 않는 잔떨림이 된다
            bool support = Weapon != null && Weapon.IsSupport;
            if (cfg.cameraShake > 0f && !support && Time.time - lastShake > 0.12f)
            {
                lastShake = Time.time;
                WeaponBase.ShakeCamera(Weapon, cfg.cameraShake);
            }

            RichBlasts++;
        }
    }
}
