using UnityEngine;

/// <summary>
/// 상위 무기 "팔랑크스" (창 + 에너지 방패)의 연출 — 방패를 든 채 찌르는 동작 자체는
/// 상위 무기 데이터(attackWhileShielding)와 ShieldAbility 가 맡는다. 여기서는 찌를 때마다 붉은 잔상,
/// 방패를 든 채 빨라진 찌르기는 잔상을 여러 줄 겹쳐 그린다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Phalanx", fileName = "AE_EVO_Phalanx")]
public class PhalanxEffect : AttachmentEffect
{
    public Material material;
    public Color color = new Color(1f, 0.25f, 0.2f, 1f);
    [Min(0.02f)] public float width = 0.22f;
    [Range(1, 5)] public int shieldedStreaks = 3;

    public static int Thrusts { get; private set; }
    public static int ShieldedThrusts { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Thrusts = 0; ShieldedThrusts = 0; }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly PhalanxEffect cfg;
        public Runtime(PhalanxEffect cfg) => this.cfg = cfg;

        public override void OnAttack(in WeaponAttack attack)
        {
            if (Weapon == null) return;

            Vector3 dir = EvoStrikes.Flat(attack.Direction, Vector3.forward);
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 origin = attack.Origin + Vector3.up * 1f;

            // 방패를 들고 있으면 연사가 빨라져 있다 (ShieldAbility 가 FireRateBoost 를 건다)
            bool shielded = Weapon.FireRateBoost > 1.01f;
            int streaks = shielded ? cfg.shieldedStreaks : 1;

            for (int i = 0; i < streaks; i++)
            {
                float lateral = streaks > 1 ? (i - (streaks - 1) * 0.5f) * 0.35f + Random.Range(-0.08f, 0.08f) : 0f;
                float reach = attack.Range * Random.Range(0.85f, 1.05f);
                Vector3 from = origin + side * lateral + dir * 0.4f;
                Color c = i == 0 ? cfg.color : cfg.color * new Color(1f, 1f, 1f, 0.55f);
                FxLine.Line(cfg.material, from, from + dir * reach, c, cfg.width * (i == 0 ? 1f : 0.7f), 0.12f);
            }

            FxLine.Line(cfg.material, origin, origin + dir * attack.Range, Color.white, cfg.width * 0.3f, 0.08f);

            Thrusts++;
            if (shielded) ShieldedThrusts++;
        }
    }
}
