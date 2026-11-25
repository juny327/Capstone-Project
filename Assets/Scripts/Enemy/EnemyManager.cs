using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance;

    public List<EnemySpawner> spawners = new List<EnemySpawner>();
    private List<Enemy> enemies = new List<Enemy>();
    public IReadOnlyList<Enemy> ActiveEnemies => enemies;

    [Header("Spawn")]
    public Enemy[] enemyPrefabs;
    public Transform player;
    public float spawnInterval = 3f;
    [Min(0.1f)] public float rangedProjectileSpeed = 3.5f;
    [Tooltip("새로 나오는 몬스터의 체력 배율 — StageManager 가 스테이지 데이터 값으로 채운다 (2026-11-12)")]
    [Min(0.1f)] public float healthMultiplier = 1f;

    // 2026-11-17 — 스테이지별 등장 비율 (StageManager 가 StageData.spawnWeights 를 넣는다).
    // 비어 있으면 몬스터 데이터의 spawnWeight 그대로, 적혀 있으면 목록에 없는 몬스터는 나오지 않는다.
    [System.NonSerialized] public StageData.SpawnWeight[] stageSpawnWeights;
    public int maxEnemyCount = 20;

    // 2026-11-23 — 보스 스테이지 잡몹 (StageManager 가 StageData 값으로 채운다)
    /// <summary>켜져 있으면 보스가 깨어나 싸우는 동안(BeginBossFight ~ EndBossFight)만 몬스터를 내보낸다.</summary>
    [System.NonSerialized] public bool waitForBossFight;
    /// <summary>꺼져 있으면 몬스터가 경험치 구슬 · 코인을 떨어뜨리지 않는다 (보스 스테이지).</summary>
    [System.NonSerialized] public bool dropRewards = true;
    bool bossFighting;

    // 2026-11-25 사용자: "예전엔 근접이 훨씬 많았는데 지금 원거리가 너무 많이 나온다" — 원거리(C · E)는 멀리서 쏘고 잘 안 잡혀서,
    // 동시 최대 수에 닿으면 근접만 잡히고 그 빈자리를 등장 비율대로 채우는 동안 원거리가 계속 쌓였다
    // (측정 kept_RangedMixCheck, 하드 · 회전 기관포: 스테이지 2 설정 10% → 2분 뒤 살아 있는 몬스터의 24% · 스테이지 3 15% → 29%).
    // 켜져 있으면 살아 있는 원거리가 그 종류의 등장 비율만큼 차 있을 때 이번에는 그 종류를 빼고 고른다 — 근접끼리는 비율 그대로.
    /// <summary>살아 있는 원거리 몬스터가 등장 비율을 넘지 않게 한다 (시험에서 끄고 비교한다).</summary>
    [System.NonSerialized] public bool capRangedShare = true;
    /// <summary>원거리가 비율만큼 차서 다른 몬스터로 바꿔 내보낸 횟수 (시험).</summary>
    public int RangedCapSkips { get; private set; }
    readonly Dictionary<Enemy, bool> rangedPrefab = new Dictionary<Enemy, bool>();
    readonly Dictionary<EnemyData, int> aliveByData = new Dictionary<EnemyData, int>();
    int[] pickWeights;

    [Header("Stage Event")]
    [Tooltip("Stage2에서 자동으로 로드되는 특수 몬스터. 다른 스테이지에서는 비워 둔다.")]
    [SerializeField] private Enemy specialStageEnemy;
    [SerializeField] private float specialSpawnDelay = -1f;
    private bool specialSpawned;
    private float stageElapsed;

    [Header("Spawn Distance")]
    public float minSpawnDistance = 10f;
    public float maxSpawnDistance = 40f;

    private float spawnTimer;

    private List<EnemySpawner> candidates = new List<EnemySpawner>();

    void Awake()
    {
        Instance = this;

        if (SceneManager.GetActiveScene().name == "Stage2" && specialStageEnemy == null)
        {
            specialStageEnemy = Resources.Load<Enemy>("MonsterF");
            specialSpawnDelay = 10f;
        }
        else if (SceneManager.GetActiveScene().name == "Stage3" && specialStageEnemy == null)
        {
            specialStageEnemy = Resources.Load<Enemy>("MonsterG");
            specialSpawnDelay = 15f;
        }
    }

    void Update()
    {
        TickEnemies();
        UpdateSpawn();
        stageElapsed += Time.deltaTime;
        UpdateSpecialSpawn();
    }

    void OnEnable()
    {
        GameEvents.OnPlayerSpawned += SetPlayer;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerSpawned -= SetPlayer;
    }

    void SetPlayer(Transform p)
    {
        player = p;
    }

    void TickEnemies()
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            enemies[i].Tick();
        }
    }

    void UpdateSpawn()
    {
        if (player == null) return;
        if (spawners.Count == 0) return;
        if (waitForBossFight && !bossFighting) return;
        if (StageEnded) return;   // 클리어 뒤에는 내보내지 않는다 (2026-11-25)

        spawnTimer += Time.deltaTime;

        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;

            if (enemies.Count < maxEnemyCount)
            {
                int requestedSpawnCount = Random.Range(0, 100) < 75 ? 1 : 2;
                int spawnCount = Mathf.Min(requestedSpawnCount, maxEnemyCount - enemies.Count);

                for (int i = 0; i < spawnCount && enemies.Count < maxEnemyCount; i++)
                {
                    SpawnEnemy();
                }
            }
        }
    }

    void SpawnEnemy()
    {
        if (player == null) return;

        EnemySpawner spawner = FindValidSpawner();
        if (spawner == null) return;

        Enemy prefab = GetWeightedRandomEnemy();
        if (prefab == null) return;

        spawner.Spawn(prefab, player);
    }

    void UpdateSpecialSpawn()
    {
        if (specialSpawned || specialStageEnemy == null || player == null || StageEnded)
            return;

        if (specialSpawnDelay < 0f || stageElapsed < specialSpawnDelay)
            return;

        EnemySpawner spawner = FindValidSpawner();
        if (spawner == null)
            return;

        spawner.Spawn(specialStageEnemy, player);
        specialSpawned = true;
        Debug.Log($"[EnemyManager] {specialStageEnemy.name} spawned in {SceneManager.GetActiveScene().name} after {specialSpawnDelay:0.#} seconds.");
    }

    // 특정 거리 내에 있는 spawner만 고려하자.
    EnemySpawner FindValidSpawner()
    {
        candidates.Clear();

        foreach (var spawner in spawners)
        {
            if (spawner == null)
                continue;

            if (!spawner.isActiveAndEnabled)
                continue;

            float dist = Vector3.Distance(
                player.position,
                spawner.transform.position
            );

            if (dist > minSpawnDistance && dist < maxSpawnDistance)
            {
                candidates.Add(spawner);
            }
        }

        if (candidates.Count == 0)
            return FindFallbackSpawner();

        return candidates[Random.Range(0, candidates.Count)];
    }

    /// <summary>범위 밖 등장 지점을 쓴 횟수 (시험).</summary>
    public int FallbackSpawns { get; private set; }

    /// <summary>
    /// 범위(min ~ max) 안에 등장 지점이 하나도 없을 때 (2026-11-18) — 범위 밖에서 가장 가까운 지점들 가운데 하나를 쓴다.
    ///
    /// 등장 지점은 맵마다 무작위로 10개가 놓이고 범위는 10 ~ 25m 라서, 서 있는 자리에 따라 범위 안에 지점이 없을 수 있다
    /// (계산: 맵 안 아무 자리 평균 4.5% · 모서리 근처 약 20%). 그러면 플레이어가 움직이기 전까지 몬스터가 영영 안 나왔다 —
    /// 시작 자리에서 걸리면 한 마리도 안 나온다 (측정 판에서 처치 0 으로 멈춤: 가장 가까운 지점이 27m).
    /// 범위 안에 지점이 있으면 예전과 똑같이 그 안에서 고른다.
    /// </summary>
    EnemySpawner FindFallbackSpawner()
    {
        // 너무 가까운 것(화면 안에서 튀어나온다)은 빼고 가장 가까운 거리
        float nearest = float.MaxValue;
        foreach (var spawner in spawners)
        {
            if (spawner == null || !spawner.isActiveAndEnabled) continue;
            float dist = Vector3.Distance(player.position, spawner.transform.position);
            if (dist > minSpawnDistance && dist < nearest) nearest = dist;
        }

        // 모두 너무 가깝다 (지점이 플레이어 둘레에만 있다) — 그중에서라도 고른다
        bool allowClose = nearest == float.MaxValue;

        // 가장 가까운 것에서 10m 안쪽까지를 후보로 — 한 지점에서만 줄지어 나오지 않게
        foreach (var spawner in spawners)
        {
            if (spawner == null || !spawner.isActiveAndEnabled) continue;
            float dist = Vector3.Distance(player.position, spawner.transform.position);
            if (allowClose || (dist > minSpawnDistance && dist <= nearest + 10f))
                candidates.Add(spawner);
        }

        if (candidates.Count == 0)
            return null;

        FallbackSpawns++;
        return candidates[Random.Range(0, candidates.Count)];
    }

    // Cumulative Weight Algorithm
    Enemy GetWeightedRandomEnemy()
    {
        int totalWeight = 0;

        foreach (var prefab in enemyPrefabs)
            totalWeight += WeightOf(prefab);

        if (totalWeight <= 0)
            return enemyPrefabs[0];

        // 살아 있는 원거리가 비율만큼 차 있는 종류는 이번에 빼고 고른다 (2026-11-25) — 모두 빠지면 원래 비율 그대로
        if (pickWeights == null || pickWeights.Length != enemyPrefabs.Length) pickWeights = new int[enemyPrefabs.Length];
        CountAlive();
        int pickTotal = 0;
        bool skipped = false;
        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            int weight = WeightOf(enemyPrefabs[i]);
            if (weight > 0 && RangedFull(enemyPrefabs[i], weight, totalWeight)) { weight = 0; skipped = true; }
            pickWeights[i] = weight;
            pickTotal += weight;
        }
        if (pickTotal <= 0)
        {
            for (int i = 0; i < enemyPrefabs.Length; i++) pickWeights[i] = WeightOf(enemyPrefabs[i]);
            pickTotal = totalWeight;
            skipped = false;
        }

        int random = Random.Range(0, pickTotal);

        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            random -= pickWeights[i];

            if (random < 0)
            {
                // 원거리를 빼서 다른 종류가 나왔을 때만 센다 (어차피 근접이 뽑혔을 차례는 세지 않는다 — 근사)
                if (skipped) RangedCapSkips++;
                return enemyPrefabs[i];
            }
        }

        return enemyPrefabs[0];
    }

    /// <summary>살아 있는 몬스터 수를 종류별로 센다 (뽑을 때마다 — 최대 140마리라 가볍다).</summary>
    int CountAlive()
    {
        aliveByData.Clear();
        int alive = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (e == null || e.state == EnemyState.Dead || e.data == null) continue;
            alive++;
            aliveByData[e.data] = aliveByData.TryGetValue(e.data, out int n) ? n + 1 : 1;
        }
        aliveTotal = alive;
        return alive;
    }

    int aliveTotal;

    /// <summary>원거리 몬스터(원거리 · 레이저 공격)인가 — 프리팹마다 한 번만 본다.</summary>
    public bool IsRanged(Enemy prefab)
    {
        if (prefab == null) return false;
        if (!rangedPrefab.TryGetValue(prefab, out bool ranged))
        {
            ranged = prefab.GetComponentInChildren<RangedAttack>(true) != null || prefab.GetComponentInChildren<LaserAttack>(true) != null;
            rangedPrefab[prefab] = ranged;
        }
        return ranged;
    }

    /// <summary>
    /// 원거리이고, 살아 있는 그 종류가 등장 비율만큼 차 있는가 — (살아 있는 수 + 1) × 비율을 올림한 수 (적어도 1마리).
    /// 예: 스테이지 3 의 C(8%)는 140마리 중 12마리까지.
    /// </summary>
    bool RangedFull(Enemy prefab, int weight, int totalWeight)
    {
        if (!capRangedShare || prefab == null || prefab.data == null || !IsRanged(prefab)) return false;
        int same = aliveByData.TryGetValue(prefab.data, out int n) ? n : 0;
        int allowed = Mathf.Max(1, (weight * (aliveTotal + 1) + totalWeight - 1) / totalWeight);   // 올림 (정수로 — 소수 오차 없이)
        return same >= allowed;
    }

    /// <summary>이 몬스터의 등장 비율 — 스테이지 목록이 있으면 그 값(없는 몬스터는 0), 없으면 몬스터 데이터의 값.</summary>
    public int WeightOf(Enemy prefab)
    {
        if (prefab == null || prefab.data == null) return 0;
        if (stageSpawnWeights == null || stageSpawnWeights.Length == 0) return Mathf.Max(0, prefab.data.spawnWeight);

        foreach (StageData.SpawnWeight w in stageSpawnWeights)
            if (w.enemy == prefab.data) return Mathf.Max(0, w.weight);
        return 0;
    }

    readonly List<Enemy> visibleCandidates = new List<Enemy>();

    /// <summary>
    /// 벽에 가리지 않은 가장 가까운 적 (2026-11-17 — 파이어볼 · 일렉트릭 샷이 벽 너머 적을 노려 벽을 뚫고 날아갔다).
    /// eye = 쏘는 자리(발밑 + 높이), 적은 몸 가운데(+1m)를 본다. 가까운 순서로 몇 개만 시선을 검사한다.
    /// </summary>
    public Enemy GetClosestVisibleEnemy(Vector3 from, float maxDistance, Vector3 eye, int maxChecks = 8)
    {
        visibleCandidates.Clear();
        float maxSqr = maxDistance * maxDistance;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (enemy == null || !enemy.gameObject.activeInHierarchy || enemy.state == EnemyState.Dead) continue;
            if ((enemy.transform.position - from).sqrMagnitude < maxSqr) visibleCandidates.Add(enemy);
        }

        visibleCandidates.Sort((a, b) => (a.transform.position - from).sqrMagnitude.CompareTo((b.transform.position - from).sqrMagnitude));

        for (int i = 0; i < visibleCandidates.Count && i < maxChecks; i++)
            if (CombatFx.HasLineOfSight(eye, visibleCandidates[i].transform.position + Vector3.up))
                return visibleCandidates[i];

        return null;
    }

    public Enemy GetClosestEnemy(Vector3 from, float maxDistance)
    {
        Enemy closest = null;
        float closestSqrDistance = maxDistance * maxDistance;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];

            if (enemy == null ||
                !enemy.gameObject.activeInHierarchy ||
                enemy.state == EnemyState.Dead)
                continue;

            float sqrDistance = (enemy.transform.position - from).sqrMagnitude;

            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closest = enemy;
            }
        }

        return closest;
    }

    /// <summary>보스가 깨어났다 — 보스 스테이지 잡몹을 내보내기 시작한다 (waitForBossFight 가 꺼진 스테이지에서는 아무 일 없음).</summary>
    public void BeginBossFight()
    {
        if (!waitForBossFight || bossFighting) return;
        bossFighting = true;
        spawnTimer = 0f;
    }

    /// <summary>보스가 쓰러졌다 · 플레이어가 쓰러졌다 — 잡몹을 더 내보내지 않는다. defeatRemaining 이면 남은 몬스터도 쓰러뜨린다.</summary>
    /// <summary>스테이지가 끝났다 (클리어) — 더 내보내지 않고, 이 뒤로 쓰러지는 몬스터는 보상이 없다.</summary>
    public bool StageEnded { get; private set; }

    /// <summary>시험용 — 클리어 때 치운 몬스터 수.</summary>
    public int StageEndCleared { get; private set; }

    /// <summary>
    /// 스테이지 클리어 (2026-11-25 사용자: "클리어 문구가 나오는 동안 캐릭터가 계속 공격해 남은 몬스터를 잡고 골드 · 경험치를 먹는다") —
    /// 맵에 남은 몬스터를 모두 쓰러뜨리고 더 내보내지 않는다. 이때 쓰러진 몬스터는 경험치 · 코인 · 보급을 떨어뜨리지 않고
    /// 처치 수(스테이지 목표 · 판 기록)에도 들어가지 않는다. 이미 바닥에 떨어진 구슬 · 코인은 그대로 둔다.
    /// </summary>
    public void EndStage()
    {
        if (StageEnded) return;
        StageEnded = true;
        dropRewards = false;

        foreach (Enemy enemy in enemies.ToArray())
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy || enemy.state == EnemyState.Dead) continue;
            // 처치로 세지 않게 — 쓰러지는 동안(Die → OnDeath → OnEnemyDeath)만 끄고, 풀에서 다시 쓰이므로 바로 돌려놓는다
            bool counted = enemy.countsTowardKillGoal;
            enemy.countsTowardKillGoal = false;
            enemy.DefeatSilently();
            enemy.countsTowardKillGoal = counted;
            StageEndCleared++;
        }
    }

    public void EndBossFight(bool defeatRemaining)
    {
        if (!waitForBossFight) return;
        bossFighting = false;
        if (!defeatRemaining) return;

        foreach (Enemy enemy in enemies.ToArray())
            if (enemy != null && enemy.gameObject.activeInHierarchy && enemy.state != EnemyState.Dead)
                enemy.TakeDamage(new DamageInfo { damage = 999999f, hitPoint = enemy.transform.position + Vector3.up });
    }

    public void RegisterSpawner(EnemySpawner spawner)
    {
        if (!spawners.Contains(spawner))
            spawners.Add(spawner);
    }

    public void UnregisterSpawner(EnemySpawner spawner)
    {
        spawners.Remove(spawner);
    }

    public void RegisterEnemy(Enemy enemy)
    {
        enemies.Add(enemy);
        enemy.OnDeath += OnEnemyDeath;
    }

    void OnEnemyDeath(Enemy enemy)
    {
        enemies.Remove(enemy);

        // 실제로 쓰러진 몬스터만 처치로 센다 (2026-11-23) — 자폭 몬스터(D)는 돌진해 터질 때 쓰러지지 않고(Die 를 거치지 않음)
        // OnDeath 만 알리고 풀로 돌아가는데, 예전엔 그것도 플레이어가 잡은 것으로 세어 스테이지 처치 목표 · 판 기록이 올라갔다.
        // (경험치 · 코인은 Die 에서만 나오므로 원래 안 나왔다)
        if (enemy != null && enemy.state == EnemyState.Dead && enemy.countsTowardKillGoal)
            GameEvents.OnEnemyKilled?.Invoke();
    }
}
