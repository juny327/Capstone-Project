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
        // 2026-11-23 사용자: "보스가 패턴으로 소환하는 근접 몬스터 체력 3배 정도"
        [Tooltip("부하 중 근접 몬스터(A 근접 · B 돌진 · D 자폭)의 체력 배율 — 보스 스테이지 체력 배율(×2.5) · 난이도 배율 위에 곱한다. 원거리(C · E)는 그대로")]
        [Min(0.1f)] public float minionMeleeHealth = 1f;
        [Min(0.5f)] public float firePatchLife = 6f;
        [Min(0)] public int firePatchMax = 14;

        [Header("새 패턴 (2단계 — 불의 벽 · 퍼지는 고리 · 도약 내려찍기)")]
        public bool newPatterns;

        // 2026-11-26 팀원(parkjunyoung) '보스 공격패턴 수정' — 코드에 박혀 있던 값을 난이도 값으로. 기본값(Legacy)은 그 전 동작.
        // 사용자: "보스 변경은 하드에 적용, 일부 수치는 조금 낮춰 이지에" → 하드 = 팀원 값, 이지 = 예전 값과 팀원 값의 중간
        [Header("팀원 보스 공격패턴 (11-26) — 하드 = 팀원 값 · 이지 = 중간")]
        [Tooltip("일반 물기 진행 빠르기 (예고 · 동작 · 판정이 같이). 2 면 예고가 절반 — 예전 1")]
        [Range(1f, 3f)] public float biteSpeed = 1f;
        [Tooltip("지연 물기 진행 빠르기 — 예전 1")]
        [Range(1f, 2f)] public float delayedBiteSpeed = 1f;
        [Tooltip("지연 물기 앞쪽 범위 배율 — 예전 1")]
        [Range(1f, 2f)] public float delayedBiteReach = 1f;
        [Tooltip("흩뿌리는 불비 수 배율 (rainCount 에 곱한다) — 예전 1")]
        [Range(1f, 3f)] public float rainCountScale = 1f;
        [Tooltip("불비를 고르는 무게 — x 중간 거리 · y 먼 거리 (예전 2 · 3)")]
        public Vector2 fireRainWeight = new Vector2(2f, 3f);
        [Tooltip("불비 쿨타임 (초, cooldownScale 을 곱한다) — 예전 10")]
        [Min(1f)] public float fireRainCooldown = 10f;
        [Tooltip("불비가 떨어진 자리에 남는 작은 불 장판의 피해 (0.9초마다). 0 이면 안 남는다 — 예전 0")]
        [Min(0f)] public float rainPatchDamage = 0f;
        [Tooltip("그 불 장판이 남는 시간 (초, firePatchLife 보다 길지 않게)")]
        [Min(0.5f)] public float rainPatchLife = 3.5f;
        [Tooltip("꼬리 휩쓸기에 맞으면 밀려나는 빠르기 (m/s). 0 이면 안 밀린다 — 예전 0")]
        [Min(0f)] public float tailKnockback = 0f;
        [Tooltip("밀려나는 시간 (초)")]
        [Min(0f)] public float tailKnockbackTime = 0.35f;
        [Tooltip("2페이즈부터 꼬리 휩쓸기 뒤 바로 돌진 — 그 돌진의 예고 (초). 0 이면 이 연계를 하지 않는다 — 예전 0")]
        [Min(0f)] public float tailChargeWindup = 0f;

        [Header("마지막 발악 — 남은 체력이 Staging.enrageAt 아래로 내려가면")]
        [Tooltip("패턴 진행 속도에 곱한다 (1 = 없음). 몸이 달아오르는 표시는 연출(Staging)이 낸다")]
        [Range(1f, 1.5f)] public float enrageTempo = 1f;
    }

    /// <summary>
    /// 보스 연출 (2026-11-20) — 화면 흔들림 · 공격 이펙트 · 압박감. 두 난이도 공통.
    /// 판정 · 패턴에는 손대지 않는다 (마지막 발악의 속도만 난이도 값 Tuning.enrageTempo).
    /// </summary>
    [System.Serializable]
    public class Staging
    {
        [Tooltip("연출 개선 전체 — 끄면 2026-11-20 이전 그대로 (세기만 다른 흔들림 하나 · 이펙트 없음)")]
        public bool enabled = true;

        [Header("화면 흔들림 — 설정 창의 '화면 흔들림' 을 따른다")]
        [Range(0f, 2f)] public float shakeScale = 1f;
        [Tooltip("플레이어가 이 거리(m) 안이면 그대로")]
        [Min(0f)] public float nearRange = 8f;
        [Tooltip("이 거리(m) 밖이면 farShake 배 — 가까이 있을수록 위협적이다")]
        [Min(1f)] public float farRange = 16f;
        [Range(0f, 1f)] public float farShake = 0.5f;
        [Tooltip("발걸음 흔들림 — 멀 때(stepRange) · 바로 옆일 때. 다가오는 것이 화면으로 느껴진다")]
        public Vector2 stepShake = new Vector2(0.05f, 0.16f);
        [Tooltip("이 거리(m) 밖의 발걸음은 흔들지 않는다")]
        [Min(1f)] public float stepRange = 15f;
        [Tooltip("큰 한 방(땅 물어뜯기 · 벽 충돌 · 도약 착지 · 포효)에 시야를 당기는 비율")]
        [Range(0f, 0.1f)] public float zoom = 0.035f;
        [Tooltip("큰 한 방에 화면 가장자리가 불빛으로 물드는 세기")]
        [Range(0f, 1f)] public float edgeFlash = 0.12f;
        [Tooltip("이어지는 떨림 — 불을 뿜는 동안 · 돌진으로 달리는 동안 · 포효하는 동안")]
        [Range(0f, 0.6f)] public float tremor = 0.16f;

        [Header("공격 이펙트 — 무기 연출과 같은 입자 재료(FxKit)")]
        public bool attackFx = true;
        [Tooltip("이펙트 크기 · 양 배율")]
        [Range(0.3f, 2f)] public float fxScale = 1f;
        [Tooltip("페이즈가 몸에 보인다 — 2페이즈 입가의 불티 · 3페이즈 몸 둘레의 열기")]
        public bool bodyMarks = true;
        [Tooltip("큰 한 방을 맞으면 몸이 번쩍이고 움찔한다 (패턴은 끊지 않는다)")]
        public bool flinch = true;
        [Tooltip("한 번에 최대 체력의 이 비율 넘게 깎이면 큰 한 방")]
        [Range(0.002f, 0.1f)] public float flinchDamage = 0.012f;
        [Tooltip("예고가 다 차기 직전에 테두리가 빠르게 깜박인다")]
        public bool telegraphBlink = true;

        [Header("압박감")]
        public bool pressure = true;
        [Tooltip("보스전 동안 시야를 넓히는 배율 (1 = 그대로)")]
        [Range(1f, 1.3f)] public float cameraWide = 1.1f;
        [Tooltip("큰 공격을 준비할 때 시야를 당기는 비율 — 판정 뒤 돌아온다")]
        [Range(0f, 0.08f)] public float windupLean = 0.035f;
        [Tooltip("화면 가장자리가 어두워지는 세기 — 1 · 2 · 3페이즈 (붉은색은 '맞았다' 전용이라 쓰지 않는다)")]
        public Vector3 edgeDark = new Vector3(0f, 0.28f, 0.5f);
        [Tooltip("큰 공격을 준비할 때 가장자리가 한 번 맥박친다 (더해지는 세기)")]
        [Range(0f, 0.6f)] public float edgePulse = 0.22f;
        [Tooltip("큰 공격을 준비할 때 배경음을 잠깐 줄인다")]
        public bool windupDuck = true;
        [Tooltip("예고가 캐릭터를 덮으면 발밑 고리가 붉어진다")]
        public bool dangerRing = true;
        [Tooltip("화면 밖의 보스가 공격을 시작하면 가장자리 화살표로 알린다")]
        public bool edgeArrow = true;
        [Tooltip("페이즈가 바뀔 때 체력바가 번쩍이며 흔들린다")]
        public bool barBreak = true;
        [Tooltip("남은 체력이 이 비율 아래면 마지막 발악 — 몸이 달아오른다 (속도는 난이도 값)")]
        [Range(0f, 0.5f)] public float enrageAt = 0.1f;
    }

    [Tooltip("연출 — 화면 흔들림 · 공격 이펙트 · 압박감 (두 난이도 공통)")]
    public Staging staging = new Staging();

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
        minionMeleeHealth = 3f,
        firePatchLife = 4f, firePatchMax = 8,
        // 11-26 팀원 보스 공격패턴 — 예전 값과 팀원(하드) 값의 중간
        biteSpeed = 1.5f, delayedBiteSpeed = 1.17f, delayedBiteReach = 1.17f, rainCountScale = 1.5f,
        fireRainWeight = new Vector2(3.5f, 4.5f), fireRainCooldown = 8.75f,
        rainPatchDamage = 1f, rainPatchLife = 2.5f, tailKnockback = 5.5f, tailKnockbackTime = 0.35f, tailChargeWindup = 0.75f,
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
        minionMeleeHealth = 3f,
        enrageTempo = 1.1f,
        // 11-26 팀원 보스 공격패턴 — 팀원 값 그대로
        biteSpeed = 2f, delayedBiteSpeed = 4f / 3f, delayedBiteReach = 4f / 3f, rainCountScale = 2f,
        fireRainWeight = new Vector2(5f, 6f), fireRainCooldown = 7.5f,
        rainPatchDamage = 2f, rainPatchLife = 3.5f, tailKnockback = 11f, tailKnockbackTime = 0.35f, tailChargeWindup = 0.5f,
    };
}
