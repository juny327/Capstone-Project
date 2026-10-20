using UnityEngine;

/// <summary>
/// 상위 무기 "거울칼날" (검 + 에너지 방패) — 휘두를 때마다 정면 몬스터 탄을 되돌리고,
/// N 번째 휘두르기마다 초승달 검기가 부채꼴로 날아간다 (상위무기-계획.md 5장 · 14-5).
///
///  · 되돌리기는 방패 변형 "반사판" · 패링과 같은 길(DeflectedProjectile) — 몬스터 탄은 콜라이더가 없어 찾아서 본다
///  · 검기는 날아가며 지나온 구간 전체를 훑는다 (프레임이 끊겨도 사이의 적을 건너뛰지 않게) — 적마다 한 번
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Mirror Edge", fileName = "AE_EVO_MirrorEdge")]
public class MirrorEdgeEffect : AttachmentEffect
{
    [Header("되돌리기")]
    [Tooltip("휘두를 때 이 반경(사거리 + 값) 안 정면 탄을 되돌린다")]
    [Min(0f)] public float deflectReachBonus = 1.2f;
    [Range(30f, 360f)] public float deflectArc = 150f;
    [Min(0.1f)] public float deflectSpeedMultiplier = 1.4f;
    [Min(0f)] public float deflectMinSpeed = 12f;
    [Tooltip("되돌린 탄 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float deflectDamageRatio = 1f;
    [Min(0.1f)] public float deflectLifeTime = 2f;

    [Header("검기")]
    [Tooltip("몇 번째 휘두르기마다")]
    [Range(1, 10)] public int waveEvery = 4;
    [Range(1, 7)] public int waveCount = 3;
    [Tooltip("검기 사이 각도(도)")]
    [Range(0f, 60f)] public float waveSpread = 18f;
    [Min(0.5f)] public float waveDistance = 8f;
    [Min(1f)] public float waveSpeed = 22f;
    [Tooltip("판정 반경")]
    [Min(0.1f)] public float waveRadius = 0.9f;
    [Tooltip("검기 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float waveDamageMul = 2f;

    [Header("연출")]
    public Material waveMaterial;
    public Material sparkMaterial;
    public Color color = new Color(0.25f, 0.95f, 1f, 1f);
    [Min(0.1f)] public float waveWidth = 3f;
    public GameSoundSet.Entry deflectSound = new GameSoundSet.Entry();
    public GameSoundSet.Entry waveSound = new GameSoundSet.Entry();

    [Tooltip("휘두를 때마다 — 금속 날이 스치는 \"슈잉\" (무기 원래 휘두르기 소리 위에 겹친다, 2026-10-13)")]
    public GameSoundSet.Entry swingEdgeSound = new GameSoundSet.Entry();

    [Tooltip("휘두를 때마다 — 마법 에너지 반짝임 (고음)")]
    public GameSoundSet.Entry swingMagicSound = new GameSoundSet.Entry();

    [Tooltip("탄을 되돌릴 때 함께 — 유리처럼 맑게 울리는 소리")]
    public GameSoundSet.Entry deflectShimmerSound = new GameSoundSet.Entry();

    /// <summary>날린 검기 수 · 되돌린 탄 수 (테스트).</summary>
    public static int WavesLaunched { get; private set; }
    public static int Deflected { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        WavesLaunched = 0;
        Deflected = 0;
    }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly MirrorEdgeEffect cfg;
        int swings;

        public Runtime(MirrorEdgeEffect cfg) => this.cfg = cfg;

        public override void OnAttack(in WeaponAttack attack)
        {
            if (Weapon == null) return;

            Vector3 forward = attack.Direction;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            DeflectProjectiles(attack.Origin, forward, attack.Range + cfg.deflectReachBonus);

            CombatFx.PlaySound(cfg.swingEdgeSound);
            CombatFx.PlaySound(cfg.swingMagicSound);

            swings++;
            if (swings % cfg.waveEvery != 0) return;

            float damage = Weapon.Stats.Damage * cfg.waveDamageMul;
            Vector3 start = attack.Origin + forward * 0.8f + Vector3.up * 0.9f;

            for (int i = 0; i < cfg.waveCount; i++)
            {
                float angle = cfg.waveCount > 1 ? -cfg.waveSpread * (cfg.waveCount - 1) * 0.5f + cfg.waveSpread * i : 0f;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
                CrescentWave.Launch(cfg, start, dir, damage);
                WavesLaunched++;
            }

            CombatFx.PlaySound(cfg.waveSound);
        }

        void DeflectProjectiles(Vector3 origin, Vector3 forward, float reach)
        {
            RangedProjectile[] flying = Object.FindObjectsByType<RangedProjectile>(FindObjectsSortMode.None);
            if (flying.Length == 0) return;

            float damage = Mathf.Max(1f, Weapon.Stats.Damage * cfg.deflectDamageRatio);
            bool any = false;

            foreach (RangedProjectile p in flying)
            {
                if (p == null || !p.enabled || !p.gameObject.activeInHierarchy) continue;

                Vector3 d = p.transform.position - origin;
                d.y = 0f;
                if (d.magnitude > reach || Vector3.Angle(forward, d) > cfg.deflectArc * 0.5f) continue;

                // 칼면에 거울처럼 — 정면으로 크게 꺾이지 않으면 휘두른 방향으로 보낸다
                Vector3 normal = d.sqrMagnitude > 0.0001f ? d.normalized : forward;
                Vector3 incoming = p.transform.forward;
                incoming.y = 0f;
                Vector3 outgoing = incoming.sqrMagnitude > 0.0001f ? Vector3.Reflect(incoming.normalized, normal) : forward;
                outgoing.y = 0f;
                if (Vector3.Dot(outgoing, forward) < 0.3f) outgoing = forward;

                Vector3 at = p.transform.position;
                if (!DeflectedProjectile.Launch(p, outgoing.normalized, cfg.deflectSpeedMultiplier, cfg.deflectMinSpeed, damage, cfg.deflectLifeTime))
                    continue;

                Deflected++;
                any = true;
                FxLine.Ring(cfg.sparkMaterial, at, 0.1f, 0.7f, cfg.color, 0.1f, 0.15f);
            }

            if (any)
            {
                CombatFx.PlaySound(cfg.deflectSound);
                CombatFx.PlaySound(cfg.deflectShimmerSound);
            }
        }
    }
}
