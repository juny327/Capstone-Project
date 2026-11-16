using UnityEngine;

/// <summary>큰 순간 — 화면 반응(히트 스톱 · 줌 · 흔들림 · 가장자리 번쩍)을 주는 곳 (2026-11-14 연출 개선 4).</summary>
public enum ImpactMoment
{
    SiegeFire,          // 공성포 발사 (밀림만)
    SiegeBlast,         // 공성포 포탄 폭발 (한 번 쏠 때 한 번)
    SunLanceFire,       // 태양창 발사
    GravityCollapse,    // 중력 대검 붕괴 폭발
    KillChainFinale,    // 처형자 큰 연쇄 (폭발 하나가 여럿을 쓰러뜨려 다음 단이 터지는 순간)
    BloodFrenzyStart,   // 혈검 폭주 시작
    PrismConverge,      // 프리즘 노바 수렴 광선 시작
    SpearCatch,         // 아킬레우스의 창 회수
    MeleeCrit,          // 근접 치명타 (패링 직후 한 방 포함)
    BossKill,           // 보스 체력이 0 이 되는 마지막 한 방 (엔딩 ①)
    BossBurst,          // 쓰러진 보스가 빛으로 터지는 순간 (엔딩 ②)
}

/// <summary>
/// 큰 한 방 손맛 설정 (2026-11-14 연출 개선 4) — Resources/Fx/ImpactFxSettings 한 곳에서 정한다.
/// 에셋이 없으면 전부 꺼져 예전 그대로다 (무기마다 가진 흔들림만 난다).
///
///  · 히트 스톱 — 아주 짧게 게임 시간을 거의 멈춘다 (패링과 같은 방식). <b>hitStop 하나로 통째로 끈다</b>
///  · 줌 펀치 — 카메라가 살짝 당겨졌다 돌아온다
///  · 흔들림 3종 — 쿵(짧고 큼 · 폭발) · 덜덜(길고 작음 · 광선 지속) · 밀림(쏘는 반대 방향 · 큰 발사)
///  · 가장자리 번쩍 — 화면 가장자리가 무기 테마 색으로 잠깐 물든다
///
/// 보조 칸의 무기는 히트 스톱 · 줌을 일으키지 않는다 (스스로 쏘는 무기가 화면을 멈추거나 당기면 조작이 끊긴다).
///  · 보조 전용 무기(공성포 · 태양창 · 프리즘 노바 — 손에 들 수 없다)는 번쩍 · 흔들림을 supportOnly* 비율로 낸다 (2026-11-15)
///  · 손에도 들 수 있는 무기가 보조 칸에 있을 때는 예전처럼 약한 흔들림만 (그 무기의 큰 순간은 손에 들었을 때 나온다)
/// </summary>
[CreateAssetMenu(menuName = "Fx/Impact Fx Settings", fileName = "ImpactFxSettings")]
public class ImpactFxSettings : ScriptableObject
{
    public const string ResourcePath = "Fx/ImpactFxSettings";

    [System.Serializable]
    public class Moment
    {
        [Tooltip("이 순간에 히트 스톱을 건다 (위의 hitStop 이 켜져 있을 때만)")]
        public bool hitStop;
        [Tooltip("줌 펀치 — 시야를 이 비율만큼 당긴다 (0.03 = 3%). 0 이면 없음")]
        [Range(0f, 0.1f)] public float zoom = 0.03f;
        [Tooltip("가장자리 번쩍 세기 (0~1). 0 이면 없음. 0.1 만 돼도 또렷하다 — 색을 선형 공간에서 섞어 낮은 값도 밝게 보인다")]
        [Range(0f, 1f)] public float flash = 0.1f;
        [Tooltip("쿵 — 짧고 큰 흔들림. 0 이면 무기 데이터의 원래 흔들림을 그대로 쓴다")]
        [Min(0f)] public float thud;
        [Tooltip("밀림 — 쏘는 반대 방향으로 밀리는 흔들림. 0 이면 없음")]
        [Min(0f)] public float kick;
        [Tooltip("이 순간에 배경음 · 다른 효과음을 잠깐 낮춘다 (큰 소리가 묻히지 않게)")]
        public bool duck;
    }

    [Header("히트 스톱 — 끄려면 hitStop 하나만 끈다")]
    [Tooltip("끄면 어느 순간에도 히트 스톱이 걸리지 않는다 (줌 · 흔들림 · 번쩍은 그대로)")]
    public bool hitStop = true;
    [Tooltip("멈추는 시간 (실제 시간 초)")]
    [Range(0.02f, 0.12f)] public float hitStopDuration = 0.06f;
    [Tooltip("멈춘 동안의 게임 속도 (0.05 = 20배 느리게)")]
    [Range(0.01f, 0.5f)] public float hitStopScale = 0.05f;
    [Tooltip("한 번 걸린 뒤 이 시간 안에는 다시 걸리지 않는다 (실제 시간 초)")]
    [Min(0.1f)] public float hitStopCooldown = 0.4f;

