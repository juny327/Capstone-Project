using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// FPS 표시 (2026-11-18, 성능 3단계) — F3 키 또는 설정 창 'FPS 표시'로 켠다. 편집기 · 빌드 둘 다.
///
/// 화면 위 가운데에 한 줄: FPS · 프레임 시간(ms) · 최근 3초에서 가장 길었던 프레임 · 적 수 · 그래픽 품질.
/// 그림자 · 화면 효과 같은 GPU 부담은 측정 봇(배치 모드)으로 잴 수 없어, 실제 화면에서 직접 보려고 넣었다.
/// 0.5초마다 한 번만 글자를 바꾼다 (표시 자체가 프레임을 흔들지 않게).
/// </summary>
public class PerfHud : MonoBehaviour
{
    const float RefreshSeconds = 0.5f;
    const float WorstWindowSeconds = 3f;

    static PerfHud instance;

    Canvas canvas;
    Text label;
    float sum, worst, worstShown, refreshLeft, worstLeft;
    int frames;

    /// <summary>지금 보이는가.</summary>
    public static bool Visible => instance != null && instance.canvas != null && instance.canvas.enabled;

    /// <summary>마지막으로 적은 글 · 값 (시험).</summary>
    public static string LastText { get; private set; } = "";
    public static float LastFps { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        LastText = "";
        LastFps = 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() => Ensure();   // 꺼져 있어도 F3 을 듣는다

    /// <summary>켜고 끈다 (GameSettings.SetShowFps 가 부른다).</summary>
    public static void Show(bool on)
    {
        Ensure();
        if (instance.canvas.enabled == on) return;
        instance.canvas.enabled = on;
        instance.sum = instance.worst = instance.worstShown = 0f;
        instance.frames = 0;
        instance.refreshLeft = 0.2f;
        instance.worstLeft = WorstWindowSeconds;
        if (on) instance.label.text = "FPS …";
    }

    static void Ensure()
    {
        if (instance != null) return;

        var go = new GameObject("PerfHud", typeof(RectTransform));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PerfHud>();

        instance.canvas = go.AddComponent<Canvas>();
        instance.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        instance.canvas.sortingOrder = 31000;   // 모든 화면 위, 입장 가림막(32000) 아래
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 어두운 띠 + 글 — 화면 위 가운데
        var back = new GameObject("Back", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(go.transform, false);
        var brt = (RectTransform)back.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.anchoredPosition = new Vector2(0f, -1f);
        brt.sizeDelta = new Vector2(720f, 28f);   // 로비의 '조작 가이드' 버튼(위에서 32) 위로 겹치지 않게
        Image image = back.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.55f);
        image.raycastTarget = false;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(back.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        instance.label = textGo.AddComponent<Text>();
        instance.label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        instance.label.fontSize = 20;
        instance.label.alignment = TextAnchor.MiddleCenter;
        instance.label.color = new Color(0.75f, 1f, 0.85f);
        instance.label.raycastTarget = false;
        instance.label.horizontalOverflow = HorizontalWrapMode.Overflow;

        instance.canvas.enabled = false;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            GameSettings.SetShowFps(!GameSettings.ShowFps);

        if (!canvas.enabled) return;

        float dt = Time.unscaledDeltaTime;
        sum += dt;
        frames++;
        if (dt > worst) worst = dt;

        worstLeft -= dt;
        if (worstLeft <= 0f)
        {
            worstShown = worst;
            worst = 0f;
            worstLeft = WorstWindowSeconds;
        }

        refreshLeft -= dt;
        if (refreshLeft > 0f || frames == 0 || sum <= 0f) return;

        float fps = frames / sum;
        float ms = sum / frames * 1000f;
        float worstMs = Mathf.Max(worstShown, worst) * 1000f;
        int enemies = EnemyManager.Instance != null ? EnemyManager.Instance.ActiveEnemies.Count : 0;
        LastFps = fps;
        LastText = $"FPS {fps:0}  ·  {ms:0.0}ms  ·  최근 3초 최악 {worstMs:0}ms  ·  적 {enemies}  ·  그래픽 {GraphicsQuality.Name(GraphicsQuality.Current)}";
        if (PlayMetrics.Recording) LastText += "  ·  ● 기록";   // 이번 판의 프레임 지표를 기록하는 중 (PlayMetrics)
        label.text = LastText;

        sum = 0f;
        frames = 0;
        refreshLeft = RefreshSeconds;
    }
}
