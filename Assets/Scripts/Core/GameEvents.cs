using System;
using UnityEngine;

public static class GameEvents
{
    public static Action OnEnemyKilled;

    public static Action<int> OnPlayerLevelUp;

    public static Action OnStageClear;

    public static Action OnOpenUpgradeUI;

    public static Action<UpgradeData> OnUpgradeSelected;

    public static Action OnNextStage;

    public static Action<UpgradeData> OnUpgradeSuccess;

    public static Action<UpgradeData> OnUpgradeFailed;

    public static Action OnPlayerDeadStart;

    public static Action OnPlayerDeadEnd;

    public static Action OnPlayerDead;

    public static Action<int, int> OnStageProgress;

    public static Action<float> OnBulletDamageChanged;

    public static Action<Transform> OnPlayerSpawned;

    public static Action<Camera> OnCameraReady;

    public static Action<float> OnMoveSpeedChanged;

    public static Action<float> OnBulletSpeedChanged;

    // 무기 시스템 (07번 설계)
    public static Action<IWeapon> OnWeaponSwapped;       // 활성 무기가 바뀔 때
    public static Action<IWeapon> OnWeaponStatsChanged;  // 레벨업·모디파이어 적용 시

    public static Action<BossHealth> OnBossSpawned; // 보스 ui 연결용임

    public static Action OnGameWin; // 보스 처리 고나련

    // 밋밋한거 바꾼거
    public static Action OnShowStageClearUI;
    public static Action OnHideStageClearUI;
    // 이거 두개

    // 테스트용 꼭 지울것
    public static Transform Player;
}
