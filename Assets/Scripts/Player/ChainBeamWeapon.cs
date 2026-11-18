using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전격 소총 — 마우스 커서 방향으로 **얇은 전기 레이저**를 쏘고,
/// 적을 맞히면 그 적에서 주위 적으로 전기가 약하게 퍼진다.
///
/// 투사체가 아니라 즉시 명중(히트스캔)이다. 총알이 날아가는 동안 적이 비켜서는 일이 없다.
///
/// 퍼지는 부분은 되돌린 서브유닛 `ChainWeapon`(테슬라 코일)과 같은 방식이다 —
/// 반경 판정이 아니라 **적 → 적 경로**를 따라간다. 연쇄는 성격이 범위 공격과 다르기 때문이다.
/// 다만 코일은 가장 가까운 적을 자동으로 때렸고, 이쪽은 **조준한 적이 시작점**이다.
///
/// 연출은 프리팹 없이 LineRenderer 로 그린다. 지그재그를 주지 않으면 전기로 보이지 않는다.
/// </summary>
public class ChainBeamWeapon : WeaponBase
{
    private ChainBeamWeaponData Config => (ChainBeamWeaponData)data;

    protected override bool AutoReload => Config == null || Config.autoReload;

    // 할당 없이 재사용하는 버퍼
    private readonly RaycastHit[] beamHits = new RaycastHit[16];
    private readonly Collider[] nearby = new Collider[64];

    // 다음 연쇄 대상을 찾는 구(반경 4.5m 이상)는 바닥 타일 충돌체(Ground)에 닿는다 — 버퍼가 바닥으로 차지 않게 뺀다
    private static int ChainMask(LayerMask layers)
    {
        int ground = LayerMask.NameToLayer("Ground");
        return ground >= 0 ? layers & ~(1 << ground) : layers;
    }
    private readonly HashSet<Transform> struck = new HashSet<Transform>();

    // 줄기 하나당 LineRenderer 하나. 본 줄기 1 + 연쇄 N 개를 미리 만들어 돌려 쓴다.
    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private Material runtimeMaterial;
    private float hideAt = -1f;
    private bool visible;

    // 이번 발의 줄기들 — 보이는 동안 다시 흔들어 그린다 (rejitterInterval)
    struct BoltSpec { public Vector3 from, to; public Color color; public float width; }
    private readonly List<BoltSpec> bolts = new List<BoltSpec>();
    private Vector3[] boltPoints = new Vector3[9];
    private float nextJitterAt;

