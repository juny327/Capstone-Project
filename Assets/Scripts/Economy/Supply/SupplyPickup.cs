using UnityEngine;

public enum SupplyKind { Magnet, Heal, Beacon }

/// <summary>
/// 바닥에 놓인 보급 아이템 하나 — 둥실 떠서 돌고, 플레이어가 가까이 오면 SupplySystem 에 알린다.
/// 자석 · 힐팩은 itemLifetime 뒤에 사라지고(마지막 blinkTime 초는 깜빡임), 정비 신호기는 스테이지가 끝날 때까지 남는다.
/// 모양은 프리팹(Tools / 재화 · 상점 / 보급 아이템 만들기)이 정한다. "Visual" 자식이 뜨고 돈다.
/// </summary>
public class SupplyPickup : MonoBehaviour
{
    [SerializeField] SupplyKind kind;
    [Tooltip("위아래로 뜨는 높이 (m)")]
    [Min(0f)] [SerializeField] float bobHeight = 0.18f;
    [Tooltip("초당 회전 (도)")]
    [SerializeField] float spinSpeed = 90f;

    public SupplyKind Kind => kind;

    Transform visual;
    Renderer[] renderers;
    float age;
    float lifetime;
    float blink;
    float radius;

    public void Begin(float lifetime, float blink, float pickupRadius)
    {
        this.lifetime = lifetime;
        this.blink = blink;
        radius = pickupRadius;
        age = 0f;
    }

    void Awake()
    {
        visual = transform.Find("Visual");
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;   // 정비 · 카드 창으로 멈춘 동안

        age += dt;

        if (visual != null)
        {
            visual.localPosition = new Vector3(0f, Mathf.Sin(age * 2.4f) * bobHeight, 0f);
            visual.Rotate(0f, spinSpeed * dt, 0f, Space.World);
        }

        if (lifetime > 0f)
        {
            float left = lifetime - age;
            if (left <= 0f) { Destroy(gameObject); return; }
            if (left < blink) SetVisible(Mathf.Repeat(left * (left < blink * 0.4f ? 8f : 4f), 1f) > 0.35f);
        }

        SupplySystem system = SupplySystem.Instance;
        if (system == null || !system.HasLivePlayer) return;

        // 높이는 넉넉히 — 위아래 다른 층에 있는 것은 줍지 않는다
        Vector3 d = system.PlayerPosition - transform.position;
        if (Mathf.Abs(d.y) > 4f) return;
        d.y = 0f;
        if (d.sqrMagnitude <= radius * radius && system.TryPick(this))
            Destroy(gameObject);
    }

    void SetVisible(bool on)
    {
        foreach (Renderer r in renderers)
            if (r != null && r.enabled != on) r.enabled = on;
    }
}
