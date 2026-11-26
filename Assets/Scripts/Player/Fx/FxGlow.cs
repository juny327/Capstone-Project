using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 무기 연출의 빛 번짐 (2026-11-14 연출 개선 2). 연출 재질의 밝기를 블룸 문턱 위로 올려 빛나 보이게 한다.
///
/// 선 · 메시의 정점 색은 0 ~ 1 로 잘리므로, 렌더러마다 재질 색(_BaseColor · _Color)에 배율을 곱한다 (MaterialPropertyBlock —
/// 재질 에셋은 건드리지 않는다). 배율은 FxGlowSettings 한 곳에서 정한다: 흰 심지 · 섬광은 coreGlow, 무기 색 몸통은 bodyGlow.
/// <b>가는 선에만 건다</b> — 탄 궤적 · 불꽃 · 섬광 · 전격 줄기 · 광선 심지. 고리 · 연기 · 바닥 빛 · 넓은 메시(검격 · 검기 · 바람 · 프리즘 빛줄기 ·
/// 태양창 바깥 광선)는 올리지 않는다: 겹쳐 더해지는 넓은 면은 이미 밝아서, 올리면 무기 색이 흰색으로 날아간다 (11-14 캡처로 확인).
/// </summary>
public static class FxGlow
{
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int LegacyColor = Shader.PropertyToID("_Color");

    static FxGlowSettings settings;
    static bool loaded;
    static MaterialPropertyBlock block;

    public static FxGlowSettings Settings
    {
        get
        {
            if (!loaded)
            {
                settings = Resources.Load<FxGlowSettings>(FxGlowSettings.ResourcePath);
                loaded = true;
            }
            return settings;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        settings = null;
        loaded = false;
        Applied = 0;
    }

    /// <summary>흰 심지 · 섬광 · 광선 심지의 밝기 배율.</summary>
    public static float Core => Settings != null ? Settings.coreGlow : 1f;

    /// <summary>무기 색 몸통의 밝기 배율.</summary>
    public static float Body => Settings != null ? Settings.bodyGlow : 1f;

    /// <summary>시험용 — 배율(1 초과)을 건 횟수.</summary>
    public static int Applied { get; private set; }

    /// <summary>색으로 배율을 정한다 — 흰색에 가까울수록 심지로 보고 세게, 색이 짙을수록 몸통으로 본다.</summary>
    public static float For(Color color)
    {
        float white = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
        return Mathf.Lerp(Body, Core, Mathf.InverseLerp(0.55f, 0.95f, white));
    }

    /// <summary>렌더러의 재질 색에 배율을 건다 (재질의 원래 색은 지킨다). 돌려 쓰는 렌더러는 쓸 때마다 다시 부른다 — 1 이면 원래대로.</summary>
    public static void Apply(Renderer renderer, float glow)
    {
        if (renderer == null) return;
        Material material = renderer.sharedMaterial;
        if (material == null) return;

        if (block == null) block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        if (material.HasProperty(BaseColor)) block.SetColor(BaseColor, Scale(material.GetColor(BaseColor), glow));
        if (material.HasProperty(LegacyColor)) block.SetColor(LegacyColor, Scale(material.GetColor(LegacyColor), glow));
        renderer.SetPropertyBlock(block);
        if (glow > 1.001f) Applied++;
    }

    static Color Scale(Color c, float glow) => new Color(c.r * glow, c.g * glow, c.b * glow, c.a);

    /// <summary>
    /// 테스트 룸의 후처리를 스테이지와 같게 덮는다 (FxGlowSettings.stageLook — 스테이지 1 의 Atmosphere 프로필을 우선순위 20 으로).
    /// 테스트 룸은 블룸 세기가 2.68 이고 스테이지는 0.28 이라, 덮지 않으면 테스트 룸에서 맞춘 밝기가 실제 게임과 전혀 다르다.
    /// </summary>
    public static void ApplyStageLook(Scene scene)
    {
        FxGlowSettings s = Settings;
        if (s == null || s.stageLook == null) return;

        var go = new GameObject("Stage look (FxGlowSettings)");
        if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 20f;
        volume.sharedProfile = s.stageLook;
    }
}

/// <summary>
/// 바닥 빛 (2026-11-14) — 쏘는 자리 · 맞는 자리 · 폭발 자리에 잠깐 깔리는 부드러운 원.
/// 바닥 셰이더(DeepSpaceDeck)가 주 조명만 받아 점 조명을 켜도 바닥은 밝아지지 않는다 → 가운데가 밝고 가장자리로 옅어지는 원판을
/// 더하기 재질로 바닥 위에 그린다. 원판 하나를 만들어 두고 크기 · 색만 바꿔 돌려 쓴다.
/// </summary>
public class GroundGlow : MonoBehaviour
{
    const int MaxAlive = 40;
    const float Lift = 0.04f;   // 바닥과 겹쳐 깜빡이지 않게 살짝 띄운다
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    static readonly List<GroundGlow> pool = new List<GroundGlow>();
    static Mesh disc;

