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

    // 산탄 8발이 한 프레임에 터지면 소리가 겹쳐 터진다 — 짧게 한 번만
    static float lastSound;

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
                CombatFx.Damage(t, hit.Damage * cfg.damageRatio, CombatFx.Body(t), dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward);
            }

            FxLine.Ring(cfg.material, at, cfg.radius * 0.25f, cfg.radius, cfg.color, 0.2f, 0.25f);

            if (Time.time - lastSound > 0.08f)
            {
                lastSound = Time.time;
                CombatFx.PlaySound(cfg.sound);
            }
        }
    }
}
