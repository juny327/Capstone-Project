using UnityEngine;

/// <summary>
/// 끌어올 수 있는 대상 — 적(Enemy)이 아닌 것도 중력장에 끌리게 한다 (테스트 룸 무리 표적).
/// 적은 이것 없이 EnemyShove(내비메시 위 이동)로 끈다.
/// </summary>
public interface IPullable
{
    /// <summary>point 까지 duration 초 동안 옮긴다.</summary>
    void PullTo(Vector3 point, float duration);
}
