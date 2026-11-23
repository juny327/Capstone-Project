using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 망치 · 묠니르의 동작 (2026-11-20 근접 신규 무기).
///  · 맞힌 적을 잠깐 기절시킨다 (경직 — 보스는 없음). 세 번째 공격(마무리 내려찍기)은 더 길게 + 친 자리 둘레 충격파 (직접 안 맞은 적 50%)
///  · 묠니르 (lightning) — 맞힐 때 40% 로 둘레 적 둘에게 번개가 튄다 · 내려찍은 자리에 낙뢰 (반경 3m · 150% · 기절 1초)
///  · 묠니르 (throwAfterFinisher) — 낙뢰 뒤 망치를 던진다. 곧게 날아갔다가(벽에서 멈춤) 손으로 돌아오며, 가는 길 · 오는 길의 적을 모두 친다
///  · 묠니르 — 머리에 전기가 튄다. 다음 공격이 마무리 내려찍기면 세진다 (낙뢰 신호)
/// 기본 망치는 단촐하게 (충격파 고리 · 별), 묠니르는 화려하게 (번개 · 먹구름 번쩍 · 큰 순간).
/// 손에 든 망치 · 떠 있는 보조 망치 모두 같다 (보조 칸도 연출을 줄이지 않는다 — EvoFxScale).
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Melee Effects/Hammer", fileName = "AE_Hammer")]
public class HammerEffect : AttachmentEffect
{
    [Header("기절 (보스는 없음)")]
    [Min(0f)] public float stun = 0.4f;
    [Tooltip("마무리 내려찍기로 맞힌 적 · 둘레 충격파에 든 적")]
    [Min(0f)] public float finisherStun = 0.8f;

