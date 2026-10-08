using UnityEngine;

/// <summary>
/// 튕김탄 — 맞은 적에서 가장 가까운 다른 적에게 한 번 튕긴다 (커스터마이징-구현계획.md 4-4).
/// 벽 충돌이 없어서 <b>적 사이로만</b> 튕긴다. 탄을 새로 만들지 않고 즉시 맞히며 궤적만 그린다 —
/// 튕긴 탄이 또 튕기는 일이 없고, 빗나가지도 않는다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Attachment Effects/Bounce On Hit", fileName = "AE_Bounce")]
public class BounceOnHitEffect : AttachmentEffect
{
    [Min(0.5f)] public float range = 6f;
    [Tooltip("튕긴 탄의 피해 = 맞힌 피해 × 이 값 (부착물 자체의 피해 −15% 는 이미 들어 있다)")]
    [Min(0f)] public float damageRatio = 1f;

    public Material material;
    public Color color = new Color(1f, 0.95f, 0.8f, 0.9f);

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly BounceOnHitEffect cfg;
        public Runtime(BounceOnHitEffect cfg) => this.cfg = cfg;

        public override void OnHit(in WeaponHit hit)
        {
            if (hit.Target == null) return;

            Transform next = CombatFx.FindNearest(hit.Target.position, cfg.range, hit.Target);
            if (next == null) return;

            Vector3 from = hit.Point;
            Vector3 to = CombatFx.Body(next);

            CombatFx.Damage(next, hit.Damage * cfg.damageRatio, to, (to - from).normalized, hit.IsCritical);
            FxLine.Line(cfg.material, from, to, cfg.color, 0.06f, 0.1f);
        }
    }
}
