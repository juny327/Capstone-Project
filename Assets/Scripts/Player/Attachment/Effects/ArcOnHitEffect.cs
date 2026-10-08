using UnityEngine;

/// <summary>
/// 전격탄 — 맞으면 일정 확률로 가장 가까운 다른 적에게 전기가 한 번 튄다 (커스터마이징-구현계획.md 4-4).
/// 전격 소총의 연쇄와 같은 모양(지그재그 줄기)이지만 한 번뿐이고 확률이다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Attachment Effects/Arc On Hit", fileName = "AE_Arc")]
public class ArcOnHitEffect : AttachmentEffect
{
    [Range(0f, 1f)] public float chance = 0.25f;
    [Tooltip("튄 전기의 피해 = 맞힌 피해 × 이 값")]
    [Min(0f)] public float damageRatio = 0.5f;
    [Min(0.5f)] public float range = 4.5f;

    public Material material;
    public Color color = new Color(0.45f, 0.75f, 1f, 0.9f);
    public GameSoundSet.Entry sound = new GameSoundSet.Entry();

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly ArcOnHitEffect cfg;
        public Runtime(ArcOnHitEffect cfg) => this.cfg = cfg;

        public override void OnHit(in WeaponHit hit)
        {
            if (hit.Target == null || Random.value > cfg.chance) return;

            Transform next = CombatFx.FindNearest(hit.Target.position, cfg.range, hit.Target);
            if (next == null) return;

            Vector3 from = CombatFx.Body(hit.Target);
            Vector3 to = CombatFx.Body(next);

            CombatFx.Damage(next, hit.Damage * cfg.damageRatio, to, (to - from).normalized);
            FxLine.Bolt(cfg.material, from, to, cfg.color, 0.05f, 0.12f);
            CombatFx.PlaySound(cfg.sound);
        }
    }
}
