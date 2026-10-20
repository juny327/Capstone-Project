using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전격 소총 전용 개조 "방전" — 장전을 시작하는 순간 주변으로 전기를 터뜨린다 (커스터마이징-구현계획.md 4-5).
/// 둘러싸여 탄이 떨어진 순간이 가장 위험하다 — 그때 숨을 돌리게 한다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Attachment Effects/Reload Discharge", fileName = "AE_Discharge")]
public class ReloadDischargeEffect : AttachmentEffect
{
    [Min(0.5f)] public float radius = 4f;
    [Tooltip("피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float damageRatio = 2.5f;
    [Min(0f)] public float stun = 0.5f;

    public Material material;
    public Color color = new Color(0.45f, 0.8f, 1f, 0.95f);
    public GameSoundSet.Entry sound = new GameSoundSet.Entry();

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly ReloadDischargeEffect cfg;
        readonly List<Transform> targets = new List<Transform>();

        public Runtime(ReloadDischargeEffect cfg) => this.cfg = cfg;

        /// <summary>마지막으로 맞힌 적 수 (검증용).</summary>
        public int LastHitCount { get; private set; }

        public override void OnReloadStarted()
        {
            if (!Weapon.IsActive || Weapon.Controller == null) return;

            Transform owner = Weapon.Controller.transform;
            Vector3 center = owner.position;
            float damage = Weapon.Stats.Damage * cfg.damageRatio;

            CombatFx.FindEnemies(center, cfg.radius, targets);
            foreach (Transform t in targets)
            {
                Vector3 to = CombatFx.Body(t);
                Vector3 dir = t.position - center;
                dir.y = 0f;

                CombatFx.Damage(t, damage, to, dir.sqrMagnitude > 0.0001f ? dir.normalized : owner.forward);
                CombatFx.Stun(t, cfg.stun);
                FxLine.Bolt(cfg.material, center + Vector3.up * 1f, to, cfg.color, 0.08f, 0.25f, 0.25f);
            }

            LastHitCount = targets.Count;
            FxLine.Ring(cfg.material, center, 0.5f, cfg.radius, cfg.color, 0.22f, 0.4f);
            CombatFx.PlaySound(cfg.sound);
        }
    }
}
