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

    public const float DefaultMaster = 1f;
    public const float DefaultMusic = 1f;
    public const float DefaultSfx = 1f;

    public static float Master { get; private set; } = DefaultMaster;
    public static float Music { get; private set; } = DefaultMusic;
    public static float Sfx { get; private set; } = DefaultSfx;

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

    public static void ResetToDefaults()
    {
        SetMaster(DefaultMaster);
        SetMusic(DefaultMusic);
        SetSfx(DefaultSfx);
    }

    /// <summary>디스크에 바로 쓴다. 슬라이더를 움직일 때마다 쓰지 않고 창을 닫을 때 한 번 부른다.</summary>
    public static void Save() => PlayerPrefs.Save();
}
