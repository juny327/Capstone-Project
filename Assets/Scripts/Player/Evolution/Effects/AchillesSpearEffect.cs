using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 상위 무기 "아킬레우스의 창" (창 + 표창 — 팀원 스킬, 2026-10-31) — 네 번째 찌르기마다 창을 던진다.
/// 창은 날아가며 꿰뚫고, 던진 거리 끝에서 캐릭터를 중심으로 원을 그리며 orbitLaps 바퀴 돈 뒤 손으로 돌아온다.
///
///  · 바탕: 하데스 「바라타(Varatha)」 — 던지고 불러들이는 창 (상위무기-계획.md 16-17)
///  · 던진 동안 손에 창이 없다 — 손의 창을 숨기고 공격하지 않는다 (WeaponBase.HeldAway). 한 번에 하나
///  · 날아가다 벽 · 엄폐물에 닿으면 거기까지가 궤도 반경 (탄과 같은 규칙). 도는 동안은 캐릭터를 따라간다
///  · 2026-11-01 사용자 요청으로 "떨어진 자리에서 제자리 회오리" → "캐릭터 둘레 궤도"로 바꿨다
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Achilles Spear", fileName = "AE_EVO_AchillesSpear")]
public class AchillesSpearEffect : AttachmentEffect
{
    [Header("던지기")]
    [Tooltip("이 횟수째 찌르기마다 던진다")]
    [Min(1)] public int throwEvery = 4;
    [Tooltip("던지는 거리 = 도는 원의 반경")]
    [Min(1f)] public float throwDistance = 6.5f;
    [Min(1f)] public float flySpeed = 32f;
    [Tooltip("날아가며 꿰뚫는 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float throwDamageMul = 1.5f;
    [Tooltip("창의 판정 반경 (날아갈 때 · 돌 때 · 돌아올 때)")]
    [Min(0.2f)] public float hitRadius = 0.8f;

    [Header("궤도 — 캐릭터 둘레")]
    [Min(0.5f)] public float orbitLaps = 2f;
    [Tooltip("다 도는 데 걸리는 시간")]
    [Min(0.2f)] public float orbitDuration = 1.4f;
    [Tooltip("한 바퀴마다 원 위의 적을 한 번 벤다 — 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float orbitDamageMul = 0.8f;
    [Tooltip("true 면 시계 방향 (위에서 볼 때)")]
    public bool clockwise = true;

    [Header("회수")]
    [Min(1f)] public float returnSpeed = 28f;
    [Tooltip("돌아오며 베는 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float returnDamageMul = 1f;

    [Header("연출")]
    [Tooltip("궤적 · 고리 재질")]
    public Material material;
    public Color color = new Color(1f, 0.62f, 0.28f, 1f);
    [Tooltip("창이 나는 높이 (바닥 기준)")]
    [Min(0f)] public float flyHeight = 1f;

    [Header("소리")]
    public GameSoundSet.Entry throwSound = new GameSoundSet.Entry();
    [Tooltip("궤도에 들어서는 순간")]
    public GameSoundSet.Entry landSound = new GameSoundSet.Entry();
    [Tooltip("도는 동안 반 바퀴마다 — 칼바람")]
    [FormerlySerializedAs("spinSound")]
    public GameSoundSet.Entry orbitSound = new GameSoundSet.Entry();
    public GameSoundSet.Entry catchSound = new GameSoundSet.Entry();

    /// <summary>던진 수 · 단계별 맞힌 수 · 돈 바퀴 (테스트).</summary>
    public static int Thrown { get; internal set; }
    public static int ThrowHits { get; internal set; }
    public static int OrbitHits { get; internal set; }
    public static int ReturnHits { get; internal set; }
    public static int Caught { get; internal set; }
    public static float LastLaps { get; internal set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Thrown = 0; ThrowHits = 0; OrbitHits = 0; ReturnHits = 0; Caught = 0; LastLaps = 0f; }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly AchillesSpearEffect cfg;
        ThrownSpear active;
        int count;

        public Runtime(AchillesSpearEffect cfg) => this.cfg = cfg;

        public override void OnAttack(in WeaponAttack attack)
        {
            if (Weapon == null || Weapon.Controller == null) return;

            count++;
            if (count < cfg.throwEvery || active != null) return;   // 창이 돌아오기 전이면 다음 찌르기로 미룬다
            count = 0;

            Vector3 dir = EvoStrikes.Flat(attack.Direction, Weapon.Controller.transform.forward);
            active = ThrownSpear.Launch(cfg, Weapon, Weapon.Controller.transform, dir, Weapon.Stats.Damage);
            Thrown++;
        }

        public override void Dispose()
        {
            // 무기가 사라져도 날아간 창은 끝까지 간다 (주인이 없으면 스스로 지우고 손 창을 되살린다)
            active = null;
        }
    }
}
