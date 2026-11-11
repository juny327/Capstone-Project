using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class ExpOrb : MonoBehaviour, IPoolable
{
    [Header("Exp Settings")]
    public int expValue = 1;
    public float moveSpeed = 5f;
    public float accelerate = 10f;

    [Header("Attract Settings")]
    public float attractDistance = 3f;
    public float pickupDistance = 0.2f;

    private Transform player;
    private bool isAttracting = false;

    private Rigidbody rb;
    private BoxCollider bc;

    Renderer orbRenderer;   // renderer 는 Component.renderer 와 겹쳐 CS0108 경고가 났다 (2026-11-06)
    MaterialPropertyBlock mpb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;

        SphereCollider sc = GetComponent<SphereCollider>();
        bc = GetComponent<BoxCollider>();

        bc.isTrigger = false; // 바닥 충돌
        sc.isTrigger = true;  // 플레이어 감지
        sc.radius = attractDistance;
        orbRenderer = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
    }

    public void OnSpawn()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        moveSpeed = 5f;
        isAttracting = false;
        player = null;

        bc.isTrigger = false;
    }

    public void OnDespawn()
    {
        player = null;
    }

    public void Initialize(int value)
    {
        expValue = value;
        orbRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat("_ExpValue", value);
        orbRenderer.SetPropertyBlock(mpb);
    }

    private void Update()
    {
        if (!isAttracting || player == null) return;

        Vector3 target = player.position + Vector3.up * 1.5f;
        Vector3 dir = (target - transform.position).normalized;

        rb.MovePosition(rb.position + dir * moveSpeed * Time.deltaTime);

        moveSpeed += accelerate * Time.deltaTime;

        if (Vector3.Distance(rb.position, target) < pickupDistance)
        {
            PlayerStats stats = player.GetComponent<PlayerStats>();

            if (stats != null)
                stats.AddExp(expValue);

            ReturnToPool();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            player = other.transform;
            isAttracting = true;

            bc.isTrigger = true; // 플레이어랑 충돌 안하게
        }
    }

    private void ReturnToPool()
    {
        GetComponent<Poolable>().ReturnToPool();
    }
}