    /// <summary>시험용 — 연쇄가 옮겨 붙은 자리에 번쩍임을 그린 횟수.</summary>
    public static int ChainFlashes { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetChainFlashes() => ChainFlashes = 0;

    /// <summary>
    /// 사거리 안에 적이 있을 때만 쏜다.
    ///
    /// "일정 범위 안에 적이 들어오면" 이 조건이 여기다.
    /// 이걸 확인하지 않으면 적이 없는데도 계속 쏘며 탄을 버린다.
    /// </summary>
    public override bool HasTargetInReach(in WeaponFireContext context)
    {
        if (context.Targets == null) return false;

        return context.Targets.GetNearest(Muzzle.position, Stats.Range) != null;
    }

    protected override void OnFire(in WeaponRuntimeStats stats, in WeaponFireContext context)
    {
        ChainBeamWeaponData cfg = Config;

        if (cfg == null)
        {
            Debug.LogError($"[ChainBeamWeapon] ChainBeamWeaponData 가 아닙니다. ({name})", this);
            enabled = false;
            return;
        }

        struck.Clear();
        ResetLines();
        bolts.Clear();

        Vector3 origin = Muzzle.position;
        Vector3 dir = context.AimDirection;   // 마우스 커서 방향

        // 1. 커서 방향으로 얇은 레이저 — 처음 맞은 적 하나를 찾는다. 벽 · 엄폐물이 있으면 거기까지만
        float reach = CombatFx.ObstacleDistance(origin, dir, stats.Range);
        Transform first = CastBeam(cfg, origin, dir, reach, out Vector3 hitPoint);

        // 맞지 않아도 레이저는 보여야 한다. 안 그러면 쏘는지 안 쏘는지 알 수 없다.
        Vector3 beamEnd = first != null ? hitPoint : origin + dir * reach;

        DrawBolt(cfg, origin, beamEnd, cfg.beamColor, cfg.beamWidth);
        SpawnFx(cfg.muzzleFlashPrefab, origin, Quaternion.LookRotation(dir));
        if (cfg.muzzleRing > 0f && cfg.beamMaterial != null)
            FxLine.Ring(cfg.beamMaterial, origin - Vector3.up * 0.15f, 0.05f, cfg.muzzleRing, cfg.beamColor, 0.07f, 0.12f);

        if (first == null)
        {
            // 벽에 막혔으면 거기서 튄다
            if (reach < stats.Range)
            {
                SpawnFx(cfg.hitEffectPrefab, beamEnd, Quaternion.LookRotation(-dir));
                CombatFx.ObstacleHitSound();
            }
            Show(cfg);
            return;
        }

        // 2. 맞은 적에게 피해
        float damage = stats.Damage;

        Hit(cfg, in stats, first, hitPoint, damage, dir, isFirst: true, damageFactor: 1f);
        struck.Add(first);

        // 3. 거기서부터 주위로 약하게 퍼진다
        Vector3 from = hitPoint;
        Transform current = first;

        float damageFactor = 1f;
        for (int i = 0; i < stats.MaxTargets; i++)
        {
            damage *= cfg.chainFalloff;
            damageFactor *= cfg.chainFalloff;

            if (damage <= 0.01f) break;

            Transform next = FindNext(cfg, current.position);

            if (next == null) break;

            Vector3 to = next.position + Vector3.up * cfg.hitHeight;

            Hit(cfg, in stats, next, to, damage, (to - from).normalized, isFirst: false, damageFactor: damageFactor);
            DrawBolt(cfg, from, to, cfg.arcColor, cfg.arcWidth);

            struck.Add(next);
            current = next;
            from = to;
        }

        Show(cfg);
    }

    // ───────── 판정 ─────────

    /// <summary>커서 방향으로 훑어 처음 맞는 적을 찾는다. 없으면 null.</summary>
    Transform CastBeam(ChainBeamWeaponData cfg, Vector3 origin, Vector3 dir, float range,
        out Vector3 point)
    {
        point = Vector3.zero;

        int count = Physics.SphereCastNonAlloc(
            origin, cfg.beamRadius, dir, beamHits, range,
            cfg.hitLayers, QueryTriggerInteraction.Collide);

        Transform best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = beamHits[i].collider;

            if (col == null) continue;

            // Bullet 의 기존 규약과 맞춘다 — 태그로 적을 가린다
            if (!col.CompareTag("Enemy") && !col.CompareTag("Boss")) continue;

            if (col.GetComponentInParent<IDamageable>() == null) continue;

            // SphereCast 는 순서를 보장하지 않는다. 가장 가까운 것을 직접 고른다.
            float distance = beamHits[i].distance;

            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = ResolveRoot(col);

            Vector3 p = beamHits[i].point;

            // 시작 지점에 이미 겹쳐 있으면 point 가 0 으로 온다
            point = p == Vector3.zero ? col.ClosestPoint(origin) : p;
        }

        return best;
    }

    /// <summary>from 에서 가장 가까운, 아직 맞지 않은 적.</summary>
    Transform FindNext(ChainBeamWeaponData cfg, Vector3 from)
    {
        int count = Physics.OverlapSphereNonAlloc(
            from, cfg.chainRange, nearby, ChainMask(cfg.hitLayers), QueryTriggerInteraction.Collide);

        Transform best = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = nearby[i];

            if (col == null) continue;
            if (!col.CompareTag("Enemy") && !col.CompareTag("Boss")) continue;
            if (col.GetComponentInParent<IDamageable>() == null) continue;

            Transform root = ResolveRoot(col);

            // 같은 적을 두 번 때리지 않는다. 막지 않으면 두 적 사이를 왕복한다.
            if (struck.Contains(root)) continue;

            float sqr = (root.position - from).sqrMagnitude;

            if (sqr >= bestSqr) continue;

            // 벽 너머로는 튀지 않는다 (몸통 높이에서 잰다 — 발밑은 바닥에 닿는다)
            Vector3 up = Vector3.up * cfg.hitHeight;
            if (!CombatFx.HasLineOfSight(from + up, root.position + up)) continue;

            bestSqr = sqr;
            best = root;
        }

