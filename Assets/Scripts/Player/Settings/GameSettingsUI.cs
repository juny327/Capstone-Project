using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 설정창 — 왼쪽: 전체 · 배경음 · 효과음 음량 · 화면 흔들림, 오른쪽: 카메라 거리 · 번쩍임 줄이기 · 피해 숫자 · 피격 화면 효과 · 그래픽 품질 · FPS 표시 (2026-11-26 두 칸으로).
///
///  · 로비: 오른쪽 위 "설정" 버튼 · ESC
///  · 게임 중(스테이지 · 테스트 룸): ESC. 여는 동안 게임을 멈추고 닫으면 다시 흐른다 — 제목이 "일시정지" 로 바뀐다
///    — 이미 멈춰 있으면(레벨업 카드 창 등) 시간은 건드리지 않는다
///  · 게임 중에는 왼쪽 아래 "로비로" 버튼 (2026-11-02). 스테이지는 이번 판 진행이 사라지므로 한 번 더 눌러 확인한다
///
/// 씬 파일에 넣지 않는다. 게임이 시작될 때 Resources/UI/GameSettings 프리팹을 하나 만들어 씬을 넘어 살려 둔다 —
/// 팀원들이 고치는 씬과 병합 충돌이 나지 않게 하기 위해서다. 프리팹은 Tools / Settings UI 도구가 만든다.
/// </summary>
public class GameSettingsUI : MonoBehaviour
{
    const string ResourcePath = "UI/GameSettings";
    const string LobbyScene = "Loby";

    [SerializeField] private GameObject window;        // 어두운 배경 + 창
    [SerializeField] private Button lobbyButton;       // 로비에서만 보이는 "설정" 버튼

    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Text masterValue;
    [SerializeField] private TMP_Text musicValue;
    [SerializeField] private TMP_Text sfxValue;

    // 화면 흔들림 세기 (2026-11-14) — 예전 프리팹에는 없을 수 있다 (없으면 그 줄만 빠진다)
    [SerializeField] private Slider shakeSlider;
    [SerializeField] private TMP_Text shakeValue;

    // 피격 화면 효과 켜기 / 끄기 (2026-11-17) — 예전 프리팹에는 없을 수 있다 (없으면 그 줄만 빠진다)
    [SerializeField] private Button hurtFxButton;
    [SerializeField] private TMP_Text hurtFxValue;

    // 그래픽 품질(높음 → 중간 → 낮음) · FPS 표시 (2026-11-18 성능 3단계) — 예전 프리팹에는 없을 수 있다 (없으면 그 줄만 빠진다)
    [SerializeField] private Button graphicsButton;
    [SerializeField] private TMP_Text graphicsValue;
    [SerializeField] private Button fpsButton;
    [SerializeField] private TMP_Text fpsValue;

    // 접근성 (2026-11-26) — 카메라 거리(가까이 → 보통 → 멀리) · 번쩍임 줄이기 · 피해 숫자. 예전 프리팹에는 없을 수 있다 (없으면 그 줄만 빠진다)
    [SerializeField] private Button cameraButton;
    [SerializeField] private TMP_Text cameraValue;
    [SerializeField] private Button flashButton;
    [SerializeField] private TMP_Text flashValue;
    [SerializeField] private Button damageNumbersButton;
    [SerializeField] private TMP_Text damageNumbersValue;

    [SerializeField] private Button closeButton;
    [SerializeField] private Button resetButton;

    [SerializeField] private TMP_Text title;           // 로비 "설정" · 게임 중 "일시정지"
    [SerializeField] private Button toLobbyButton;     // 게임 중에만 — 로비로 돌아가기
    [SerializeField] private TMP_Text toLobbyLabel;
    [SerializeField] private TMP_Text toLobbyWarning;  // 스테이지에서 첫 번째로 눌렀을 때 확인 문구

    [Tooltip("스테이지에서 \"로비로\" 를 한 번 누른 뒤 이 시간(실제 초) 안에 다시 눌러야 나간다")]
    [Min(1f)] [SerializeField] private float confirmWindow = 4f;

    static GameSettingsUI instance;

    private bool pausedByMe;
    private bool syncing;
    private float confirmUntil = -1f;

