using UnityEngine;

/// <summary>
/// 땅에 떨어진 코인 한 개.
/// 몬스터 자리에서 튀어 나와 바닥에 놓이고(Pop → Rest), 플레이어가 가까이 오면 날아와(Fly) 지갑에 들어간다.
///
/// 물리를 쓰지 않는다 — 수십 개가 동시에 있어도 가볍게, 벽 · 경사에 걸리지 않게.
/// 모양 · 크기 · 회전 속도는 프리팹(Prefabs/Economy/Coin)에서, 줍기 거리 · 속도는 EconomySettings 에서 바꾼다.
/// </summary>
public class CoinPickup : MonoBehaviour, IPoolable
{
    [Tooltip("돌아가는 모델 (자식). 비우면 회전하지 않는다")]
    [SerializeField] Transform model;

    [SerializeField] float spinSpeed = 220f;
    [SerializeField] float bobHeight = 0.08f;
    [SerializeField] float bobSpeed = 3f;

    [Tooltip("바닥에서 띄우는 높이 (m)")]
    [SerializeField] float restHeight = 0.4f;

    [Tooltip("튀어 나와 바닥에 닿기까지 (초)")]
    [SerializeField] float popDuration = 0.45f;

    [Tooltip("튀어 오르는 높이 (m)")]
    [SerializeField] float popHeight = 1.4f;

    enum State { Pop, Rest, Fly }

    State state;
    int value = 1;
    Vector3 from;
    Vector3 to;
    float elapsed;
    float speed;
    float bobPhase;

    /// <summary>이 코인이 가진 크레딧.</summary>
    public int Value => value;

    /// <summary>origin 에서 튀어 나와 landing 에 놓인다. 둘 다 바닥 높이 기준.</summary>
    public void Launch(Vector3 origin, Vector3 landing, int amount)
    {
        value = Mathf.Max(1, amount);
        from = origin + Vector3.up * restHeight;
        to = landing + Vector3.up * restHeight;

        transform.position = from;
        elapsed = 0f;
        speed = 0f;
        bobPhase = Random.value * Mathf.PI * 2f;
        state = State.Pop;
    }

    /// <summary>거리와 상관없이 플레이어에게 날아오게 한다 (스테이지 클리어 때).</summary>
    public void Attract()
    {
        if (state == State.Fly) return;

        state = State.Fly;
        EconomySystem system = EconomySystem.Instance;
        speed = system != null && system.Settings != null ? system.Settings.flySpeed : 8f;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (model != null)
            model.Rotate(0f, spinSpeed * dt, 0f, Space.World);

        EconomySystem system = EconomySystem.Instance;
        if (system == null || system.Settings == null) return;

        switch (state)
        {
            case State.Pop:
            {
                elapsed += dt / Mathf.Max(0.01f, popDuration);
                float k = Mathf.Clamp01(elapsed);
                transform.position = Vector3.Lerp(from, to, k) + Vector3.up * (popHeight * 4f * k * (1f - k));

                if (k >= 1f) state = State.Rest;
                break;
            }

            case State.Rest:
                bobPhase += dt * bobSpeed;
                transform.position = to + Vector3.up * (Mathf.Sin(bobPhase) * bobHeight);

                if (system.InMagnetRange(transform.position))
                    Attract();
                break;

            case State.Fly:
            {
                if (!system.HasLivePlayer)
                {
                    // 플레이어가 죽으면 그 자리에 다시 놓인다
                    to = new Vector3(transform.position.x, to.y, transform.position.z);
                    state = State.Rest;
                    break;
                }

                speed += system.Settings.flyAcceleration * dt;
                Vector3 target = system.PlayerCenter;
                Vector3 next = Vector3.MoveTowards(transform.position, target, speed * dt);
                transform.position = next;

                if ((target - next).sqrMagnitude <= system.Settings.pickupDistance * system.Settings.pickupDistance)
                    system.Collect(this);
                break;
            }
        }
    }

    public void OnSpawn()
    {
        state = State.Rest;
        elapsed = 0f;
        speed = 0f;
    }

    public void OnDespawn()
    {
        state = State.Rest;
    }
}
