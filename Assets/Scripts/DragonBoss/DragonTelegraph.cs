using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 드래곤 보스 바닥 예고 (2026-11-13) — 데칼이 아니라 바닥 바로 위 얇은 메시라 맵 바닥 셰이더 · 데칼 설정과 상관없이 보인다.
/// 몬스터 B 돌진 예고와 같은 재질(DashTelegraph — 반투명 단색)을 쓴다. 보스 예고 규칙 (보스 개선 문서):
///  · 빨강 = 피할 수 있는 범위 공격. 테두리가 먼저 나오고 안이 차오르며, <b>다 차는 순간에 맞는다</b>
///  · 모양: 직선(물기 · 돌진 · 조준선) · 부채꼴(브레스) · 원
/// SetProgress(0~1) 로 차오름, Flash() 로 맞는 순간 번쩍 뒤 사라짐, Hold() 로 다 찬 채 남김(브레스 지속 중).
/// </summary>
public class DragonTelegraph : MonoBehaviour
{
    public static readonly Color RangeColor = new Color(1f, 0.12f, 0.08f, 0.16f);
    public static readonly Color FillColor = new Color(1f, 0.22f, 0.10f, 0.50f);
    public static readonly Color EdgeColor = new Color(1f, 0.55f, 0.35f, 0.95f);
    public static readonly Color FlashColor = new Color(1f, 0.85f, 0.65f, 0.6f);
    const float Lift = 0.045f;      // 바닥 위 높이 — 바닥 무늬와 겹쳐 깜박이지 않게
    const float FlashTime = 0.14f;

    enum Shape { Rect, Sector, Circle }

    Shape shape;
    bool thin;                       // 가는 선(조준선) — 번쩍임을 옅게
    float size;                      // 직선: 길이 · 부채꼴 · 원: 반지름
    float near;                      // 직선: 시작 거리
    float halfWidth;                 // 직선: 폭의 절반
    float halfAngle = 180f;          // 부채꼴: 반각 (원은 180)
    Transform fill;
    readonly List<Mesh> meshes = new List<Mesh>();
    readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
    MaterialPropertyBlock block;
    float flashUntil = -1f;
    bool dying;

    public bool Alive => this != null && !dying;

    /// <summary>켜면 다 차기 직전(80%부터) 테두리가 빠르게 깜박인다 — 곧 맞는다는 신호 (2026-11-20 연출, 보스가 설정에 따라 켠다).</summary>
    public static bool Blink;
    const float BlinkFrom = 0.8f;
    float progressNow;
    bool held, blinking;

    // ───────── 만들기 ─────────

    /// <summary>origin 에서 forward 쪽으로 near ~ far, 폭 width 인 직사각형. 안쪽은 앞쪽으로 차오른다.</summary>
    public static DragonTelegraph Rect(Material material, Vector3 origin, Vector3 forward, float width, float near, float far)
    {
        DragonTelegraph t = Create("Telegraph_Rect", material, origin, forward);
        t.shape = Shape.Rect;
        t.thin = width < 1.6f;
        t.near = near;
        t.halfWidth = width * 0.5f;
        t.size = Mathf.Max(0.1f, far - near);
        float w = width * 0.5f, edge = Mathf.Clamp(width * 0.06f, 0.06f, 0.16f);
        t.Quad("Range", -w, w, near, far, 0f, RangeColor);
        t.fill = t.Quad("Fill", -w, w, 0f, 1f, 0.002f, FillColor).transform;
        t.fill.localPosition = new Vector3(0f, 0f, near);
        t.Quad("EdgeL", -w, -w + edge, near, far, 0.004f, EdgeColor);
        t.Quad("EdgeR", w - edge, w, near, far, 0.004f, EdgeColor);
        t.Quad("EdgeNear", -w, w, near, near + edge, 0.004f, EdgeColor);
        t.Quad("EdgeFar", -w, w, far - edge, far, 0.004f, EdgeColor);
        t.SetProgress(0f);
        return t;
    }

    /// <summary>apex 에서 forward 를 가운데로 ±halfAngle, 반지름 radius 인 부채꼴. 안쪽은 바깥으로 차오른다.</summary>
    public static DragonTelegraph Sector(Material material, Vector3 apex, Vector3 forward, float radius, float halfAngle)
    {
        DragonTelegraph t = Create("Telegraph_Sector", material, apex, forward);
        t.shape = Shape.Sector;
        t.halfAngle = halfAngle;
        t.size = radius;
        t.Fan("Range", 0f, radius, halfAngle, 0f, RangeColor);
        t.fill = t.Fan("Fill", 0f, 1f, halfAngle, 0.002f, FillColor).transform;
        float edge = Mathf.Clamp(radius * 0.015f, 0.06f, 0.16f);
        t.Fan("EdgeArc", radius - edge, radius, halfAngle, 0.004f, EdgeColor);
        t.Spoke("EdgeL", radius, -halfAngle, edge, EdgeColor);
        t.Spoke("EdgeR", radius, halfAngle, edge, EdgeColor);
        t.SetProgress(0f);
        return t;
    }

