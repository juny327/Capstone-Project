using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 에너지 방패 — 누르고 있는 동안 몸 앞에 반투명 방패면을 펴서 <b>정면 120°</b> 피해를 막는다 (커스터마이징-구현계획.md 5-3).
///
///  · 들고 있는 동안 자동 공격이 멈추고 이동이 느려진다 — 무적 버튼이 되지 않게 (라인하르트 · DOOM 방패)
///  · 막을 때마다 내구도가 준다. 0 이 되면 깨지고 4초 동안 못 편다. 내리면 2초 뒤부터 다시 찬다
///  · 0.3초 이상 들고 있다가 떼면 방패 밀치기 — 정면 적을 밀고 경직 (Hades 혼돈의 방패)
///
/// 레벨업 변형 (Lv2 · Lv3 에서 둘 중 하나 — 트라이포드)
///  · Lv2 A 반사판 — 들고 있는 동안 정면으로 날아온 몬스터 탄을 되돌려 보낸다 (내구도를 쓰지 않는다)
///  · Lv2 B 강화 방벽 — 내구도 50 → 90 · 회복 ×1.5
///  · Lv3 A 방패 돌진 — 떼면 밀치기 대신 5m 돌진하며 부딪힌 적을 모두 밀친다 (돌진 중 무적)
///  · Lv3 B 방패 던지기 — 떼면 방패를 던진다. 적을 꿰뚫고 돌아온다 (돌아올 때까지 못 든다)
///
/// 막는 방향은 모든 적 · 보스 공격이 넣는 DamageInfo.hitDirection(공격이 날아온 방향)으로 판단한다.
/// 패링(우클릭)과 역할을 나눈다 — 패링은 순간 · 반사, 방패는 지속 · 정면 · 막기만.
/// </summary>
public class ShieldAbility : SubAbility, IDamageFilter
{
    [Header("막기")]
    [Range(30f, 240f)] [SerializeField] float blockAngle = 120f;
    [Min(1f)] [SerializeField] float durability = 50f;
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

