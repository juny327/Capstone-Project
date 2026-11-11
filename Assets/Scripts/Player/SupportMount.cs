using UnityEngine;

/// <summary>
/// 보조 무기 하나가 떠 있는 자리 (무기-개편-개발-계획 · 보조 무기 규칙).
///
///   · 플레이어 둘레의 정해진 자리(왼쪽 · 오른쪽 · 뒤)를 살짝 늦게 따라오며 위아래로 조금 떠다닌다
///   · 쏠 때는 노리는 적 쪽으로 바로 돌고, 한동안 그쪽을 보다가 플레이어가 보는 방향으로 천천히 돌아온다
///   · 드론 전용 무기(전격 · 레이저 · 샷건)는 기존 드론 모형을 조금 크게 띄우고 강조색을 무기마다 바꾼다.
///     총 모형은 숨기고, 탄 · 빔은 드론 총구에서 나간다 (WeaponController 가 총구를 맞춘다)
///
/// 무기 오브젝트는 <see cref="Holder"/> 아래에 붙는다. WeaponController 가 만들고 치운다.
/// </summary>
public class SupportMount : MonoBehaviour
{
    [Tooltip("자리 (플레이어 기준 로컬). 왼쪽 · 오른쪽 · 뒤")]
    public Vector3 localOffset;

    [Tooltip("따라오는 부드러움 (초) — 0 이면 딱 붙는다")]
    [Min(0f)] public float followTime = 0.08f;

    [Tooltip("위아래로 떠다니는 폭 (m) · 빠르기 (Hz)")]
    public float bobHeight = 0.06f;
    public float bobSpeed = 1.3f;

    [Tooltip("쏜 뒤 그 방향을 보고 있는 시간 (초) — 지나면 플레이어 방향으로 돌아온다")]
    public float holdAimTime = 0.8f;

    [Tooltip("돌아오는 빠르기 (도/초)")]
    public float turnSpeed = 540f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    Transform player;
    Transform holder;
    Transform bodyMuzzle;
    GameObject body;
    Vector3 velocity;
    Vector3 aimDirection = Vector3.forward;
    float holdTimer;
    float phase;
    bool placed;

    // 떠 있는 근접 무기가 뛰쳐나가 벨 때 — 그동안 자리 대신 이 자세 (MeleeWeapon 이 프레임마다 넣는다)
    bool overriding;
    Vector3 overridePosition;
    Quaternion overrideRotation;
    float returnTimer;

    /// <summary>뛰쳐나가 공격하는 중인가.</summary>
    public bool IsStriking => overriding;

    /// <summary>이번 프레임 자세를 덮어쓴다 (공격하는 동안 매 프레임).</summary>
    public void OverridePose(Vector3 position, Quaternion rotation)
    {
        overriding = true;
        overridePosition = position;
        overrideRotation = rotation;
    }

    /// <summary>공격이 끝났다 — 자리로 돌아온다 (조금 느긋하게).</summary>
    public void ReleasePose()
    {
        if (!overriding) return;
        overriding = false;
        velocity = Vector3.zero;
        returnTimer = 0.35f;
        aimDirection = Flat(transform.forward).sqrMagnitude > 0.0001f ? Flat(transform.forward).normalized : aimDirection;
        holdTimer = 0.2f;
    }

    /// <summary>무기가 붙는 곳.</summary>
    public Transform Holder => holder;

    /// <summary>드론 몸체를 달았는가.</summary>
    public bool HasBody => body != null;

    /// <summary>드론 총구 — 드론 전용 무기의 탄 · 빔이 나오는 곳. 몸체가 없으면 null.</summary>
    public Transform BodyMuzzle => body != null ? bodyMuzzle : null;

    /// <summary>
    /// 자리를 잡는다. bodyPrefab 이 있으면 드론 몸체를 bodyScale 배로 띄우고 강조색(tint, 투명이면 그대로)을 칠한다.
    /// </summary>
    public void Setup(Transform owner, Vector3 offset, int index, GameObject bodyPrefab, float weaponScale, float bodyScale, Color tint)
    {
        player = owner;
        localOffset = offset;
        phase = index * 2.1f;
        overriding = false;
        returnTimer = 0f;

        if (holder == null)
        {
            holder = new GameObject("Holder").transform;
            holder.SetParent(transform, false);
        }

        SetBody(bodyPrefab, Mathf.Max(0.1f, bodyScale), tint);
        holder.localPosition = Vector3.zero;
        holder.localRotation = Quaternion.identity;
        holder.localScale = Vector3.one * Mathf.Max(0.05f, weaponScale);

        if (owner != null) aimDirection = Flat(owner.forward);
        placed = false;
        Snap();
    }

