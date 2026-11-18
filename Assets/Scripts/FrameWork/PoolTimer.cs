using UnityEngine;

/// <summary>
/// 정한 시간(게임 시간)이 지나면 풀로 돌려놓는다 — 풀링한 오브젝트의 Destroy(obj, t) 대신 (2026-11-18 성능 2단계).
/// 그 전에 다른 이유로 돌아가면(꺼지면) 취소된다 — 다음에 꺼낼 때 지난 시간이 남아 일찍 돌아가지 않게.
/// </summary>
public class PoolTimer : MonoBehaviour
{
    float remaining;
    bool armed;

    public void Begin(float seconds)
    {
        remaining = seconds;
        armed = true;
        enabled = true;
    }

    void Update()
    {
        if (!armed)
        {
            enabled = false;
            return;
        }

        remaining -= Time.deltaTime;
        if (remaining > 0f) return;

        armed = false;
        enabled = false;
        PoolManager.Despawn(gameObject);
    }

    void OnDisable() => armed = false;
}
