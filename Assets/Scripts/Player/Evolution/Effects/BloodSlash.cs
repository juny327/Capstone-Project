using UnityEngine;

/// <summary>
/// 혈검 폭주 검격 하나 (혈풍 난무 · 2026-11-03 "딱딱하고 멋이 없다 — 날카로운 붉은 검격으로" 사용자 피드백).
///
/// 예전에는 굵기가 일정한 선(FxLine.SlashArc)이 그 자리에서 옅어지기만 해서 막대처럼 보였다. 이제는:
///  · 모양 — 꼬리는 바늘처럼 가늘고, 앞쪽 3/4 지점이 가장 두껍고, 머리 끝은 다시 뾰족한 초승달
///  · 움직임 — 칼이 지나가듯 sweepTime 동안 호를 따라 순식간에 그어지고(머리가 달려 나감), 그 뒤 꼬리부터 머리 쪽으로 걷히며 사라진다
///  · 겹 — 넓은 검붉은 빛무리 · 붉은 몸통 · 바깥 날을 따라 흰빛이 도는 가는 심지 (날이 서 보이게)
/// 메시 하나에 세 겹을 정점 색으로 담는다 (FxLine 과 같은 더하기 재질 — 정점 색을 곱한다). 다 사라지면 스스로 지운다.
/// </summary>
public class BloodSlash : MonoBehaviour
{
    const int Samples = 28;
    const int Rows = 3;          // 한 단면에 바깥 · 가운데 · 안쪽 정점
    const int Layers = 3;        // 빛무리 · 몸통 · 심지

    Mesh mesh;
    Vector3[] vertices;
    Color[] colors;
    float radius, halfAngle, width, sweepTime, fadeTime, age;
    bool reverse;
    Color glow, body, core;

    public static int Alive { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Alive = 0;

    /// <summary>
    /// center 에서 forward 쪽으로, 바닥에서 tilt 만큼 기울어진 면에 반경 radius · 반각 halfAngle 의 검격.
    /// width 는 가장 두꺼운 곳의 몸통 두께(m). reverse 면 반대쪽에서 그어진다.
    /// </summary>
    public static BloodSlash Spawn(Material material, Vector3 center, Vector3 forward, float radius, float halfAngle, float tilt,
                                   float width, Color color, Color coreColor, float sweepTime, float fadeTime, bool reverse)
    {
        if (material == null) return null;

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

        var go = new GameObject("BloodSlash");
        go.transform.SetPositionAndRotation(center, Quaternion.LookRotation(forward.normalized) * Quaternion.Euler(0f, 0f, tilt));

        var slash = go.AddComponent<BloodSlash>();
        slash.radius = radius;
        slash.halfAngle = halfAngle;
        slash.width = width;
        slash.sweepTime = Mathf.Max(0.01f, sweepTime);
        slash.fadeTime = Mathf.Max(0.01f, fadeTime);
        slash.reverse = reverse;
        slash.body = color;
        slash.core = coreColor;
        slash.glow = new Color(color.r * 0.75f, color.g * 0.3f, color.b * 0.35f, 0.5f);   // 몸통보다 검붉게 · 옅게
        slash.Build(material);
        Alive++;
        return slash;
    }

    void Build(Material material)
    {
        int count = Layers * (Samples + 1) * Rows;
        vertices = new Vector3[count];
        colors = new Color[count];
        var uvs = new Vector2[count];
        for (int i = 0; i < count; i++) uvs[i] = new Vector2(0.5f, 0.5f);   // 선 재질의 가로 흐림은 쓰지 않는다 — 가장자리는 정점 알파로

        var triangles = new int[Layers * Samples * (Rows - 1) * 6 * 2];
        int k = 0;
        for (int layer = 0; layer < Layers; layer++)
        {
            int start = layer * (Samples + 1) * Rows;
            for (int s = 0; s < Samples; s++)
            {
                for (int r = 0; r < Rows - 1; r++)
                {
                    int a = start + s * Rows + r, b = a + 1, c = a + Rows, d = c + 1;
                    triangles[k++] = a; triangles[k++] = c; triangles[k++] = b;
                    triangles[k++] = b; triangles[k++] = c; triangles[k++] = d;
                    // 뒷면 — 기울어진 면을 아래에서 봐도 보이게
                    triangles[k++] = a; triangles[k++] = b; triangles[k++] = c;
                    triangles[k++] = b; triangles[k++] = d; triangles[k++] = c;
                }
            }
        }

        mesh = new Mesh { name = "BloodSlash" };
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r2 = gameObject.AddComponent<MeshRenderer>();
        r2.sharedMaterial = material;
        r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r2.receiveShadows = false;

        Refresh();
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= sweepTime + fadeTime)
        {
            Destroy(gameObject);
            return;
        }
        Refresh();
    }

