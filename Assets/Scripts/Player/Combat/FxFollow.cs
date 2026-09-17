using UnityEngine;

/// <summary>
/// 켜져 있는 동안 대상의 위치를 따라가는 연출용 컴포넌트 (11번 문서 B5).
///
/// 풀 효과를 플레이어의 자식으로 붙이지 않기 위해 만들었다.
/// 플레이어는 DontDestroyOnLoad 이고 PoolManager 는 스테이지 씬마다 새로 생기므로,
/// 자식으로 붙인 채 반납하면 씬이 바뀐 뒤에도 플레이어 밑에 남고 이미 사라진 풀에 반납을 시도한다.
///
/// 위치만 복사하고 회전은 건드리지 않는다 —
/// 플레이어가 마우스를 따라 계속 돌기 때문에 회전까지 따라가면 효과가 함께 휩쓸려 돈다
/// (드론이 월드 좌표로 궤도를 잡는 것과 같은 이유).
/// </summary>
public class FxFollow : MonoBehaviour
{
    private Transform target;
    private Vector3 offset;

    /// <summary>따라갈 대상을 지정한다. offset 은 월드 기준 보정값이다.</summary>
    public void Follow(Transform newTarget, Vector3 worldOffset)
    {
        target = newTarget;
        offset = worldOffset;

        if (target != null)
            transform.position = target.position + offset;
    }

    /// <summary>대상 없이 그 자리에 두고 싶을 때.</summary>
    public void Release()
    {
        target = null;
    }

    void LateUpdate()
    {
        // 대상이 사라지면(사망·씬 전환) 그 자리에 멈춘다
        if (target == null) return;

        transform.position = target.position + offset;
    }

    void OnDisable()
    {
        // 풀로 돌아갈 때 대상을 놓는다. 다음에 꺼낼 때 옛 대상을 따라가지 않게 한다.
        target = null;
    }
}
