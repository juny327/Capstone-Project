using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상위 무기 "사슬검" (검 + 그랩 훅) — 휘두르기가 사슬 채찍이 된다 (사거리 · 부채꼴 · 대상 수는 상위 무기 데이터).
/// pullEvery 번째 휘두르기마다 이번에 맞은 적 중 가장 먼 pullCount 명을 내 앞으로 끌어온다.
/// 근접은 맞힌 알림이 먼저, 휘두른 알림이 나중에 온다 — 그래서 OnAttack 에서 이번 휘두르기의 적이 다 모여 있다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Chain Whip", fileName = "AE_EVO_ChainWhip")]
public class ChainWhipEffect : AttachmentEffect
{
    [Range(1, 10)] public int pullEvery = 3;
    [Range(1, 10)] public int pullCount = 3;
    [Tooltip("내 앞 이 거리로 끌어온다")]
    [Min(0.5f)] public float pullTo = 1.6f;
    [Min(0.05f)] public float pullTime = 0.25f;
    [Min(0f)] public float pullStun = 0.6f;

    [Header("연출")]
    public Material material;
    public Color color = new Color(0.35f, 1f, 0.55f, 1f);
    [Min(0.02f)] public float whipWidth = 0.28f;
    public GameSoundSet.Entry pullSound = new GameSoundSet.Entry();

    public static int Whips { get; private set; }
    public static int Pulled { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Whips = 0; Pulled = 0; }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly ChainWhipEffect cfg;
        readonly List<Transform> struck = new List<Transform>();
        int swings;

        public Runtime(ChainWhipEffect cfg) => this.cfg = cfg;

        public override void OnHit(in WeaponHit hit)
        {
            if (hit.Target != null && !hit.Killed && !struck.Contains(hit.Target)) struck.Add(hit.Target);
        }

        public override void OnAttack(in WeaponAttack attack)
        {
            if (Weapon == null) return;

            Vector3 forward = EvoStrikes.Flat(attack.Direction, Vector3.forward);
            Vector3 center = attack.Origin + Vector3.up * 0.9f;
            float half = Weapon.Data is MeleeWeaponData melee ? melee.arcAngle * 0.5f : 75f;

            // 사슬이 호를 그린다 — 바깥 초록 + 안쪽 흰 선, 조금 안쪽에 한 줄 더
            FxLine.Arc(cfg.material, center, forward, attack.Range, half, cfg.color, cfg.whipWidth, 0.22f);
            FxLine.Arc(cfg.material, center, forward, attack.Range * 0.97f, half * 0.95f, Color.white, cfg.whipWidth * 0.35f, 0.16f);
            FxLine.Arc(cfg.material, center, forward, attack.Range * 0.7f, half * 0.8f, cfg.color * new Color(1f, 1f, 1f, 0.6f), cfg.whipWidth * 0.6f, 0.18f);
            Whips++;

            swings++;
            if (swings % cfg.pullEvery == 0) PullFarthest(attack.Origin, forward);
            struck.Clear();
        }

        void PullFarthest(Vector3 origin, Vector3 forward)
        {
            struck.RemoveAll(t => t == null || !CombatFx.IsAlive(t));
            if (struck.Count == 0) return;

            struck.Sort((a, b) => (b.position - origin).sqrMagnitude.CompareTo((a.position - origin).sqrMagnitude));

            Vector3 side = Vector3.Cross(Vector3.up, forward);
            int n = Mathf.Min(cfg.pullCount, struck.Count);

            for (int i = 0; i < n; i++)
            {
                Transform t = struck[i];
                if ((t.position - origin).sqrMagnitude < cfg.pullTo * cfg.pullTo * 1.5f) continue;

                // 앞에 나란히 — 한 점에 겹치지 않게 옆으로 벌린다
                float offset = (i - (n - 1) * 0.5f) * 0.9f;
                Vector3 point = origin + forward * cfg.pullTo + side * offset;

                FxLine.Line(cfg.material, CombatFx.Body(t), origin + Vector3.up * 0.9f, cfg.color, 0.1f, cfg.pullTime + 0.1f);
                CombatFx.Stun(t, cfg.pullStun);
                if (EvoStrikes.PullTo(Weapon, t, point, cfg.pullTime)) Pulled++;
            }

            CombatFx.PlaySound(cfg.pullSound);
        }
    }
}
