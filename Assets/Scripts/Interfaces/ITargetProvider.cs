using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기가 "어떤 적을 노릴지"를 묻는 창구.
/// 감지 로직을 무기마다 복제하지 않기 위해 추상화한다.
///
/// GetTargets 가 버퍼를 인자로 받는 이유: 자동공격은 매 프레임 여러 무기가 호출한다.
/// 매번 새 List 를 반환하면 GC 압력이 생기므로 호출자가 버퍼를 재사용한다.
/// </summary>
public interface ITargetProvider
{
    /// <summary>감지 범위에 적이 하나라도 있는지.</summary>
    bool HasTarget { get; }

    /// <summary>감지된 적 수.</summary>
    int Count { get; }

    /// <summary>from 에서 가장 가까운 적. 없으면 null.</summary>
    Transform GetNearest(Vector3 from);

    /// <summary>from 에서 maxDistance 안쪽의 가장 가까운 적. 없으면 null.</summary>
    Transform GetNearest(Vector3 from, float maxDistance);

    /// <summary>
    /// from 에서 가까운 순으로 최대 max 개를 buffer 에 채우고 개수를 반환한다.
    /// buffer 는 호출 전에 비워진다.
    /// </summary>
    int GetTargets(Vector3 from, int max, List<Transform> buffer);
}
