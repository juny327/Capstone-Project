using UnityEngine;

/// <summary>
/// 총의 쏘는 모습 (2026-11-14 연출 개선 1 — 기본 총 4종이 똑같이 보였다).
/// 탄 프리팹 하나(Bullet_new)를 여러 총이 같이 쓰므로, 모습은 프리팹이 아니라 <b>쏜 무기의 데이터</b>에 둔다.
/// 탄(Bullet) · 무기(ProjectileWeapon) · 드론(DroneUnit)이 이 값을 읽어 코드로 그린다 — 팀원 프리팹 · 풀 설정은 그대로.
/// material 이 비어 있으면 아무것도 그리지 않는다 (예전 모습 그대로).
/// </summary>
[System.Serializable]
public class ShotFx
{
    [Tooltip("선 재질 (Assets/Prefabs/Attachment/FxLine.mat). 비우면 아래 연출을 그리지 않는다")]
    public Material material;

    [Header("탄 궤적")]
    public Color tracerColor = new Color(1f, 0.95f, 0.7f, 0.9f);
    [Tooltip("탄 머리 쪽 굵기(m). 꼬리로 갈수록 가늘어진다")]
    [Min(0f)] public float tracerWidth = 0.06f;
    [Tooltip("탄 뒤로 남는 길이(m). 0 이면 궤적 없음")]
    [Min(0f)] public float tracerLength = 1.2f;
    [Tooltip("켜면 쏘는 순간 총구에서 끝까지 한 줄을 그린다 — 아주 빠른 탄(스나이퍼 초속 60m)은 탄 뒤 궤적이 끊겨 보인다")]
    public bool tracerInstant;
    [Tooltip("한 줄 궤적이 사라지는 시간(초)")]
    [Min(0.02f)] public float tracerInstantFade = 0.15f;

    [Header("총구 섬광")]
    public Color flashColor = new Color(1f, 0.9f, 0.55f, 1f);
    [Tooltip("앞으로 뻗는 길이(m). 0 이면 섬광 없음")]
    [Min(0f)] public float flashSize = 0.5f;
    [Tooltip("부채로 벌어지는 각도 (샷건). 0 이면 십자 모양")]
    [Range(0f, 90f)] public float flashFan;
    [Tooltip("섬광과 함께 퍼지는 고리의 끝 반경(m). 0 이면 없음")]
    [Min(0f)] public float flashRing;

    [Header("맞힘 불꽃")]
    public Color sparkColor = new Color(1f, 0.9f, 0.45f, 1f);
    [Tooltip("불꽃 길이(m). 0 이면 없음")]
    [Min(0f)] public float sparkSize = 0.4f;
    [Range(1, 4)] public int sparkLines = 2;
    [Tooltip("맞은 자리에 퍼지는 고리의 끝 반경(m). 0 이면 없음 (치명타는 늘 고리가 뜬다)")]
    [Min(0f)] public float sparkRing;

    [Header("쏠 때 흔들림")]
    [Tooltip("쏘는 순간 화면 흔들림. 연사가 빠른 총은 0 으로 둔다")]
    [Min(0f)] public float fireShake;

    [Header("섬광 좌우 번갈아 (스웜 카빈) — 0 이면 늘 앞으로")]
    [Tooltip("쏠 때마다 섬광을 왼쪽 · 오른쪽 · 가운데로 이 각도만큼 돌린다 — 점사 세 발이 양옆 발사관에서 나가는 것처럼 보인다")]
    [Range(0f, 60f)] public float flashSwing;

    [Header("연사 열 (회전 기관포) — 값이 0 이면 안 씀. 무기의 ShotHeat(0~1)로 섞는다")]
    [Tooltip("연사가 최고일 때 섬광 길이(m)")]
    [Min(0f)] public float hotFlashSize;
    [Tooltip("연사가 최고일 때 탄 궤적 길이(m)")]
    [Min(0f)] public float hotTracerLength;
    [Tooltip("연사가 최고일 때 탄 궤적 굵기(m)")]
    [Min(0f)] public float hotTracerWidth;
    [Tooltip("연사가 최고일 때 궤적 · 섬광 색. 알파가 0 이면 색은 그대로")]
    public Color hotColor = new Color(0f, 0f, 0f, 0f);

