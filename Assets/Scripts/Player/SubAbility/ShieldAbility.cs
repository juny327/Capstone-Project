using UnityEngine;

/// <summary>
/// 에너지 방패 — 누르고 있는 동안 몸 앞에 반투명 방패면을 펴서 <b>정면 120°</b> 피해를 막는다 (커스터마이징-구현계획.md 5-3).
///
///  · 들고 있는 동안 자동 공격이 멈추고 이동이 느려진다 — 무적 버튼이 되지 않게 (라인하르트 · DOOM 방패)
///  · 막을 때마다 내구도가 준다. 0 이 되면 깨지고 4초 동안 못 편다. 내리면 2초 뒤부터 다시 찬다
///  · 0.3초 이상 들고 있다가 떼면 방패 밀치기 — 정면 적을 밀고 경직 (Hades 혼돈의 방패)
///
/// 막는 방향은 모든 적 · 보스 공격이 넣는 DamageInfo.hitDirection(공격이 날아온 방향)으로 판단한다.
/// 패링(우클릭)과 역할을 나눈다 — 패링은 순간 · 반사, 방패는 지속 · 정면 · 막기만.
/// </summary>
public class ShieldAbility : SubAbility, IDamageFilter
{
    [Header("막기")]
    [Range(30f, 240f)] [SerializeField] float blockAngle = 120f;
    [Min(1f)] [SerializeField] float durability = 50f;
    [Tooltip("레벨마다 내구도 증가 (Lv2 +20 · Lv3 +40)")]
    [Min(0f)] [SerializeField] float durabilityPerLevel = 20f;
    [Min(0f)] [SerializeField] float regenDelay = 2f;
    [Min(0f)] [SerializeField] float regenPerSecond = 20f;
    [Tooltip("깨진 뒤 다시 펼 수 있게 되기까지(초)")]
    [Min(0f)] [SerializeField] float brokenDelay = 4f;
    [Tooltip("이 값 이상 차야 다시 펼 수 있다")]
    [Min(0f)] [SerializeField] float minimumToRaise = 5f;
    [Range(0.1f, 1f)] [SerializeField] float moveMultiplier = 0.6f;

    [Header("밀치기 (떼면)")]
    [Min(0f)] [SerializeField] float bashMinHold = 0.3f;
    [Min(0.5f)] [SerializeField] float bashRange = 2.5f;
    [Range(10f, 180f)] [SerializeField] float bashAngle = 100f;
    [Min(0f)] [SerializeField] float bashPush = 2f;
    [Min(0.01f)] [SerializeField] float bashPushDuration = 0.15f;
    [Min(0f)] [SerializeField] float bashStun = 0.8f;
    [Min(0f)] [SerializeField] float bashDamageRatio = 0.5f;
    [Min(0f)] [SerializeField] float bashCooldown = 3f;
    [SerializeField] LayerMask enemyLayers = ~0;

    [Header("방패면")]
    [SerializeField] MeshRenderer face;
    [SerializeField] float faceRadius = 1.1f;
    [SerializeField] float faceHeight = 1.5f;
    [SerializeField] float faceBottom = 0.15f;
    [SerializeField] Color faceColor = new Color(0.25f, 0.85f, 1f, 0.55f);
    [SerializeField] Color hitColor = new Color(0.8f, 1f, 1f, 1f);
    [SerializeField] Color lowColor = new Color(1f, 0.35f, 0.3f, 0.55f);

    [Header("연출")]
    [SerializeField] ParryFlash sparkPrefab;
    [SerializeField] GameSoundSet.Entry raiseSound = new GameSoundSet.Entry();
    [SerializeField] GameSoundSet.Entry blockSound = new GameSoundSet.Entry();
    [SerializeField] GameSoundSet.Entry breakSound = new GameSoundSet.Entry();
    [SerializeField] GameSoundSet.Entry bashSound = new GameSoundSet.Entry();

