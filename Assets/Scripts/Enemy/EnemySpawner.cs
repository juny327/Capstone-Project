using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public float lastSpawnTime;
    void OnEnable()
    {
        // spawner를 manager에 등록
        EnemyManager.Instance.RegisterSpawner(this);
    }

    void OnDisable()
    {
        EnemyManager.Instance.UnregisterSpawner(this);
    }
    
    // public Enemy Spawn(Enemy prefab, Transform target)
    // {
    //     Enemy enemy = Instantiate(prefab, transform.position, Quaternion.identity);
    //     // 각 enemy 객체 값 초기화
    //     enemy.Initialize(target, EnemyManager.Instance);

    //     return enemy;
    // }

    // 오브젝트 풀링 방식
    public Enemy Spawn(Enemy prefab, Transform target)
    {
        GameObject obj = PoolManager.Instance.Get(prefab.gameObject);

        obj.transform.position = transform.position;
        obj.transform.rotation = Quaternion.identity;

        Enemy enemy = obj.GetComponent<Enemy>();
        lastSpawnTime = Time.time;

        enemy.Initialize(target, EnemyManager.Instance);

        return enemy;
    }
}