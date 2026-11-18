using UnityEngine;

[CreateAssetMenu(menuName = "Stage/StageData")]
public class StageData : ScriptableObject
{
    public int stageIndex;

    public int killTarget;

    public int maxEnemyCount;

    [Min(0.1f)]
    public float spawnInterval = 3f;

    [Min(0.1f)]
    public float rangedProjectileSpeed = 3.5f;

    // 2026-11-12 — 스테이지가 오를수록 몬스터가 단단해지게 (보조 무기가 차면 너무 쉽게 녹았다).
    // 몬스터 데이터의 maxHp 에 곱한다. 지금 값: 1 · 3 · 6 · 보스 스테이지 2.5 (11-12 사용자 결정, 처음 계획안은 1 · 1.5 · 2.2)
    [Tooltip("몬스터 체력 배율 — 몬스터 데이터의 최대 체력 × 이 값")]
    [Min(0.1f)]
    public float enemyHealthMultiplier = 1f;
}
