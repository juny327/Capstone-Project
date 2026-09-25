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
    /// <summary>손에 든 무기의 탄약이 바뀔 때 (현재 탄약, 탄창 크기). 탄창 크기 0 이면 표시하지 않는다.</summary>
    public static Action<int, int> OnAmmoChanged;

    public static Action<IWeapon> OnWeaponSwapped;       // 활성 무기가 바뀔 때
    public static Action<IWeapon> OnWeaponStatsChanged;  // 레벨업·모디파이어 적용 시

    /// <summary>
    /// 보유 무기 목록이 바뀔 때 (획득). 슬롯 UI 가 처음부터 다시 그린다.
    /// 서브유닛을 얻으면 활성 무기가 바뀌지 않아 OnWeaponSwapped 가 나가지 않으므로 따로 필요하다.
    /// </summary>
    public static Action OnWeaponsChanged;

    public static Action<BossHealth> OnBossSpawned; // 보스 ui 연결용임

    public static Action OnGameWin; // 보스 처리 고나련

    // 밋밋한거 바꾼거
    // 스테이지 클리어 보상 (무기 · 부착물 …)
    /// <summary>보상 카드를 띄울 때. 스테이지 흐름이 발행한다.</summary>
    public static Action OnStageRewardOpen;

    /// <summary>카드를 골랐을 때. StageRewardManager 가 받아 적용한다.</summary>
    public static Action<StageReward> OnStageRewardSelected;

    /// <summary>
    /// 보상 적용이 끝났을 때. 스테이지 흐름이 이 신호를 기다렸다 다음 씬으로 넘어간다.
    /// 줄 보상이 없으면 null 로 발행된다.
    /// </summary>
    public static Action<StageReward> OnStageRewardApplied;

    public static Action OnShowStageClearUI;
    public static Action OnHideStageClearUI;
    // 이거 두개

    // 테스트용 꼭 지울것
    public static Transform Player;
}