    void OnDestroy()
    {
        Alive--;
        if (mesh != null) Destroy(mesh);
    }

    /// <summary>지금 보이는 구간(꼬리 ~ 머리)과 밝기로 메시를 다시 만든다.</summary>
    void Refresh()
    {
        // 머리: 순식간에 달려 나간다 (끝으로 갈수록 느려지게) · 꼬리: 다 그어진 뒤 머리 쪽으로 걷힌다
        float sweep = Mathf.Clamp01(age / sweepTime);
        float head = 1f - (1f - sweep) * (1f - sweep) * (1f - sweep);
        float fade = Mathf.Clamp01((age - sweepTime) / fadeTime);
        float tail = Mathf.Lerp(0f, 0.92f, fade * fade);
        float alpha = 1f - fade * fade;
        float flash = 1f + 0.6f * (1f - sweep);   // 그어지는 동안 더 밝게

        // 지금 보이는 길이에 맞춰 두께도 줄인다 — 짧을 때 뭉툭한 덩어리가 되지 않게
        float span = Mathf.Max(0.0001f, head - tail);
        float thick = width * Mathf.Clamp01(span * 2.2f);

        for (int layer = 0; layer < Layers; layer++)
        {
            int start = layer * (Samples + 1) * Rows;
            for (int s = 0; s <= Samples; s++)
            {
                float t = s / (float)Samples;                 // 꼬리 0 → 머리 1
                float u = Mathf.Lerp(tail, head, t);
                float deg = Mathf.Lerp(-halfAngle, halfAngle, reverse ? 1f - u : u);
                float rad = deg * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

                // 바늘 꼬리 · 앞쪽 3/4 에서 가장 두껍고 · 머리는 뾰족 — t^1.2 (1-t)^0.35 를 최대 1 로
                float profile = Mathf.Pow(t, 1.2f) * Mathf.Pow(1f - t, 0.35f) / 0.437f;
                float T = thick * profile;

                float outer, mid, inner, ao, am, ai;
                Color c;
                switch (layer)
                {
                    case 0:  // 빛무리 — 몸통 둘레로 넓게, 양 가장자리는 0
                        outer = radius + 0.35f * T; mid = radius - 0.45f * T; inner = radius - 1.8f * T;
                        ao = 0f; am = 0.55f; ai = 0f; c = glow;
                        break;
                    case 1:  // 몸통 — 바깥 날은 또렷하고 안쪽으로 흐려진다
                        outer = radius; mid = radius - 0.4f * T; inner = radius - T;
                        ao = 1f; am = 0.85f; ai = 0f; c = body;
                        break;
                    default: // 심지 — 바깥 날을 따라 가는 흰빛
                        outer = radius - 0.02f * T; mid = radius - 0.1f * T; inner = radius - 0.32f * T;
                        ao = 0.7f; am = 1f; ai = 0f; c = core;
                        break;
                }

                float a = alpha * (layer == 1 ? Mathf.Min(1f, flash) : flash) * Mathf.Clamp01(profile * 3f);
                int i = start + s * Rows;
                vertices[i] = dir * outer;
                vertices[i + 1] = dir * mid;
                vertices[i + 2] = dir * inner;
                colors[i] = Tint(c, ao * a);
                colors[i + 1] = Tint(c, am * a);
                colors[i + 2] = Tint(c, ai * a);
            }
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.RecalculateBounds();
    }

    static Color Tint(Color c, float alpha) => new Color(c.r, c.g, c.b, Mathf.Clamp01(c.a * alpha));
}