    [Header("연기 꼬리 (스웜 카빈 미사일) — smokeTime 이 0 이면 없음")]
    [Tooltip("연기 재질 (반투명 · 부드러운 점 — Assets/Prefabs/Evolution/EVO_Cloud.mat)")]
    public Material smokeMaterial;
    [Tooltip("연기가 남는 시간(초). 탄이 휘어 날아간 길이 연기로 보인다")]
    [Min(0f)] public float smokeTime;
    [Tooltip("연기 굵기(m) — 뒤로 갈수록 퍼진다")]
    [Min(0f)] public float smokeWidth = 0.35f;
    [Tooltip("탄 바로 뒤의 색 (불꽃)")]
    public Color smokeHeadColor = new Color(1f, 0.6f, 0.25f, 0.9f);
    [Tooltip("꼬리 쪽 색 (연기)")]
    public Color smokeTailColor = new Color(0.55f, 0.55f, 0.58f, 0.55f);

    public bool Enabled => material != null;

    public bool HasSmoke => smokeMaterial != null && smokeTime > 0f && smokeWidth > 0f;

    /// <summary>연사 열(0~1)에 맞춘 값 — hot 값이 0 이면 원래 값 그대로.</summary>
    public float FlashSizeAt(float heat) => hotFlashSize > 0f ? Mathf.Lerp(flashSize, hotFlashSize, heat) : flashSize;
    public float TracerLengthAt(float heat) => hotTracerLength > 0f ? Mathf.Lerp(tracerLength, hotTracerLength, heat) : tracerLength;
    public float TracerWidthAt(float heat) => hotTracerWidth > 0f ? Mathf.Lerp(tracerWidth, hotTracerWidth, heat) : tracerWidth;
    public Color HeatColor(Color cold, float heat)
    {
        if (hotColor.a <= 0.01f || heat <= 0f) return cold;
        Color c = Color.Lerp(cold, hotColor, heat);
        c.a = cold.a;
        return c;
    }
}

/// <summary>ShotFx 를 그리는 곳 — 선 도구(FxLine)로 그려 풀에서 돌려 쓴다.</summary>
public static class ShotFxDraw
{
    /// <summary>떠 있는 보조 총은 조금 작게.</summary>
    public const float SupportScale = 0.8f;

    /// <summary>보조 총의 바닥 빛 세기 — 보조 셋이 같이 쏘면 겹쳐 밝아진다.</summary>
    const float SupportGlow = 0.6f;
    static float GlowOf(float scale) => scale < 0.99f ? SupportGlow : 1f;

    /// <summary>시험용 — 그린 횟수.</summary>
    public static int Flashes { get; private set; }
    public static int Sparks { get; private set; }
    public static int InstantTracers { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Flashes = 0; Sparks = 0; InstantTracers = 0; swingTurn = 0; }

    static int swingTurn;

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    /// <summary>총구 섬광 — 십자(앞 가시 + 양옆 짧은 가시) 또는 부채(샷건). 흰 심지 한 줄과 선택 고리.</summary>
    /// <summary>바닥 높이를 모르면(NaN) 그 자리에서 drop 만큼 아래로 본다.</summary>
    static Vector3 Ground(Vector3 at, float groundY, float drop) => new Vector3(at.x, float.IsNaN(groundY) ? at.y - drop : groundY, at.z);

