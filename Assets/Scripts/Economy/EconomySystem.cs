using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 재화 시스템 — 몬스터 처치 → 코인 떨어뜨리기 · 줍기 · 스테이지 클리어 보너스.
///
/// 게임이 시작될 때 스스로 하나 생겨 씬을 넘어 살아 있다. 씬 · 프리팹을 고치지 않는다.
/// 수치는 EconomySettings 에셋, 코인 모양은 코인 프리팹에서 바꾼다.
///
///  · 몬스터 처치 (GameEvents.OnEnemyDefeated) → 종류별 코인을 그 자리에 흩뿌린다
///  · 플레이어가 가까이 가면 코인이 날아와 지갑(Wallet)에 들어간다
///  · 스테이지 클리어 → 클리어 보너스, 남은 코인은 모두 날아온다
///  · 정비(상점)를 열기 전에 CollectAllNow 로 남은 코인을 바로 넣는다 — 상점에서 쓸 수 있게
/// </summary>
public class EconomySystem : MonoBehaviour
{
    public static EconomySystem Instance { get; private set; }

    public EconomySettings Settings { get; private set; }

    /// <summary>지금 플레이어의 지갑. 플레이어가 없으면 null.</summary>
    public Wallet Wallet { get; private set; }

    Transform player;
    PlayerStats playerStats;

    readonly List<CoinPickup> coins = new List<CoinPickup>();
    readonly List<CoinPickup> buffer = new List<CoinPickup>();

    // 클리어 보너스를 이미 준 씬 — StageManager 는 목표를 넘긴 뒤에도 처치할 때마다 클리어를 다시 보낸다
    int bonusGivenScene = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;

        EconomySettings settings = EconomySettings.Load();
        if (settings == null)
        {
            Debug.LogWarning($"[EconomySystem] Resources/{EconomySettings.ResourcePath} 가 없습니다 — Tools / 재화 · 상점 / 전체 만들기를 실행하세요.");
            return;
        }

        var go = new GameObject("EconomySystem");
        DontDestroyOnLoad(go);
        go.AddComponent<EconomySystem>().Settings = settings;
    }

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        GameEvents.OnPlayerSpawned += OnPlayerSpawned;
        GameEvents.OnEnemyDefeated += OnEnemyDefeated;
        GameEvents.OnStageClear += OnStageClear;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerSpawned -= OnPlayerSpawned;
        GameEvents.OnEnemyDefeated -= OnEnemyDefeated;
        GameEvents.OnStageClear -= OnStageClear;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ───────── 플레이어 ─────────

    void OnPlayerSpawned(Transform t)
    {
        if (t == null) return;

        player = t;
        playerStats = t.GetComponent<PlayerStats>();
        Wallet = Wallet.Ensure(t.gameObject);
    }

    /// <summary>OnPlayerSpawned 를 놓쳤을 때(Stage 씬을 바로 Play 등)를 대비해 한 번 더 찾는다.</summary>
    void ResolvePlayer()
    {
        if (player != null && Wallet != null) return;

        GameObject found = GameAppManager.Instance != null ? GameAppManager.Instance.Player : null;

        if (found == null)
        {
            PlayerStats stats = FindFirstObjectByType<PlayerStats>();
            if (stats != null) found = stats.gameObject;
        }

        if (found != null) OnPlayerSpawned(found.transform);
    }

    public bool HasLivePlayer => player != null && player.gameObject.activeInHierarchy && (playerStats == null || !playerStats.IsDead);

    public Vector3 PlayerCenter => player != null ? player.position + Vector3.up : Vector3.zero;

    public bool InMagnetRange(Vector3 position)
    {
        if (!HasLivePlayer) return false;

        Vector3 d = player.position - position;
        d.y = 0f;
        float r = Settings.magnetRadius;
        return d.sqrMagnitude <= r * r;
    }

    // ───────── 코인 ─────────

    void OnEnemyDefeated(Enemy enemy)
    {
        if (enemy == null || Settings == null || Settings.coinPrefab == null) return;

        ResolvePlayer();
        if (!HasLivePlayer) return;

        int total = Settings.CoinsFor(enemy.data);
        if (total <= 0) return;

        int count = Mathf.Clamp(total, 1, Settings.maxCoinObjects);
        int each = total / count;
        int extra = total % count;
        Vector3 origin = enemy.transform.position;

        for (int i = 0; i < count; i++)
        {
            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir = dir.normalized * Random.Range(Settings.scatterRadius.x, Settings.scatterRadius.y);

            CoinPickup coin = Spawn();
            if (coin == null) continue;

            coin.Launch(origin, origin + new Vector3(dir.x, 0f, dir.y), each + (i < extra ? 1 : 0));
        }
    }

    CoinPickup Spawn()
    {
        GameObject prefab = Settings.coinPrefab.gameObject;
        GameObject go = PoolManager.Instance != null ? PoolManager.Instance.Get(prefab) : Instantiate(prefab);
        if (go == null) return null;

        CoinPickup coin = go.GetComponent<CoinPickup>();
        if (coin != null && !coins.Contains(coin)) coins.Add(coin);

        return coin;
    }

    /// <summary>코인 하나를 지갑에 넣고 치운다 (CoinPickup 이 플레이어에게 닿았을 때 부른다).</summary>
    public void Collect(CoinPickup coin)
    {
        if (coin == null) return;

        ResolvePlayer();
        if (Wallet != null) Wallet.Add(coin.Value);

        PlayPickupSound();
        Despawn(coin);
    }

    /// <summary>땅에 남은 코인을 모두 바로 지갑에 넣는다 (정비를 열기 전).</summary>
    public void CollectAllNow()
    {
        ResolvePlayer();

        buffer.Clear();
        buffer.AddRange(coins);

        int total = 0;
        for (int i = 0; i < buffer.Count; i++)
        {
            if (buffer[i] == null || !buffer[i].gameObject.activeInHierarchy) continue;

            total += buffer[i].Value;
            Despawn(buffer[i]);
        }

        coins.Clear();

        if (total > 0 && Wallet != null && HasLivePlayer)
        {
            Wallet.Add(total);
            PlayPickupSound();
        }
    }

    void Despawn(CoinPickup coin)
    {
        coins.Remove(coin);

        Poolable poolable = coin.GetComponent<Poolable>();
        if (poolable != null) poolable.ReturnToPool();
        else Destroy(coin.gameObject);
    }

    void PlayPickupSound()
    {
        if (SoundManager.Instance != null && Settings.pickupSound != null)
            SoundManager.Instance.Play(Settings.pickupSound);
    }

    // ───────── 스테이지 ─────────

    void OnStageClear()
    {
        int scene = SceneManager.GetActiveScene().handle;
        if (bonusGivenScene == scene) return;

        ResolvePlayer();
        if (!HasLivePlayer) return;   // 사망이 먼저면 클리어가 아니다 (GameManager 와 같은 규칙)

        bonusGivenScene = scene;

        if (Wallet != null)
            Wallet.Add(Settings.ClearBonusFor(StageIndex()));

        if (Settings.collectAllOnClear)
        {
            for (int i = 0; i < coins.Count; i++)
                if (coins[i] != null) coins[i].Attract();
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 코인은 씬의 풀과 함께 사라진다
        coins.Clear();
    }

    /// <summary>지금 스테이지 번호 (1 · 2 · 3). 스테이지가 아니면 1.</summary>
    public static int StageIndex()
    {
        StageManager stage = FindFirstObjectByType<StageManager>();
        return stage != null && stage.currentStage != null ? Mathf.Max(1, stage.currentStage.stageIndex) : 1;
    }
}
