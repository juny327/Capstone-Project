using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public float lastSpawnTime;

    private Coroutine registerRoutine;

    void OnEnable()
    {
        registerRoutine = StartCoroutine(RegisterSpawner());
    }

    IEnumerator RegisterSpawner()
    {
        // EnemyManager의 Awake가 끝날 때까지 대기
        yield return null;

        if (EnemyManager.Instance == null)
            yield break;

        EnemyManager.Instance.RegisterSpawner(this);
        registerRoutine = null;
    }

    void OnDisable()
    {
        // 여기서는 EnemyManager를 호출하지 않는다.
        if (registerRoutine != null)
        {
            StopCoroutine(registerRoutine);
            registerRoutine = null;
        }
    }

    void OnDestroy()
    {
        // 파괴될 때만 Manager에서 제거
        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.UnregisterSpawner(this);
        }
    }

    public Enemy Spawn(Enemy prefab, Transform target)
    {
        if (PoolManager.Instance == null)
            return null;

        if (prefab == null)
            return null;

        GameObject obj = PoolManager.Instance.Get(prefab.gameObject);

        if (obj == null)
            return null;

        obj.transform.position = transform.position;
        obj.transform.rotation = Quaternion.identity;

        Enemy enemy = obj.GetComponent<Enemy>();

        if (enemy == null)
            return null;

        lastSpawnTime = Time.time;

        if (EnemyManager.Instance != null)
        {
            enemy.Initialize(target, EnemyManager.Instance);
        }

        return enemy;
    }
}