    /// <summary>center 둘레 반지름 radius 원. 안쪽은 가운데부터 차오른다.</summary>
    public static DragonTelegraph Circle(Material material, Vector3 center, float radius)
    {
        DragonTelegraph t = Create("Telegraph_Circle", material, center, Vector3.forward);
        t.shape = Shape.Circle;
        t.size = radius;
        t.Fan("Range", 0f, radius, 180f, 0f, RangeColor);
        t.fill = t.Fan("Fill", 0f, 1f, 180f, 0.002f, FillColor).transform;
        t.Fan("Edge", radius - Mathf.Clamp(radius * 0.04f, 0.06f, 0.16f), radius, 180f, 0.004f, EdgeColor);
        t.SetProgress(0f);
        return t;
    }

    static DragonTelegraph Create(string name, Material material, Vector3 origin, Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
        var go = new GameObject(name);
        go.layer = 2;   // Ignore Raycast — 탄 · 조준이 무시한다
        go.transform.SetPositionAndRotation(new Vector3(origin.x, origin.y + Lift, origin.z), Quaternion.LookRotation(forward.normalized));
        var t = go.AddComponent<DragonTelegraph>();
        t.material = material;
        t.block = new MaterialPropertyBlock();
        return t;
    }

    Material material;

    /// <summary>
    /// 그 자리가 예고 범위 안인가 (margin m 만큼 넉넉히 본다 — 플레이어 반지름) — 2026-11-20. 시험 봇이 예고를 읽고 피할 때 쓴다
    /// (나중에 '예고가 캐릭터를 덮으면 발밑 고리가 붉게' 같은 위험 표시에도 쓸 수 있다).
    /// </summary>
    public bool Contains(Vector3 point, float margin = 0f)
    {
        if (dying) return false;
        Vector3 local = transform.InverseTransformPoint(new Vector3(point.x, transform.position.y, point.z));
        var flat = new Vector2(local.x, local.z);
        switch (shape)
        {
            case Shape.Rect:
                return Mathf.Abs(local.x) <= halfWidth + margin && local.z >= near - margin && local.z <= near + size + margin;
            case Shape.Sector:
                if (flat.magnitude > size + margin) return false;
                if (flat.magnitude < 0.6f) return true;
                return Vector2.Angle(Vector2.up, flat) <= halfAngle + Mathf.Rad2Deg * Mathf.Atan2(margin, flat.magnitude);
            default:
                return flat.magnitude <= size + margin;
        }
    }

    // ───────── 진행 ─────────

    /// <summary>0 = 테두리만, 1 = 다 참 (이때 맞는다).</summary>
    public void SetProgress(float progress)
    {
        if (fill == null) return;
        float p = Mathf.Clamp01(progress);
        progressNow = p;
        switch (shape)
        {
            case Shape.Rect: fill.localScale = new Vector3(1f, 1f, Mathf.Max(0.0001f, size * p)); break;
            default: fill.localScale = new Vector3(Mathf.Max(0.0001f, size * p), 1f, Mathf.Max(0.0001f, size * p)); break;
        }
    }

    /// <summary>맞는 순간 — 밝게 번쩍이고 곧 사라진다.</summary>
    public void Flash()
    {
        if (dying) return;
        SetProgress(1f);
        Tint(thin ? new Color(FlashColor.r, FlashColor.g, FlashColor.b, 0.32f) : FlashColor);
        flashUntil = Time.time + FlashTime;
        dying = true;
    }

    public static readonly Color LockEdge = new Color(1f, 1f, 1f, 1f);
    public static readonly Color LockFill = new Color(1f, 0.04f, 0.04f, 0.62f);
    public static readonly Color LockRange = new Color(1f, 0.08f, 0.05f, 0.26f);

    /// <summary>
    /// 방향이 굳었다 (2026-11-20 보스 패턴 개선) — 따라오던 예고가 멈추는 순간 테두리가 하얗게, 안쪽이 더 붉게 바뀌고 한 번 번쩍인다.
    /// 이때부터가 피하는 시간이다.
    /// </summary>
    public void Lock()
    {
        if (dying) return;
        foreach (MeshRenderer r in renderers)
        {
            if (r == null) continue;
            baseColors[r] = r.name.StartsWith("Edge") ? LockEdge : r.name.StartsWith("Fill") ? LockFill : LockRange;
        }
        Tint(FlashColor);
        flashUntil = Time.time + 0.07f;
    }

