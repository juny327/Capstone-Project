using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 큰 한 방 손맛 (2026-11-14 연출 개선 4) — 큰 순간에 화면이 반응한다: 히트 스톱 · 줌 펀치 · 흔들림(쿵 · 덜덜 · 밀림) · 가장자리 번쩍.
/// 무엇을 얼마나 줄지는 ImpactFxSettings (Resources/Fx/ImpactFxSettings) 한 곳에서 정한다. 에셋이 없으면 예전처럼 흔들림만 난다.
///
///  · 보조 칸의 무기는 히트 스톱 · 줌을 일으키지 않는다. 보조 전용 무기(손에 들 수 없는 계열)는 번쩍 · 흔들림을 설정 비율로 내고,
///    손에도 들 수 있는 무기가 보조 칸에 있을 때는 예전처럼 약한 흔들림만 (WeaponBase.ShakeCamera 규칙 그대로)
///  · 팀원의 CameraShakeManager · 카메라 프리팹은 고치지 않는다: 그 충격 소스의 채널만 읽어 같은 채널로 내 소스를 따로 만든다
///  · 설정창의 화면 흔들림 슬라이더(GameSettings.Shake)는 카메라의 충격 수신기 세기에 곱한다 — 보스 · 피격 흔들림까지 전부 같이 줄어든다
/// </summary>
public static class ImpactFx
{
    static ImpactFxSettings settings;
    static bool loaded;
    static readonly Dictionary<ImpactMoment, float> lastAt = new Dictionary<ImpactMoment, float>();

    public static ImpactFxSettings Settings
    {
        get
        {
            if (!loaded)
            {
                settings = Resources.Load<ImpactFxSettings>(ImpactFxSettings.ResourcePath);
                loaded = true;
            }
            return settings;
        }
    }

    /// <summary>시험용 — 일어난 횟수.</summary>
    public static int HitStops { get; internal set; }
    public static int Zooms { get; internal set; }
    public static int Flashes { get; internal set; }
    public static int Thuds { get; internal set; }
    public static int Kicks { get; internal set; }
    public static int Rumbles { get; internal set; }
    public static int Played { get; private set; }
    /// <summary>그 가운데 보조 전용 무기가 낸 것 (번쩍 · 흔들림만).</summary>
    public static int SupportOnlyPlayed { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        settings = null;
        loaded = false;
        lastAt.Clear();
        HitStops = Zooms = Flashes = Thuds = Kicks = Rumbles = Played = SupportOnlyPlayed = 0;
    }

    /// <summary>
    /// 큰 순간 하나. color 는 가장자리 번쩍 색(무기 테마 색), fallbackShake 는 무기 데이터의 원래 흔들림 —
    /// 설정이 없거나 그 순간의 '쿵'이 0 이거나 보조 칸이면 이 값으로 예전처럼 흔든다. fireDirection 은 밀림 방향(쏜 쪽)이다.
    /// </summary>
    public static void Play(ImpactMoment moment, WeaponBase source, Color color, float fallbackShake = 0f, Vector3 fireDirection = default)
    {
        ImpactFxSettings s = Settings;
        ImpactFxSettings.Moment m = s != null ? s.Get(moment) : null;
        bool support = source != null && source.IsSupport;
        // 보조 전용 무기(샷건 · 충전 레이저 · 전격 소총 계열) — 손에 들 수 없어 큰 순간이 보조 칸에서만 온다
        bool supportOnly = support && source.Data != null && source.Data.SupportOnly;

        // 설정이 없다 · 손에도 들 수 있는 무기가 보조 칸에 있다 → 예전처럼 흔들림만 (WeaponBase.ShakeCamera 가 약하게 · 간격 제한)
        if (m == null || (support && !(supportOnly && s.supportOnlyShake > 0f)))
        {
            WeaponBase.ShakeCamera(source, fallbackShake);
            if (m != null && supportOnly) SupportFlash(moment, m, s, color);   // 흔들림 비율을 0 으로 둬도 번쩍은 따로
            return;
        }

        // 같은 순간이 겹쳐 오면 한 번만 (연쇄 폭발 여러 개가 한꺼번에 터질 때)
        float now = Time.unscaledTime;
        if (lastAt.TryGetValue(moment, out float last) && now - last < s.repeatGap) return;
        lastAt[moment] = now;
        Played++;

        ImpactHost host = ImpactHost.Get();
        // 쿵 값이 없으면 무기의 원래 흔들림으로 — 다만 밀림만 주는 순간(공성포 발사)은 밀림이 그 흔들림을 대신한다
        float shakeScale = supportOnly ? s.supportOnlyShake : 1f;
        float thud = (m.thud > 0f ? m.thud : m.kick > 0f ? 0f : fallbackShake) * shakeScale;
        if (thud > 0f) host.Thud(thud);
        if (m.kick > 0f) host.Kick(fireDirection, m.kick * shakeScale);

        // 보조 전용 무기 — 번쩍까지만. 줌 · 히트 스톱 · 소리 낮춤은 없다 (스스로 쏘는 무기가 화면을 당기거나 멈추면 조작이 끊긴다)
        if (supportOnly)
        {
            float glow = m.flash * s.flashScale * s.supportOnlyFlash;
            if (glow > 0f) host.Flash(color, glow);
            SupportOnlyPlayed++;
            return;
        }

        if (m.zoom > 0f && s.zoomScale > 0f) host.Zoom(m.zoom * s.zoomScale);
        if (m.flash > 0f && s.flashScale > 0f) host.Flash(color, m.flash * s.flashScale);
        if (m.hitStop && s.hitStop) host.HitStop();

        // 큰 소리가 묻히지 않게 — 배경음 · 다른 효과음을 잠깐 낮춘다 (이 순간의 소리 자신은 같은 프레임이라 그대로 난다)
        if (m.duck && s.duckTime > 0f && SoundManager.Instance != null)
            SoundManager.Instance.DuckForImpact(s.duckTime, s.duckMusic, s.duckSfx);
    }

