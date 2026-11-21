using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기절 · 홀림 표시 (2026-11-20 전투 망치 · 묠니르 · 도깨비 방망이).
///  · Stars  — 머리 위를 도는 별 셋 (전투 망치 기절)
///  · Sparks — 머리 위를 도는 전기 별 + 튀는 알갱이 (묠니르 기절)
///  · Haunt  — 몸에서 피어오르는 도깨비불 (도깨비불 타구에 홀려 느려진 적)
/// 표시마다 오브젝트를 만들지 않는다 — 관리자 하나가 짧은 간격으로 그 자리에 짧은 입자를 뿌린다 (FxKit). FxKit 이 없으면 보이지 않는다.
/// 적이 죽거나 꺼지면 바로 지운다. 보스는 기절 · 느려짐이 없어 표시하지 않는다.
/// 테스트 룸 무리 표적(IPullable — 적 AI 가 없어 기절 · 느려짐은 걸리지 않는다)에도 표시는 보인다 — 시험할 때 효과가 들어간 걸 알 수 있게.
/// </summary>
public class StunMarks : MonoBehaviour
{
    public enum Kind { Stars, Sparks, Haunt }

    struct Mark
    {
        public Transform target;
        public float until;
        public Color color;
        public Kind kind;
        public float height;
        public float next;
        public float phase;
    }

    const int MaxMarks = 32;
    const float Interval = 0.05f;   // 뿌리는 간격 (초) — 프레임 수와 상관없이 같은 양

    static StunMarks host;
    readonly List<Mark> marks = new List<Mark>();

    /// <summary>표시를 새로 붙인 수 (시험).</summary>
    public static int Shown { get; private set; }

    /// <summary>지금 붙어 있는 표시 수 (시험).</summary>
    public static int Active => host != null ? host.marks.Count : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        host = null;
        Shown = 0;
    }

    /// <summary>target 에 seconds 동안 표시 — 같은 종류가 이미 있으면 더 긴 쪽으로 늘린다.</summary>
    public static void Show(Transform target, float seconds, Color color, Kind kind)
    {
        if (target == null || seconds <= 0f || !FxKit.Ready || !target.gameObject.activeInHierarchy) return;
        if (target.TryGetComponent(out Enemy enemy)) { if (enemy.state == EnemyState.Dead) return; }
        else if (target.GetComponent<IPullable>() == null) return;   // 보스 — 기절 · 느려짐이 없다 (테스트 룸 무리 표적은 보이게만)

        StunMarks h = Host;
        float until = Time.time + seconds;
        for (int i = 0; i < h.marks.Count; i++)
        {
            Mark m = h.marks[i];
            if (m.target != target || m.kind != kind) continue;
            m.until = Mathf.Max(m.until, until);
            m.color = color;
            h.marks[i] = m;
            return;
        }

        if (h.marks.Count >= MaxMarks) h.marks.RemoveAt(0);
        h.marks.Add(new Mark { target = target, until = until, color = color, kind = kind, height = HeadHeight(target), phase = Random.value * 6.28f });
        Shown++;
    }

    static StunMarks Host
    {
        get
        {
            if (host == null) host = new GameObject("StunMarks").AddComponent<StunMarks>();
            return host;
        }
    }

    /// <summary>머리 위 높이 — 몸 콜라이더 꼭대기 + 조금. 콜라이더가 없으면 1.8m.</summary>
    static float HeadHeight(Transform t)
    {
        float top = 1.8f;
        Collider c = t.GetComponentInChildren<Collider>();
        if (c != null) top = Mathf.Clamp(c.bounds.max.y - t.position.y, 1f, 4f);
        return top + 0.35f;
    }

    void Update()
    {
        float now = Time.time;
        for (int i = marks.Count - 1; i >= 0; i--)
        {
            Mark m = marks[i];
            if (m.target == null || !m.target.gameObject.activeInHierarchy || now > m.until || !CombatFx.IsAlive(m.target))
            {
                marks.RemoveAt(i);
                continue;
            }

            if (now < m.next) continue;
            m.next = now + Interval;
            marks[i] = m;
            Draw(in m, now);
        }
    }

    static void Draw(in Mark m, float now)
    {
        Vector3 top = m.target.position + Vector3.up * m.height;
        switch (m.kind)
        {
            case Kind.Stars:
            case Kind.Sparks:
            {
                // 초당 약 0.9바퀴 — 별 셋이 120° 간격으로 돈다 (조금씩 위아래로 흔들린다)
                float a = m.phase + now * 5.5f;
                for (int k = 0; k < 3; k++)
                {
                    float t = a + k * 2.094f;
                    Vector3 p = top + new Vector3(Mathf.Cos(t) * 0.5f, Mathf.Sin(t * 2f) * 0.06f, Mathf.Sin(t) * 0.5f);
                    FxKit.Spot(p, m.color, m.kind == Kind.Sparks ? 0.6f : 0.75f, 0.12f);   // 캡처에서 작게 보여 키움 (11-20)
                }
                if (m.kind == Kind.Sparks && Random.value < 0.4f)
                    FxKit.TrailDust(top + Random.insideUnitSphere * 0.45f, m.color, 1f, 0.09f);
                break;
            }

            case Kind.Haunt:
            {
                Vector2 c = Random.insideUnitCircle * 0.35f;
                Vector3 p = m.target.position + new Vector3(c.x, Random.Range(0.4f, Mathf.Max(0.5f, m.height - 0.5f)), c.y);
                FxKit.GhostPuff(p, Vector3.up * Random.Range(0.6f, 1.4f), Random.Range(0.35f, 0.6f), Random.Range(0.3f, 0.5f),
                    new Color(m.color.r, m.color.g, m.color.b, 0.75f));
                break;
            }
        }
    }
}
