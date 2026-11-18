using UnityEngine;

/// <summary>
/// 드래곤 몸과 플레이어 사이 막기 (2026-11-14 — 보스와 부딪히면 바닥으로 꺼지거나 맵 밖으로 밀려나던 버그).
///
/// 원인: 몸통 콜라이더는 옆으로 누운 둥근 캡슐(반경 1.9m · 가운데 높이 약 2m)이고 키네마틱 강체다. 플레이어(키 2m)와 겹치면
///       물리 엔진이 캡슐 축에서 바깥쪽 — 옆 + <b>아래</b> — 으로 밀어낸다. 배 밑으로 들어가면 곧장 아래로 밀려 바닥을 뚫고 떨어졌다
///       (돌진 · 꼬리 휩쓸기 · 물러나기처럼 몸이 플레이어를 빠르게 덮칠 때). 벽과 몸 사이에 끼면 벽 쪽으로도 밀려 나갔다.
/// 고침: 몸통 콜라이더와 플레이어 콜라이더끼리는 물리 충돌을 끈다 — 탄 · 조준 · 근접 판정(레이캐스트 · 겹침 검사)은 그대로다.
///       막는 일은 여기서 한다: 몸통을 바닥에 비춘 모양(양 끝이 둥근 띠) 안으로는 못 걸어 들어가고, 겹쳤으면 <b>수평으로만</b> 밀어낸다.
///       위아래로는 절대 밀지 않는다. 위치를 옮기지 않고 속도로 밀기 때문에 벽 · 바닥은 물리가 그대로 막아 준다
///       (벽과 몸 사이에 끼면 몸과 겹친 채로 있을 뿐 튕겨 나가지 않는다).
///
/// PlayerMove.FixedUpdate 가 수평 속도를 정한 뒤에 돌아야 한다 → 실행 순서를 뒤로. DragonBoss.Awake 가 붙인다.
/// </summary>
[DefaultExecutionOrder(200)]
public class DragonBodyBlock : MonoBehaviour
{
    [Tooltip("겹쳤을 때 밀어내는 가장 빠른 속도 (m/s). 한 물리 걸음에 플레이어 반지름보다 덜 가야 벽을 뚫지 않는다 (12m/s = 0.24m)")]
    [SerializeField] float maxPushSpeed = 12f;

    const float RefreshEvery = 0.5f;

    CapsuleCollider capsule;
    Rigidbody target;
    float targetRadius = 0.5f;
    float nextRefresh;

    /// <summary>지금 플레이어를 밀어내는 중인가 (시험용).</summary>
    public bool Pushing { get; private set; }

    void Awake() => capsule = GetComponent<CapsuleCollider>();

    void OnEnable() => nextRefresh = 0f;

    void FixedUpdate()
    {
        Pushing = false;
        if (capsule == null) return;

        // 플레이어가 바뀌었거나 콜라이더가 껐다 켜졌을 수 있다 — 가끔 다시 건다 (값싼 호출)
        if (Time.time >= nextRefresh)
        {
            nextRefresh = Time.time + RefreshEvery;
            Refresh();
        }
        if (target == null || !capsule.enabled) return;   // 쓰러지면 콜라이더가 꺼진다 — 시체는 지나갈 수 있다

        // 몸통 캡슐의 축 (양 끝 반구 가운데) — 바닥에 비추면 양 끝이 둥근 띠가 된다
        Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
        float half = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
        Vector3 a3 = transform.TransformPoint(capsule.center - axis * half), b3 = transform.TransformPoint(capsule.center + axis * half);
        Vector3 scale = transform.lossyScale;
        float bodyRadius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));

        Vector3 p3 = target.position;
        Vector2 a = new Vector2(a3.x, a3.z), b = new Vector2(b3.x, b3.z), p = new Vector2(p3.x, p3.z);
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        Vector2 away = p - (a + ab * t);
        float distance = away.magnitude, reach = bodyRadius + targetRadius;
        if (distance >= reach) return;

        Vector3 velocity = target.linearVelocity;
        Vector2 flat = new Vector2(velocity.x, velocity.z);
        Vector2 normal;
        if (distance > 0.02f) normal = away / distance;
        else
        {
            // 축 바로 위 — 옆으로 뺀다 (움직이던 쪽, 가만히 있었으면 오른쪽)
            Vector2 side = ab.sqrMagnitude > 0.0001f ? new Vector2(ab.y, -ab.x).normalized : Vector2.right;
            normal = Vector2.Dot(flat, side) < 0f ? -side : side;
        }

        // 몸 안쪽으로 가는 속도는 지운다 (벽처럼 막는다) · 겹친 만큼은 바깥으로 밀어낸다
        float inward = -Vector2.Dot(flat, normal);
        if (inward > 0f) flat += normal * inward;
        flat += normal * Mathf.Min((reach - distance) / Time.fixedDeltaTime, maxPushSpeed);

        velocity.x = flat.x;
        velocity.z = flat.y;   // y(중력 · 점프)는 건드리지 않는다
        target.linearVelocity = velocity;
        Pushing = true;
    }

    /// <summary>플레이어를 찾고, 몸통 콜라이더와 플레이어의 단단한 콜라이더끼리 물리 충돌을 끈다.</summary>
    void Refresh()
    {
        Transform player = GameEvents.Player;
        Rigidbody body = player != null ? player.GetComponentInParent<Rigidbody>() : null;
        if (body == null) { target = null; return; }
        target = body;

        foreach (Collider c in body.GetComponentsInChildren<Collider>(true))
        {
            if (c.isTrigger || c == capsule) continue;
            Physics.IgnoreCollision(capsule, c, true);
            if (c is CapsuleCollider cc && c.attachedRigidbody == body)
            {
                Vector3 s = c.transform.lossyScale;
                targetRadius = cc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
            }
        }
    }
}