    /// <summary>다 찬 채 남긴다 (브레스처럼 판정이 이어질 때) — 한 번 번쩍인 뒤 채움색으로.</summary>
    public void Hold()
    {
        held = true;
        SetProgress(1f);
        Tint(FlashColor);
        flashUntil = Time.time + FlashTime;
    }

    public void Remove()
    {
        dying = true;
        if (this != null) Destroy(gameObject);
    }

    void Update()
    {
        if (flashUntil >= 0f)
        {
            if (Time.time < flashUntil) return;
            flashUntil = -1f;
            if (dying) { Destroy(gameObject); return; }
            RestoreTint();
        }

        // 다 차기 직전 — 테두리만 깜박인다 (가는 조준선 · 이미 다 찬 예고는 빼고)
        if (!Blink || dying || held || thin || progressNow < BlinkFrom || progressNow >= 1f)
        {
            if (blinking) { blinking = false; RestoreTint(); }
            return;
        }
        blinking = true;
        bool bright = Mathf.Repeat(Time.time * 11f, 1f) < 0.5f;
        foreach (MeshRenderer r in renderers)
        {
            if (r == null || !r.name.StartsWith("Edge") || !baseColors.TryGetValue(r, out Color c)) continue;
            block.SetColor("_BaseColor", bright ? Color.Lerp(c, Color.white, 0.75f) : new Color(c.r, c.g, c.b, c.a * 0.35f));
            r.SetPropertyBlock(block);
        }
    }

    void OnDestroy()
    {
        foreach (Mesh m in meshes) if (m != null) Destroy(m);
        meshes.Clear();
    }

    // ───────── 색 ─────────

    readonly Dictionary<MeshRenderer, Color> baseColors = new Dictionary<MeshRenderer, Color>();

    void Tint(Color c)
    {
        foreach (MeshRenderer r in renderers)
        {
            if (r == null) continue;
            block.SetColor("_BaseColor", r.name.StartsWith("Range") ? new Color(c.r, c.g, c.b, 0.22f) : c);
            r.SetPropertyBlock(block);
        }
    }

    void RestoreTint()
    {
        foreach (MeshRenderer r in renderers)
        {
            if (r == null || !baseColors.TryGetValue(r, out Color c)) continue;
            block.SetColor("_BaseColor", c);
            r.SetPropertyBlock(block);
        }
    }

    // ───────── 메시 ─────────

    GameObject Quad(string label, float left, float right, float rear, float front, float y, Color color) =>
        Part(label, new[] { new Vector3(left, y, rear), new Vector3(left, y, front), new Vector3(right, y, front), new Vector3(right, y, rear) },
             new[] { 0, 1, 2, 0, 2, 3 }, color);

    /// <summary>부채꼴 고리 (inner ~ outer). halfAngle 180 이면 원.</summary>
    GameObject Fan(string label, float inner, float outer, float halfAngle, float y, Color color)
    {
        int segments = Mathf.Max(8, Mathf.CeilToInt(halfAngle * 2f / 6f));
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int i = 0; i <= segments; i++)
        {
            float a = Mathf.Deg2Rad * Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments);
            var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            vertices.Add(dir * inner + Vector3.up * y);
            vertices.Add(dir * outer + Vector3.up * y);
            if (i == 0) continue;
            int b = vertices.Count - 4;
            triangles.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2 });
        }
        return Part(label, vertices.ToArray(), triangles.ToArray(), color);
    }

    /// <summary>부채꼴 옆 선 — 꼭짓점에서 angle 방향으로.</summary>
    GameObject Spoke(string label, float length, float angle, float width, Color color)
    {
        float a = Mathf.Deg2Rad * angle;
        var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        var side = new Vector3(dir.z, 0f, -dir.x) * (width * 0.5f);
        Vector3 y = Vector3.up * 0.004f;
        return Part(label, new[] { y - side, y + side, dir * length + side + y, dir * length - side + y }, new[] { 0, 2, 1, 0, 3, 2 }, color);
    }

    GameObject Part(string label, Vector3[] vertices, int[] triangles, Color color)
    {
        var part = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
        part.layer = 2;
        part.transform.SetParent(transform, false);
        var mesh = new Mesh { name = label, vertices = vertices, triangles = triangles };
        mesh.RecalculateBounds();
        meshes.Add(mesh);
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        block.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(block);
        renderers.Add(renderer);
        baseColors[renderer] = color;
        return part;
    }
}
