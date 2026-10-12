using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상위 무기 "천공창" (창 + 그랩 훅) — 찌르기 끝에서 빛의 창이 extend m 더 뻗는다 (관통 무제한).
/// 빛의 창에 맞은 적 중 가장 먼 적은 내 앞까지 끌려온다 — 다음 찌르기에 걸리게.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Lance Extend", fileName = "AE_EVO_LanceExtend")]
public class LanceExtendEffect : AttachmentEffect
{
    [Min(1f)] public float extend = 10f;
    [Min(0.1f)] public float hitRadius = 0.7f;
    [Tooltip("빛의 창 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float damageMul = 1.2f;

    [Header("끌어오기")]
    public bool pullFarthest = true;
    [Min(0.5f)] public float pullTo = 1.6f;
    [Min(0.05f)] public float pullTime = 0.3f;
    [Min(0f)] public float pullStun = 0.5f;

    [Header("연출")]
    public Material material;
    public Color color = new Color(0.3f, 0.95f, 1f, 1f);
    [Min(0.05f)] public float width = 0.45f;
    public GameSoundSet.Entry sound = new GameSoundSet.Entry();

    public static int Thrusts { get; private set; }
    public static int LanceHits { get; private set; }
    public static int Pulled { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Thrusts = 0; LanceHits = 0; Pulled = 0; }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly LanceExtendEffect cfg;
        readonly List<Transform> found = new List<Transform>();

        public Runtime(LanceExtendEffect cfg) => this.cfg = cfg;

        public override void OnAttack(in WeaponAttack attack)
        {
            if (Weapon == null) return;

            Vector3 dir = EvoStrikes.Flat(attack.Direction, Vector3.forward);
            Vector3 origin = attack.Origin;
            Vector3 from = origin + dir * attack.Range * 0.8f;
            Vector3 to = origin + dir * (attack.Range + cfg.extend);

            // 빛의 창 — 손에서 끝까지 굵게 뻗었다 사라진다
            Vector3 lift = Vector3.up * 1f;
            FxLine.Line(cfg.material, origin + lift, to + lift, cfg.color, cfg.width, 0.2f);
            FxLine.Line(cfg.material, origin + lift, to + lift, Color.white, cfg.width * 0.35f, 0.14f);
            FxLine.Ring(cfg.material, to, 0.1f, 0.9f, cfg.color, 0.12f, 0.2f);
            Thrusts++;

            float damage = Weapon.Stats.Damage * cfg.damageMul;
            CombatFx.FindEnemiesAlong(from, to, cfg.hitRadius, found);

            Transform farthest = null;
            float far = 0f;

            foreach (Transform t in found)
            {
                CombatFx.Damage(t, damage, CombatFx.Body(t), dir);
                LanceHits++;

                float d = (t.position - origin).sqrMagnitude;
                if (d > far && CombatFx.IsAlive(t))
                {
                    far = d;
                    farthest = t;
                }
            }

            if (found.Count > 0) CombatFx.PlaySound(cfg.sound);
            if (!cfg.pullFarthest || farthest == null) return;

            Vector3 point = origin + dir * cfg.pullTo;
            FxLine.Line(cfg.material, CombatFx.Body(farthest), origin + lift, cfg.color, 0.08f, cfg.pullTime + 0.1f);
            CombatFx.Stun(farthest, cfg.pullStun);
            if (EvoStrikes.PullTo(Weapon, farthest, point, cfg.pullTime)) Pulled++;
        }
    }
}
