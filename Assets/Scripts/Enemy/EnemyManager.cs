using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance;

    public List<EnemySpawner> spawners = new List<EnemySpawner>();
    private List<Enemy> enemies = new List<Enemy>();

    [Header("Spawn")]
    public Enemy[] enemyPrefabs;
    public Transform player;
    public float spawnInterval = 3f;
    public int maxEnemyCount = 20;

    [Header("Spawn Distance")]
    public float minSpawnDistance = 10f;
    public float maxSpawnDistance = 40f;

    private float spawnTimer;

    private List<EnemySpawner> candidates = new List<EnemySpawner>();

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        TickEnemies();
        UpdateSpawn();
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
                SpawnEnemy();
            }
        }
    }

    void SpawnEnemy()
    {
        if (player == null) return;

        EnemySpawner spawner = FindValidSpawner();

        if (spawner == null)
            return;

        Enemy prefab = GetWeightedRandomEnemy();

        if (prefab == null)
            return;

        spawner.Spawn(prefab, player);
    }

    // 특정 거리 내에 있는 spawner만 고려하자.
    EnemySpawner FindValidSpawner()
    {
        candidates.Clear();

        foreach (var spawner in spawners)
        {
            float dist = Vector3.Distance(player.position, spawner.transform.position);

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
        {
            Enemy enemy = prefab.GetComponent<Enemy>();
            totalWeight += enemy.data.spawnWeight;
        }

        int random = Random.Range(0, totalWeight);

        foreach (var prefab in enemyPrefabs)
        {
            Enemy enemy = prefab.GetComponent<Enemy>();

            random -= enemy.data.spawnWeight;

            if (random < 0)
                return prefab;
        }

        return enemyPrefabs[0];
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
        // EnemyEvent 발생
        GameEvents.OnEnemyKilled?.Invoke();
    }

    // void OnDrawGizmosSelected()
    // {
    //     if (player == null)
    //         return;

    //     // 최소 거리 원 (빨간색)
    //     Gizmos.color = Color.red;
    //     Gizmos.DrawWireSphere(player.position, minSpawnDistance);

    //     // 최대 거리 원 (초록색)
    //     Gizmos.color = Color.green;
    //     Gizmos.DrawWireSphere(player.position, maxSpawnDistance);
    // }
}