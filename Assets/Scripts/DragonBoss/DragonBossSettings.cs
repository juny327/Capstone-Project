using UnityEngine;

/// <summary>
/// 본 게임 보스 설정 (2026-11-14) — Resources/Dragon/DragonBossSettings.asset.
/// 보스 스테이지가 열리면 DragonBossRoute 가 여기 적힌 프리팹을 세운다 (3스테이지 다음 보스).
/// 만드는 도구: Tools/Dragon Boss/본 게임 보스로 연결 (DragonBossSetup.LinkMainGame) — 이미 있으면 값은 건드리지 않는다.
/// </summary>
public class DragonBossSettings : ScriptableObject
{
    public const string ResourcePath = "Dragon/DragonBossSettings";

    [Tooltip("보스 스테이지에 들어가며 등장 연출(격리 장치 파괴 → 낙하 → 포효)을 보여 준다 — 끄면 바로 전투")]
    public bool playEntrance = true;

    [Tooltip("보스 프리팹 (Assets/Prefabs/DragonBoss/DragonBoss.prefab)")]
    public GameObject bossPrefab;
}
