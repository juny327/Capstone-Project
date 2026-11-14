using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 상위 무기 "공성포" (샷건 + 슬러그) — 슬러그가 굵은 포탄이 된다.
///  · 적에게 부딪히면 <b>거기서 멈추고 터진다</b> — 둘레 적에게 폭발 피해 · 밀치기 (맞은 적은 포탄 피해를 이미 받았다)
///  · 아무도 못 맞히면 사거리 끝 · 벽에서 같은 폭발
///  · 쏠 때 총구 충격파 고리
/// 2026-11-03 사용자 요청 — 예전에는 관통 무제한(맞힐 때마다 작은 폭발 + 사거리 끝 큰 폭발)이었다.
/// 슬러그 개조의 관통 +2 를 이어받아도 첫 적에서 멈추게 StopsProjectile 로 탄을 세운다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Siege Shell", fileName = "AE_EVO_SiegeShell")]
public class SiegeShellEffect : AttachmentEffect
{
    [Header("폭발 (적 · 벽 · 사거리 끝)")]
    [FormerlySerializedAs("endRadius")]
    [Min(0.5f)] public float blastRadius = 3f;
    [Tooltip("폭발 피해 = 무기 공격력 × 이 값 (맞은 적은 빼고 둘레 적에게 — 맞은 적은 포탄 피해)")]
    [FormerlySerializedAs("endRatio")]
    [Min(0f)] public float blastRatio = 1.5f;
    [Tooltip("둘레 적을 밀치는 힘 (맞은 적은 포탄 방향으로)")]
    [Min(0f)] public float push = 2.4f;

    [Header("연출")]
    public Material material;
    public Color color = new Color(1f, 0.75f, 0.25f, 1f);
    public GameObject explosionPrefab;
    [Min(0f)] public float cameraShake = 0.45f;
    public GameSoundSet.Entry blastSound = new GameSoundSet.Entry();

    /// <summary>적에게 부딪혀 터진 수 · 벽 · 사거리 끝에서 터진 수 (테스트).</summary>
    public static int HitBlasts { get; private set; }
    public static int EndBlasts { get; private set; }
    /// <summary>폭발로 흔든 횟수 (테스트) — 한 번 쏠 때 한 번.</summary>
    public static int BlastShakes { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { HitBlasts = 0; EndBlasts = 0; BlastShakes = 0; }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly SiegeShellEffect cfg;
        public Runtime(SiegeShellEffect cfg) => this.cfg = cfg;

        // 포탄은 첫 적에서 멈춘다 (관통 무시)
        public override bool StopsProjectile => true;

        // 포탄 조각(8발)이 저마다 터진다 — 폭발 흔들림은 한 번 쏠 때 한 번만 (2026-11-14: 조각마다 흔들어 4초에 41번이었다)
        bool blastShaken = true;
        float lastBlastShake = -99f;
        const float BlastShakeGap = 0.15f;

        public override void OnAttack(in WeaponAttack attack)
        {
            blastShaken = false;
            Vector3 at = attack.Origin;
            FxLine.Ring(cfg.material, at, 0.2f, 1.6f, cfg.color, 0.22f, 0.22f);
            GroundGlow.Flash(new Vector3(at.x, Weapon != null && Weapon.Controller != null ? Weapon.Controller.transform.position.y : at.y - 1.2f, at.z), 2.4f, cfg.color, 0.16f);
            ImpactFx.Play(ImpactMoment.SiegeFire, Weapon, cfg.color, cfg.cameraShake * 0.5f, attack.Direction);   // 쏘는 반대쪽으로 밀린다 (보조는 예전처럼 약한 흔들림만)
        }

        public override void OnHit(in WeaponHit hit)
        {
            if (hit.Target == null) return;

            // 부딪힌 자리 (높이는 적 발밑)
            Vector3 at = hit.Point;
            at.y = hit.Target.position.y;
            Explode(at, hit.Target);
            if (cfg.push > 0f) CombatFx.Push(Weapon, hit.Target, hit.Direction, cfg.push, 0.2f);
            HitBlasts++;
        }

        public override void OnProjectileExpired(Vector3 position, Vector3 direction)
        {
            Vector3 at = position;
            at.y = Weapon.Controller != null ? Weapon.Controller.transform.position.y : at.y;
            Explode(at, null);
            EndBlasts++;
        }

        void Explode(Vector3 at, Transform struck)
        {
            float shake = 0f;
            if (!blastShaken && Time.time - lastBlastShake > BlastShakeGap)
            {
                blastShaken = true;
                lastBlastShake = Time.time;
                shake = cfg.cameraShake;
                BlastShakes++;
            }

            EvoStrikes.Blast(Weapon, cfg.material, cfg.color, at, cfg.blastRadius, Weapon.Stats.Damage * cfg.blastRatio, cfg.push,
                struck, cfg.explosionPrefab, 1.4f, 0f);
            if (shake > 0f) ImpactFx.Play(ImpactMoment.SiegeBlast, Weapon, cfg.color, shake);   // 한 번 쏠 때 한 번 — 쿵 · 줌 · 가장자리 번쩍
            CombatFx.PlaySound(cfg.blastSound);
        }
    }
}