    bool raised;
    bool broken;
    float holdTime;
    float current;
    float regenWait;
    float bashCd;
    float hitFlash;
    ParryFlash sparks;
    MaterialPropertyBlock block;
    readonly Collider[] overlaps = new Collider[32];
    readonly System.Collections.Generic.HashSet<Enemy> bashed = new System.Collections.Generic.HashSet<Enemy>();

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    float MaxDurability => durability + durabilityPerLevel * (Level - 1);

    public override float CooldownRemaining => broken ? Mathf.Max(0f, regenWait) : Mathf.Max(0f, bashCd);
    public override float CooldownTotal => broken ? brokenDelay : bashCooldown;
    public override bool IsBusy => raised;
    public override bool IsHolding => raised;
    public override float Gauge => MaxDurability > 0f ? Mathf.Clamp01(current / MaxDurability) : 0f;

    /// <summary>들고 있는지 (테스트 · HUD).</summary>
    public bool Raised => raised;
    public bool Broken => broken;
    public float Durability => current;

    public override void Equip(GameObject owner, SubAbilityData data, int level)
    {
        base.Equip(owner, data, level);

        current = MaxDurability;
        BuildFace();
        if (face != null) face.enabled = false;
    }

    protected override void OnLevelChanged()
    {
        // 레벨업하면 늘어난 만큼 바로 채운다
        current = Mathf.Min(current + durabilityPerLevel, MaxDurability);
    }

    public override void OnPressed()
    {
        if (raised || Owner == null) return;
        if (broken || current < minimumToRaise) return;
        if (Move != null && (Move.IsRolling || Move.IsDashing)) return;

        raised = true;
        holdTime = 0f;

        if (Move != null)
        {
            Move.AddSpeedModifier(moveMultiplier);
            Move.SetActionLock(this, true);
        }

        if (Stats != null) Stats.AddDamageFilter(this);
        if (face != null) face.enabled = true;

        SetBool("ShieldUp", true);
        PlaySound(raiseSound);
    }

    public override void OnHeld(float deltaTime)
    {
        if (!raised) return;

        holdTime += deltaTime;

        // 들고 있는 동안 공격이 멈춘다 (쿨다운은 그대로 흐른다)
        if (Weapons != null) Weapons.SuppressFiring(0.15f);
    }

    public override void OnReleased()
    {
        if (!raised) return;

        bool bash = holdTime >= bashMinHold && bashCd <= 0f;
        Lower();
        if (bash) Bash();
    }

    public override void Cancel()
    {
        if (raised) Lower();
    }

    /// <summary>정면에서 온 공격이면 내구도로 받고 피해를 막는다.</summary>
    public bool Absorb(ref DamageInfo info)
    {
        if (!raised || Owner == null) return false;

        Vector3 from = -info.hitDirection;
        from.y = 0f;

        // 방향이 없는 공격은 맞은 자리로 판단한다
        if (from.sqrMagnitude < 0.0001f)
        {
            from = info.hitPoint - Owner.transform.position;
            from.y = 0f;
        }

        if (from.sqrMagnitude < 0.0001f) return false;
        if (Vector3.Angle(Forward, from) > blockAngle * 0.5f) return false;

        current -= Mathf.Max(0f, info.damage);
        hitFlash = 0.12f;

        Vector3 at = Owner.transform.position + from.normalized * faceRadius + Vector3.up * 1.1f;
        Spark(at, from.normalized);
        PlaySound(blockSound);

        if (current <= 0f) Break();
        return true;
    }

    void Lower()
    {
        raised = false;

        if (Move != null)
        {
            Move.RemoveSpeedModifier(moveMultiplier);
            Move.SetActionLock(this, false);
        }

        if (Stats != null) Stats.RemoveDamageFilter(this);
        if (face != null) face.enabled = false;

        SetBool("ShieldUp", false);
        if (!broken) regenWait = regenDelay;
    }

    void Break()
    {
        current = 0f;
        broken = true;
        regenWait = brokenDelay;
        Lower();
        PlaySound(breakSound);
    }