    public static void MuzzleFlash(ShotFx fx, Vector3 at, Vector3 direction, float scale = 1f, float groundY = float.NaN, float heat = 0f)
    {
        if (fx == null || !fx.Enabled || fx.flashSize <= 0f) return;

        float size = fx.FlashSizeAt(heat) * scale;
        Vector3 dir = Flat(direction);
        Color c = fx.HeatColor(fx.flashColor, heat);

        // 왼쪽 · 오른쪽 · 가운데로 번갈아 (스웜 카빈 — 점사 세 발)
        if (fx.flashSwing > 0.5f)
        {
            int turn = swingTurn++ % 3;
            if (turn < 2) dir = Quaternion.AngleAxis(turn == 0 ? -fx.flashSwing : fx.flashSwing, Vector3.up) * dir;
        }

        if (fx.flashFan > 0.5f)
        {
            const int Spikes = 5;
            for (int i = 0; i < Spikes; i++)
            {
                float angle = Mathf.Lerp(-fx.flashFan * 0.5f, fx.flashFan * 0.5f, i / (float)(Spikes - 1)) + Random.Range(-3f, 3f);
                Vector3 d = Quaternion.AngleAxis(angle, Vector3.up) * dir;
                FxLine.Line(fx.material, at, at + d * size * Random.Range(0.7f, 1f), c, size * 0.22f, 0.1f);
            }
        }
        else
        {
            float side = 55f + Random.Range(-14f, 14f);   // 쏠 때마다 조금씩 다르게
            FxLine.Line(fx.material, at, at + dir * size, c, size * 0.36f, 0.09f);
            FxLine.Line(fx.material, at, at + Quaternion.AngleAxis(side, Vector3.up) * dir * size * 0.55f, c, size * 0.24f, 0.08f);
            FxLine.Line(fx.material, at, at + Quaternion.AngleAxis(-side, Vector3.up) * dir * size * 0.55f, c, size * 0.24f, 0.08f);
        }

        // 총구의 밝은 덩어리 (짧고 굵은 선) + 흰 심지
        FxLine.Line(fx.material, at - dir * size * 0.08f, at + dir * size * 0.3f, c, size * 0.6f, 0.06f);
        FxLine.Line(fx.material, at, at + dir * size * 0.65f, Color.white, size * 0.16f, 0.06f);
        if (fx.flashRing > 0f) FxLine.Ring(fx.material, at - Vector3.up * 0.15f, 0.15f * scale, fx.flashRing * scale, c, 0.09f, 0.16f);
        GroundGlow.Flash(Ground(at + dir * size * 0.4f, groundY, 1.2f), size * 1.7f, c, 0.09f, GlowOf(scale));
        Flashes++;
    }

    /// <summary>
    /// 맞은 자리 불꽃 — 탄이 온 쪽으로 튀는 짧은 줄기 + 흰 심지. 치명타는 크게 · 고리 추가.
    /// </summary>
    public static void HitSpark(ShotFx fx, Vector3 at, Vector3 shotDirection, bool critical, float scale = 1f, float groundY = float.NaN)
    {
        if (fx == null || !fx.Enabled || fx.sparkSize <= 0f) return;

        float size = fx.sparkSize * scale * (critical ? 1.4f : 1f);
        Vector3 back = -Flat(shotDirection);
        Color c = fx.sparkColor;

        for (int i = 0; i < fx.sparkLines; i++)
        {
            Vector3 d = Quaternion.AngleAxis(Random.Range(-70f, 70f), Vector3.up) * back;
            Vector3 to = at + d * size * Random.Range(0.65f, 1.1f) + Vector3.up * Random.Range(-0.1f, 0.3f) * size;
            FxLine.Line(fx.material, at, to, c, size * 0.32f, 0.14f);
        }
        FxLine.Line(fx.material, at - back * size * 0.15f, at + back * size * 0.4f, Color.white, size * 0.2f, 0.09f);

        float ring = fx.sparkRing > 0f ? fx.sparkRing * scale : (critical ? size * 1.5f : 0f);
        if (ring > 0f) FxLine.Ring(fx.material, at - Vector3.up * 0.9f, 0.1f, ring, critical ? Color.white : c, 0.08f, 0.18f);
        GroundGlow.Flash(Ground(at, groundY, 1f), size * (critical ? 2.2f : 1.6f), c, 0.14f, (critical ? 1f : 0.7f) * GlowOf(scale));
        Sparks++;
    }

    /// <summary>쏘는 순간 총구에서 끝까지 한 줄 (스나이퍼) — 무기 색 몸통 + 흰 심지.</summary>
    public static void InstantTracer(ShotFx fx, Vector3 from, Vector3 to, float scale = 1f, float groundY = float.NaN)
    {
        if (fx == null || !fx.Enabled || fx.tracerWidth <= 0f) return;

        GroundGlow.Strip(Ground(from, groundY, 1.2f), Ground(to, groundY, 1.2f), fx.tracerWidth * scale * 6f, fx.tracerColor, fx.tracerInstantFade);

        FxLine.Line(fx.material, from, to, fx.tracerColor, fx.tracerWidth * scale, fx.tracerInstantFade);
        FxLine.Line(fx.material, from, to, Color.white, fx.tracerWidth * scale * 0.35f, fx.tracerInstantFade * 0.6f);
        InstantTracers++;
    }
}