    [Header("마무리 내려찍기 — 친 자리 둘레 충격파")]
    [Min(0f)] public float slamRadius = 2f;
    [Tooltip("직접 맞지 않은 둘레 적에게 주는 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float slamDamageMul = 0.5f;

    [Header("모습")]
    [Tooltip("선 · 고리 재질 (Assets/Prefabs/Attachment/FxLine.mat)")]
    public Material material;
    public Color color = new Color(0.62f, 0.86f, 1f, 1f);
    [Tooltip("기절 표시 별 색 (기본 망치)")]
    public Color starColor = new Color(1f, 0.9f, 0.5f, 1f);
    [Tooltip("공통 재료(FxKit)로 화려하게 — 묠니르. 끄면 고리 · 별 · 작은 충격파만")]
    public bool kit;

    [Header("묠니르 — 번개")]
    public bool lightning;
    [Range(0f, 1f)] public float chainChance = 0.4f;
    [Min(0)] public int chainTargets = 2;
    [Min(0f)] public float chainRange = 5f;
    [Tooltip("튄 번개 피해 = 맞힌 피해 × 이 값")]
    [Min(0f)] public float chainDamageMul = 0.4f;
    [Tooltip("낙뢰 반경 (마무리 내려찍기)")]
    [Min(0f)] public float strikeRadius = 3f;
    [Tooltip("낙뢰 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float strikeDamageMul = 1.5f;
    [Min(0f)] public float strikeStun = 1f;
    [Tooltip("낙뢰 줄기 높이 (m) — 화면 위에서 떨어진다")]
    [Min(1f)] public float boltHeight = 14f;
    [Tooltip("머리에 튀는 전기 알갱이 — 초당 수. 다음이 마무리 내려찍기면 ×3")]
    [Min(0f)] public float idleSparks = 14f;

    [Header("묠니르 — 던지기 (마무리 내려찍기 뒤)")]
    public bool throwAfterFinisher;
    [Tooltip("낙뢰 뒤 던지기까지 (초)")]
    [Min(0f)] public float throwDelay = 0.14f;
    [Min(1f)] public float throwDistance = 9f;
    [Min(1f)] public float flySpeed = 26f;
    [Min(1f)] public float returnSpeed = 30f;
    [Tooltip("날아가며 치는 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float throwDamageMul = 1.2f;
    [Tooltip("돌아오며 치는 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float returnDamageMul = 1f;
    [Tooltip("날아가는 망치의 판정 반경")]
    [Min(0.2f)] public float hitRadius = 1.1f;
    [Tooltip("날아가는 망치가 맞힌 적의 기절 (초)")]
    [Min(0f)] public float throwStun = 0.3f;
    [Tooltip("날아가는 동안 도는 빠르기 (도/초)")]
    public float spinSpeed = 1080f;
    [Min(0f)] public float flyHeight = 1.1f;

    [Header("소리")]
    public GameSoundSet.Entry slamSound = new GameSoundSet.Entry();
    public GameSoundSet.Entry chainSound = new GameSoundSet.Entry();
    public GameSoundSet.Entry strikeSound = new GameSoundSet.Entry();
    public GameSoundSet.Entry throwSound = new GameSoundSet.Entry();
    public GameSoundSet.Entry catchSound = new GameSoundSet.Entry();

    public bool UsesKit => kit && FxKit.Ready;

    /// <summary>시험 집계 — 기절 · 내려찍기 · 튄 번개 · 낙뢰 · 던지기 · 맞힘 · 받기.</summary>
    public static int Stunned, Slams, Chains, Strikes, Thrown, ThrowHits, ReturnHits, Caught;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Stunned = Slams = Chains = Strikes = Thrown = ThrowHits = ReturnHits = Caught = 0; }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    /// <summary>기절 · 표시 — 적이면 기절 (보스는 넘어간다). 테스트 룸 무리 표적은 표시만.</summary>
    internal void StunTarget(Transform target, float seconds)
    {
        if (seconds <= 0f || target == null || !CombatFx.IsAlive(target)) return;
        if (target.TryGetComponent(out Enemy _)) CombatFx.Stun(target, seconds);
        else if (target.GetComponent<IPullable>() == null) return;   // 보스 — 기절 없음
        StunMarks.Show(target, seconds, lightning ? color : starColor, lightning ? StunMarks.Kind.Sparks : StunMarks.Kind.Stars);
        Stunned++;
    }

    class Runtime : AttachmentRuntime
    {
        readonly HammerEffect cfg;
        readonly HashSet<Transform> direct = new HashSet<Transform>();   // 이번 휘두르기에 직접 맞은 적 (충격파 · 튄 번개에서 뺀다)
        readonly List<Transform> found = new List<Transform>();
        ThrownHammer active;
        float sparkCarry;

        public Runtime(HammerEffect cfg) => this.cfg = cfg;

        MeleeWeapon Melee => Weapon as MeleeWeapon;
        float Scale => Weapon != null ? Weapon.EvoFxScale : 1f;

        public override void OnHit(in WeaponHit hit)
        {
            if (hit.Target == null) return;
            direct.Add(hit.Target);

            cfg.StunTarget(hit.Target, hit.Finisher ? cfg.finisherStun : cfg.stun);

            if (cfg.lightning && cfg.chainTargets > 0 && Random.value < cfg.chainChance) Chain(in hit);
        }

        /// <summary>맞은 적에서 둘레 적으로 번개가 사슬처럼 튄다 (가까운 순).</summary>
        void Chain(in WeaponHit hit)
        {
            Transform from = hit.Target;
            Vector3 fromPos = CombatFx.Body(from);
            float damage = hit.EffectDamage * cfg.chainDamageMul;
            int jumped = 0;

            for (int n = 0; n < cfg.chainTargets; n++)
            {
                CombatFx.FindEnemies(from.position, cfg.chainRange, found, from);
                Transform next = null;
                float best = float.PositiveInfinity;
                foreach (Transform t in found)
                {
                    if (direct.Contains(t)) continue;
                    float d = (t.position - from.position).sqrMagnitude;
                    if (d < best) { best = d; next = t; }
                }
                if (next == null) break;

                Vector3 to = CombatFx.Body(next);
                Vector3 dir = EvoStrikes.Flat(to - fromPos, Vector3.forward);
                CombatFx.Damage(next, damage, to, dir, false, 0f, HitSound.Electric);
                direct.Add(next);   // 한 번 튄 적은 다시 튀지 않는다

                if (cfg.material != null)
                {
                    FxLine.Bolt(cfg.material, fromPos, to, cfg.color, 0.2f * Scale, 0.18f, 0.45f);
                    FxLine.Bolt(cfg.material, fromPos, to, Color.white, 0.07f * Scale, 0.12f, 0.3f);
                }
                if (cfg.UsesKit && EvoStrikes.Take(6))
                {
                    FxKit.Hit(to, dir, cfg.color, 1f * Scale);
                    FxKit.LightningStrike(next.position, 0.5f, cfg.color, 0.6f * Scale);
                }

                from = next;
                fromPos = to;
                jumped++;
                Chains++;
            }

            if (jumped > 0) CombatFx.PlaySound(cfg.chainSound);
        }

        public override void OnAttack(in WeaponAttack attack)
        {
            if (attack.Finisher) Slam(in attack);
            direct.Clear();
        }

        /// <summary>마무리 내려찍기 — 친 자리 둘레 충격파 (+ 묠니르 낙뢰 · 던지기).</summary>
        void Slam(in WeaponAttack attack)
        {
            Slams++;
            Vector3 dir = EvoStrikes.Flat(attack.Direction, Vector3.forward);
            float reach = Mathf.Max(1f, attack.Range * (Weapon != null && Weapon.IsSupport ? 0.55f : 0.75f));
            Vector3 at = attack.Origin + dir * reach;
            at.y = attack.Origin.y;
            float damage = Weapon != null ? Weapon.Stats.Damage : 0f;

            // 둘레 충격파 — 직접 맞지 않은 적에게 50%, 든 적은 모두 기절
            CombatFx.FindEnemies(at, cfg.slamRadius, found);
            foreach (Transform t in found)
            {
                if (!direct.Contains(t) && cfg.slamDamageMul > 0f)
                    CombatFx.Damage(t, damage * cfg.slamDamageMul, CombatFx.Body(t), EvoStrikes.Flat(t.position - at, dir));
                cfg.StunTarget(t, cfg.finisherStun);
            }

            SlamFx(at);
            CombatFx.PlaySound(cfg.slamSound);

            if (cfg.lightning) Strike(at, damage);

            // 묠니르 — 낙뢰 뒤 던진다 (돌아오기 전이면 다음 마무리로 미룬다)
            if (cfg.throwAfterFinisher && active == null && Weapon != null && Weapon.Controller != null)
            {
                active = ThrownHammer.Launch(cfg, Weapon, dir, damage);
                Thrown++;
            }
        }

        /// <summary>내려찍은 자리 — 기본 망치는 고리 · 바닥 빛 · 작은 충격파 · 돌 부스러기만.</summary>
        void SlamFx(Vector3 at)
        {
            Color dust = cfg.lightning ? cfg.color : new Color(0.85f, 0.82f, 0.74f, 1f);
            if (cfg.material != null)
            {
                FxLine.Ring(cfg.material, at, 0.3f, cfg.slamRadius, dust, 0.16f, 0.3f);
                FxLine.Ring(cfg.material, at, 0.2f, cfg.slamRadius * 0.6f, Color.white, 0.07f, 0.2f);
            }
            GroundGlow.Flash(at, cfg.slamRadius * 1.2f, dust, 0.24f, cfg.lightning ? 1f : 0.7f);

            if (!FxKit.Ready) return;
            FxKit.Shockwave(at, cfg.slamRadius * 2.1f, dust, 0.3f, cfg.lightning ? 0.95f : 0.7f);
            FxKit.Debris(at, 0.7f, cfg.lightning ? 10f : 5f);
            if (cfg.UsesKit)
            {
                FxKit.Wave(at, cfg.slamRadius * 3f, cfg.color, 0.45f, 0.6f);
                FxKit.Scorch(at, cfg.slamRadius * 1.3f, 0.8f);
            }
        }

        /// <summary>묠니르 낙뢰 — 하늘에서 내려찍은 자리로. 반경 안 적에게 피해 · 기절.</summary>
        void Strike(Vector3 at, float damage)
        {
            Strikes++;
            float s = Scale;
            Vector3 sky = at + Vector3.up * cfg.boltHeight + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));

            if (cfg.material != null)
                EvoStrikes.Lightning(cfg.material, cfg.color, sky, at, cfg.strikeRadius, damage * cfg.strikeDamageMul, cfg.strikeStun, null, 0.6f, 6, 0f, s);

            if (cfg.UsesKit)
            {
                FxKit.LightningStrike(at, cfg.strikeRadius * 0.9f, cfg.color, s, cfg.boltHeight);
                FxKit.CloudFlash(sky - Vector3.up * 2f, 4.5f, cfg.color, 1f);
                FxKit.EnergyBurst(at, cfg.strikeRadius * 0.6f, cfg.color, s, true);
                FxKit.Pillar(at, 0.9f * s, 7f, cfg.color, 0.4f, 6);
                FxKit.Glints(at + Vector3.up, cfg.strikeRadius, Color.white, 18f * s, 0.8f);
            }
            FlashLight.Flash(at + Vector3.up, cfg.color, 9f, 0.2f, s);

            // 낙뢰가 경직시킨 적에게 전기 별
            CombatFx.FindEnemies(at, cfg.strikeRadius, found);
            foreach (Transform t in found)
                StunMarks.Show(t, cfg.strikeStun, cfg.color, StunMarks.Kind.Sparks);

            ImpactFx.Play(ImpactMoment.MjolnirStrike, Weapon, cfg.color, 0.5f);
            CombatFx.PlaySound(cfg.strikeSound);
        }

        /// <summary>묠니르 머리의 전기 — 다음이 마무리 내려찍기면 세진다 (낙뢰 신호).</summary>
        public override void Tick(float deltaTime)
        {
            if (!cfg.lightning || !cfg.UsesKit || Weapon == null || !Weapon.IsActive || Weapon.HeldAway) return;
            MeleeWeapon melee = Melee;
            if (melee == null) return;

            bool charged = melee.NextIsFinisher;
            sparkCarry += cfg.idleSparks * (charged ? 3f : 1f) * deltaTime;
            if (sparkCarry < 1f) return;

            int n = Mathf.FloorToInt(sparkCarry);
            sparkCarry -= n;
            Vector3 head = melee.HeadWorld;
            FxKit.TrailDust(head, cfg.color, n, charged ? 0.14f : 0.1f, charged, charged ? 0.5f : 0f);

            if (cfg.material != null && Random.value < (charged ? 0.6f : 0.2f))
            {
                Vector3 a = head + Random.insideUnitSphere * 0.45f;
                Vector3 b = head + Random.insideUnitSphere * 0.7f;
                FxLine.Bolt(cfg.material, a, b, Color.Lerp(cfg.color, Color.white, 0.5f), charged ? 0.08f : 0.05f, 0.07f, 0.25f);
            }
        }

        public override void Dispose()
        {
            // 무기가 사라져도 날아간 망치는 끝까지 간다 (무기가 없으면 스스로 지운다)
            active = null;
        }
    }
}
