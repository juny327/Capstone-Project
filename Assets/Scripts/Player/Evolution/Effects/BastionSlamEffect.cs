using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상위 무기 "성채 대검" (대검 + 에너지 방패) — 내려찍기가 360° 충격파가 된다.
/// 방패로 막은 피해를 모아(× storeRatio, 최대 공격력 × storeCap) 다음 내려찍기 충격파에 더한다.
/// 모은 만큼 검신 둘레에 노란 고리가 밝아진다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Bastion Slam", fileName = "AE_EVO_BastionSlam")]
public class BastionSlamEffect : AttachmentEffect
{
    [Min(1f)] public float radius = 5f;
    [Tooltip("충격파 피해 = 무기 공격력 × 이 값 + 모은 피해")]
    [Min(0f)] public float damageMul = 1.5f;
    [Min(0f)] public float push = 2f;
    [Min(0f)] public float stun = 0.8f;

    [Header("막은 피해 모으기")]
    [Tooltip("막은 피해 × 이 값을 모은다")]
    [Min(0f)] public float storeRatio = 3f;
    [Tooltip("모으는 최대치 = 무기 공격력 × 이 값")]
    [Min(0f)] public float storeCap = 5f;

    [Header("연출")]
    public Material material;
    public Color color = new Color(1f, 0.85f, 0.25f, 1f);
    public GameObject impactPrefab;
    [Min(0f)] public float cameraShake = 0.8f;
    public GameSoundSet.Entry slamSound = new GameSoundSet.Entry();

    public static int Slams { get; private set; }
    public static int SlamHits { get; private set; }
    public static float LastBonus { get; private set; }
    public static float Stored { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Slams = 0; SlamHits = 0; LastBonus = 0f; Stored = 0f; }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly BastionSlamEffect cfg;
        readonly List<Transform> found = new List<Transform>();
        ShieldAbility shield;
        float stored;
        float glowTimer;

        public Runtime(BastionSlamEffect cfg) => this.cfg = cfg;

        public override void Tick(float deltaTime)
        {
            if (shield == null) FindShield();

            // 모은 만큼 검 둘레 고리 — 가득 차면 0.25초마다 반짝
            if (stored <= 0f || Weapon == null || !Weapon.IsActive || Weapon.Controller == null) return;
            glowTimer -= deltaTime;
            if (glowTimer > 0f) return;

            float fill = Mathf.Clamp01(stored / Mathf.Max(1f, Weapon.Stats.Damage * cfg.storeCap));
            glowTimer = Mathf.Lerp(0.6f, 0.25f, fill);
            Color c = cfg.color;
            c.a = 0.3f + 0.7f * fill;
            FxLine.Ring(cfg.material, Weapon.Controller.transform.position, 0.6f, 0.9f + fill * 0.6f, c, 0.06f + fill * 0.08f, glowTimer);
        }

        public override void OnAttack(in WeaponAttack attack)
        {
            if (!attack.Vertical || Weapon == null) return;

            Vector3 at = attack.Origin;
            float bonus = stored;
            float damage = Weapon.Stats.Damage * cfg.damageMul + bonus;
            stored = 0f;
            Stored = 0f;
            LastBonus = bonus;

            CombatFx.FindEnemies(at, cfg.radius, found);
            foreach (Transform t in found)
            {
                Vector3 away = EvoStrikes.Flat(t.position - at, attack.Direction);
                CombatFx.Damage(t, damage, CombatFx.Body(t), away);
                CombatFx.Stun(t, cfg.stun);
                CombatFx.Push(Weapon, t, away, cfg.push, 0.25f);
                SlamHits++;
            }

            // 바닥 균열 고리 — 세 겹으로 퍼진다
            FxLine.Ring(cfg.material, at, 0.5f, cfg.radius, cfg.color, 0.5f, 0.35f);
            FxLine.Ring(cfg.material, at, 0.3f, cfg.radius * 0.7f, Color.white, 0.25f, 0.25f);
            FxLine.Ring(cfg.material, at, cfg.radius * 0.9f, cfg.radius * 1.1f, cfg.color * new Color(1f, 1f, 1f, 0.5f), 0.2f, 0.45f);
            for (int i = 0; i < 8; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 45f + Random.Range(-12f, 12f), 0f) * Vector3.forward;
                FxLine.Bolt(cfg.material, at + d * 0.6f + Vector3.up * 0.05f, at + d * cfg.radius * Random.Range(0.7f, 1f) + Vector3.up * 0.05f, cfg.color, 0.12f, 0.5f, 0.25f);
            }
            EvoStrikes.Spawn(cfg.impactPrefab, at, cfg.color, 1.5f);

            float shake = cfg.cameraShake * (bonus > 0f ? 1.4f : 1f);
            if (shake > 0f && CameraShakeManager.Instance != null) CameraShakeManager.Instance.Shake(shake);
            CombatFx.PlaySound(cfg.slamSound);
            Slams++;
        }

        public override void Dispose()
        {
            if (shield != null) shield.OnAbsorbed -= OnAbsorbed;
            shield = null;
        }

        void FindShield()
        {
            SubAbilitySlot slot = SubAbilitySlot.Current;
            if (slot == null) return;

            for (int i = 0; i < slot.SlotCount; i++)
            {
                if (slot.Get(i) is not ShieldAbility s) continue;
                shield = s;
                shield.OnAbsorbed += OnAbsorbed;
                return;
            }
        }

        void OnAbsorbed(float amount)
        {
            if (Weapon == null) return;
            float cap = Weapon.Stats.Damage * cfg.storeCap;
            stored = Mathf.Min(cap, stored + amount * cfg.storeRatio);
            Stored = stored;
        }
    }
}
