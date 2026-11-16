using UnityEngine;

public class StageManager : MonoBehaviour
{
    public StageData currentStage;

    int killCount = 0;

    void OnEnable()
    {
        GameEvents.OnEnemyKilled += OnEnemyKilled;
    }

    void OnDisable()
    {
        GameEvents.OnEnemyKilled -= OnEnemyKilled;
    }

    void Start()
    {
        EnemyManager.Instance.maxEnemyCount = currentStage.maxEnemyCount;
        EnemyManager.Instance.spawnInterval = currentStage.spawnInterval;
        EnemyManager.Instance.rangedProjectileSpeed = currentStage.rangedProjectileSpeed;
        EnemyManager.Instance.healthMultiplier = currentStage.enemyHealthMultiplier;
        EnemyManager.Instance.stageSpawnWeights = currentStage.spawnWeights;   // 스테이지별 등장 몬스터 (2026-11-17)
        GameEvents.OnStageProgress?.Invoke(killCount, currentStage.killTarget);
    }

    void OnEnemyKilled()
    {
        killCount++;
        GameEvents.OnStageProgress?.Invoke(killCount, currentStage.killTarget); 

        if (killCount >= currentStage.killTarget)
        {
            GameEvents.OnStageClear?.Invoke();
        }
    }
}
