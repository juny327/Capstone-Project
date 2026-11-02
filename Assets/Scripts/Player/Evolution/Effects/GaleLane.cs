using System.Collections.Generic;
using UnityEngine;

/// <summary>실행 중에 생기는 바람길 하나 (돌풍 저격총). 끝나면 스스로 지운다.</summary>
public class GaleLane : MonoBehaviour
{
    static readonly List<GaleLane> lanes = new List<GaleLane>();

    GaleLaneEffect cfg;
    Vector3 start;
    Vector3 dir;
    float length;
    float damage;
    float age;
    float tick;
    float streak;
    bool ended;
    GameObject tornado;
    AudioSource wind;
    float windVolume;
    MeshRenderer band;
    MaterialPropertyBlock block;
    readonly List<Gust> gusts = new List<Gust>();

    /// <summary>길을 따라 흘러가는 바람 줄기 하나 — 부드러운 S 자 선이 길 방향으로 날아가며 생겼다 사라진다.</summary>
    class Gust
    {
        public LineRenderer Line;
        public Vector3 Origin;
        public float Age, Life, Len, Amp, Phase;
        public bool Alive;
    }

    const int GustPoints = 14;
    readonly List<Transform> found = new List<Transform>();

    static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    /// <summary>지금 남아 있는 길 수 (테스트).</summary>
    public static int Active => lanes.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => lanes.Clear();

    public static GaleLane Spawn(GaleLaneEffect cfg, Vector3 start, Vector3 dir, float length, float tickDamage)
    {
        // 많이 쌓이면 가장 오래된 것부터 지운다
        lanes.RemoveAll(l => l == null);
        while (lanes.Count >= cfg.maxLanes) lanes[0].End();

        var go = new GameObject("GaleLane");
        go.transform.position = start;

        var lane = go.AddComponent<GaleLane>();
        lane.cfg = cfg;
        lane.start = start;
        lane.dir = dir;
        lane.length = length;
        lane.damage = tickDamage;
        lanes.Add(lane);
        lane.Open();
        return lane;
    }

    Vector3 Tip => start + dir * length;