    MeshRenderer body;
    MaterialPropertyBlock block;
    Color color;
    float age, duration;

    /// <summary>시험용 — 그린 횟수.</summary>
    public static int Count { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        pool.Clear();
        disc = null;
        Count = 0;
    }

    /// <summary>ground(바닥 높이의 자리)에 반경 radius 의 빛을 duration 동안. strength 는 이 빛만의 세기 (설정의 groundStrength 에 곱한다).</summary>
    public static void Flash(Vector3 ground, float radius, Color color, float duration, float strength = 1f)
    {
        strength *= GameSettings.FlashScale;   // 설정 '번쩍임 줄이기' (2026-11-26)
        FxGlowSettings s = FxGlow.Settings;
        if (s == null || s.groundMaterial == null || s.groundStrength <= 0f || radius <= 0.01f || strength <= 0f) return;

        GroundGlow glow = Get(s.groundMaterial);
        if (glow == null) return;

        glow.transform.SetPositionAndRotation(ground + Vector3.up * Lift, Quaternion.identity);
        glow.transform.localScale = new Vector3(radius, 1f, radius);
        glow.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * s.groundStrength * strength));
        glow.duration = Mathf.Max(0.03f, duration);
        glow.age = 0f;
        glow.Paint(1f);
        Count++;
    }

    /// <summary>from → to 를 따라 바닥에 깔리는 빛 띠 (광선 · 한 줄 궤적 아래).</summary>
    public static void Strip(Vector3 groundFrom, Vector3 groundTo, float width, Color color, float duration, float strength = 1f)
    {
        FxGlowSettings s = FxGlow.Settings;
        if (s == null || s.groundMaterial == null || s.groundStrength <= 0f || width <= 0.01f || strength <= 0f) return;

        color.a = Mathf.Clamp01(color.a * s.groundStrength * strength * 0.6f);
        FxLine.Glowless = true;   // 바닥 빛은 번지게 하지 않는다
        FxLine.Line(s.groundMaterial, groundFrom + Vector3.up * Lift, groundTo + Vector3.up * Lift, color, width, duration);
        FxLine.Glowless = false;
        Count++;
    }

    static GroundGlow Get(Material material)
    {
        int alive = 0;
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            if (pool[i] == null) { pool.RemoveAt(i); continue; }
            if (pool[i].gameObject.activeSelf) { alive++; continue; }

            pool[i].body.sharedMaterial = material;
            pool[i].gameObject.SetActive(true);
            return pool[i];
        }
        if (alive >= MaxAlive) return null;   // 한꺼번에 너무 많이 — 더 그리지 않는다

        var go = new GameObject("GroundGlow");
        var glow = go.AddComponent<GroundGlow>();
        go.AddComponent<MeshFilter>().sharedMesh = Disc();
        glow.body = go.AddComponent<MeshRenderer>();
        glow.body.sharedMaterial = material;
        glow.body.shadowCastingMode = ShadowCastingMode.Off;
        glow.body.receiveShadows = false;
        glow.block = new MaterialPropertyBlock();
        pool.Add(glow);
        return glow;
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        Paint((1f - t) * (1f - t));
        if (t >= 1f) gameObject.SetActive(false);
    }

    void Paint(float fade)
    {
        Color c = color;
        c.a *= fade;
        body.GetPropertyBlock(block);
        block.SetColor(BaseColor, c);
        body.SetPropertyBlock(block);
    }

    /// <summary>반경 1 의 원판 — 가운데 불투명 · 가장자리 투명 (정점 알파). 위아래 어느 쪽에서 봐도 보이게 양면.</summary>
    static Mesh Disc()
    {
        if (disc != null) return disc;

        const int Segments = 28;
        float[] radii = { 0.3f, 0.62f, 1f };
        float[] alpha = { 0.72f, 0.3f, 0f };

        var vertices = new List<Vector3> { Vector3.zero };
        var colors = new List<Color> { Color.white };
        var uv = new List<Vector2> { new Vector2(0.5f, 0.5f) };
        for (int r = 0; r < radii.Length; r++)
            for (int i = 0; i < Segments; i++)
            {
                float a = i / (float)Segments * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radii[r]);
                colors.Add(new Color(1f, 1f, 1f, alpha[r]));
                uv.Add(new Vector2(0.5f, 0.5f));   // 선 재질의 가로 흐림은 쓰지 않는다 — 가장자리는 정점 알파로
            }

        var triangles = new List<int>();
        void Tri(int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); triangles.Add(a); triangles.Add(c); triangles.Add(b); }
        for (int i = 0; i < Segments; i++)
        {
            int next = (i + 1) % Segments;
            Tri(0, 1 + i, 1 + next);
            for (int r = 0; r < radii.Length - 1; r++)
            {
                int a = 1 + r * Segments + i, b = 1 + r * Segments + next, c = a + Segments, d = b + Segments;
                Tri(a, c, b);
                Tri(b, c, d);
            }
        }

        disc = new Mesh { name = "GroundGlowDisc" };
        disc.SetVertices(vertices);
        disc.SetColors(colors);
        disc.SetUVs(0, uv);
        disc.SetTriangles(triangles, 0);
        disc.RecalculateBounds();
        return disc;
    }
}

