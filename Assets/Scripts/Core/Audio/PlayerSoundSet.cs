using UnityEngine;

/// <summary>
/// 플레이어 몸에서 나는 소리.
///
/// `GameSoundSet`(게임 이벤트)과 나눈 이유: 플레이어는 `DontDestroyOnLoad` 로 씬을 넘어
/// 살아 있는데, `SoundManager` 는 씬마다 새로 생긴다.
/// 플레이어가 쓰는 클립은 **플레이어 프리팹이 직접 들고 있어야** 씬 전환에 흔들리지 않는다.
///
/// 항목 형식은 `GameSoundSet.Entry` 를 그대로 쓴다 —
/// 변형 로테이션 · 피치 흔들기 · 최소 간격이 이미 들어 있다.
/// </summary>
[CreateAssetMenu(menuName = "Audio/Player Sound Set", order = 1)]
public class PlayerSoundSet : ScriptableObject
{
    [Header("이동")]
    [Tooltip("걷기 · 달리기 공용. 이동 거리로 간격을 잡으므로 빨리 달리면 저절로 잦아진다")]
    public GameSoundSet.Entry footstep = new GameSoundSet.Entry();

    public GameSoundSet.Entry jump = new GameSoundSet.Entry();
    public GameSoundSet.Entry land = new GameSoundSet.Entry();

    [Tooltip("구르기 — 옷 스치는 소리")]
    public GameSoundSet.Entry roll = new GameSoundSet.Entry();

    [Header("스태미나")]
    [Tooltip("바닥나 달리기가 풀릴 때. HUD 색 변화만으로는 약하다")]
    public GameSoundSet.Entry exhausted = new GameSoundSet.Entry();

    [Tooltip("다시 달릴 수 있게 됐을 때")]
    public GameSoundSet.Entry staminaReady = new GameSoundSet.Entry();
}
