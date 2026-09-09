using UnityEngine;
using UnityEngine.SceneManagement;

public class GameAppManager : MonoBehaviour
{
    public static GameAppManager Instance;

    public GameObject playerPrefab;

    public GameObject Player { get; private set; }
    public PlayerStats PlayerStats { get; private set; }

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

        //PlayerStats.ResetState();

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