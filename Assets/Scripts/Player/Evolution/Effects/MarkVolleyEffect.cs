using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상위 무기 "다중 표적" (스나이퍼 + 반자동) — 맞힌 적에게 표식(markTime 초).
/// volleyEvery 번째 발마다 표식 있는 적 최대 maxTargets 명에게 동시에 자동 사격 (치명타 확정).
/// 표식은 적 발밑의 붉은 조준 고리로 보인다. 일제 사격은 추가 피해라 알림을 보내지 않는다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Mark Volley", fileName = "AE_EVO_MarkVolley")]
public class MarkVolleyEffect : AttachmentEffect
{
    [Min(0.5f)] public float markTime = 3f;
    [Range(2, 10)] public int volleyEvery = 5;
    [Range(1, 12)] public int maxTargets = 6;
    [Tooltip("일제 사격 한 줄 피해 = 무기 공격력 × 치명타 배율 × 이 값")]
    [Min(0f)] public float volleyDamageMul = 1f;
    [Tooltip("이 거리 밖의 표식은 쏘지 않는다")]
    [Min(1f)] public float volleyRange = 30f;

    [Header("연출")]
    public Material material;
    public Color color = new Color(1f, 0.2f, 0.2f, 1f);
    [Min(0.1f)] public float markRadius = 0.7f;
    public GameSoundSet.Entry volleySound = new GameSoundSet.Entry();

    public static int Volleys { get; private set; }
    public static int VolleyHits { get; private set; }
    public static int Marked { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Volleys = 0; VolleyHits = 0; Marked = 0; }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Mark
    {
        public Transform target;
        public float until;
        public LineRenderer ring;
    }

    class Runtime : AttachmentRuntime
    {
        const int Segments = 20;

        readonly MarkVolleyEffect cfg;
        readonly List<Mark> marks = new List<Mark>();
        readonly Stack<LineRenderer> spare = new Stack<LineRenderer>();
        int shots;

        public Runtime(MarkVolleyEffect cfg) => this.cfg = cfg;

        public override void OnHit(in WeaponHit hit)
        {
            if (hit.Target == null || hit.Killed || !CombatFx.IsAlive(hit.Target)) return;

            foreach (Mark m in marks)
            {
                if (m.target != hit.Target) continue;
                m.until = Time.time + cfg.markTime;
                return;
            }

            marks.Add(new Mark { target = hit.Target, until = Time.time + cfg.markTime, ring = TakeRing() });
            Marked++;
        }

        public override void OnAttack(in WeaponAttack attack)
        {
            shots++;
            if (shots % cfg.volleyEvery != 0 || Weapon == null) return;

            Prune();
            if (marks.Count == 0) return;

            Vector3 origin = attack.Origin;
            marks.Sort((a, b) => (a.target.position - origin).sqrMagnitude.CompareTo((b.target.position - origin).sqrMagnitude));

            WeaponRuntimeStats stats = Weapon.Stats;
            float damage = stats.Damage * Mathf.Max(1f, stats.CritMultiplier) * cfg.volleyDamageMul;
            int fired = 0;

            foreach (Mark m in marks)
            {
                if (fired >= cfg.maxTargets) break;
                Vector3 to = CombatFx.Body(m.target);
                if ((to - origin).sqrMagnitude > cfg.volleyRange * cfg.volleyRange) continue;

                CombatFx.Damage(m.target, damage, to, EvoStrikes.Flat(to - origin, Vector3.forward), true);
                FxLine.Line(cfg.material, origin, to, cfg.color, 0.14f, 0.18f);
                FxLine.Line(cfg.material, origin, to, Color.white, 0.05f, 0.12f);
                FxLine.Ring(cfg.material, m.target.position, cfg.markRadius * 1.6f, 0.1f, cfg.color, 0.12f, 0.16f);
                fired++;
                VolleyHits++;
            }

            if (fired > 0)
            {
                Volleys++;
                CombatFx.PlaySound(cfg.volleySound);
            }
        }

        public override void Tick(float deltaTime)
        {
            Prune();

            float spin = Time.time * 120f;
            foreach (Mark m in marks)
            {
                if (m.ring == null) continue;
                m.ring.enabled = true;

                Vector3 c = m.target.position + Vector3.up * 0.08f;
                float left = Mathf.Clamp01((m.until - Time.time) / cfg.markTime);
                float r = cfg.markRadius * (0.85f + 0.15f * Mathf.Sin(Time.time * 10f));

                for (int i = 0; i < Segments; i++)
                {
                    float a = (spin + i * 360f / Segments) * Mathf.Deg2Rad;
                    m.ring.SetPosition(i, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                }

                Color col = cfg.color;
                col.a = 0.4f + 0.6f * left;
                m.ring.startColor = col;
                m.ring.endColor = col;
            }
        }

        public override void OnActiveChanged(bool active)
        {
            if (!active) Clear();
        }

        public override void Dispose()
        {
            Clear();
            while (spare.Count > 0)
            {
                LineRenderer r = spare.Pop();
                if (r != null) Object.Destroy(r.gameObject);
            }
        }

        void Prune()
        {
            for (int i = marks.Count - 1; i >= 0; i--)
            {
                Mark m = marks[i];
                if (m.target != null && m.until > Time.time && CombatFx.IsAlive(m.target)) continue;
                GiveBack(m.ring);
                marks.RemoveAt(i);
            }
        }

        void Clear()
        {
            foreach (Mark m in marks) GiveBack(m.ring);
            marks.Clear();
        }

        LineRenderer TakeRing()
        {
            while (spare.Count > 0)
            {
                LineRenderer r = spare.Pop();
                if (r != null) return r;
            }

            var go = new GameObject("TargetMark");
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = Segments;
            line.widthMultiplier = 0.08f;
            line.sharedMaterial = cfg.material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        void GiveBack(LineRenderer ring)
        {
            if (ring == null) return;
            ring.enabled = false;
            spare.Push(ring);
        }
    }
}
