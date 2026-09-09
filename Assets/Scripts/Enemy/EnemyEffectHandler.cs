using UnityEngine;

public class EnemyEffectHandler : MonoBehaviour
{
    public GameObject criticalDecalPrefab;

    private Enemy enemy;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
    }

    void OnEnable()
    {
        enemy.OnCriticalHit += HandleCriticalHit;
    }

    void OnDisable()
    {
        enemy.OnCriticalHit -= HandleCriticalHit;
    }

    void HandleCriticalHit()
    {
        GameObject go = PoolManager.Instance.Get(criticalDecalPrefab);

        var decal = go.GetComponent<CriticalDecal>();
        decal.Initialize(transform, 1f);
    }
}