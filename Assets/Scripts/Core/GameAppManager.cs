using UnityEngine;
using UnityEngine.SceneManagement;

public class GameAppManager : MonoBehaviour
{
    public static GameAppManager Instance;

    public GameObject playerPrefab;

    public GameObject Player { get; private set; }
    public PlayerStats PlayerStats { get; private set; }

    /// <summary>
    /// 로비에서 고른 주 무기. 씬을 넘어 플레이어 생성 시점까지 살아 있어야 하므로 여기에 둔다.
    /// 비어 있으면 WeaponController 가 프리팹의 starterWeapon 으로 떨어진다
    /// (에디터에서 Stage 씬을 직접 Play 하는 경우).
    /// </summary>
    public WeaponData SelectedWeapon { get; private set; }

    public void SelectWeapon(WeaponData data)
    {
        SelectedWeapon = data;
    }

    Vector3 spawnPosition = new Vector3(0, 1, 0);

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 씬 로드 시 발동됨.
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnEnable()
    {
        GameEvents.OnPlayerDead += OnPlayerDead;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerDead -= OnPlayerDead;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name.StartsWith("Stage"))
        {
            SetupPlayer();

            if (Player != null)
                Player.SetActive(true);

            GameEvents.OnCameraReady?.Invoke(Camera.main);
        }

        // 인트로 앤딩에선 플레이어 비활성화
        else if (scene.name == "IntroScene" || scene.name == "EndingScene")
        {
            if (Player != null)
                Player.SetActive(false);
        }
    }

    void SetupPlayer()
    {
        // 플레이어가 없다면 생성
        if (Player == null)
        {
            Player = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            PlayerStats = Player.GetComponent<PlayerStats>();

            DontDestroyOnLoad(Player);
        }

        // 죽은 채로 다음 스테이지에 들어가지 않는다. GameManager 가 사망 뒤 클리어를 막으므로 정상이면 일어나지 않는다.
        // ResetState() 로 되살리지 않는 이유: 체력은 0 그대로라 "HP 0 으로 살아 있는" 상태가 된다
        if (PlayerStats != null && PlayerStats.IsDead)
        {
            Debug.LogError("[GameAppManager] 죽은 플레이어로 다음 스테이지에 들어가려 했습니다 — 로비로 돌아갑니다.");
            ReturnToLobby();
            return;
        }

        // 지난 스테이지를 클리어할 때 GameManager 가 건 무적을 푼다
        if (PlayerStats != null)
            PlayerStats.ClearInvulnerable();

        // 씬 넘어갈 때 위치 리셋
        Player.transform.position = spawnPosition;


        // 테스트용 코드 꼭 지울것
        GameEvents.Player = Player.transform;

        // 각 씬 시스템들이 다시 Player 참조하도록 이벤트 발생
        GameEvents.OnPlayerSpawned?.Invoke(Player.transform);
    }

    void OnPlayerDead()
    {
        Destroy(Player);

        Player = null;
        PlayerStats = null;
    }
    public void ReturnToLobby()
    {
        Time.timeScale = 1f;
        if (Player != null)
        {
            Destroy(Player);
            Player = null;
            PlayerStats = null;
        }

        SceneManager.LoadScene("Loby");
    }

    public void StartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Stage1");
    }
}