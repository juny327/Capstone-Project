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

    /// <summary>
    /// 난이도별 보스 값 (2026-11-20 보스 패턴 개선) — 보스가 깨어날 때 고른 난이도의 묶음을 읽는다.
    /// 그 전에는 두 난이도의 보스가 똑같이 움직였다 (다른 것은 체력 · 받는 피해 · 부하 체력뿐 — DifficultySettings).
    ///   하드 — 옆걸음 하나로는 안 풀리게: 늦게까지 따라오다 굳히는 조준 · 앞질러 쏘기 · 둘러싸는 불비 · 2페이즈부터 연계
    ///   이지 — 조준은 예전 그대로(예고를 보고 옆으로 걸으면 피해진다) · 패턴이 느리게 진행되고 쉴 틈이 길다
    /// 페이즈별 값은 x · y · z = 1 · 2 · 3페이즈.
    /// </summary>
    [System.Serializable]
    public class Tuning
    {
        [Header("흐름")]
        [Tooltip("패턴 진행 속도 — 1 보다 작으면 동작 · 예고 · 판정이 같이 느려진다 (예고가 그만큼 길어진다)")]
        [Range(0.5f, 1.5f)] public float tempo = 1f;
        [Tooltip("패턴 사이 빈틈 배율 — 페이즈 1 · 2 · 3")]
        public Vector3 recoverScale = Vector3.one;
        [Tooltip("패턴 쿨타임 배율")]
        [Min(0.1f)] public float cooldownScale = 1f;
        [Tooltip("휩쓸기 브레스(3페이즈)가 한쪽 끝에서 반대쪽 끝까지 가는 시간 (초) — 길수록 불이 천천히 온다")]
        [Min(0.5f)] public float sweepTime = 1.9f;
        [Tooltip("연계 확률 (앞 패턴이 끝나자마자 빈틈 없이 다음 패턴) — 페이즈 1 · 2 · 3")]
        public Vector3 comboChance = new Vector3(0f, 0f, 0.55f);

        [Header("조준")]
        [Tooltip("물기 · 지연 물기 · 돌진이 예고 동안 플레이어를 따라 돌다가 판정 직전에만 방향을 굳힌다. 끄면 예고 시작 때의 방향 그대로")]
        public bool lateLock;
        [Tooltip("물기가 방향을 굳힌 뒤 판정까지 (초) — 페이즈 1 · 2 · 3. 짧을수록 어렵다 (걷기 초속 3m 로 2.9m 를 벗어나야 한다)")]
        public Vector3 biteLock = new Vector3(0.75f, 0.57f, 0.46f);
        [Tooltip("돌진이 방향을 굳힌 뒤 플레이어 자리에 닿기까지 (초) — 멀수록 그만큼 늦게 굳힌다 (굳힌 뒤 출발까지 적어도 0.2초). "
                 + "길 폭 절반이 2.55m 라, 0.7초면 걷던 사람(초속 3 ~ 3.3m)은 2.1 ~ 2.3m 로 못 벗어나고 달리면(4.8m) 벗어난다. 구르기 · 패링은 언제나 통한다")]
        [Min(0.1f)] public float chargeLock = 0.7f;
        [Tooltip("따라 도는 빠르기 (도/초)")]
        [Min(0f)] public float trackTurn = 200f;
        [Tooltip("불덩이 — 둘째 발은 가는 쪽을 앞질러, 셋째 발은 그 절반을 앞질러 쏜다. 끄면 세 발 모두 지금 자리")]
        public bool leadFireballs;
        [Tooltip("브레스가 뿜는 동안 따라 도는 빠르기 (도/초) — 2페이즈부터. 0 이면 고정")]
        [Min(0f)] public float breathTrack;
        [Tooltip("휩쓸기 브레스가 플레이어가 가는 쪽에서 시작해 마주 온다. 끄면 무작위")]
        public bool sweepMeets;
        [Tooltip("불비가 플레이어를 둘러싸고 한 군데만 비운다 (가는 쪽은 막는다). 끄면 예전처럼 흩뿌린다")]
        public bool rainRing;
        [Tooltip("흩뿌리는 불비의 원 수 (둘러싸는 불비는 가운데 1 + 둘레 5)")]
        [Min(1)] public int rainCount = 7;

        [Header("부하 · 불 장판")]
        [Min(0)] public int minionsPhase2 = 3;
        [Min(0)] public int minionsPhase3 = 4;
        [Tooltip("살아 있는 부하가 이만큼이면 더 부르지 않는다")]
        [Min(0)] public int minionCap = 6;
        [Min(0.5f)] public float firePatchLife = 6f;
        [Min(0)] public int firePatchMax = 14;

        [Header("새 패턴 (2단계 — 불의 벽 · 퍼지는 고리 · 도약 내려찍기)")]
        public bool newPatterns;
    }

    [Tooltip("이지 — 조준은 예전 그대로, 패턴이 느리고 쉴 틈이 길다")]
    public Tuning easy = DefaultEasy();
    [Tooltip("하드 — 옆걸음 하나로는 안 풀린다")]
    public Tuning hard = DefaultHard();

    public Tuning Get(DifficultyMode mode) => mode == DifficultyMode.Hard ? hard : easy;

    /// <summary>고치기 전 움직임 그대로 (2026-11-19 까지 두 난이도 공통) — 측정 기준선 · 비교용.</summary>
    public static Tuning Legacy() => new Tuning();

    public static Tuning DefaultEasy() => new Tuning
    {
        tempo = 0.85f,
        recoverScale = new Vector3(1.4f, 1.4f, 1.4f),
        cooldownScale = 1.25f,
        sweepTime = 2.3f,
        comboChance = new Vector3(0f, 0f, 0.25f),
        rainCount = 5,
        minionsPhase2 = 2, minionsPhase3 = 3, minionCap = 4,
        firePatchLife = 4f, firePatchMax = 8,
    };

    public static Tuning DefaultHard() => new Tuning
    {
        recoverScale = new Vector3(1f, 0.85f, 0.7f),   // 쉴 틈이 페이즈마다 15%씩 줄어든다
        comboChance = new Vector3(0f, 0.35f, 0.55f),
        lateLock = true,
        leadFireballs = true,
        breathTrack = 15f,
        sweepMeets = true,
        rainRing = true,
        newPatterns = true,
    };
}
