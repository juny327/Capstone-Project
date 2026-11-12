using UnityEngine;

/// <summary>UI 소리 종류 — 버튼 하나가 어느 소리를 낼지.</summary>
public enum UISoundKind { None, Hover, Click, Back, CharacterSwitch, Start }

/// <summary>
/// 로비 · 설정창 UI 소리 한 벌 (2026-11-12 — 소리 후보 페이지에서 사용자가 고름).
///
/// 씬 · 프리팹의 버튼을 하나하나 고치지 않는다 — <see cref="UISoundBinder"/> 가 실행할 때 붙인다.
/// 에셋은 Resources/UI/UISoundSet (씬을 넘어 쓰여서 Resources 에서 읽는다).
/// </summary>
[CreateAssetMenu(menuName = "Audio/UI Sound Set", order = 3)]
public class UISoundSet : ScriptableObject
{
    public const string ResourcePath = "UI/UISoundSet";

    [Tooltip("마우스를 버튼에 올렸을 때 — 자주 나므로 작고 짧게 · 최소 간격")]
    public GameSoundSet.Entry hover = new GameSoundSet.Entry();

    [Tooltip("버튼 누름")]
    public GameSoundSet.Entry click = new GameSoundSet.Entry();

    [Tooltip("닫기 · 뒤로 — 누름보다 낮게")]
    public GameSoundSet.Entry back = new GameSoundSet.Entry();

    [Tooltip("로비에서 캐릭터가 바뀔 때 (사수 ↔ 검사) — 같은 캐릭터 안에서 무기만 바꾸면 '누름'")]
    public GameSoundSet.Entry characterSwitch = new GameSoundSet.Entry();

    [Tooltip("게임 시작")]
    public GameSoundSet.Entry start = new GameSoundSet.Entry();

    [Tooltip("설정 슬라이더를 밀 때 — 값이 클수록 높아진다 (sliderPitch)")]
    public GameSoundSet.Entry slider = new GameSoundSet.Entry();

    [Tooltip("슬라이더 값 0 → 100% 일 때 피치 배율")]
    public Vector2 sliderPitch = new Vector2(0.8f, 1.25f);

    public GameSoundSet.Entry Get(UISoundKind kind) => kind switch
    {
        UISoundKind.Hover => hover,
        UISoundKind.Click => click,
        UISoundKind.Back => back,
        UISoundKind.CharacterSwitch => characterSwitch,
        UISoundKind.Start => start,
        _ => null,
    };
}

/// <summary>UI 소리 내기 — SoundManager 의 UI 그룹으로 (설정창 효과음 음량을 따른다).</summary>
public static class UISound
{
    static UISoundSet set;
    static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        set = null;
        loaded = false;
        Played = 0;
        LastKind = UISoundKind.None;
    }

    public static UISoundSet Set
    {
        get
        {
            if (!loaded)
            {
                set = Resources.Load<UISoundSet>(UISoundSet.ResourcePath);
                loaded = true;
            }
            return set;
        }
    }

    /// <summary>낸 횟수 · 마지막 종류 (테스트).</summary>
    public static int Played { get; private set; }
    public static UISoundKind LastKind { get; private set; }
    public static float LastSliderPitch { get; private set; }

    public static void Play(UISoundKind kind)
    {
        UISoundSet s = Set;
        if (s == null || kind == UISoundKind.None) return;
        if (Emit(s.Get(kind), 1f)) LastKind = kind;
    }

    /// <summary>슬라이더 틱 — normalized 0 ~ 1 에 따라 높아진다.</summary>
    public static void PlaySlider(float normalized)
    {
        UISoundSet s = Set;
        if (s == null) return;
        float pitch = Mathf.Lerp(s.sliderPitch.x, s.sliderPitch.y, Mathf.Clamp01(normalized));
        if (Emit(s.slider, pitch)) LastSliderPitch = pitch;
    }

    static bool Emit(GameSoundSet.Entry entry, float pitch)
    {
        SoundManager sm = SoundManager.Instance;
        if (sm == null || entry == null || !entry.HasClip) return false;
        sm.Play(entry, sm.UiGroup, 1f, pitch);
        Played++;
        return true;
    }
}