    [Header("변형 Lv2 A — 반사판")]
    [Tooltip("방패면 바깥 이만큼 안에 들어온 몬스터 탄을 되돌린다")]
    [Min(0.1f)] [SerializeField] float reflectReach = 0.9f;
    [Min(0.1f)] [SerializeField] float reflectSpeedMultiplier = 1.3f;
    [Min(0.1f)] [SerializeField] float reflectMinSpeed = 8f;
    [Tooltip("되돌린 탄의 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] [SerializeField] float reflectDamageRatio = 1f;
    [Min(0.1f)] [SerializeField] float reflectLifeTime = 2.5f;

    [Header("변형 Lv2 B — 강화 방벽")]
    [Min(1f)] [SerializeField] float bulwarkDurability = 90f;
    [Min(1f)] [SerializeField] float bulwarkRegenMultiplier = 1.5f;

    [Header("변형 Lv3 A — 방패 돌진")]
    [Min(0.5f)] [SerializeField] float chargeDistance = 5f;
    [Min(0.05f)] [SerializeField] float chargeDuration = 0.3f;
    [Min(0f)] [SerializeField] float chargeInvulnerable = 0.4f;
    [Min(0.3f)] [SerializeField] float chargeRadius = 1.3f;
    [Min(0f)] [SerializeField] float chargePush = 2.5f;
    [Min(0f)] [SerializeField] float chargeDamageRatio = 0.8f;
    [Min(0f)] [SerializeField] float chargeCooldown = 4f;

    [Header("변형 Lv3 B — 방패 던지기")]
    [SerializeField] Transform disc;
    [Min(1f)] [SerializeField] float throwRange = 8f;
    [Min(1f)] [SerializeField] float throwSpeed = 20f;
    [Min(0.2f)] [SerializeField] float discRadius = 0.9f;
    [Min(0f)] [SerializeField] float throwDamageRatio = 1f;
    [Min(0f)] [SerializeField] float throwStun = 0.5f;
    [Min(0f)] [SerializeField] float throwCooldown = 3f;
    [Min(0f)] [SerializeField] float discSpin = 900f;

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

    enum DiscState { Held, Out, Back }

    bool raised;
    bool broken;
    bool charging;
    float holdTime;
    float current;
    float regenWait;
    float bashCd;
    float bashCdTotal;
    float hitFlash;
    float reflectScan;
    ParryFlash sparks;
    MaterialPropertyBlock block;
    readonly Collider[] overlaps = new Collider[32];
    readonly HashSet<Enemy> bashed = new HashSet<Enemy>();
    readonly HashSet<Transform> struck = new HashSet<Transform>();
    readonly List<Transform> targets = new List<Transform>();

    DiscState discState = DiscState.Held;
    Vector3 discPos;
    Vector3 discDir;
    float discTraveled;

    // 돌진 — 지난 프레임 판정 자리. 이번 프레임까지의 구간을 훑는다
    Vector3 chargeSweepFrom;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    // 변형
    bool Reflector => Has(2, 0);
    bool Bulwark => Has(2, 1);
    bool Charge => Has(3, 0);
    bool Throw => Has(3, 1);

    float MaxDurability => Bulwark ? bulwarkDurability : durability;
    float Regen => regenPerSecond * (Bulwark ? bulwarkRegenMultiplier : 1f);

    public override float CooldownRemaining => broken ? Mathf.Max(0f, regenWait) : Mathf.Max(0f, bashCd);
    public override float CooldownTotal => broken ? PracticeRules.Cooldown(brokenDelay) : (bashCdTotal > 0f ? bashCdTotal : PracticeRules.Cooldown(bashCooldown));
    public override bool IsBusy => raised || charging;
    public override bool IsHolding => raised;
    public override float Gauge => MaxDurability > 0f ? Mathf.Clamp01(current / MaxDurability) : 0f;

    /// <summary>들고 있는지 (테스트 · HUD).</summary>
    public bool Raised => raised;
    public bool Broken => broken;
    public float Durability => current;
    public float MaxDurabilityValue => MaxDurability;

    /// <summary>방패를 던져 아직 돌아오지 않았는가.</summary>
    public bool IsThrown => discState != DiscState.Held;

    /// <summary>방패 돌진 중인가.</summary>
    public bool IsCharging => charging;

    /// <summary>되돌린 탄 수 — 반사판 (검증용).</summary>
    public int ReflectedCount { get; private set; }

    /// <summary>마지막 돌진 · 던지기에 맞은 적 수 (검증용).</summary>
    public int LastStruckCount => struck.Count;

    public override void Equip(GameObject owner, SubAbilityData data, int level)
    {
        base.Equip(owner, data, level);

        current = MaxDurability;
        BuildFace();
        if (face != null) face.enabled = false;
        if (disc != null) disc.gameObject.SetActive(false);
    }

    protected override void OnVariantsChanged()
    {
        // 강화 방벽을 고르면 늘어난 내구도까지 바로 채운다
        current = Bulwark ? MaxDurability : Mathf.Min(current, MaxDurability);
    }

    public override void OnPressed()
    {
        if (raised || charging || IsThrown || Owner == null) return;
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

        // 들고 있는 동안 공격이 멈춘다 (쿨다운은 그대로 흐른다).
        // (방패를 든 채 찌르던 팔랑크스 · 막은 피해를 모으던 성채 대검은 2026-10-12 보관)
        if (Weapons != null) Weapons.SuppressFiring(0.15f);
    }

    public override void OnReleased()
    {
        if (!raised) return;

        bool act = holdTime >= bashMinHold && bashCd <= 0f;
        Lower();
        if (!act) return;

        if (Charge) StartCharge();
        else if (Throw) ThrowDisc();
        else Bash();
    }

    public override void Cancel()
    {
        if (raised) Lower();
        if (charging)
        {
            if (Move != null) Move.StopDash();
            EndCharge();
        }
    }

    /// <summary>정면에서 온 공격이면 내구도로 받고 피해를 막는다.</summary>
    public bool Absorb(ref DamageInfo info)
    {
        if (Owner == null) return false;

        // 돌진 중에는 무적이 따로 걸려 있다 — 여기서는 들고 있을 때만
        if (!raised) return false;

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
        regenWait = PracticeRules.Cooldown(brokenDelay);
        Lower();
        PlaySound(breakSound);
    }

    void StartCooldown(float seconds)
    {
        bashCd = bashCdTotal = PracticeRules.Cooldown(seconds);
    }

    void Bash()
    {
        StartCooldown(bashCooldown);
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

    // ───────── Lv3 A — 방패 돌진 ─────────

    // 이번 돌진의 피해 배율 (팔랑크스는 무기 공격력 × 3)
    float chargeDamage;

    void StartCharge() => StartCharge(chargeDistance, chargeDamageRatio);

    void StartCharge(float distance, float damageRatio)
    {
        chargeDamage = damageRatio;
        float duration = chargeDuration * Mathf.Max(0.2f, distance / Mathf.Max(0.1f, chargeDistance));
        if (Move == null || !Move.TryDash(Forward, distance, duration, chargeInvulnerable))
        {
            Bash();   // 대시를 못 하면(구르는 중 등) 그냥 밀친다
            return;
        }

        charging = true;
        struck.Clear();
        chargeSweepFrom = Owner.transform.position + Forward * 0.8f;
        StartCooldown(chargeCooldown);

        Move.SetActionLock(this, true);
        if (face != null) face.enabled = true;
        SetBool("ShieldUp", true);
        PlaySound(bashSound);
        ChargeSweep();
    }

    void ChargeSweep()
    {
        Vector3 center = Owner.transform.position + Forward * 0.8f;
        CombatFx.FindEnemiesAlong(chargeSweepFrom, center, chargeRadius, targets);
        chargeSweepFrom = center;
        float damage = WeaponDamage(6f) * chargeDamage;

        foreach (Transform t in targets)
        {
            if (!struck.Add(t)) continue;

            Vector3 away = t.position - Owner.transform.position;
            away.y = 0f;
            Vector3 push = (away.normalized + Forward).normalized;

            CombatFx.Damage(t, damage, CombatFx.Body(t), Forward, false, 0.15f);
            CombatFx.Stun(t, bashStun);
            CombatFx.Push(this, t, push, chargePush, bashPushDuration);
            Spark(CombatFx.Body(t), -Forward);
        }
    }

    void EndCharge()
    {
        charging = false;
        if (Move != null) Move.SetActionLock(this, false);
        if (!raised && face != null) face.enabled = false;
        if (!raised) SetBool("ShieldUp", false);
    }

    // ───────── Lv3 B — 방패 던지기 ─────────

    void ThrowDisc()
    {
        StartCooldown(throwCooldown);
        SetTrigger("ShieldBash");
        PlaySound(bashSound);

        discDir = Forward;
        discPos = Owner.transform.position + Vector3.up * 1.1f + discDir * 0.8f;
        discTraveled = 0f;
        discState = DiscState.Out;
        struck.Clear();

        if (disc != null)
        {
            disc.gameObject.SetActive(true);
            disc.position = discPos;
        }
    }

    void TickDisc(float dt)
    {
        if (discState == DiscState.Held) return;

        float step = throwSpeed * dt;
        Vector3 before = discPos;

        if (discState == DiscState.Out)
        {
            discPos += discDir * step;
            discTraveled += step;
            if (discTraveled >= throwRange)
            {
                discState = DiscState.Back;
                struck.Clear();   // 돌아오는 길에 한 번 더 벤다
            }
        }
        else
        {
            Vector3 home = Owner.transform.position + Vector3.up * 1.1f;
            Vector3 back = home - discPos;
            float left = back.magnitude;

            if (left <= step + 0.3f)
            {
                discState = DiscState.Held;
                if (disc != null) disc.gameObject.SetActive(false);
                return;
            }

            discPos += back / left * step;
        }

        if (disc != null)
        {
            disc.position = discPos;
            disc.Rotate(0f, discSpin * dt, 0f, Space.World);
        }

        // 원반이 지나온 구간 전체 (바닥 높이) — 프레임이 끊겨도 사이의 적을 건너뛰지 않는다
        float y = Owner.transform.position.y;
        CombatFx.FindEnemiesAlong(new Vector3(before.x, y, before.z), new Vector3(discPos.x, y, discPos.z), discRadius, targets);
        float damage = WeaponDamage(6f) * throwDamageRatio;

        foreach (Transform t in targets)
        {
            if (!struck.Add(t)) continue;

            CombatFx.Damage(t, damage, CombatFx.Body(t), discDir, false, 0.08f);
            CombatFx.Stun(t, throwStun);
            Spark(CombatFx.Body(t), -discDir);
        }
    }

    // ───────── Lv2 A — 반사판 ─────────

    /// <summary>들고 있는 동안 정면으로 다가온 몬스터 탄을 되돌려 보낸다 (패링의 DeflectedProjectile 재사용).</summary>
    void ReflectProjectiles(float dt)
    {
        reflectScan -= dt;
        if (reflectScan > 0f) return;
        reflectScan = 0.05f;

        // 몬스터 탄은 콜라이더 없이 스스로 움직인다 — 겹침 검사에 안 잡혀 찾아서 본다 (패링과 같은 방식)
        RangedProjectile[] flying = FindObjectsByType<RangedProjectile>(FindObjectsSortMode.None);
        Vector3 origin = Owner.transform.position;
        Vector3 forward = Forward;

        for (int i = 0; i < flying.Length; i++)
        {
            RangedProjectile p = flying[i];
            if (p == null || !p.enabled || !p.gameObject.activeInHierarchy) continue;

            Vector3 d = p.transform.position - origin;
            d.y = 0f;
            if (d.magnitude > faceRadius + reflectReach || Vector3.Angle(forward, d) > blockAngle * 0.5f) continue;

            Vector3 normal = d.sqrMagnitude > 0.0001f ? d.normalized : forward;
            Vector3 incoming = p.transform.forward;
            incoming.y = 0f;
            Vector3 outgoing = incoming.sqrMagnitude > 0.0001f ? Vector3.Reflect(incoming.normalized, normal) : normal;
            outgoing.y = 0f;
            if (Vector3.Dot(outgoing, normal) < 0.2f) outgoing = normal;

            float damage = Mathf.Max(1f, WeaponDamage(6f) * reflectDamageRatio);
            Vector3 at = p.transform.position;

            if (!DeflectedProjectile.Launch(p, outgoing.normalized, reflectSpeedMultiplier, reflectMinSpeed, damage, reflectLifeTime)) continue;

            ReflectedCount++;
            hitFlash = 0.12f;
            Spark(at, outgoing.normalized);
            PlaySound(blockSound);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (bashCd > 0f) bashCd -= dt;
        if (hitFlash > 0f) hitFlash -= dt;

        if (raised && Reflector && Owner != null) ReflectProjectiles(dt);

        if (charging)
        {
            ChargeSweep();
            if (Move == null || !Move.IsDashing) EndCharge();
        }

        TickDisc(dt);

        if (!raised)
        {
            if (regenWait > 0f)
            {
                regenWait -= dt;
                if (regenWait <= 0f) broken = false;
            }
            else
            {
                current = Mathf.Min(MaxDurability, current + Regen * dt);
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
        if (charging) EndCharge();
    }

    void OnDestroy()
    {
        if (sparks != null) Destroy(sparks.gameObject);
    }
}
