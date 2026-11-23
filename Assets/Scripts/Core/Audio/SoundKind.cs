using UnityEngine;

/// <summary>
/// 소리의 종류 (2026-11-19 효과음 재정비 1단계). 종류마다 동시에 나는 수와 중요도를 따로 둔다 — SoundManager.
///
/// 중요도는 계획 문서(소리 · 상위 무기 연출 개선 계획 — 효과음 전면 재정비)의 규칙을 따른다:
/// 내 피격(전용 칸) &gt; 처치 &gt; 치명타 &gt; 맞힘 &gt; 발사 &gt; 보조 무기 &gt; 환경.
/// 칸이 꽉 차면 덜 중요한 종류부터 끊는다 — 총소리가 아무리 많아도 처치음 · 패링음은 끊기지 않는다.
/// </summary>
public enum SoundKind
{
    /// <summary>부르는 쪽이 정하지 않았다 — UI 그룹이면 UI, PlayClip 이면 Event, 그 밖은 Ability.</summary>
    Auto = 0,
    /// <summary>버튼 · 레벨업 · 보상 · 정비 창 · 보급 신호.</summary>
    UI,
    /// <summary>한 번뿐인 큰 순간 — 진화 · 보스 등장 · 플레이어 사망 · 엔딩.</summary>
    Event,
    /// <summary>처치.</summary>
    Kill,
    /// <summary>패링 · 떠 있는 무기의 패링 연계.</summary>
    Parry,
    /// <summary>플레이어 몸 — 점프 · 착지 · 구르기 · 스태미나 (내 피격은 전용 칸, 발소리는 Footstep).</summary>
    Player,
    /// <summary>치명타.</summary>
    Crit,
    /// <summary>맞힘 (총알 · 근접 · 레이저 · 전기 · 무기 전용 적중 층).</summary>
    Hit,
    /// <summary>폭발 (유탄 · 폭발탄 · 연쇄 폭발 · 공성포 · 중력장 붕괴).</summary>
    Explosion,
    /// <summary>손에 든 무기 — 발사 · 휘두름 · 베기 · 장전.</summary>
    Fire,
    /// <summary>팀원 스킬 — 시전 · 명중.</summary>
    Skill,
    /// <summary>부착물 효과 · 서브 능력 · 상위 무기 효과 · 연계.</summary>
    Ability,
    /// <summary>보조 칸 무기 — 떠 있는 총 · 드론 · 떠 있는 근접.</summary>
    SupportFire,
    /// <summary>코인 줍기.</summary>
    Pickup,
    /// <summary>발소리.</summary>
    Footstep,
    /// <summary>환경 — 총알이 벽에 막힘 등.</summary>
    Ambient,
}

/// <summary>
/// 소리 층 하나 (2026-11-19). 한 동작에 여러 층(순간음 · 저음 몸통 · 꼬리)을 함께 낼 때 쓴다 — SoundManager.PlayStack.
/// delay 가 0 이면 같은 순간, 0 보다 크면 그만큼 뒤에 시작한다 (오디오 시계로 맞춰 프레임과 상관없이 정확하다).
/// </summary>
[System.Serializable]
public class SoundLayer
{
    [Tooltip("이 층의 소리 — 여러 개면 무작위 · 음량 · 피치 범위 · 최소 간격은 다른 소리 칸과 같다")]
    public GameSoundSet.Entry sound = new GameSoundSet.Entry();

    [Tooltip("동작 순간부터 이 층이 시작되기까지의 시간(초). 꼬리 · 잔향을 조금 늦게 깔 때")]
    [Min(0f)] public float delay;
}
