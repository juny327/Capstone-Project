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
        if (specialSpawned || specialStageEnemy == null || player == null)
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
            return null;

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

        int random = Random.Range(0, totalWeight);

        foreach (var prefab in enemyPrefabs)
        {
            random -= WeightOf(prefab);

            if (random < 0)
                return prefab;
        }

        return enemyPrefabs[0];
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
        if (enemy == null || enemy.countsTowardKillGoal)
            GameEvents.OnEnemyKilled?.Invoke();
    }
}
