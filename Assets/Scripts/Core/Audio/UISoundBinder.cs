using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 아래에 있는 버튼 · 토글 · 슬라이더에 UI 소리(<see cref="UISound"/>)를 붙인다 (2026-11-12).
///
///   · 버튼: 마우스 올림 '틱' + 누름 '딸깍' (닫기 · 뒤로는 낮은 '톡', 게임 시작은 상승 + '쿵')
///   · 슬라이더: 마우스로 밀 때만 값만큼 높아지는 틱 (코드가 값을 맞출 때는 조용히)
///   · 로비 무기 · 캐릭터 버튼은 누름 소리를 붙이지 않는다 — 로비 선택 화면(LobbyScreen)이 캐릭터가 바뀌었는지 보고 낸다
///
/// 씬 · 프리팹을 고치지 않는다 — 로비 씬은 로드될 때 Canvas 가 있는 최상위 오브젝트에 이 컴포넌트를 붙이고,
/// 설정창(GameSettingsUI)은 스스로 붙인다. 팀원들이 고치는 씬과 병합 충돌이 나지 않게 하기 위해서다.
/// 소리를 바꾸고 싶은 버튼에는 <see cref="UISoundRole"/> 을 단다.
/// </summary>
public class UISoundBinder : MonoBehaviour
{
    /// <summary>로드될 때 자동으로 붙이는 씬.</summary>
    static readonly string[] AutoScenes = { "Loby" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindScene(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => BindScene(scene);

    /// <summary>그 씬의 UI 에 소리를 붙인다 (자동 대상 씬만).</summary>
    public static void BindScene(Scene scene)
    {
        if (!scene.IsValid() || System.Array.IndexOf(AutoScenes, scene.name) < 0) return;

        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<Canvas>(true) != null && !root.TryGetComponent(out UISoundBinder _))
                root.AddComponent<UISoundBinder>();
    }

    readonly HashSet<Selectable> bound = new HashSet<Selectable>();

    /// <summary>붙인 수 (테스트).</summary>
    public int BoundCount => bound.Count;

    void Awake() => BindAll();

    /// <summary>아래의 Selectable 을 다시 훑는다 (나중에 생긴 버튼이 있으면 불러 준다).</summary>
    public void BindAll()
    {
        foreach (Selectable s in GetComponentsInChildren<Selectable>(true))
            Bind(s);
    }

    void Bind(Selectable s)
    {
        if (s == null || !bound.Add(s)) return;

        if (!s.TryGetComponent(out UISoundHover hover)) hover = s.gameObject.AddComponent<UISoundHover>();
        hover.target = s;
        hover.hoverSound = s is Button || s is Toggle;   // 슬라이더 · 스크롤은 올릴 때 조용히 (슬라이더는 밀 때만)

        switch (s)
        {
            case Button button:
            {
                UISoundKind kind = RoleOf(button);
                if (kind != UISoundKind.None) button.onClick.AddListener(() => UISound.Play(kind));
                break;
            }
            case Toggle toggle:
                toggle.onValueChanged.AddListener(_ => UISound.Play(UISoundKind.Click));
                break;
            case Slider slider:
                slider.onValueChanged.AddListener(_ => { if (hover.Pressed) UISound.PlaySlider(slider.normalizedValue); });
                break;
        }
    }

    /// <summary>버튼이 낼 소리 — UISoundRole 이 있으면 그것, 없으면 연결된 메서드 · 이름으로 고른다.</summary>
    public static UISoundKind RoleOf(Button button)
    {
        if (button.TryGetComponent(out UISoundRole role)) return role.kind;

        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            string method = button.onClick.GetPersistentMethodName(i);
            if (method == "StartGame") return UISoundKind.Start;
            if (method == "SelectWeapon" || method == "SelectCharacter") return UISoundKind.None;   // LobbyScreen 이 낸다
            if (method == "Close" || method == "Back") return UISoundKind.Back;
        }

        string name = button.name;
        string lower = name.ToLowerInvariant();
        if (lower.Contains("close") || lower.Contains("back") || name.Contains("닫기") || name.Contains("뒤로"))
            return UISoundKind.Back;

        return UISoundKind.Click;
    }
}