/// <summary>
/// 큰 순간의 점 조명 (2026-11-14) — 폭발 · 붕괴 · 큰 광선이 캐릭터와 적을 잠깐 비춘다.
/// 켜지는 순간 가장 밝고 바로 줄어든다. 동시에 FxGlowSettings.maxLights 개까지 — 넘치면 가장 오래 켜져 있던 것을 다시 쓴다.
/// (바닥은 이 조명을 받지 않는다 — 바닥 빛은 GroundGlow)
/// </summary>
public class FlashLight : MonoBehaviour
{
    static readonly List<FlashLight> pool = new List<FlashLight>();

    Light source;
    float age, duration, peak;

    /// <summary>시험용 — 켠 횟수 · 지금 켜진 수.</summary>
    public static int Count { get; private set; }
    public static int Alive
    {
        get
        {
            int n = 0;
            for (int i = 0; i < pool.Count; i++) if (pool[i] != null && pool[i].gameObject.activeSelf) n++;
            return n;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        pool.Clear();
        Count = 0;
    }

    /// <summary>at(바닥 자리) 위 1.2m 에 조명을 켠다. strength 는 이 조명만의 세기 (설정의 lightIntensity 에 곱한다).</summary>
    public static void Flash(Vector3 at, Color color, float range, float duration = 0.15f, float strength = 1f)
    {
        strength *= GameSettings.FlashScale;   // 설정 '번쩍임 줄이기' (2026-11-26) — 폭발 · 낙뢰의 순간 조명
        FxGlowSettings s = FxGlow.Settings;
        if (s == null || s.maxLights <= 0 || s.lightIntensity <= 0f || range <= 0.1f || strength <= 0f) return;

        FlashLight light = Get(s.maxLights);
        if (light == null) return;

        light.transform.position = at + Vector3.up * 1.2f;
        light.source.color = new Color(color.r, color.g, color.b, 1f);
        light.source.range = range;
        light.peak = s.lightIntensity * strength;
        light.source.intensity = light.peak;
        light.duration = Mathf.Max(0.04f, duration);
        light.age = 0f;
        Count++;
    }

    static FlashLight Get(int max)
    {
        FlashLight oldest = null;
        int alive = 0;
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            if (pool[i] == null) { pool.RemoveAt(i); continue; }
            if (!pool[i].gameObject.activeSelf)
            {
                pool[i].gameObject.SetActive(true);
                return pool[i];
            }
            alive++;
            if (oldest == null || pool[i].age / pool[i].duration > oldest.age / oldest.duration) oldest = pool[i];
        }
        if (alive >= max) return oldest;   // 가장 많이 사그라든 것을 다시 쓴다

        var go = new GameObject("FlashLight");
        var light = go.AddComponent<FlashLight>();
        light.source = go.AddComponent<Light>();
        light.source.type = LightType.Point;
        light.source.shadows = LightShadows.None;
        pool.Add(light);
        return light;
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        source.intensity = peak * (1f - t) * (1f - t);
        if (t >= 1f) gameObject.SetActive(false);
    }
}
