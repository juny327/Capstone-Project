using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 설정 값(지금은 음량 세 가지)을 저장하고 적용한다. 설정창(GameSettingsUI)이 여기를 부른다.
///
///  · 전체   → AudioListener.volume — 모든 소리 (SoundManager 를 거치지 않는 카드 · 몬스터 예고음 포함)
///  · 배경음 → SoundManager.SetMusicVolume
///  · 효과음 → SoundManager.SetSfxVolume — SoundManager 가 내는 모든 효과음 · UI 소리
///
/// PlayerPrefs 에 저장해 다음 실행에도 유지된다. SoundManager 는 씬을 넘어 살아남지만 첫 씬에서 늦게 생길 수 있어,
/// 씬이 바뀔 때마다 한 번 더 적용한다.
/// </summary>
public static class GameSettings
{
    const string MasterKey = "Settings.MasterVolume";
    const string MusicKey = "Settings.MusicVolume";
    const string SfxKey = "Settings.SfxVolume";
    const string ShakeKey = "Settings.ScreenShake";
    const string HurtFxKey = "Settings.HurtFx";
    const string GraphicsKey = "Settings.Graphics";
    const string ShowFpsKey = "Settings.ShowFps";

    public const float DefaultMaster = 1f;
    public const float DefaultMusic = 1f;
    public const float DefaultSfx = 1f;
    public const float DefaultShake = 1f;
    public const bool DefaultHurtFx = true;
    public const GraphicsLevel DefaultGraphics = GraphicsLevel.High;
    public const bool DefaultShowFps = false;

    public static float Master { get; private set; } = DefaultMaster;
    public static float Music { get; private set; } = DefaultMusic;
    public static float Sfx { get; private set; } = DefaultSfx;

    /// <summary>화면 흔들림 세기 0~1 (2026-11-14) — 카메라의 충격 수신기 세기에 곱한다 (ImpactFx). 어지러움을 타는 사람을 위한 값.</summary>
    public static float Shake { get; private set; } = DefaultShake;

    /// <summary>
    /// 피격 화면 효과 (2026-11-17, 피격 연출 개선안) — 맞을 때 화면 가장자리가 붉어지고 캐릭터 · 체력바가 깜빡이는 효과.
    /// 눈이 피로한 사람을 위해 끌 수 있다. 흔들림은 위 Shake 를 따른다.
    /// </summary>
    public static bool HurtFx { get; private set; } = DefaultHurtFx;

    /// <summary>
    /// 그래픽 품질 (2026-11-18, 성능 3단계) — 높음(프로젝트 설정 그대로) · 중간(그림자 줄임) · 낮음(저사양).
    /// 무엇이 달라지는지는 GraphicsQuality 에 있다.
    /// </summary>
    public static GraphicsLevel Graphics { get; private set; } = DefaultGraphics;

    /// <summary>화면 위 FPS 표시 (2026-11-18) — F3 키로도 켜고 끈다 (PerfHud).</summary>
    public static bool ShowFps { get; private set; } = DefaultShowFps;

    static bool hooked;

    // 도메인 리로드를 끈 에디터에서도 이벤트가 두 번 걸리지 않게 처음 상태로 돌린다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        if (hooked) SceneManager.sceneLoaded -= OnSceneLoaded;
        hooked = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Initialize()
    {
        Load();
        Apply();
        GraphicsQuality.Set(Graphics);
        PerfHud.Show(ShowFps);

        if (hooked) return;
        SceneManager.sceneLoaded += OnSceneLoaded;
        hooked = true;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

    public static void Load()
    {
        Master = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, DefaultMaster));
        Music = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, DefaultMusic));
        Sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, DefaultSfx));
        Shake = Mathf.Clamp01(PlayerPrefs.GetFloat(ShakeKey, DefaultShake));
        HurtFx = PlayerPrefs.GetInt(HurtFxKey, DefaultHurtFx ? 1 : 0) != 0;
        Graphics = (GraphicsLevel)Mathf.Clamp(PlayerPrefs.GetInt(GraphicsKey, (int)DefaultGraphics), 0, 2);
        ShowFps = PlayerPrefs.GetInt(ShowFpsKey, DefaultShowFps ? 1 : 0) != 0;
    }

    /// <summary>지금 값을 소리에 적용한다.</summary>
    public static void Apply()
    {
        AudioListener.volume = Master;

        SoundManager sound = SoundManager.Instance;
        if (sound == null) return;

        sound.SetMusicVolume(Music);
        sound.SetSfxVolume(Sfx);
    }

    public static void SetMaster(float value) { Master = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MasterKey, Master); Apply(); }
    public static void SetMusic(float value) { Music = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MusicKey, Music); Apply(); }
    public static void SetSfx(float value) { Sfx = Mathf.Clamp01(value); PlayerPrefs.SetFloat(SfxKey, Sfx); Apply(); }
    public static void SetShake(float value) { Shake = Mathf.Clamp01(value); PlayerPrefs.SetFloat(ShakeKey, Shake); ImpactFx.ApplyShakeScale(); }
    public static void SetHurtFx(bool on) { HurtFx = on; PlayerPrefs.SetInt(HurtFxKey, on ? 1 : 0); }
    public static void SetGraphics(GraphicsLevel level) { Graphics = (GraphicsLevel)Mathf.Clamp((int)level, 0, 2); PlayerPrefs.SetInt(GraphicsKey, (int)Graphics); GraphicsQuality.Set(Graphics); }
    public static void SetShowFps(bool on) { ShowFps = on; PlayerPrefs.SetInt(ShowFpsKey, on ? 1 : 0); PerfHud.Show(on); }

    public static void ResetToDefaults()
    {
        SetMaster(DefaultMaster);
        SetMusic(DefaultMusic);
        SetSfx(DefaultSfx);
        SetShake(DefaultShake);
        SetHurtFx(DefaultHurtFx);
        SetGraphics(DefaultGraphics);
        SetShowFps(DefaultShowFps);
    }

    /// <summary>디스크에 바로 쓴다. 슬라이더를 움직일 때마다 쓰지 않고 창을 닫을 때 한 번 부른다.</summary>
    public static void Save() => PlayerPrefs.Save();
}
