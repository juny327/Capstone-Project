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