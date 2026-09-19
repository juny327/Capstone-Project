using UnityEngine;

public class StageManager : MonoBehaviour
{
    public StageData currentStage;

    int killCount = 0;

    /// <summary>클리어를 이미 알렸는지. 중복 발행을 막는다.</summary>
    bool cleared;

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
        GameEvents.OnStageProgress?.Invoke(killCount, currentStage.killTarget);
    }

    void OnEnemyKilled()
    {
        killCount++;
        GameEvents.OnStageProgress?.Invoke(killCount, currentStage.killTarget);

        if (cleared) return;

        // 킬 목표가 0 인 스테이지(보스전)는 처치 수로 끝나지 않는다.
        // 이 가드가 없으면 0 >= 0 이 성립해 **적을 잡을 때마다** 클리어가 발행된다.
        if (currentStage.killTarget <= 0) return;

        if (killCount < currentStage.killTarget) return;

        cleared = true;   // 클리어는 한 번만
        GameEvents.OnStageClear?.Invoke();
    }
}
