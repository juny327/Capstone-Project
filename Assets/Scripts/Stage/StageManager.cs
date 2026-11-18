using UnityEngine;

public class StageManager : MonoBehaviour
{
    public StageData currentStage;

    int killCount = 0;
    int killTarget;   // 스테이지 처치 목표 × 난이도 배율 (2026-11-17)

    /// <summary>이번 스테이지의 처치 목표 (난이도 배율을 곱한 값).</summary>
    public int KillTarget => killTarget > 0 ? killTarget : Difficulty.KillTarget(currentStage.killTarget);

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
        // 난이도 (2026-11-17) — 로비에서 고른 이지 / 하드의 배율을 스테이지 값 위에 곱한다. 하드는 모두 ×1 (그때까지의 밸런스)
        DifficultySettings.Mode mode = Difficulty.Values;
        killTarget = Difficulty.KillTarget(currentStage.killTarget);

        // 보스 스테이지는 일반 몬스터 0 — 0 은 0 그대로 둔다 (1 로 올리면 보스전에 몬스터가 나오고, 잡으면 클리어 흐름이 열린다)
        EnemyManager.Instance.maxEnemyCount = currentStage.maxEnemyCount <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(currentStage.maxEnemyCount * mode.maxEnemies));
        EnemyManager.Instance.spawnInterval = currentStage.spawnInterval * mode.spawnInterval;
        EnemyManager.Instance.rangedProjectileSpeed = currentStage.rangedProjectileSpeed;
        EnemyManager.Instance.healthMultiplier = currentStage.enemyHealthMultiplier * mode.enemyHealth;
        EnemyManager.Instance.stageSpawnWeights = currentStage.spawnWeights;   // 스테이지별 등장 몬스터 (2026-11-17)
        GameEvents.OnStageProgress?.Invoke(killCount, killTarget);
    }

    void OnEnemyKilled()
    {
        killCount++;
        GameEvents.OnStageProgress?.Invoke(killCount, KillTarget);

        if (killCount >= KillTarget)
        {
            GameEvents.OnStageClear?.Invoke();
        }
    }
}