    /// <summary>창이 열려 있는지.</summary>
    public bool IsOpen => window != null && window.activeSelf;

    /// <summary>씬을 넘어 살아 있는 설정 창. 아직 없으면 null.</summary>
    public static GameSettingsUI Instance => instance;

    /// <summary>로비의 "설정" 버튼 (로비 선택 화면이 테스트 룸 버튼을 그 옆에 맞춘다).</summary>
    public RectTransform LobbyButtonRect => lobbyButton != null ? (RectTransform)lobbyButton.transform : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        if (instance != null) return;

        var prefab = Resources.Load<GameSettingsUI>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[GameSettingsUI] Resources/{ResourcePath} 프리팹이 없습니다 — Tools / Settings UI 를 실행하세요.");
            return;
        }

        instance = Instantiate(prefab);
        instance.name = "GameSettings";
        DontDestroyOnLoad(instance.gameObject);
    }

    void Awake()
    {
        masterSlider.onValueChanged.AddListener(v => { if (!syncing) GameSettings.SetMaster(v); ShowValues(); });
        musicSlider.onValueChanged.AddListener(v => { if (!syncing) GameSettings.SetMusic(v); ShowValues(); });
        sfxSlider.onValueChanged.AddListener(v => { if (!syncing) GameSettings.SetSfx(v); ShowValues(); });
        if (shakeSlider != null) shakeSlider.onValueChanged.AddListener(v => { if (!syncing) GameSettings.SetShake(v); ShowValues(); });
        if (hurtFxButton != null) hurtFxButton.onClick.AddListener(() => { GameSettings.SetHurtFx(!GameSettings.HurtFx); ShowValues(); });
        if (graphicsButton != null) graphicsButton.onClick.AddListener(() => { GameSettings.SetGraphics((GraphicsLevel)(((int)GameSettings.Graphics + 1) % 3)); ShowValues(); });
        if (fpsButton != null) fpsButton.onClick.AddListener(() => { GameSettings.SetShowFps(!GameSettings.ShowFps); ShowValues(); });
        if (cameraButton != null) cameraButton.onClick.AddListener(() => { GameSettings.SetCameraDistance((GameSettings.CameraDistance + 1) % CameraDistance.Count); ShowValues(); });
        if (flashButton != null) flashButton.onClick.AddListener(() => { GameSettings.SetReduceFlash(!GameSettings.ReduceFlash); ShowValues(); });
        if (damageNumbersButton != null) damageNumbersButton.onClick.AddListener(() => { GameSettings.SetDamageNumbers(!GameSettings.DamageNumbers); ShowValues(); });

        closeButton.onClick.AddListener(Close);
        resetButton.onClick.AddListener(() => { GameSettings.ResetToDefaults(); SyncSliders(); });
        lobbyButton.onClick.AddListener(Open);
        if (toLobbyButton != null) toLobbyButton.onClick.AddListener(OnToLobby);

        // UI 소리 (2026-11-12) — 닫기는 낮은 '톡', 나머지 버튼은 '딸깍', 슬라이더는 밀 때 값만큼 높아지는 틱
        if (!closeButton.TryGetComponent(out UISoundRole closeRole)) closeRole = closeButton.gameObject.AddComponent<UISoundRole>();
        closeRole.kind = UISoundKind.Back;
        if (!TryGetComponent(out UISoundBinder _)) gameObject.AddComponent<UISoundBinder>();

        window.SetActive(false);
        UpdateLobbyButton(SceneManager.GetActiveScene());
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬이 바뀌면 창을 닫는다. 시간은 새 씬이 알아서 맞추므로(GameAppManager 등) 되돌리지 않는다
        window.SetActive(false);
        pausedByMe = false;
        UpdateLobbyButton(scene);
    }

    void UpdateLobbyButton(Scene scene)
    {
        lobbyButton.gameObject.SetActive(scene.name == LobbyScene);
    }

    void Update()
    {
        // 확인 시간이 지나면 버튼을 처음 모습으로 (창이 멈춘 시간에도 흐르도록 실제 시간)
        if (confirmUntil > 0f && Time.unscaledTime > confirmUntil) ResetLobbyConfirm();

        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (GuideUI.HandlesEscape) return;   // 조작 가이드 창이 이 ESC 로 닫힌다 (2026-11-16)
        if (TutorialUI.HandlesEscape) return;   // 튜토리얼 창도 같다 (2026-11-27)

        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (IsOpen) return;

        SyncSliders();
        window.SetActive(true);

        // 로비가 아니면 게임을 멈춘다. 이미 멈춰 있으면 그대로 둔다
        bool inGame = SceneManager.GetActiveScene().name != LobbyScene;
        if (title != null) title.text = inGame ? "일시정지" : "설정";
        if (toLobbyButton != null) toLobbyButton.gameObject.SetActive(inGame);
        ResetLobbyConfirm();
        if (inGame && Time.timeScale > 0f)
        {
            Time.timeScale = 0f;
            pausedByMe = true;
        }
    }

    public void Close()
    {
        if (!IsOpen) return;

        window.SetActive(false);
        GameSettings.Save();
        ResetLobbyConfirm();

        // 내가 멈췄고, 그사이 다른 코드가 시간을 바꾸지 않았을 때만 되돌린다
        if (pausedByMe && Time.timeScale == 0f)
            Time.timeScale = 1f;

        pausedByMe = false;
    }

    /// <summary>
    /// "로비로" — 테스트 룸은 바로, 스테이지는 한 번 더 눌러야 나간다 (이번 판 진행 · 무기 · 재화가 사라진다).
    /// 나가는 길은 각 장면 담당 코드를 그대로 쓴다: 테스트 룸 TestRoomManager.ReturnToLobby (연습 규칙 · 소리 정리),
    /// 스테이지 GameAppManager.ReturnToLobby (플레이어 정리 · 시간 되돌림 — 게임 오버 · 클리어 창과 같은 길).
    /// </summary>
    void OnToLobby()
    {
        bool testRoom = TestRoomManager.Instance != null;
        if (!testRoom && confirmUntil < 0f)
        {
            confirmUntil = Time.unscaledTime + confirmWindow;
            if (toLobbyLabel != null) toLobbyLabel.text = "정말 나가기";
            if (toLobbyWarning != null) toLobbyWarning.gameObject.SetActive(true);
            return;
        }

        Close();
        if (testRoom) TestRoomManager.Instance.ReturnToLobby();
        else if (GameAppManager.Instance != null) GameAppManager.Instance.ReturnToLobby();
        else
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(LobbyScene);
        }
    }

    void ResetLobbyConfirm()
    {
        confirmUntil = -1f;
        if (toLobbyLabel != null) toLobbyLabel.text = "로비로";
        if (toLobbyWarning != null) toLobbyWarning.gameObject.SetActive(false);
    }

    void SyncSliders()
    {
        syncing = true;
        masterSlider.value = GameSettings.Master;
        musicSlider.value = GameSettings.Music;
        sfxSlider.value = GameSettings.Sfx;
        if (shakeSlider != null) shakeSlider.value = GameSettings.Shake;
        syncing = false;
        ShowValues();
    }

    void ShowValues()
    {
        masterValue.text = Percent(masterSlider.value);
        musicValue.text = Percent(musicSlider.value);
        sfxValue.text = Percent(sfxSlider.value);
        if (shakeSlider != null && shakeValue != null) shakeValue.text = Percent(shakeSlider.value);
        if (hurtFxValue != null) hurtFxValue.text = GameSettings.HurtFx ? "켜짐" : "꺼짐";
        if (graphicsValue != null) graphicsValue.text = GraphicsQuality.Name(GameSettings.Graphics);
        if (fpsValue != null) fpsValue.text = GameSettings.ShowFps ? "켜짐" : "꺼짐";
        if (cameraValue != null) cameraValue.text = CameraDistance.Name(GameSettings.CameraDistance);
        if (flashValue != null) flashValue.text = GameSettings.ReduceFlash ? "켜짐" : "꺼짐";
        if (damageNumbersValue != null) damageNumbersValue.text = GameSettings.DamageNumbers ? "켜짐" : "꺼짐";
    }

    static string Percent(float v) => $"{Mathf.RoundToInt(v * 100f)}%";
}
