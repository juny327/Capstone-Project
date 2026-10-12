using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상위 무기 "파편 폭풍" (기관단총 + 난사) — 명중할 때마다 파편이 가까운 다른 적 count 명에게 튄다.
/// 파편은 다시 튀지 않는다 (명중 알림을 보내지 않는다). 연사가 빨라 그물처럼 번진다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Fragment On Hit", fileName = "AE_EVO_Fragment")]
public class FragmentOnHitEffect : AttachmentEffect
{
    [Range(1, 6)] public int count = 2;
    [Min(0.5f)] public float range = 4f;
    [Tooltip("파편 피해 = 맞힌 피해 × 이 값")]
    [Min(0f)] public float damageRatio = 0.4f;
    [Range(1, 64)] public int maxPerFrame = 24;

    public Material material;
    public Color color = new Color(1f, 0.82f, 0.55f, 1f);

    public static int Fragments { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Fragments = 0;

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly FragmentOnHitEffect cfg;
        readonly List<Transform> found = new List<Transform>();

        public Runtime(FragmentOnHitEffect cfg) => this.cfg = cfg;

        public override void OnHit(in WeaponHit hit)
        {
            if (hit.Target == null) return;

            Vector3 from = hit.Point;
            CombatFx.FindEnemies(hit.Target.position, cfg.range, found, hit.Target);
            found.Sort((a, b) => (a.position - from).sqrMagnitude.CompareTo((b.position - from).sqrMagnitude));

            for (int i = 0; i < found.Count && i < cfg.count; i++)
            {
                if (!EvoStrikes.Take(cfg.maxPerFrame)) return;

                Transform t = found[i];
                Vector3 to = CombatFx.Body(t);
                Vector3 dir = to - from;
                dir.y = 0f;

                CombatFx.Damage(t, hit.Damage * cfg.damageRatio, to, dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward);
                FxLine.Line(cfg.material, from, to, cfg.color, 0.07f, 0.12f);
                Fragments++;
            }
        }
    }
}