    void SetBody(GameObject bodyPrefab, float scale, Color tint)
    {
        if (body != null) { Destroy(body); body = null; }
        if (bodyPrefab == null) return;

        // 드론 프리팹의 모형(Model)만 떼어 쓴다 — 드론 동작(궤도 · 발사) 컴포넌트는 가져오지 않는다
        Transform model = bodyPrefab.transform.Find("Model");
        GameObject source = model != null ? model.gameObject : bodyPrefab;

        body = Instantiate(source, transform);
        body.name = "DroneBody";
        body.transform.localPosition = model != null ? model.localPosition * scale : Vector3.zero;
        body.transform.localRotation = model != null ? model.localRotation : Quaternion.identity;
        body.transform.localScale = (model != null ? model.localScale : Vector3.one) * scale;

        foreach (MonoBehaviour mb in body.GetComponentsInChildren<MonoBehaviour>(true))
            DestroyImmediate(mb);
        foreach (Collider c in body.GetComponentsInChildren<Collider>(true))
            DestroyImmediate(c);

        // 총구 — 드론 프리팹의 Muzzle 자리를 같은 배율로
        if (bodyMuzzle == null)
        {
            bodyMuzzle = new GameObject("BodyMuzzle").transform;
            bodyMuzzle.SetParent(transform, false);
        }
        Transform m = bodyPrefab.transform.Find("Muzzle");
        bodyMuzzle.localPosition = (m != null ? m.localPosition : new Vector3(0f, 0.06f, 0.31f)) * scale;
        bodyMuzzle.localRotation = Quaternion.identity;

        // 강조색 — 몸체의 주황 강조 재질(Main)만 바꾼다. 재질 에셋은 건드리지 않는다 (속성 블록)
        if (tint.a <= 0.01f) return;

        var block = new MaterialPropertyBlock();
        foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || !mats[i].name.StartsWith("Main")) continue;
                r.GetPropertyBlock(block, i);
                block.SetColor(BaseColorId, new Color(tint.r, tint.g, tint.b, 1f));
                r.SetPropertyBlock(block, i);
            }
        }
    }

    /// <summary>그 방향을 바로 본다 (쏘기 직전).</summary>
    public void FaceNow(Vector3 direction)
    {
        Vector3 d = Flat(direction);
        if (d.sqrMagnitude < 0.0001f) return;

        aimDirection = d.normalized;
        holdTimer = holdAimTime;
        transform.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);
    }

    /// <summary>지금 자리 · 방향으로 바로 옮긴다 (처음 생길 때 · 순간 이동 뒤).</summary>
    public void Snap()
    {
        if (player == null) return;

        transform.position = Goal();
        transform.rotation = Quaternion.LookRotation(aimDirection.sqrMagnitude > 0.0001f ? aimDirection : Vector3.forward, Vector3.up);
        velocity = Vector3.zero;
        placed = true;
    }

    Vector3 Goal()
    {
        float bob = Mathf.Sin((Time.time * bobSpeed * Mathf.PI * 2f) + phase) * bobHeight;
        return player.position + player.rotation * localOffset + Vector3.up * bob;
    }

    void LateUpdate()
    {
        if (player == null) return;

        if (overriding)
        {
            transform.SetPositionAndRotation(overridePosition, overrideRotation);
            return;
        }

        Vector3 goal = Goal();

        // 공격을 마치고 돌아오는 동안은 느긋하게 (휙 하고 순간 이동하지 않게)
        float follow = followTime;
        if (returnTimer > 0f) { returnTimer -= Time.deltaTime; follow = Mathf.Max(followTime, 0.12f); }

        // 멀리 떨어졌으면(씬 이동 · 순간 이동) 바로 붙인다
        if (!placed || (returnTimer <= 0f && (transform.position - goal).sqrMagnitude > 25f) || (transform.position - goal).sqrMagnitude > 400f)
            Snap();
        else
            transform.position = follow > 0f
                ? Vector3.SmoothDamp(transform.position, goal, ref velocity, follow, Mathf.Infinity, Time.deltaTime)
                : goal;

        if (holdTimer > 0f) holdTimer -= Time.deltaTime;
        else aimDirection = Vector3.RotateTowards(aimDirection, Flat(player.forward).normalized, turnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);

        if (aimDirection.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(aimDirection, Vector3.up), turnSpeed * 2f * Time.deltaTime);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
