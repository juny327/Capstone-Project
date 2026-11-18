using UnityEngine;

/// <summary>
/// 상위 무기 "돌풍 저격총" (스나이퍼 + 토네이도 — 팀원 스킬, 2026-10-31) — 탄이 지나간 길에 바람길이 남는다.
/// 그 위의 적은 느려지고 tickInterval 마다 베인다. 탄이 날아가는 동안은 탄 둘레로 바람이 휘감긴다 (WindVortex — 탄 프리팹).
///
///  · 바탕: 원신 벤티 활의 바람 차지 샷 · 엘든링 폭풍 계열 전회 (상위무기-계획.md 16-17)
///  · 길은 총구 → 벽 · 사거리 끝까지. 벽 너머로는 생기지 않는다 (탄과 같은 규칙)
///  · 모양은 바닥 띠 + 길 따라 흘러가는 바람 줄기 (2026-11-01 — 지그재그 줄기가 전기처럼 보여 바꿨고, 길 끝 회오리는 뺐다)
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Gale Lane", fileName = "AE_EVO_GaleLane")]
public class GaleLaneEffect : AttachmentEffect
{
    [Header("바람길")]
    [Min(0.2f)] public float duration = 2f;
    [Tooltip("길 폭의 절반 (m)")]
    [Min(0.2f)] public float halfWidth = 0.9f;
    [Min(0.05f)] public float tickInterval = 0.3f;
    [Tooltip("한 번 베는 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float tickDamageMul = 0.15f;
    [Tooltip("길 위 적의 이동 속도 배율 (0.6 = 40% 느리게)")]
    [Range(0.1f, 1f)] public float slowFactor = 0.6f;
    [Tooltip("길에서 벗어난 뒤에도 느린 시간")]
    [Min(0f)] public float slowLinger = 0.35f;
    [Tooltip("길 최대 길이 — 저격탄은 수백 m 를 날아가므로 화면 안쪽까지만")]
    [Min(2f)] public float maxLength = 14f;
    [Tooltip("동시에 남는 길 수 — 넘으면 가장 오래된 것부터 지운다")]
    [Min(1)] public int maxLanes = 3;

    [Header("연출")]
    public Material material;
    public Color color = new Color(0.55f, 1f, 0.85f, 1f);
    [Tooltip("길 끝의 회오리 (팀원 토네이도 이펙트) — 2026-11-01 부터 비워 둔다 (탄 둘레 소용돌이로 바꿈). 넣으면 다시 선다")]
    public GameObject tornadoPrefab;
    [Min(0f)] public float tornadoScale = 1.6f;
    [Tooltip("바람 줄기를 하나씩 띄우는 간격")]
    [Min(0.02f)] public float streakInterval = 0.07f;
    [Tooltip("바람 줄기가 길 방향으로 흘러가는 속도 (m/s)")]
    [Min(0f)] public float gustSpeed = 9f;
    [Tooltip("바닥 띠 진하기 — 길이 어디까지인지 보이게")]
    [Range(0f, 1f)] public float bandAlpha = 0.35f;

    [Header("소리")]
    [Tooltip("길이 있는 동안 나는 바람 소리 (팀원 토네이도 스킬 소리) — 길마다 하나, 끝날 때 줄어든다")]
    public AudioClip windClip;
    [Range(0f, 1f)] public float windVolume = 0.22f;
    public GameSoundSet.Entry openSound = new GameSoundSet.Entry();

    /// <summary>만든 길 수 · 벤 횟수 (테스트).</summary>
    public static int Spawned { get; private set; }
    public static int Ticks { get; internal set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Spawned = 0; Ticks = 0; }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly GaleLaneEffect cfg;

        public Runtime(GaleLaneEffect cfg) => this.cfg = cfg;

        public override void OnAttack(in WeaponAttack attack)
        {
            if (Weapon == null) return;

            Vector3 dir = EvoStrikes.Flat(attack.Direction, Vector3.forward);

            // 총은 사거리(Range)가 없다 — 탄 속도 × 수명이 사거리
            WeaponRuntimeStats stats = Weapon.Stats;
            float range = attack.Range > 0f ? attack.Range : stats.ProjectileSpeed * stats.ProjectileLifeTime;
            float reach = CombatFx.ObstacleDistance(attack.Origin, dir, Mathf.Min(range, cfg.maxLength));
            if (reach < 0.5f) return;

            // 길은 바닥에 — 적 판정 기둥(바닥 기준)과 같은 높이
            Vector3 start = attack.Origin;
            start.y = Weapon.Controller != null ? Weapon.Controller.transform.position.y : attack.Origin.y - 1.2f;

            GaleLane.Spawn(cfg, start, dir, reach, stats.Damage * cfg.tickDamageMul);
            Spawned++;
        }
    }
}
