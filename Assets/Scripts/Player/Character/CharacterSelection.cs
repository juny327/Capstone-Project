using UnityEngine;

/// <summary>
/// 로비에서 고른 캐릭터를 스테이지 씬까지 들고 가는 정적 보관소 (설계 4-6).
///
/// 무기 선택은 GameAppManager.SelectedWeapon 이 들고 가지만, GameAppManager 는 Core 라 고치지 않는다.
/// 비어 있으면(스테이지 씬을 에디터에서 바로 Play) CharacterLoadout 이 기본 캐릭터(사수)로 떨어진다.
/// </summary>
public static class CharacterSelection
{
    public static CharacterData Current { get; private set; }

    public static void Select(CharacterData data) => Current = data;

    // Enter Play Mode Options 에서 도메인 리로드를 꺼도 이전 Play 의 선택이 남지 않게 한다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => Current = null;
}
