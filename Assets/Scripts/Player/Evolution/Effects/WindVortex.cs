using UnityEngine;

/// <summary>
/// 돌풍 저격총 탄 둘레를 휘감는 바람 소용돌이 (2026-11-01 사용자 요청 — 로스트아크 니나브 에스더 스킬처럼).
/// 탄 프리팹(Bullet_EVO_Gale)의 자식에 붙는다.
///
/// 궤적(TrailRenderer)은 프레임마다 점을 찍어 60 m/s 탄에서는 나선이 직선처럼 깨진다 —
/// 그래서 탄 위치에서 뒤로 length m 구간의 나선을 매 프레임 직접 그린다 (탄은 곧게 날아가므로 뒤쪽 = 지나온 길).
/// 쏜 자리보다 뒤로는 그리지 않는다 (총구 뒤로 나선이 삐져나오지 않게). 풀에서 다시 나올 때 처음부터.
/// </summary>
public class WindVortex : MonoBehaviour
{
    [System.Serializable]
    public class Strand
    {
        [Min(1)] public int count = 3;
        [Min(0.05f)] public float radius = 0.6f;
        [Tooltip("탄 뒤로 그리는 길이 (m)")]
        [Min(0.5f)] public float length = 5f;
        [Tooltip("그 길이 안에서 몇 바퀴 감기는가")]
        public float turns = 1.5f;
        [Tooltip("초당 회전 (도) — 음수면 반대로")]
        public float spin = 900f;
        [Min(0.01f)] public float width = 0.16f;
        [Range(0f, 1f)] public float alpha = 0.95f;
        [Tooltip("뒤로 갈수록 반경이 이만큼 커진다 (1 = 그대로)")]
        [Min(0.1f)] public float flare = 1.5f;
    }

    public Material material;
    public Color color = new Color(0.55f, 1f, 0.85f, 1f);
    public Strand inner = new Strand();
    public Strand outer = new Strand { count = 2, radius = 1.15f, length = 4f, turns = 1f, spin = -620f, width = 0.1f, alpha = 0.55f, flare = 1.3f };
    [Min(4)] public int points = 24;

    LineRenderer[] lines;
    Strand[] owners;
    float[] offsets;
    Vector3 origin;
    bool fresh;
    float phase;

    void Awake()
    {
        int n = inner.count + outer.count;
        lines = new LineRenderer[n];
        owners = new Strand[n];
        offsets = new float[n];

        for (int i = 0; i < n; i++)
        {
            Strand s = i < inner.count ? inner : outer;
            int k = i < inner.count ? i : i - inner.count;
            owners[i] = s;
            offsets[i] = k * Mathf.PI * 2f / s.count;
            lines[i] = Build(s, i);
        }
    }

    LineRenderer Build(Strand s, int index)
    {
        var go = new GameObject($"Wind{index}");
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.sharedMaterial = material;
        line.positionCount = points;
        line.widthCurve = new AnimationCurve(new Keyframe(0f, s.width * 0.6f), new Keyframe(0.15f, s.width), new Keyframe(1f, 0f));
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.Lerp(color, Color.white, 0.6f), 0f), new GradientColorKey(color, 0.4f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(s.alpha, 0f), new GradientAlphaKey(s.alpha * 0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
        line.colorGradient = gradient;
        line.enabled = false;
        return line;
    }

    void OnEnable()
    {
        // 풀에서 나온 순간에는 아직 총구로 옮겨지기 전이다 — 첫 LateUpdate 에서 출발점을 잡는다
        fresh = true;
        if (lines == null) return;
        foreach (LineRenderer l in lines) l.enabled = false;
    }

    void LateUpdate()
    {
        if (lines == null) return;

        Vector3 pos = transform.position;
        Vector3 fwd = transform.forward;

        if (fresh)
        {
            fresh = false;
            origin = pos;
            phase = Random.Range(0f, Mathf.PI * 2f);
        }

        phase += Time.deltaTime * Mathf.Deg2Rad;   // 줄마다 자기 spin 을 곱한다
        float flown = Mathf.Max(0f, Vector3.Dot(pos - origin, fwd));

        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
        right.Normalize();
        Vector3 up = Vector3.Cross(fwd, right);

        for (int i = 0; i < lines.Length; i++)
        {
            Strand s = owners[i];
            LineRenderer line = lines[i];
            float len = Mathf.Min(s.length, flown);
            if (len < 0.2f) { line.enabled = false; continue; }
            line.enabled = true;

            float a0 = offsets[i] + phase * s.spin;
            for (int p = 0; p < points; p++)
            {
                float t = p / (float)(points - 1);
                float a = a0 + t * s.turns * Mathf.PI * 2f * Mathf.Sign(s.spin == 0f ? 1f : s.spin);
                float r = s.radius * Mathf.Lerp(1f, s.flare, t) * Mathf.Clamp01(t * 6f + 0.35f);   // 탄 바로 뒤는 조여 있다
                line.SetPosition(p, pos - fwd * (t * len) + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * r);
            }
        }
    }
}
