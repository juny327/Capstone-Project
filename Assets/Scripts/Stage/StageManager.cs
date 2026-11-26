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

        // 최대 수 0 인 스테이지는 몬스터 0 그대로 둔다 (배율을 곱해 1 로 올리지 않는다).
        // 보스 스테이지는 2026-11-23 부터 잡몹이 나온다 (BossStage.asset 최대 수 · 간격) — 처치 목표 0 이라 잡아도 클리어되지 않는다 (OnEnemyKilled)
        EnemyManager.Instance.maxEnemyCount = currentStage.maxEnemyCount <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(currentStage.maxEnemyCount * mode.maxEnemies));
        EnemyManager.Instance.spawnInterval = currentStage.spawnInterval * mode.spawnInterval;
        EnemyManager.Instance.rangedProjectileSpeed = currentStage.rangedProjectileSpeed;
        EnemyManager.Instance.healthMultiplier = currentStage.enemyHealthMultiplier * mode.enemyHealth;
        EnemyManager.Instance.damageMultiplier = currentStage.enemyDamageMultiplier;   // 2026-11-25 스테이지별 몬스터 피해 (난이도 배율은 PlayerStats 에서 따로)
        EnemyManager.Instance.stageSpawnWeights = currentStage.spawnWeights;   // 스테이지별 등장 몬스터 (2026-11-17)
        // 보스 스테이지 (2026-11-23) — 잡몹은 보스가 깨어난 뒤에만 · 몬스터는 경험치 · 코인을 떨어뜨리지 않는다
        EnemyManager.Instance.waitForBossFight = currentStage.spawnDuringBossFight;
        EnemyManager.Instance.dropRewards = currentStage.enemyRewards;
        EnemyManager.Instance.expMultiplier = currentStage.expMultiplier;   // 2026-11-25 처치 목표를 늘린 스테이지의 경험치 배율
        EnemyManager.Instance.maxRangedAlive = currentStage.maxRangedEnemies;   // 2026-11-25 원거리 동시 최대 (0 = 제한 없음)
        GameEvents.OnStageProgress?.Invoke(killCount, killTarget);
    }

    void OnEnemyKilled()
    {
        // 처치 목표가 0 인 스테이지(보스)는 처치로 끝나지 않는다 — 보스전 잡몹을 잡아도 클리어 흐름이 열리지 않게 (2026-11-23)
        if (KillTarget <= 0) return;

        killCount++;
        GameEvents.OnStageProgress?.Invoke(killCount, KillTarget);

        if (killCount >= KillTarget)
        {
            GameEvents.OnStageClear?.Invoke();
        }
    }
}