    [Header("줌 펀치")]
    [Tooltip("당겨졌다 돌아오는 데 걸리는 시간 (초)")]
    [Range(0.05f, 0.4f)] public float zoomReturn = 0.12f;
    [Tooltip("모든 줌에 곱한다 (0 이면 줌 없음)")]
    [Range(0f, 2f)] public float zoomScale = 1f;

    [Header("가장자리 번쩍")]
    [Range(0.05f, 0.4f)] public float flashDuration = 0.1f;
    [Tooltip("모든 번쩍에 곱한다 (0 이면 번쩍 없음)")]
    [Range(0f, 2f)] public float flashScale = 1f;

    [Header("흔들림 모양")]
    [Tooltip("쿵 — 흔들리는 시간 (초). 짧을수록 딱 끊긴다")]
    [Range(0.08f, 0.4f)] public float thudDuration = 0.16f;
    [Tooltip("덜덜 — 한 번의 길이 (초). 광선이 나가는 동안 이어서 낸다")]
    [Range(0.15f, 1f)] public float rumbleDuration = 0.45f;
    [Tooltip("밀림 — 밀렸다 돌아오는 시간 (초)")]
    [Range(0.08f, 0.5f)] public float kickDuration = 0.22f;
    [Tooltip("같은 순간이 이 시간 안에 다시 오면 줌 · 번쩍 · 쿵을 다시 내지 않는다 (연쇄 폭발 여러 개가 한꺼번에 터질 때)")]
    [Min(0f)] public float repeatGap = 0.2f;

    [Header("보조 전용 무기 (샷건 · 충전 레이저 · 전격 소총 계열 — 늘 보조 칸에서 스스로 쏜다)")]
    [Tooltip("보조 전용 무기의 큰 순간 — 가장자리 번쩍에 곱한다. 0 이면 번쩍 없음. 히트 스톱 · 줌은 이 값과 상관없이 주지 않는다")]
    [Range(0f, 1.5f)] public float supportOnlyFlash = 0.85f;
    [Tooltip("보조 전용 무기의 큰 순간 — 쿵 · 밀림 · 덜덜에 곱한다. 0 이면 예전처럼 약한 흔들림(보조 ×0.4)만")]
    [Range(0f, 1.5f)] public float supportOnlyShake = 0.7f;

    [Header("소리 낮춤 (연출 개선 5) — duck 을 켠 순간에")]
    [Tooltip("낮추는 시간 (초)")]
    [Range(0f, 1f)] public float duckTime = 0.3f;
    [Tooltip("그동안 배경음 배율")]
    [Range(0f, 1f)] public float duckMusic = 0.55f;
    [Tooltip("그동안 다른 효과음 배율")]
    [Range(0f, 1f)] public float duckSfx = 0.6f;

    [Header("큰 순간 9곳")]
    public Moment siegeFire = new Moment { zoom = 0f, flash = 0f, kick = 0.3f };
    public Moment siegeBlast = new Moment { zoom = 0.02f, flash = 0.06f, thud = 0.6f };
    public Moment sunLanceFire = new Moment { duck = true, zoom = 0.03f, flash = 0.11f, thud = 0.7f, kick = 0.3f };
    public Moment gravityCollapse = new Moment { duck = true, hitStop = true, zoom = 0.03f, flash = 0.11f, thud = 0.7f };
    public Moment killChainFinale = new Moment { duck = true, hitStop = true, zoom = 0.03f, flash = 0.11f, thud = 0.6f };
    public Moment bloodFrenzyStart = new Moment { duck = true, hitStop = true, zoom = 0.03f, flash = 0.14f, thud = 0.4f };
    public Moment prismConverge = new Moment { duck = true, zoom = 0.03f, flash = 0.11f, thud = 0.4f };
    public Moment spearCatch = new Moment { zoom = 0.015f, flash = 0.05f, thud = 0.2f };
    public Moment meleeCrit = new Moment { hitStop = true, zoom = 0.02f, flash = 0.07f };

    [Header("엔딩 (2026-11-15) — 보스를 쓰러뜨리는 순간")]
    public Moment bossKill = new Moment { duck = true, hitStop = true, zoom = 0.05f, flash = 0.16f, thud = 0.8f };
    public Moment bossBurst = new Moment { duck = true, zoom = 0.04f, flash = 0.2f, thud = 0.9f };

    public Moment Get(ImpactMoment moment)
    {
        switch (moment)
        {
            case ImpactMoment.SiegeFire: return siegeFire;
            case ImpactMoment.SiegeBlast: return siegeBlast;
            case ImpactMoment.SunLanceFire: return sunLanceFire;
            case ImpactMoment.GravityCollapse: return gravityCollapse;
            case ImpactMoment.KillChainFinale: return killChainFinale;
            case ImpactMoment.BloodFrenzyStart: return bloodFrenzyStart;
            case ImpactMoment.PrismConverge: return prismConverge;
            case ImpactMoment.SpearCatch: return spearCatch;
            case ImpactMoment.MeleeCrit: return meleeCrit;
            case ImpactMoment.BossKill: return bossKill;
            case ImpactMoment.BossBurst: return bossBurst;
            default: return null;
        }
    }
}
