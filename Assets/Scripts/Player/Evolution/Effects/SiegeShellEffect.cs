using UnityEngine;

/// <summary>
/// 상위 무기 "공성포" (샷건 + 슬러그) — 슬러그가 굵은 포탄이 된다.
///  · 맞힌 적마다 그 자리에서 작은 폭발 (둘레 적에게 피해 · 밀치기)
///  · 관통 무제한이라 사거리 끝까지 날아가 큰 폭발
///  · 쏠 때 총구 충격파 고리
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Siege Shell", fileName = "AE_EVO_SiegeShell")]
public class SiegeShellEffect : AttachmentEffect
{
    [Header("맞힐 때")]
    [Min(0.3f)] public float hitRadius = 1.5f;
    [Tooltip("둘레 피해 = 맞힌 피해 × 이 값")]
    [Min(0f)] public float hitRatio = 0.6f;
    [Min(0f)] public float push = 2f;

    [Header("사거리 끝")]
    [Min(0.5f)] public float endRadius = 3f;
    [Tooltip("끝 폭발 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float endRatio = 1.5f;

    [Header("연출")]
    public Material material;
    public Color color = new Color(1f, 0.75f, 0.25f, 1f);
    public GameObject explosionPrefab;
    [Min(0f)] public float cameraShake = 0.45f;
    public GameSoundSet.Entry blastSound = new GameSoundSet.Entry();

    public static int HitBlasts { get; private set; }
    public static int EndBlasts { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { HitBlasts = 0; EndBlasts = 0; }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly SiegeShellEffect cfg;
        public Runtime(SiegeShellEffect cfg) => this.cfg = cfg;

        public override void OnAttack(in WeaponAttack attack)
        {
            Vector3 at = attack.Origin;
            FxLine.Ring(cfg.material, at, 0.2f, 1.6f, cfg.color, 0.22f, 0.22f);
            if (CameraShakeManager.Instance != null) CameraShakeManager.Instance.Shake(cfg.cameraShake * 0.5f);
        }

        public override void OnHit(in WeaponHit hit)
        {
            if (hit.Target == null || !EvoStrikes.Take(12)) return;

            Vector3 at = hit.Target.position;
            EvoStrikes.Blast(Weapon, cfg.material, cfg.color, at, cfg.hitRadius, hit.Damage * cfg.hitRatio, cfg.push, hit.Target);
            if (cfg.push > 0f) CombatFx.Push(Weapon, hit.Target, hit.Direction, cfg.push, 0.2f);
            HitBlasts++;
        }

        public override void OnProjectileExpired(Vector3 position, Vector3 direction)
        {
            Vector3 at = position;
            at.y = Weapon.Controller != null ? Weapon.Controller.transform.position.y : at.y;
            EvoStrikes.Blast(Weapon, cfg.material, cfg.color, at, cfg.endRadius, Weapon.Stats.Damage * cfg.endRatio, cfg.push * 1.2f,
                null, cfg.explosionPrefab, 1.4f, cfg.cameraShake);
            CombatFx.PlaySound(cfg.blastSound);
            EndBlasts++;
        }
    }
}
