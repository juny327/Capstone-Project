#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

public static class UpgradeDebugTool
{
    [MenuItem("Tools/Upgrade/Open Upgrade Cards")]
    public static void OpenUpgradeCards()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Play 모드에서 실행해주세요.");
            return;
        }

        GameEvents.OnOpenUpgradeUI?.Invoke();

        Debug.Log("Upgrade UI 강제 오픈");
    }

    [MenuItem("Tools/Upgrade/EXP +10")]
    public static void AddExp10()
    {
        AddExp(10);
    }

    [MenuItem("Tools/Upgrade/EXP +50")]
    public static void AddExp50()
    {
        AddExp(50);
    }

    [MenuItem("Tools/Upgrade/EXP +100")]
    public static void AddExp100()
    {
        AddExp(100);
    }

    [MenuItem("Tools/Upgrade/EXP +500")]
    public static void AddExp500()
    {
        AddExp(500);
    }

    [MenuItem("Tools/Upgrade/Skill Level/FireBall +1", priority = 100)]
    public static void AddFireBallLevel()
    {
        AddSkillLevel<FireBallEffect>();
    }

    [MenuItem("Tools/Upgrade/Skill Level/Shuriken +1", priority = 101)]
    public static void AddShurikenLevel()
    {
        AddSkillLevel<ShurikenEffect>();
    }

    [MenuItem("Tools/Upgrade/Skill Level/Tornado +1", priority = 102)]
    public static void AddTornadoLevel()
    {
        AddSkillLevel<TornadoEffect>();
    }

    [MenuItem("Tools/Upgrade/Skill Level/Black Hole +1", priority = 103)]
    public static void AddBlackHoleLevel()
    {
        AddSkillLevel<BlackHoleEffect>();
    }

    [MenuItem("Tools/Upgrade/Skill Level/Chain Lightning +1", priority = 104)]
    public static void AddChainLightningLevel()
    {
        AddSkillLevel<ChainLightningEffect>();
    }

    [MenuItem("Tools/Upgrade/Skill Level/Drain +1", priority = 105)]
    public static void AddDrainLevel()
    {
        AddSkillLevel<DrainEffect>();
    }

    [MenuItem("Tools/Upgrade/Skill Level/Electric Shot +1", priority = 106)]
    public static void AddElectricShotLevel()
    {
        AddSkillLevel<ElectricShotEffect>();
    }

    static void AddExp(int amount)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Play 모드에서 실행해주세요.");
            return;
        }

        PlayerStats playerStats =
            Object.FindFirstObjectByType<PlayerStats>();

        if (playerStats == null)
        {
            Debug.LogWarning("PlayerStats를 찾을 수 없습니다.");
            return;
        }

        playerStats.AddExp(amount);

        Debug.Log(
            $"EXP +{amount} | " +
            $"현재 EXP: {playerStats.currentExp} / {playerStats.expToNextLevel}"
        );
    }

    static void AddSkillLevel<T>() where T : UpgradeEffect
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Play 모드에서 실행해주세요.");
            return;
        }

        UpgradeManager manager =
            Object.FindFirstObjectByType<UpgradeManager>();

        if (manager == null)
        {
            Debug.LogWarning("UpgradeManager를 찾을 수 없습니다.");
            return;
        }

        if (manager.upgradePool == null ||
            manager.upgradePool.upgrades == null)
        {
            Debug.LogWarning("Upgrade Pool이 연결되지 않았습니다.");
            return;
        }

        UpgradeData target = null;

        foreach (UpgradeData data in manager.upgradePool.upgrades)
        {
            if (data != null && data.effect is T)
            {
                target = data;
                break;
            }
        }

        if (target == null)
        {
            Debug.LogWarning(
                $"해당 스킬의 UpgradeData를 찾을 수 없습니다: {typeof(T).Name}");
            return;
        }

        PlayerStats playerStats =
            Object.FindFirstObjectByType<PlayerStats>();

        if (playerStats == null)
        {
            Debug.LogWarning("PlayerStats를 찾을 수 없습니다.");
            return;
        }

        int originalExp = playerStats.currentExp;

        try
        {
            // 디버그 기능이므로 EXP는 실제로 차감하지 않는다.
            // 일반 업그레이드 이벤트를 그대로 호출해서
            // UpgradeManager의 레벨 관리와 Effect 적용을 동일하게 사용한다.
            playerStats.currentExp =
                Mathf.Max(playerStats.currentExp, target.costExp);

            GameEvents.OnUpgradeSelected?.Invoke(target);
        }
        finally
        {
            // 디버그 업그레이드로 인해 EXP가 변하지 않도록 원래 값으로 복구한다.
            playerStats.currentExp = originalExp;
        }

        int currentLevel =
            manager.GetCurrentUpgradeLevel(target);

        Debug.Log(
            $"[Debug Upgrade] {target.upgradeName} " +
            $"Lv.{currentLevel}");
    }
}

#endif
