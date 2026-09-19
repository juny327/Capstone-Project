using System;
using UnityEngine;

public static class GameEvents
{
    public static Action OnEnemyKilled;

    public static Action OnStageClear;

    public static Action OnOpenUpgradeUI;

    public static Action<UpgradeData> OnUpgradeSelected;

    public static Action OnNextStage;

    public static Action<UpgradeData> OnUpgradeSuccess;

    public static Action<UpgradeData> OnUpgradeFailed;

    /// <summary>업그레이드 확인창의 확인 버튼을 눌렀을 때. 이때 카드 창을 닫고 게임을 재개한다.</summary>
    public static Action OnUpgradeConfirmed;

    public static Action OnPlayerDeadStart;
    
    public static Action OnPlayerDeadEnd;
    
    public static Action OnPlayerDead;

    public static Action<int,int> OnStageProgress;

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
    /// 보유 무기 목록이 바뀔 때 (획득·초기화). 슬롯 UI 가 처음부터 다시 그린다.
    /// 서브유닛을 얻으면 활성 무기가 바뀌지 않아 OnWeaponSwapped 가 나가지 않으므로 이 이벤트가 필요하다.
    /// </summary>
    public static Action OnWeaponsChanged;

    /// <summary>레벨업 시 (새 레벨). 업그레이드 카드를 띄운다.</summary>
    public static Action<int> OnLevelUp;

    public static Action<BossHealth> OnBossSpawned; // 보스 ui 연결용임

    public static Action OnGameWin; // 보스 처리 고나련

    // 밋밋한거 바꾼거
    public static Action OnShowStageClearUI;
    public static Action OnHideStageClearUI;
    // 이거 두개

    // 테스트용 꼭 지울것
    public static Transform Player;
}