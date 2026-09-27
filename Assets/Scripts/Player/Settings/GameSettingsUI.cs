using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 설정창 — 전체 · 배경음 · 효과음 음량.
///
///  · 로비: 오른쪽 위 "설정" 버튼 · ESC
///  · 게임 중(스테이지 · 테스트 룸): ESC. 여는 동안 게임을 멈추고 닫으면 다시 흐른다
///    — 이미 멈춰 있으면(레벨업 카드 창 등) 시간은 건드리지 않는다
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

    [SerializeField] private Button closeButton;
    [SerializeField] private Button resetButton;

    static GameSettingsUI instance;

    private bool pausedByMe;
    private bool syncing;

    /// <summary>창이 열려 있는지.</summary>
    public bool IsOpen => window != null && window.activeSelf;

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

        closeButton.onClick.AddListener(Close);
        resetButton.onClick.AddListener(() => { GameSettings.ResetToDefaults(); SyncSliders(); });
        lobbyButton.onClick.AddListener(Open);

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
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;

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

        // 내가 멈췄고, 그사이 다른 코드가 시간을 바꾸지 않았을 때만 되돌린다
        if (pausedByMe && Time.timeScale == 0f)
            Time.timeScale = 1f;

        pausedByMe = false;
    }

    void SyncSliders()
    {
        syncing = true;
        masterSlider.value = GameSettings.Master;
        musicSlider.value = GameSettings.Music;
        sfxSlider.value = GameSettings.Sfx;
        syncing = false;
        ShowValues();
    }

    void ShowValues()
    {
        masterValue.text = Percent(masterSlider.value);
        musicValue.text = Percent(musicSlider.value);
        sfxValue.text = Percent(sfxSlider.value);
    }

    static string Percent(float v) => $"{Mathf.RoundToInt(v * 100f)}%";
}
