using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Stage/StageData")]
public class StageData : ScriptableObject
{
    /// <summary>이 스테이지에서 한 몬스터의 등장 비율 (2026-11-17).</summary>
    [Serializable]
    public struct SpawnWeight
    {
        public EnemyData enemy;
        [Min(0)] public int weight;
    }

    public int stageIndex;

    public int killTarget;

    // 2026-11-25 사용자: "스테이지 2 · 3 에서 잡는 몬스터 수를 좀 더" — 처치 목표 ×1.25 (128 → 160 · 165 → 206).
    // 골드(EconomySettings 스테이지 코인 배율 ÷1.25) · 경험치(이 값 ×0.8)는 스테이지 합계가 그대로이게 낮췄다 (사용자: 골드는 괜찮다 · 경험치는 남아돈다)
    [Tooltip("이 스테이지 몬스터의 경험치 구슬 값 배율 (소수는 확률로 올림). 처치 목표를 늘리고 스테이지 경험치 합계는 그대로 두려면 낮춘다")]
    [Min(0f)]
    public float expMultiplier = 1f;

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

    // 2026-11-25 사용자: "전체적인 몬스터들의 데미지도 스테이지가 오를수록 상향" — 몬스터 데이터의 공격력에 곱한다 (근접 · 돌진 · 자폭 · 레이저 · 탄 모두).
    // 보스 자신의 패턴 피해는 DragonBossSettings 값 그대로 (보스 스테이지 값은 보스전 잡몹에만). 난이도(이지 ×0.75)는 이 위에 곱한다.
    // 플레이어가 받는 피해는 정수로 잘리므로 (PlayerStats) 작은 배율은 약한 몬스터(A 3)에서 티가 안 난다 — 지금 값 1 · 1.35 · 1.7 · 보스 스테이지 1.7
    [Tooltip("몬스터 피해 배율 — 몬스터 데이터의 공격력 × 이 값 (보스 자신의 패턴은 빼고)")]
    [Min(0.1f)]
    public float enemyDamageMultiplier = 1f;

    // 2026-11-17 — 스테이지마다 나오는 몬스터를 다르게 (스테이지 1 = A · B, 2 = C · D 추가, 3 = 다섯 종 고르게).
    [Tooltip("이 스테이지의 몬스터 등장 비율. 비워 두면 몬스터 데이터의 spawnWeight 그대로, " +
             "적어 두면 목록에 없는 몬스터는 나오지 않는다 (특수 몬스터 F · G 와 보스의 부하는 따로)")]
    public SpawnWeight[] spawnWeights;

    // 2026-11-25 사용자: "원거리 몬스터의 최대 마릿수를 지정 — 배율을 낮춰도 쌓이다 보면 원거리 공격이 너무 많이 들어온다. 일단 10마리"
    [Tooltip("동시에 살아 있을 수 있는 원거리 몬스터(원거리 · 레이저 공격 — C · E) 수의 상한. 다 차면 근접만 나온다. 0 = 제한 없음 (등장 비율 상한만)")]
    [Min(0)]
    public int maxRangedEnemies = 0;

    // 2026-11-23 사용자: "보스 중간중간 잡몹이 자연 스폰돼 방해 · 보스룸 몬스터는 경험치 · 코인을 안 떨구게"
    [Header("보스 스테이지")]
    [Tooltip("켜면 보스가 깨어난 뒤부터 쓰러질 때까지만 몬스터가 나온다 (등장 연출 동안은 안 나온다). 보스가 쓰러지면 남은 몬스터도 같이 쓰러진다. " +
             "수 · 간격은 위의 maxEnemyCount · spawnInterval")]
    public bool spawnDuringBossFight;

    [Tooltip("이 스테이지 몬스터가 경험치 구슬 · 코인을 떨어뜨리는지 (보스 스테이지는 끔 — 보스의 부하도 같다)")]
    public bool enemyRewards = true;
}