    /// <summary>흔들림 비율을 0 으로 둔 보조 전용 무기 — 번쩍만 (같은 순간이 겹치면 한 번).</summary>
    static void SupportFlash(ImpactMoment moment, ImpactFxSettings.Moment m, ImpactFxSettings s, Color color)
    {
        float glow = m.flash * s.flashScale * s.supportOnlyFlash;
        if (glow <= 0f) return;

        float now = Time.unscaledTime;
        if (lastAt.TryGetValue(moment, out float last) && now - last < s.repeatGap) return;
        lastAt[moment] = now;
        Played++;
        SupportOnlyPlayed++;
        ImpactHost.Get().Flash(color, glow);
    }

    /// <summary>덜덜 — 광선이 나가는 동안 이어서 내는 길고 작은 흔들림. 보조 무기는 약하게 (보조 전용 무기는 supportOnlyShake 비율). 설정이 없으면 false (부른 쪽이 예전 방식으로).</summary>
    public static bool Rumble(WeaponBase source, float amount)
    {
        if (Settings == null || amount <= 0f) return false;
        float scale = source != null ? source.ShakeScale : 1f;
        if (source != null && source.IsSupport && source.Data != null && source.Data.SupportOnly && Settings.supportOnlyShake > 0f)
            scale = Settings.supportOnlyShake;
        if (scale <= 0f) return true;
        return ImpactHost.Get().Rumble(amount * scale);
    }

    /// <summary>설정창의 화면 흔들림 세기가 바뀌었다 — 지금 씬의 카메라에 다시 건다.</summary>
    public static void ApplyShakeScale() => ImpactHost.ApplyListenerGain();
}

/// <summary>ImpactFx 를 실제로 굴리는 오브젝트 — 씬마다 처음 쓸 때 하나 생긴다.</summary>
public class ImpactHost : MonoBehaviour
{
    static ImpactHost instance;

    CinemachineImpulseSource thud, rumble, kick;

    // 시야 — 원래 값 × 넓히기 × (1 − 줌 − 당김). 처음 건드릴 때 카메라와 원래 값을 잡고, 다 돌아오면 놓는다
    CinemachineCamera lensCam;
    float lensBase;
    bool lensOrtho;
    float zoomAmount;      // 줌 펀치 — 지금 당겨진 비율 (0 = 원래대로)
    float zoomPeak, zoomAge;
    float wide = 1f, wideTarget = 1f, wideSpeed = 1f;   // 넓히기 (보스전 동안 — DragonPressure)
    float lean, leanTarget, leanUntil = -1f;            // 당김 (큰 공격 준비 — 천천히 당겼다가 놓는다)

    // 가장자리 번쩍
    Image vignette;
    Color flashColor;
    float flashAge, flashTime;

