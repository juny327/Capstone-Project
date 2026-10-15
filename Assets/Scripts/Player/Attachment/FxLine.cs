using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 잠깐 보였다 사라지는 선 · 고리 — 전격탄 줄기 · 튕김탄 궤적 · 폭발 · 충격파 연출.
/// 프리팹 없이 LineRenderer 하나로 그리고 다 쓰면 돌려 쓴다 (씬이 바뀌면 같이 사라진다).
/// </summary>
public class FxLine : MonoBehaviour
{
    enum Mode { Line, Bolt, Ring }

    static readonly List<FxLine> pool = new List<FxLine>();

    LineRenderer line;
    Mode mode;
    float age;
    float duration;
    float width;
    Color color;

    // 고리
    Vector3 center;
    float startRadius;
    float endRadius;

    /// <summary>두 점을 잇는 곧은 선 (튕김탄 궤적).</summary>
    public static void Line(Material material, Vector3 from, Vector3 to, Color color, float width, float duration)
    {
        FxLine fx = Get(material);
        if (fx == null) return;

        fx.Begin(Mode.Line, color, width, duration);
        fx.line.positionCount = 2;
        fx.line.SetPosition(0, from);
        fx.line.SetPosition(1, to);
    }

    /// <summary>지그재그 전기 줄기 (전격탄 · 방전).</summary>
    public static void Bolt(Material material, Vector3 from, Vector3 to, Color color, float width, float duration, float jitter = 0.15f)
    {
        FxLine fx = Get(material);
        if (fx == null) return;

        fx.Begin(Mode.Bolt, color, width, duration);

        const int segments = 7;
        fx.line.positionCount = segments + 1;

        Vector3 axis = to - from;
        Vector3 side = Vector3.Cross(axis, Vector3.up);
        if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
        side.Normalize();

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 p = from + axis * t;
            if (i != 0 && i != segments)
                p += side * Random.Range(-jitter, jitter) * Mathf.Sin(t * Mathf.PI) + Vector3.up * Random.Range(-jitter, jitter) * 0.5f;
            fx.line.SetPosition(i, p);
        }
    }

    /// <summary>바닥에서 퍼지는 고리 (폭발 · 충격파). 반경이 start → end 로 커지며 흐려진다.</summary>
    public static void Ring(Material material, Vector3 at, float startRadius, float endRadius, Color color, float width, float duration)
    {
        FxLine fx = Get(material);
        if (fx == null) return;

        fx.Begin(Mode.Ring, color, width, duration);
        fx.center = at + Vector3.up * 0.15f;
        fx.startRadius = startRadius;
        fx.endRadius = endRadius;
        fx.line.positionCount = 33;
        fx.line.loop = false;
        fx.DrawRing(startRadius);
    }

    /// <summary>
    /// 바닥과 나란한 호 (사슬검의 채찍 궤적). center 둘레 반경 radius 에서 forward 기준 ±halfAngle(도).
    /// 고리처럼 퍼지지 않고 그 자리에서 흐려진다.
    /// </summary>
    /// <summary>
    /// 비스듬한 호 — 칼로 사선으로 벤 자국 (혈검 혈풍 난무). Arc 와 같지만 forward 축을 중심으로 tilt(도)만큼 기울여
    /// 한쪽 끝은 높고 한쪽 끝은 낮다. 그 자리에서 흐려진다.
    /// </summary>
    public static void SlashArc(Material material, Vector3 center, Vector3 forward, float radius, float halfAngle, float tilt, Color color, float width, float duration)
    {
        FxLine fx = Get(material);
        if (fx == null) return;

        fx.Begin(Mode.Bolt, color, width, duration);

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        Quaternion basis = Quaternion.LookRotation(forward.normalized) * Quaternion.Euler(0f, 0f, tilt);

        const int segments = 20;
        fx.line.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float yaw = (-halfAngle + 2f * halfAngle * i / segments) * Mathf.Deg2Rad;
            fx.line.SetPosition(i, center + basis * (new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * radius));
        }
    }

    public static void Arc(Material material, Vector3 center, Vector3 forward, float radius, float halfAngle, Color color, float width, float duration)
    {
        FxLine fx = Get(material);
        if (fx == null) return;

        fx.Begin(Mode.Bolt, color, width, duration);   // 모양만 정하고 움직이지 않는 선 — Bolt 와 같은 처리

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        float baseYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        const int segments = 20;
        fx.line.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float yaw = (baseYaw - halfAngle + 2f * halfAngle * i / segments) * Mathf.Deg2Rad;
            fx.line.SetPosition(i, center + new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * radius);
        }
    }

    static FxLine Get(Material material)
    {
        if (material == null) return null;

        for (int i = pool.Count - 1; i >= 0; i--)
        {
            if (pool[i] == null) { pool.RemoveAt(i); continue; }
            if (pool[i].gameObject.activeSelf) continue;

            pool[i].line.sharedMaterial = material;
            pool[i].gameObject.SetActive(true);
            return pool[i];
        }

        var go = new GameObject("FxLine");
        var fx = go.AddComponent<FxLine>();
        fx.line = go.AddComponent<LineRenderer>();
        fx.line.useWorldSpace = true;
        fx.line.numCapVertices = 2;
        fx.line.alignment = LineAlignment.View;
        fx.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fx.line.receiveShadows = false;
        fx.line.sharedMaterial = material;
        pool.Add(fx);
        return fx;
    }

    void Begin(Mode m, Color c, float w, float d)
    {
        mode = m;
        color = c;
        width = w;
        duration = Mathf.Max(0.02f, d);
        age = 0f;
        Apply(1f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);

        if (mode == Mode.Ring) DrawRing(Mathf.Lerp(startRadius, endRadius, 1f - (1f - t) * (1f - t)));

        Apply(1f - t);

        if (t >= 1f) gameObject.SetActive(false);
    }

    void Apply(float alpha)
    {
        Color c = color;
        c.a *= alpha;
        line.startColor = c;
        line.endColor = c;
        line.startWidth = width;
        line.endWidth = mode == Mode.Line ? width * 0.4f : width;
    }

    void DrawRing(float radius)
    {
        int n = line.positionCount;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)(n - 1) * Mathf.PI * 2f;
            line.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
        }
    }
}
