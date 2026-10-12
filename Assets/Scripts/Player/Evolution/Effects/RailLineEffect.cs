using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상위 무기 "궤도 레일캐논" (스나이퍼 + 레일건) — 탄 대신 화면 끝까지 닿는 굵은 빔 (관통 무제한),
/// delay 초 뒤 빔 자리를 따라 폭발 줄이 한 번 더 지운다 (2타).
///
///  · 빔은 쏘는 순간 판정 — 맞은 적마다 명중 알림을 보낸다 (탄과 같은 규칙: 다른 부착물 효과가 그대로 돈다)
///  · 폭발 줄은 추가 피해라 알림을 보내지 않는다
/// 상위 무기 데이터는 spawnProjectiles 를 끈다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Rail Line", fileName = "AE_EVO_RailLine")]
public class RailLineEffect : AttachmentEffect
{
    [Header("빔")]
    [Min(1f)] public float length = 32f;
    [Tooltip("판정 반경 (폭의 절반)")]
    [Min(0.1f)] public float hitRadius = 0.6f;
    [Tooltip("빔 피해 = 무기 공격력 × 이 값 (치명타 판정 포함)")]
    [Min(0f)] public float damageMul = 1f;

    [Header("폭발 줄")]
    [Min(0f)] public float delay = 0.4f;
    [Min(0.3f)] public float blastRadius = 1.2f;
    [Tooltip("폭발 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float blastRatio = 0.5f;
    [Tooltip("폭발 고리 간격 (m)")]
    [Min(0.5f)] public float ringSpacing = 2.5f;
    [Tooltip("고리가 앞에서부터 차례로 터지는 시간")]
    [Min(0f)] public float sweepTime = 0.25f;

    [Header("연출")]
    public Material material;
    public Color color = new Color(0.65f, 0.85f, 1f, 1f);
    [Min(0.1f)] public float beamWidth = 1.1f;
    [Min(0.05f)] public float beamTime = 0.3f;
    [Min(0f)] public float cameraShake = 0.4f;
    public GameSoundSet.Entry blastSound = new GameSoundSet.Entry();

    public static int Beams { get; private set; }
    public static int BeamHits { get; private set; }
    public static int BlastHits { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Beams = 0; BeamHits = 0; BlastHits = 0; }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly RailLineEffect cfg;
        readonly List<Transform> found = new List<Transform>();

        public Runtime(RailLineEffect cfg) => this.cfg = cfg;

        public override void OnAttack(in WeaponAttack attack)
        {
            if (Weapon == null) return;

            Vector3 dir = EvoStrikes.Flat(attack.Direction, Vector3.forward);
            Vector3 origin = attack.Origin;
            Vector3 end = origin + dir * cfg.length;
            float groundY = Weapon.Controller != null ? Weapon.Controller.transform.position.y : origin.y - 1f;
            Vector3 g0 = new Vector3(origin.x, groundY, origin.z);
            Vector3 g1 = new Vector3(end.x, groundY, end.z);

            // 굵은 빔 — 바깥 흰 파랑 + 안쪽 흰 심
            FxLine.Line(cfg.material, origin, end, cfg.color, cfg.beamWidth, cfg.beamTime);
            FxLine.Line(cfg.material, origin, end, Color.white, cfg.beamWidth * 0.35f, cfg.beamTime * 0.8f);
            FxLine.Ring(cfg.material, g0, 0.2f, 1.4f, cfg.color, 0.2f, 0.2f);
            Beams++;

            WeaponRuntimeStats stats = Weapon.Stats;
            CombatFx.FindEnemiesAlong(g0, g1, cfg.hitRadius, found);
            foreach (Transform t in found)
            {
                bool crit = Random.value < stats.CritChance;
                float dealt = stats.Damage * cfg.damageMul * (crit ? stats.CritMultiplier : 1f);
                bool wasAlive = CombatFx.IsAlive(t);
                Vector3 point = CombatFx.Body(t);

                CombatFx.Damage(t, dealt, point, dir, crit);
                bool killed = WeaponBase.CheckDirectKill(t, wasAlive);   // 탄 대신 쏘는 레일 — 직접 공격 (Drain)
                BeamHits++;

                Weapon.NotifyHit(new WeaponHit
                {
                    Target = t,
                    Damageable = t.GetComponent<IDamageable>(),
                    Point = point,
                    Direction = dir,
                    Damage = dealt,
                    IsCritical = crit,
                    Killed = killed,
                });
            }

            if (cfg.cameraShake > 0f && CameraShakeManager.Instance != null) CameraShakeManager.Instance.Shake(cfg.cameraShake);
            Weapon.StartCoroutine(BlastLine(g0, g1, dir, stats.Damage * cfg.blastRatio));
        }

        IEnumerator BlastLine(Vector3 from, Vector3 to, Vector3 dir, float damage)
        {
            // 빔 자리에 남는 잔상 — 터질 줄
            FxLine.Line(cfg.material, from + Vector3.up * 0.1f, to + Vector3.up * 0.1f, cfg.color * new Color(1f, 1f, 1f, 0.5f), cfg.beamWidth * 0.4f, cfg.delay + cfg.sweepTime);

            if (cfg.delay > 0f) yield return new WaitForSeconds(cfg.delay);

            // 판정은 한 번에 (줄 전체), 고리는 앞에서부터 차례로
            CombatFx.FindEnemiesAlong(from, to, cfg.blastRadius, found);
            foreach (Transform t in found)
            {
                CombatFx.Damage(t, damage, CombatFx.Body(t), dir);
                BlastHits++;
            }
            CombatFx.PlaySound(cfg.blastSound);

            float length = Vector3.Distance(from, to);
            int rings = Mathf.Max(1, Mathf.FloorToInt(length / cfg.ringSpacing));
            float gap = rings > 1 ? cfg.sweepTime / rings : 0f;

            for (int i = 0; i <= rings; i++)
            {
                Vector3 at = from + dir * (i * cfg.ringSpacing);
                FxLine.Ring(cfg.material, at, 0.2f, cfg.blastRadius * 1.3f, i % 2 == 0 ? cfg.color : Color.white, 0.22f, 0.25f);
                if (gap > 0f && i % 3 == 2) yield return new WaitForSeconds(gap * 3f);
            }
        }
    }
}