    void Open()
    {
        // 쏜 순간 길 전체를 한 번 훑는 밝은 줄기
        Vector3 up = Vector3.up * 0.6f;
        FxLine.Line(cfg.material, start + up, Tip + up, Color.Lerp(cfg.color, Color.white, 0.5f), 0.14f, 0.22f);
        FxLine.Ring(cfg.material, Tip, 0.2f, cfg.halfWidth * 1.6f, cfg.color, 0.12f, 0.3f);
        CombatFx.PlaySound(cfg.openSound);
        BuildBand();

        if (cfg.tornadoPrefab != null)
        {
            // 팀원 토네이도 스킬처럼 세운다 (이펙트 원본은 Z 축이 위) · 벽에 반쯤 묻히지 않게 조금 앞에
            Vector3 at = Tip - dir * Mathf.Min(0.6f, length * 0.2f) + Vector3.up * 1f;   // 이펙트 중심이 가운데라 바닥 위로
            tornado = Instantiate(cfg.tornadoPrefab, at, Quaternion.Euler(-90f, 0f, 0f));
            tornado.transform.localScale = Vector3.one * cfg.tornadoScale;
            foreach (Collider c in tornado.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (ParticleSystem ps in tornado.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(cfg.color, Color.white, 0.35f));
            }
        }

        if (cfg.windClip != null && cfg.windVolume > 0f)
        {
            wind = gameObject.AddComponent<AudioSource>();
            wind.clip = cfg.windClip;
            wind.loop = true;
            wind.playOnAwake = false;
            wind.spatialBlend = 0f;
            wind.volume = 0f;
            windVolume = cfg.windVolume;
            if (SoundManager.Instance != null)
            {
                wind.outputAudioMixerGroup = SoundManager.Instance.SfxGroup;
                windVolume *= SoundManager.Instance.SfxVolume;
            }
            wind.Play();
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        tick -= dt;
        streak -= dt;

        if (age >= cfg.duration)
        {
            End();
            return;
        }

        if (tick <= 0f)
        {
            tick = cfg.tickInterval;
            Cut();
        }

        if (streak <= 0f && age < cfg.duration - 0.3f)
        {
            streak = cfg.streakInterval;
            SpawnGust();
        }

        // 바람 소리 · 바닥 띠 — 0.15초에 들어오고 마지막 0.4초에 빠진다
        float fade = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01((cfg.duration - age) / 0.4f);
        UpdateGusts(dt);
        if (wind != null) wind.volume = windVolume * fade;
        SetBandAlpha(cfg.bandAlpha * fade);
    }

    /// <summary>
    /// 바닥에 깔리는 반투명 띠 — 가운데가 진하고 양옆 · 양끝이 흐려지는 띠 메시 (정점 알파 — Particles/Unlit 은 정점 색을 곱한다).
    /// 바람 줄기만으로는 길이 어디까지인지 잘 안 보였다 (2026-10-31 시험 캡처).
    /// </summary>
    void BuildBand()
    {
        if (cfg.material == null || cfg.bandAlpha <= 0f || length < 0.5f) return;

        var go = new GameObject("Band");
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(start + Vector3.up * 0.06f, Quaternion.LookRotation(dir, Vector3.up));

        int segments = Mathf.Max(2, Mathf.CeilToInt(length));
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        float w = cfg.halfWidth;

        for (int i = 0; i <= segments; i++)
        {
            float z = length * i / segments;
            float ends = Mathf.Clamp01(z / 1f) * Mathf.Clamp01((length - z) / 1.5f);
            vertices.Add(new Vector3(-w, 0f, z)); colors.Add(new Color(1f, 1f, 1f, 0f));
            vertices.Add(new Vector3(0f, 0f, z)); colors.Add(new Color(1f, 1f, 1f, ends));
            vertices.Add(new Vector3(w, 0f, z)); colors.Add(new Color(1f, 1f, 1f, 0f));
            // 선 재질과 같게 U = 길이 방향 · V = 폭 방향 (텍스처는 V 로 가장자리가 흐려진다)
            float u = z / length;
            uvs.Add(new Vector2(u, 0f)); uvs.Add(new Vector2(u, 0.5f)); uvs.Add(new Vector2(u, 1f));

            if (i == segments) continue;
            int b = i * 3;
            for (int k = 0; k < 2; k++)
            {
                int a = b + k;
                // 위에서 보이게 + 뒷면도
                triangles.Add(a); triangles.Add(a + 3); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(a + 3); triangles.Add(a + 4);
                triangles.Add(a); triangles.Add(a + 1); triangles.Add(a + 3);
                triangles.Add(a + 1); triangles.Add(a + 4); triangles.Add(a + 3);
            }
        }

        var mesh = new Mesh { name = "GaleBand" };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        band = go.AddComponent<MeshRenderer>();
        band.sharedMaterial = cfg.material;
        band.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        band.receiveShadows = false;
        block = new MaterialPropertyBlock();
        SetBandAlpha(0f);
    }

    void SetBandAlpha(float alpha)
    {
        if (band == null) return;
        Color c = cfg.color;
        c.a = alpha;
        block.SetColor(ColorId, c);
        band.SetPropertyBlock(block);
    }

    /// <summary>길 위의 적을 벤다 + 느리게.</summary>
    void Cut()
    {
        CombatFx.FindEnemiesAlong(start, Tip, cfg.halfWidth, found);
        foreach (Transform enemy in found)
        {
            CombatFx.Damage(enemy, damage, CombatFx.Body(enemy), dir);
            EnemySlow.Apply(enemy, cfg.slowFactor, cfg.tickInterval + cfg.slowLinger);
            GaleLaneEffect.Ticks++;
        }
    }

    /// <summary>
    /// 바람 줄기 하나를 띄운다 — 예전엔 FxLine.Bolt(지그재그)라 전기처럼 보였다 (2026-11-01 사용자 피드백).
    /// 지금은 부드러운 S 자 선이 길 방향으로 흘러가며 굵어졌다 가늘어진다. 줄기는 돌려 쓴다.
    /// </summary>
    void SpawnGust()
    {
        if (cfg.material == null) return;

        Gust g = gusts.Find(x => !x.Alive);
        if (g == null)
        {
            g = new Gust { Line = NewGustLine() };
            gusts.Add(g);
        }

        Vector3 side = Vector3.Cross(Vector3.up, dir);
        float span = Mathf.Max(0f, length - 1.5f);
        g.Origin = start + dir * Random.Range(0f, span * 0.85f)
                   + side * Random.Range(-cfg.halfWidth, cfg.halfWidth) * 0.8f
                   + Vector3.up * Random.Range(0.2f, 1.2f);
        g.Age = 0f;
        g.Life = Random.Range(0.45f, 0.7f);
        g.Len = Random.Range(1.4f, 2.4f);
        g.Amp = Random.Range(0.12f, 0.26f) * (Random.value < 0.5f ? -1f : 1f);
        g.Phase = Random.Range(0f, Mathf.PI * 2f);
        g.Alive = true;
        g.Line.enabled = true;
    }

    LineRenderer NewGustLine()
    {
        var go = new GameObject("Gust");
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.sharedMaterial = cfg.material;
        line.positionCount = GustPoints;
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.6f, 0.17f), new Keyframe(1f, 0.05f));
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(cfg.color, 0f), new GradientColorKey(Color.Lerp(cfg.color, Color.white, 0.55f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0.85f, 1f) });
        line.colorGradient = gradient;
        line.enabled = false;
        return line;
    }

    void UpdateGusts(float dt)
    {
        Vector3 side = Vector3.Cross(Vector3.up, dir);

        foreach (Gust g in gusts)
        {
            if (!g.Alive) continue;

            g.Age += dt;
            float travel = g.Age * cfg.gustSpeed;
            float headAlong = Vector3.Dot(g.Origin - start, dir) + travel + g.Len;
            if (g.Age >= g.Life || headAlong > length + 0.5f)
            {
                g.Alive = false;
                g.Line.enabled = false;
                continue;
            }

            // 생겼다 사라지는 굵기 — 사인 한 번 (굵기 곡선에 곱한다)
            g.Line.widthMultiplier = Mathf.Sin(Mathf.Clamp01(g.Age / g.Life) * Mathf.PI);

            Vector3 tail = g.Origin + dir * travel;
            for (int p = 0; p < GustPoints; p++)
            {
                float t = p / (float)(GustPoints - 1);
                float wave = Mathf.Sin(t * Mathf.PI * 1.8f + g.Phase + g.Age * 7f) * Mathf.Sin(t * Mathf.PI);
                Vector3 pos = tail + dir * (t * g.Len) + side * (g.Amp * wave) + Vector3.up * (g.Amp * 0.35f * wave);
                g.Line.SetPosition(p, pos);
            }
        }
    }

    public void End()
    {
        if (ended) return;
        ended = true;
        lanes.Remove(this);

        // 회오리는 바로 지우지 않고 뿜기만 멈춰 자연스럽게 사라지게 한다
        if (tornado != null)
        {
            foreach (ParticleSystem ps in tornado.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(tornado, 1.2f);
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        lanes.Remove(this);
        if (band != null) Destroy(band.GetComponent<MeshFilter>().sharedMesh);
        if (!ended && tornado != null) Destroy(tornado);
    }
}
