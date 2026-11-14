using UnityEngine;

/// <summary>
/// 탄 뒤로 남는 궤적 (2026-11-14 연출 개선 1). 탄(Bullet)에 한 번 붙어 탄과 함께 풀에서 돌려 쓰인다.
/// 탄 머리에서 뒤쪽 length 만큼을 선 하나로 잇는다 — 머리는 굵고 진하게, 꼬리는 가늘고 투명하게.
/// 모습(색 · 굵기 · 길이)은 쏜 무기의 ShotFx 에서 온다. 탄 프리팹은 고치지 않는다.
/// </summary>
public class BulletTracer : MonoBehaviour
{
    LineRenderer line;
    Transform bullet;
    Vector3 origin;
    float length;
    bool active;

    /// <summary>시험용 — 지금 그려지고 있는 궤적 수.</summary>
    public static int Alive { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Alive = 0;

    public static BulletTracer Attach(Transform bullet)
    {
        var go = new GameObject("Tracer");
        go.transform.SetParent(bullet, false);

        var tracer = go.AddComponent<BulletTracer>();
        tracer.bullet = bullet;
        tracer.line = go.AddComponent<LineRenderer>();
        tracer.line.useWorldSpace = true;
        tracer.line.positionCount = 2;
        tracer.line.numCapVertices = 2;
        tracer.line.alignment = LineAlignment.View;
        tracer.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tracer.line.receiveShadows = false;
        tracer.line.enabled = false;
        return tracer;
    }

    /// <param name="heat">연사 열 0~1 (회전 기관포) — 길이 · 굵기가 ShotFx 의 hot 값 쪽으로 간다. 색은 부르는 쪽이 섞어서 준다.</param>
    public void Begin(ShotFx fx, Color color, float scale, float heat = 0f)
    {
        float width = fx.TracerWidthAt(heat) * scale;
        line.sharedMaterial = fx.material;
        FxGlow.Apply(line, FxGlow.Body);
        line.startWidth = width;          // 머리
        line.endWidth = width * 0.15f;    // 꼬리
        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, 0f);

        length = fx.TracerLengthAt(heat) * scale;
        origin = bullet.position;
        line.SetPosition(0, origin);
        line.SetPosition(1, origin);
        line.enabled = true;
        if (!active) { active = true; Alive++; }
    }

    public void End()
    {
        if (line != null) line.enabled = false;
        if (active) { active = false; Alive--; }
    }

    void LateUpdate()
    {
        if (!active) return;

        Vector3 head = bullet.position;
        // 막 쏜 탄은 날아온 만큼만 — 총구 뒤로 궤적이 삐져나오지 않게
        float travelled = (head - origin).magnitude;
        line.SetPosition(0, head);
        line.SetPosition(1, head - bullet.forward * Mathf.Min(length, travelled));
    }

    void OnDestroy()
    {
        if (active) Alive--;
    }
}
