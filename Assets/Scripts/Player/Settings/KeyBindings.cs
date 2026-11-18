using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 추가 조작 키 — E · F · Z. WASD 에 왼손을 둔 채 닿는 키만 쓴다 (커스터마이징-구현계획.md 6장).
///
///  · E = 액티브 1 (검사: 서브 능력 1칸)
///  · F = 액티브 2 (검사: 서브 능력 2칸)
///  · Z = 장비 패널 상세 보기 켜기 / 끄기
///
/// 값은 에셋(Resources/Settings/KeyBindings)에서 바꾼다. 에셋이 없으면 기본값으로 동작한다.
/// 입력 파일(PlayerInput.inputactions)은 고치지 않고 Keyboard.current 로 직접 읽는다 — 장전 R · 교체 Q 와 같은 방식.
/// </summary>
[CreateAssetMenu(menuName = "Settings/Key Bindings", fileName = "KeyBindings")]
public class KeyBindings : ScriptableObject
{
    public const string ResourcePath = "Settings/KeyBindings";

    [Tooltip("액티브 1 — 검사: 서브 능력 1칸")]
    public Key ability1 = Key.E;

    [Tooltip("액티브 2 — 검사: 서브 능력 2칸")]
    public Key ability2 = Key.F;

    [Tooltip("장비 패널 상세 보기 켜기 / 끄기")]
    public Key equipmentDetail = Key.Z;

    static KeyBindings current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => current = null;

    public static KeyBindings Current
    {
        get
        {
            if (current != null) return current;

            current = Resources.Load<KeyBindings>(ResourcePath);
            if (current == null) current = CreateInstance<KeyBindings>();
            return current;
        }
    }

    /// <summary>서브 능력 칸 번호(0 · 1)의 키.</summary>
    public Key AbilityKey(int index) => index == 0 ? ability1 : ability2;

    /// <summary>HUD 에 붙일 글자 — "E", "F", "1".</summary>
    public static string Label(Key key)
    {
        if (key == Key.None) return string.Empty;

        string name = key.ToString();
        return name.StartsWith("Digit") ? name.Substring(5) : name;
    }

    public static bool Pressed(Key key) => Valid(key) && Keyboard.current[key].wasPressedThisFrame;

    public static bool Held(Key key) => Valid(key) && Keyboard.current[key].isPressed;

    public static bool Released(Key key) => Valid(key) && Keyboard.current[key].wasReleasedThisFrame;

    static bool Valid(Key key) => key != Key.None && Keyboard.current != null;
}