    void Bash()
    {
        bashCd = bashCooldown;
        SetTrigger("ShieldBash");
        PlaySound(bashSound);

        Vector3 origin = Owner.transform.position;
        Vector3 forward = Forward;
        Spark(origin + forward * 1.2f + Vector3.up * 1.1f, forward);

        int count = Physics.OverlapSphereNonAlloc(origin, bashRange + 0.5f, overlaps, enemyLayers, QueryTriggerInteraction.Collide);
        float damage = WeaponDamage(6f) * bashDamageRatio;
        bashed.Clear();

        for (int i = 0; i < count; i++)
        {
            Enemy enemy = overlaps[i] != null ? overlaps[i].GetComponentInParent<Enemy>() : null;
            if (!IsAlive(enemy)) continue;

            Vector3 to = enemy.transform.position - origin;
            to.y = 0f;
            if (to.magnitude > bashRange + 0.5f || Vector3.Angle(forward, to) > bashAngle * 0.5f) continue;

            // 같은 적의 콜라이더가 여럿이면 한 번만
            if (!bashed.Add(enemy)) continue;

            if (!(enemy.attack is SuicideAttack)) enemy.ChangeState(EnemyState.Hit);
            enemy.ApplyShock(bashStun);
            enemy.TakeDamage(new DamageInfo { damage = damage, hitPoint = enemy.transform.position + Vector3.up, hitDirection = forward });

            if (IsAlive(enemy))
                EnemyShove.Push(this, enemy, to.sqrMagnitude > 0.0001f ? to : forward, bashPush, bashPushDuration);
        }
    }

    void Update()
    {
        if (bashCd > 0f) bashCd -= Time.deltaTime;
        if (hitFlash > 0f) hitFlash -= Time.deltaTime;

        if (!raised)
        {
            if (regenWait > 0f)
            {
                regenWait -= Time.deltaTime;
                if (regenWait <= 0f) broken = false;
            }
            else
            {
                current = Mathf.Min(MaxDurability, current + regenPerSecond * Time.deltaTime);
            }
        }

        UpdateFaceColor();
    }

    // ───────── 방패면 — 몸 앞의 호 모양 띠 ─────────

    void BuildFace()
    {
        if (face == null) return;

        MeshFilter filter = face.GetComponent<MeshFilter>();
        if (filter == null) filter = face.gameObject.AddComponent<MeshFilter>();

        const int segments = 18;
        var vertices = new Vector3[(segments + 1) * 2];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[segments * 6];

        float half = blockAngle * 0.5f * Mathf.Deg2Rad;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float a = Mathf.Lerp(-half, half, t);
            Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * faceRadius;

            vertices[i * 2] = dir + Vector3.up * faceBottom;
            vertices[i * 2 + 1] = dir + Vector3.up * (faceBottom + faceHeight);
            uvs[i * 2] = new Vector2(t, 0f);
            uvs[i * 2 + 1] = new Vector2(t, 1f);

            if (i == segments) continue;

            int v = i * 2;
            int k = i * 6;
            triangles[k] = v; triangles[k + 1] = v + 1; triangles[k + 2] = v + 2;
            triangles[k + 3] = v + 1; triangles[k + 4] = v + 3; triangles[k + 5] = v + 2;
        }

        var mesh = new Mesh { name = "ShieldFace" };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        filter.sharedMesh = mesh;

        face.transform.localPosition = Vector3.zero;
        face.transform.localRotation = Quaternion.identity;
    }

    void UpdateFaceColor()
    {
        if (face == null || !face.enabled) return;

        if (block == null) block = new MaterialPropertyBlock();

        Color c = Color.Lerp(lowColor, faceColor, Gauge);
        if (hitFlash > 0f) c = Color.Lerp(c, hitColor, hitFlash / 0.12f);

        face.GetPropertyBlock(block);
        block.SetColor(BaseColorId, c);
        face.SetPropertyBlock(block);
    }

    void Spark(Vector3 position, Vector3 dir)
    {
        if (sparkPrefab == null) return;
        if (sparks == null) sparks = Instantiate(sparkPrefab);
        sparks.Clash(position, dir);
    }

    void OnDisable()
    {
        if (raised) Lower();
    }

    void OnDestroy()
    {
        if (sparks != null) Destroy(sparks.gameObject);
    }
}