        return best;
    }

    static Transform ResolveRoot(Collider col)
    {
        IDamageable d = col.GetComponentInParent<IDamageable>();
        Component component = d as Component;

        return component != null ? component.transform : col.transform;
    }

    void Hit(ChainBeamWeaponData cfg, in WeaponRuntimeStats stats, Transform target,
        Vector3 point, float damage, Vector3 dir, bool isFirst, float damageFactor)
    {
        IDamageable damageable = target.GetComponentInParent<IDamageable>();

        if (damageable == null) return;

        // 치명타는 조준해서 맞힌 첫 적에게만 판정한다.
        // 퍼지는 전기까지 치명타를 띄우면 한 발에 숫자가 여러 번 터져 읽히지 않는다.
        bool isCritical = isFirst && RollCritical(in stats);
        float effectDamage = FloorSupportDamage(isCritical ? ApplyCritical(in stats, true) : damage);
        float dealt = ResolveDirectDamage(effectDamage,
            damageFactor * (isCritical ? stats.CritMultiplier : 1f));
        bool notify = (isFirst || cfg.notifyEveryChain) && HasHitEffects;
        bool wasAlive = CombatFx.IsAlive(target);

        damageable.TakeDamage(new DamageInfo
        {
            damage = dealt,
            isCritical = isCritical,
            hitPoint = point,
            hitDirection = dir,
            cameraShake = isFirst ? cfg.cameraShake : 0f,
        });

        // 조준해서 맞힌 첫 적에서 한 번 흔든다 (2026-11-12 — 피해 정보의 cameraShake 는 적이 쓰지 않아 흔들린 적이 없었다)
        if (isFirst) ShakeCamera(this, cfg.cameraShake);

        // 퍼진 전기도 전격 소총의 직접 공격 — 쓰러뜨리면 Drain
        bool killed = CheckDirectKill(target, wasAlive);

        // 부착물 명중 효과는 조준해서 맞힌 첫 적에게만 — 퍼진 전기마다 터지면 연쇄가 연쇄를 부른다
        if (notify)
        {
            NotifyHit(new WeaponHit
            {
                Target = target,
                Damageable = damageable,
                Point = point,
                Direction = dir,
                Damage = dealt,
                EffectDamage = effectDamage,
                IsCritical = isCritical,
                Killed = killed,
            });
        }

        SpawnFx(cfg.hitEffectPrefab, point, Quaternion.identity);

        // 맞은 자리 바닥에 번쩍
        if (cfg.coreWidth > 0f) GroundGlow.Flash(new Vector3(point.x, target.position.y, point.z), isFirst ? 1.5f : 1.1f, cfg.beamColor, 0.16f);

        // 맞은 자리에서 튀는 짧은 전기 가지
        if (cfg.hitBranches > 0 && cfg.beamMaterial != null)
        {
            for (int i = 0; i < cfg.hitBranches; i++)
            {
                Vector3 d = Random.onUnitSphere;
                d.y = Mathf.Abs(d.y) * 0.4f;
                FxLine.Bolt(cfg.beamMaterial, point, point + d.normalized * Random.Range(0.45f, 0.9f), cfg.beamColor,
                            Mathf.Max(0.04f, cfg.beamWidth * 0.35f), 0.11f, 0.1f);
            }
        }

        // 연쇄가 옮겨 붙은 자리 — 적 머리 위에서 짧게 번쩍 (뇌운)
        if (!isFirst && cfg.chainFlash > 0f && cfg.beamMaterial != null)
        {
            Vector3 top = point + Vector3.up * 0.9f;
            float turn = Random.Range(0f, 90f);
            for (int i = 0; i < 2; i++)
            {
                Vector3 d = Quaternion.AngleAxis(turn + i * 90f, Vector3.up) * Vector3.forward * cfg.chainFlash * 0.5f;
                FxLine.Line(cfg.beamMaterial, top - d, top + d, cfg.coreColor, 0.09f, 0.1f);
            }
            FxLine.Bolt(cfg.beamMaterial, top + Vector3.up * cfg.chainFlash, point, cfg.beamColor, Mathf.Max(0.05f, cfg.arcWidth * 0.6f), 0.1f, 0.12f);
            ChainFlashes++;
        }

        // 조준한 적 + 퍼진 적이 한 프레임에 들어온다. 소리는 프레임 끝에 한 번만.
        if (SoundManager.Instance != null)
            SoundManager.Instance.ReportHit(isCritical, HitSound.Electric);
        if (isFirst) PlayHitLayer();   // 상위 무기 전용 적중음 — 조준해서 맞힌 첫 적에서 한 번
    }

    // ───────── 연출 ─────────

    protected override void OnTick(float deltaTime)
    {
        if (!visible) return;

        if (Time.time >= hideAt)
        {
            ResetLines();
            visible = false;
            return;
        }

        // 보이는 동안 줄기 모양을 다시 흔든다 — 한 모양으로 멈춰 있으면 전기가 아니라 철사로 보인다
        ChainBeamWeaponData cfg = Config;
        if (cfg == null || cfg.rejitterInterval <= 0f || Time.time < nextJitterAt) return;

        nextJitterAt = Time.time + cfg.rejitterInterval;
        ResetLines();
        for (int i = 0; i < bolts.Count; i++) RenderBolt(cfg, bolts[i]);
    }

    /// <summary>두 점 사이를 지그재그로 잇는다. 곧은 선은 전기로 보이지 않는다.</summary>
    void DrawBolt(ChainBeamWeaponData cfg, Vector3 from, Vector3 to, Color color, float width)
    {
        var spec = new BoltSpec { from = from, to = to, color = color, width = width };
        bolts.Add(spec);
        RenderBolt(cfg, spec);
    }

    /// <summary>줄기 하나를 그린다 — 바깥 줄기 + (coreWidth 가 있으면) 같은 모양의 안쪽 흰 심지.</summary>
    void RenderBolt(ChainBeamWeaponData cfg, BoltSpec spec)
    {
        Vector3 from = spec.from, to = spec.to;
        Color color = spec.color;
        float width = spec.width;

        LineRenderer line = NextLine(cfg);

        if (line == null) return;

        // 긴 줄기는 마디를 늘린다 — 마디 8개로 14m 를 그리면 곧은 레이저처럼 보인다 (마디 하나 약 0.7m)
        int segments = Mathf.Clamp(Mathf.CeilToInt((to - from).magnitude / 0.7f), Mathf.Max(2, cfg.segments), 28);
        if (boltPoints.Length < segments + 1) boltPoints = new Vector3[segments + 1];

        line.positionCount = segments + 1;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.sortingOrder = 0;
        FxGlow.Apply(line, cfg.coreWidth > 0f ? FxGlow.Body : 1f);

        Vector3 axis = to - from;

        // 흔들 방향 두 개를 축과 직각으로 잡는다
        Vector3 side = Vector3.Cross(axis, Vector3.up);

        if (side.sqrMagnitude < 0.0001f) side = Vector3.right;

        side.Normalize();

        Vector3 up = Vector3.Cross(axis.normalized, side);

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 point = from + axis * t;

            // 양 끝은 정확히 맞물려야 하므로 가운데만 흔든다
            if (i != 0 && i != segments)
            {
                float amount = cfg.jitter * Mathf.Max(0.35f, Mathf.Sin(t * Mathf.PI));   // 긴 줄기는 양 끝 가까이도 조금은 흔든다

                point += side * Random.Range(-amount, amount);
                point += up * Random.Range(-amount, amount);
            }

            boltPoints[i] = point;
            line.SetPosition(i, point);
        }

        line.enabled = true;

        if (cfg.coreWidth <= 0f) return;

        // 안쪽 심지 — 같은 점들을 그대로, 굵기는 바깥 줄기에 비례해서 (연쇄 가지는 더 가늘다)
        LineRenderer core = NextLine(cfg);
        if (core == null) return;

        float coreWidth = cfg.coreWidth * (cfg.beamWidth > 0.0001f ? width / cfg.beamWidth : 1f);
        Color coreColor = cfg.coreColor;
        coreColor.a *= color.a;
        core.positionCount = segments + 1;
        core.startWidth = coreWidth;
        core.endWidth = coreWidth;
        core.startColor = coreColor;
        core.endColor = coreColor;
        core.sortingOrder = 1;
        FxGlow.Apply(core, FxGlow.Core);
        for (int i = 0; i <= segments; i++) core.SetPosition(i, boltPoints[i]);
        core.enabled = true;
    }

    void Show(ChainBeamWeaponData cfg)
    {
        visible = true;
        hideAt = Time.time + cfg.beamDuration;
        nextJitterAt = Time.time + cfg.rejitterInterval;
    }

    LineRenderer NextLine(ChainBeamWeaponData cfg)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i] != null && !lines[i].enabled) return lines[i];
        }

        LineRenderer created = CreateLine(cfg);

        if (created != null) lines.Add(created);

        return created;
    }

    void ResetLines()
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i] != null) lines[i].enabled = false;
        }
    }

    LineRenderer CreateLine(ChainBeamWeaponData cfg)
    {
        GameObject go = new GameObject($"Bolt{lines.Count}");
        go.transform.SetParent(transform, false);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;          // 무기가 돌아가도 줄기는 쏜 자리에 남는다
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        line.material = ResolveMaterial(cfg);

        return line;
    }

    Material ResolveMaterial(ChainBeamWeaponData cfg)
    {
        if (cfg.beamMaterial != null) return cfg.beamMaterial;
        if (runtimeMaterial != null) return runtimeMaterial;

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            Debug.LogWarning($"[ChainBeamWeapon] 빔 머티리얼이 없습니다. ({name})", this);
            return null;
        }

        runtimeMaterial = new Material(shader);

        return runtimeMaterial;
    }

    void SpawnFx(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null || PoolManager.Instance == null) return;

        GameObject fx = PoolManager.Instance.Get(prefab);

        if (fx == null) return;

        fx.transform.SetPositionAndRotation(position, rotation);
    }

    public override void SetActive(bool active)
    {
        base.SetActive(active);

        // 스왑으로 손에서 빠지면 줄기도 지운다. 남아 있으면 다시 들었을 때 그대로 보인다.
        if (active) return;

        ResetLines();
        visible = false;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }
}
