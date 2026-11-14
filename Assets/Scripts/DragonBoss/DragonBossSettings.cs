using UnityEngine;

/// <summary>
/// 본 게임 보스 설정 (2026-11-14) — Resources/Dragon/DragonBossSettings.asset.
/// useInMainGame 을 켜 두면 3스테이지 다음 보스가 골렘 대신 드래곤이다 (DragonBossRoute 가 읽는다).
/// 끄면 예전 그대로 — 골렘 인트로 씬 → 골렘. 에셋이 없어도 골렘이다.
/// 만드는 도구: Tools/Dragon Boss/본 게임 보스로 연결 (DragonBossSetup.LinkMainGame) — 이미 있으면 스위치는 건드리지 않는다.
/// </summary>
public class DragonBossSettings : ScriptableObject
{
    public const string ResourcePath = "Dragon/DragonBossSettings";

    [Tooltip("켜면 본 게임(3스테이지 다음) 보스가 드래곤 — 끄면 골렘 (인트로 씬 포함 예전 그대로)")]
    public bool useInMainGame = true;

    [Tooltip("보스 스테이지에 들어가며 등장 연출(격리 장치 파괴 → 낙하 → 포효)을 보여 준다 — 골렘 인트로 씬을 대신한다")]
    public bool playEntrance = true;

    [Tooltip("드래곤 보스 프리팹 (Assets/Prefabs/DragonBoss/DragonBoss.prefab)")]
    public GameObject bossPrefab;
}