    // 히트 스톱
    bool stopping;
    float stopScale;
    float lastStopAt = -99f;

    // 카메라 충격 수신기의 원래 세기 (설정창 흔들림 슬라이더를 곱하기 전)
    static readonly Dictionary<CinemachineImpulseListener, float> baseGain = new Dictionary<CinemachineImpulseListener, float>();
    static bool hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        baseGain.Clear();
        if (hooked) SceneManager.sceneLoaded -= OnSceneLoaded;
        hooked = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        ApplyListenerGain();
        if (hooked) return;
        SceneManager.sceneLoaded += OnSceneLoaded;
        hooked = true;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyListenerGain();

    /// <summary>씬의 모든 충격 수신기 세기 = 원래 값 × 설정창 화면 흔들림 (0~1).</summary>
    public static void ApplyListenerGain()
    {
        float scale = GameSettings.Shake;
        foreach (CinemachineImpulseListener listener in FindObjectsByType<CinemachineImpulseListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!baseGain.TryGetValue(listener, out float gain))
            {
                gain = listener.Gain;
                baseGain[listener] = gain;
            }
            listener.Gain = gain * scale;
        }
    }

    public static ImpactHost Get()
    {
        if (instance != null) return instance;
        var go = new GameObject("ImpactFx");
        instance = go.AddComponent<ImpactHost>();
        return instance;
    }

    // ───────── 흔들림 3종 ─────────

    /// <summary>팀원 충격 소스와 같은 채널로, 모양 · 길이만 다른 소스 셋을 만든다 (그쪽이 없으면 흔들지 않는다).</summary>
    bool EnsureSources()
    {
        if (thud != null) return true;

        CameraShakeManager manager = CameraShakeManager.Instance;
        if (manager == null || manager.impulseSource == null) return false;

        ImpactFxSettings s = ImpactFx.Settings;
        int channel = manager.impulseSource.ImpulseDefinition.ImpulseChannel;
        thud = MakeSource("Thud", channel, CinemachineImpulseDefinition.ImpulseShapes.Explosion, s != null ? s.thudDuration : 0.16f);
        rumble = MakeSource("Rumble", channel, CinemachineImpulseDefinition.ImpulseShapes.Rumble, s != null ? s.rumbleDuration : 0.45f);
        kick = MakeSource("Kick", channel, CinemachineImpulseDefinition.ImpulseShapes.Recoil, s != null ? s.kickDuration : 0.22f);
        return true;
    }

    CinemachineImpulseSource MakeSource(string name, int channel, CinemachineImpulseDefinition.ImpulseShapes shape, float duration)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var source = go.AddComponent<CinemachineImpulseSource>();
        CinemachineImpulseDefinition def = source.ImpulseDefinition;
        def.ImpulseChannel = channel;
        def.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;   // 어디서 내도 같은 세기 (팀원 소스와 같다)
        def.ImpulseShape = shape;
        def.ImpulseDuration = duration;
        return source;
    }

    /// <summary>쿵 — 짧고 큰 흔들림 (폭발).</summary>
    public void Thud(float amount)
    {
        if (amount <= 0f || !EnsureSources()) return;
        thud.GenerateImpulseWithVelocity(Vector3.down * amount);
        ImpactFx.Thuds++;
        WeaponBase.CountShake(amount);
    }

    /// <summary>덜덜 — 길고 작은 흔들림 (광선 지속). 무작위 방향.</summary>
    public bool Rumble(float amount)
    {
        if (amount <= 0f) return true;
        if (!EnsureSources()) return false;
        Vector2 flat = Random.insideUnitCircle.normalized;
        rumble.GenerateImpulseWithVelocity(new Vector3(flat.x, Random.Range(-0.4f, 0.4f), flat.y) * amount);
        ImpactFx.Rumbles++;
        return true;
    }

    /// <summary>밀림 — 쏜 반대 방향으로 밀렸다 돌아온다 (큰 발사).</summary>
    public void Kick(Vector3 fireDirection, float amount)
    {
        if (amount <= 0f || !EnsureSources()) return;
        fireDirection.y = 0f;
        Vector3 back = fireDirection.sqrMagnitude > 0.0001f ? -fireDirection.normalized : Vector3.down;
        kick.GenerateImpulseWithVelocity(back * amount);
        ImpactFx.Kicks++;
        WeaponBase.CountShake(amount);
    }

    // ───────── 줌 펀치 ─────────

    /// <summary>시야를 amount 비율만큼 당겼다가 zoomReturn 초에 돌아온다. 이미 당겨져 있으면 더 큰 쪽으로.</summary>
    public void Zoom(float amount)
    {
        if (amount <= 0f || !HoldLens()) return;
        zoomPeak = Mathf.Max(zoomAmount, amount);
        zoomAmount = zoomPeak;
        zoomAge = 0f;
        ApplyLens();
        ImpactFx.Zooms++;
    }

    /// <summary>
    /// 시야를 scale 배로 넓힌다 (seconds 초에 걸쳐 — 1 이면 원래대로). 보스전 동안 큰 몸이 화면에 들어오게 (2026-11-20).
    /// 카메라를 아직 못 잡았으면 false — 다음 프레임에 다시 부른다.
    /// </summary>
    public bool Widen(float scale, float seconds)
    {
        scale = Mathf.Max(0.5f, scale);
        if (!HoldLens()) return Mathf.Approximately(scale, 1f);
        wideTarget = scale;
        wideSpeed = Mathf.Abs(scale - wide) / Mathf.Max(0.05f, seconds);
        return true;
    }

    /// <summary>당김 — amount 비율만큼 천천히 당기고 hold 초 뒤에 놓는다 (큰 공격을 준비하는 동안 — 2026-11-20).</summary>
    public void Lean(float amount, float hold)
    {
        if (amount <= 0f || !HoldLens()) return;
        leanTarget = amount;
        leanUntil = Time.unscaledTime + hold;
    }

    /// <summary>지금 카메라를 잡는다 (이미 잡았으면 그대로) — 건드리기 전의 시야 값을 기억한다.</summary>
    bool HoldLens()
    {
        if (lensCam != null) return true;
        CinemachineBrain brain = CinemachineBrain.GetActiveBrain(0);
        var cam = brain != null ? brain.ActiveVirtualCamera as CinemachineCamera : null;
        if (cam == null) return false;
        // 보스 등장 연출 카메라가 잡고 있을 때는 건드리지 않는다
        if (DragonEntrance.Current != null && !DragonEntrance.Current.Finished) return false;
        lensCam = cam;
        lensOrtho = cam.Lens.Orthographic;
        lensBase = lensOrtho ? cam.Lens.OrthographicSize : cam.Lens.FieldOfView;
        wide = 1f;
        lean = 0f;
        return true;
    }

    void ApplyLens()
    {
        if (lensCam == null) return;
        float value = lensBase * wide * Mathf.Max(0.5f, 1f - zoomAmount - lean);
        if (lensOrtho) lensCam.Lens.OrthographicSize = value;
        else lensCam.Lens.FieldOfView = value;
    }

    /// <summary>시야를 원래대로 돌려놓고 카메라를 놓는다.</summary>
    void ReleaseLens()
    {
        zoomAmount = lean = leanTarget = 0f;
        wide = wideTarget = 1f;
        leanUntil = -1f;
        ApplyLens();
        lensCam = null;
    }

    // ───────── 가장자리 번쩍 ─────────

    /// <summary>화면 가장자리가 color 로 잠깐 물든다 (세기 0~1).</summary>
    public void Flash(Color color, float strength)
    {
        strength *= GameSettings.FlashScale;   // 설정 '번쩍임 줄이기' (2026-11-26) — 큰 한 방 · 보스 가장자리 번쩍
        strength *= FxCrowd.Alpha;   // 적이 많으면 살짝 옅게 (2026-11-27, 남은 작업 7-4 — FxCrowd)
        if (strength <= 0f) return;
        if (vignette == null) vignette = MakeVignette();

        ImpactFxSettings s = ImpactFx.Settings;
        flashColor = new Color(color.r, color.g, color.b, Mathf.Clamp01(strength));
        flashTime = s != null ? s.flashDuration : 0.1f;
        flashAge = 0f;
        vignette.color = flashColor;
        vignette.enabled = true;
        ImpactFx.Flashes++;
    }

    Image MakeVignette()
    {
        var canvasGo = new GameObject("EdgeFlash", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -100;   // HUD · 창보다 아래 — 게임 화면만 물든다

        var imageGo = new GameObject("Vignette", typeof(RectTransform));
        imageGo.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)imageGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var image = imageGo.AddComponent<Image>();
        image.sprite = VignetteSprite();
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    /// <summary>가운데는 투명하고 가장자리로 갈수록 진해지는 그림 (부를 때마다 새로 만든다 — 쓰는 쪽이 한 번 만들어 둔다). 피격 연출(PlayerHurtFx)도 쓴다.</summary>
    internal static Sprite VignetteSprite()
    {
        const int Size = 128;
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = (x + 0.5f) / Size * 2f - 1f, v = (y + 0.5f) / Size * 2f - 1f;
                // 화면 모서리까지 고르게 물들게 — 원이 아니라 둥근 네모 거리
                float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(u), 3f) + Mathf.Pow(Mathf.Abs(v), 3f), 1f / 3f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1.05f, d));   // 화면 끝 쪽만 — 넓게 잡으면 화면이 통째로 물든다
                pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
    }

    // ───────── 히트 스톱 ─────────

    /// <summary>
    /// 아주 잠깐 게임 시간을 거의 멈춘다 (패링과 같은 방식). 게임이 이미 멈춰 있거나(카드 · 정비 · 설정창) 다른 히트 스톱 중이면 건드리지 않고,
    /// 그사이 다른 코드가 시간을 바꿨으면 되돌리지 않는다. 한 번 걸리면 hitStopCooldown 동안 다시 걸리지 않는다.
    /// </summary>
    public void HitStop()
    {
        ImpactFxSettings s = ImpactFx.Settings;
        if (s == null || stopping) return;
        if (Time.unscaledTime - lastStopAt < s.hitStopCooldown) return;
        if (!Mathf.Approximately(Time.timeScale, 1f)) return;

        lastStopAt = Time.unscaledTime;
        StartCoroutine(StopRoutine(s.hitStopScale, s.hitStopDuration));
        ImpactFx.HitStops++;
    }

    IEnumerator StopRoutine(float scale, float duration)
    {
        stopping = true;
        stopScale = scale;
        Time.timeScale = scale;
        yield return new WaitForSecondsRealtime(duration);
        RestoreTime();
    }

    void RestoreTime()
    {
        if (!stopping) return;
        stopping = false;
        if (Mathf.Approximately(Time.timeScale, stopScale)) Time.timeScale = 1f;
    }

    // ───────── 굴리기 ─────────

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        ImpactFxSettings s = ImpactFx.Settings;

        // 시야 — 줌 펀치 · 넓히기 · 당김을 한꺼번에 굴린다
        bool moved = false;
        if (zoomAmount > 0f)
        {
            // 처음에 빠르게, 끝에 천천히 돌아온다
            zoomAge += dt;
            float t = Mathf.Clamp01(zoomAge / Mathf.Max(0.02f, s != null ? s.zoomReturn : 0.12f));
            zoomAmount = t >= 1f ? 0f : zoomPeak * (1f - t) * (1f - t);
            moved = true;
        }
        if (!Mathf.Approximately(wide, wideTarget))
        {
            wide = Mathf.MoveTowards(wide, wideTarget, wideSpeed * dt);
            moved = true;
        }
        float leanGoal = Time.unscaledTime < leanUntil ? leanTarget : 0f;
        if (!Mathf.Approximately(lean, leanGoal))
        {
            lean = Mathf.MoveTowards(lean, leanGoal, (leanGoal > lean ? 0.12f : 0.3f) * dt);   // 천천히 당기고 빨리 놓는다
            moved = true;
        }
        if (lensCam == null)
        {
            // 카메라가 사라졌다 (씬이 바뀌었다) — 처음부터
            if (moved || wide != 1f) { zoomAmount = lean = leanTarget = 0f; wide = wideTarget = 1f; leanUntil = -1f; }
        }
        else if (moved)
        {
            ApplyLens();
            if (zoomAmount <= 0f && lean <= 0f && leanGoal <= 0f && Mathf.Approximately(wide, 1f) && Mathf.Approximately(wideTarget, 1f)) ReleaseLens();
        }

        if (vignette != null && vignette.enabled)
        {
            flashAge += dt;
            float t = Mathf.Clamp01(flashAge / Mathf.Max(0.02f, flashTime));
            Color c = flashColor;
            c.a *= (1f - t) * (1f - t);
            vignette.color = c;
            if (t >= 1f) vignette.enabled = false;
        }
    }

    // 씬이 바뀌어 사라질 때 — 줌 · 시간을 원래대로 돌려놓는다
    void OnDestroy()
    {
        if (lensCam != null) ReleaseLens();
        RestoreTime();
        if (instance == this) instance = null;
    }
}
