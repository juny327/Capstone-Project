using UnityEngine;

/// <summary>드래곤 보스 소리 칸 (2026-11-14) — 동작 · 패턴 · 등장 연출의 어느 순간에 나는지는 DragonSoundSet 의 설명 참고.</summary>
public enum DragonSfx
{
    Roar, Windup, Bite, Idle, Step, Death,
    Breath, Fireball, Rain, Ignite, Burn,
    Charge, Crash, Tail, Wing, Slam,
    Alarm, Crack, Land, Card,
}

/// <summary>
/// 드래곤 보스 소리 묶음 (2026-11-14) — 사용자가 미리듣기 페이지에서 고른 20개.
/// Resources/Dragon/DragonSoundSet 에 두고 DragonSound 가 읽는다 (프리팹 · 씬을 건드리지 않는다).
/// 클립 · 음량 · 높낮이 범위 · 최소 간격은 여기서 바꾼다. `Tools/Dragon Boss/드래곤 소리 넣기` 는 빈 칸만 채운다.
/// </summary>
[CreateAssetMenu(menuName = "Audio/Dragon Sound Set", fileName = "DragonSoundSet")]
public class DragonSoundSet : ScriptableObject
{
    [Header("목소리 (동작)")]
    [Tooltip("큰 포효 — 죽음 동작 앞부분(머리를 들고 입 벌림): 페이즈 전환 · 등장 연출 · 불비 · 부하 소환")]
    public GameSoundSet.Entry roar = new GameSoundSet.Entry();
    [Tooltip("준비음 — 공격 동작에서 목을 당길 때: 물기류 전부 · 브레스 · 불덩이 · 돌진 · 꼬리 휩쓸기")]
    public GameSoundSet.Entry windup = new GameSoundSet.Entry();
    [Tooltip("물기 — 턱이 닫히는 순간 (판정): 물기 · 지연 물기 · 연속 물기 · 땅 물어뜯기")]
    public GameSoundSet.Entry bite = new GameSoundSet.Entry();
    [Tooltip("낮은 그르렁 — 대기 · 추적 중 8 ~ 12초에 한 번")]
    public GameSoundSet.Entry idle = new GameSoundSet.Entry();
    [Tooltip("발소리 — 걷기 동작 한 바퀴에 두 번")]
    public GameSoundSet.Entry step = new GameSoundSet.Entry();
    [Tooltip("죽음 — 울음이 낮아지다 2초쯤 쓰러지는 쿵 (죽음 동작 전체)")]
    public GameSoundSet.Entry death = new GameSoundSet.Entry();

    [Header("불")]
    [Tooltip("불 브레스 — 불이 나가는 동안 (끝나면 줄어들며 끊긴다): 브레스 · 휩쓸기 브레스 · 물러나서 브레스")]
    public GameSoundSet.Entry breath = new GameSoundSet.Entry();
    [Tooltip("불덩이 발사 — 한 발마다")]
    public GameSoundSet.Entry fireball = new GameSoundSet.Entry();
    [Tooltip("불비 — 떨어지기 시작할 때 (0.4초 휘익 뒤 착탄이 들어 있다)")]
    public GameSoundSet.Entry rain = new GameSoundSet.Entry();
    [Tooltip("불 붙는 소리 — 불 장판이 생길 때 · 부하가 나올 때")]
    public GameSoundSet.Entry ignite = new GameSoundSet.Entry();
    [Tooltip("타는 소리 (반복) — 불 장판이 하나라도 남아 있는 동안")]
    public GameSoundSet.Entry burn = new GameSoundSet.Entry();

    [Header("몸 · 타격")]
    [Tooltip("돌진 — 웅크림이 끝나고 달려 나갈 때")]
    public GameSoundSet.Entry charge = new GameSoundSet.Entry();
    [Tooltip("벽 충돌 — 돌진이 벽에 막혔을 때")]
    public GameSoundSet.Entry crash = new GameSoundSet.Entry();
    [Tooltip("꼬리 휩쓸기 — 몸이 한 바퀴 도는 순간")]
    public GameSoundSet.Entry tail = new GameSoundSet.Entry();
    [Tooltip("날개 펄럭 — 뒤로 물러날 때")]
    public GameSoundSet.Entry wing = new GameSoundSet.Entry();
    [Tooltip("내려찍기 — 땅 물어뜯기의 충격파")]
    public GameSoundSet.Entry slam = new GameSoundSet.Entry();

    [Header("등장 연출")]
    [Tooltip("경보 — 연출 시작 · 1.4초 (두 번)")]
    public GameSoundSet.Entry alarm = new GameSoundSet.Entry();
    [Tooltip("격리 장치 균열 — 1.2초")]
    public GameSoundSet.Entry crack = new GameSoundSet.Entry();
    [Tooltip("낙하 · 착지 — 3.0초에 시작 (0.6초 휘익 뒤 착지 충격이 들어 있다)")]
    public GameSoundSet.Entry land = new GameSoundSet.Entry();
    [Tooltip("이름 카드 — 6.2초")]
    public GameSoundSet.Entry card = new GameSoundSet.Entry();

    public GameSoundSet.Entry Get(DragonSfx sfx) => sfx switch
    {
        DragonSfx.Roar => roar, DragonSfx.Windup => windup, DragonSfx.Bite => bite, DragonSfx.Idle => idle, DragonSfx.Step => step, DragonSfx.Death => death,
        DragonSfx.Breath => breath, DragonSfx.Fireball => fireball, DragonSfx.Rain => rain, DragonSfx.Ignite => ignite, DragonSfx.Burn => burn,
        DragonSfx.Charge => charge, DragonSfx.Crash => crash, DragonSfx.Tail => tail, DragonSfx.Wing => wing, DragonSfx.Slam => slam,
        DragonSfx.Alarm => alarm, DragonSfx.Crack => crack, DragonSfx.Land => land, DragonSfx.Card => card,
        _ => null,
    };
}
