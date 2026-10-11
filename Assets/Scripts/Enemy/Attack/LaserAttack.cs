using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public sealed class LaserAttack : EnemyAttack
{
    [Header("Laser")]
    [Min(0.1f)] public float warningDuration = 1.4f;
    [Min(0f)] public float aimDuration = 0.8f;
    [Min(0f)] public float predictionTime = 0.45f;
    [Min(0f)] public float maxPredictionDistance = 2.5f;
    [Min(0.05f)] public float fireDuration = 0.4f;
    [Min(0f)] public float recoveryDuration = 0.8f;
    [Min(1f)] public float maxRange = 13f;
    [Min(0.05f)] public float beamRadius = 0.3f;
    public Vector3 localFirePoint = new Vector3(0f, 1.1f, 0.65f);
    public Material beamMaterial;
    public Color warningColor = new Color(1f, 0.08f, 0.04f, 0.7f);
    public Color fireColor = new Color(1f, 0.35f, 0.08f, 1f);

    public override bool IsBusy => routine != null;

    private readonly HashSet<PlayerStats> victims = new HashSet<PlayerStats>();
    private Coroutine routine;
    private NavMeshAgent agent;
    private LineRenderer beam;
    private LineRenderer core;
    private Light muzzleLight;
    private Light impactLight;
    private Light auraLight;
    private MaterialPropertyBlock properties;
    private Vector3 previousTargetPosition;
    private Vector3 targetVelocity;
    private bool motionLocked;
    private bool oldAgentRotation;
    private bool oldRootMotion;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        var visual = new GameObject("Laser beam");
        visual.layer = 2; // Ignore Raycast
        visual.transform.SetParent(transform, false);
        beam = visual.AddComponent<LineRenderer>();
        beam.useWorldSpace = true;
        beam.positionCount = 2;
        beam.numCapVertices = 4;
        beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beam.receiveShadows = false;
        beam.sharedMaterial = beamMaterial;
        properties = new MaterialPropertyBlock();
        properties.SetFloat("_UseVertexColor", 1f);
        beam.SetPropertyBlock(properties);
        beam.enabled = false;

        var coreObject = new GameObject("Laser bright core");
        coreObject.layer = 2;
        coreObject.transform.SetParent(transform, false);
        core = coreObject.AddComponent<LineRenderer>();
        core.useWorldSpace = true;
        core.positionCount = 2;
        core.numCapVertices = 4;
        core.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        core.receiveShadows = false;
        core.sharedMaterial = beamMaterial;
        core.SetPropertyBlock(properties);
        core.enabled = false;

        muzzleLight = CreateLight("Laser muzzle flash", transform);
        impactLight = CreateLight("Laser impact flash", transform);
        auraLight = CreateLight("Laser caster aura", transform);
        auraLight.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        auraLight.range = 3.2f;
        auraLight.intensity = 0.9f;
    }

    private void OnEnable() => GameEvents.OnPlayerDeadStart += CancelAttack;

    private void OnDisable()
    {
        GameEvents.OnPlayerDeadStart -= CancelAttack;
        CancelAttack();
    }

    public override void Initialize(Enemy owner)
    {
        CancelAttack();
        base.Initialize(owner);
        lastAttackTime = Time.time - attackCooldown;
        previousTargetPosition = owner.target != null ? owner.target.position : transform.position;
        targetVelocity = Vector3.zero;
    }

    public override bool HasLineOfSight()
    {
        if (enemy == null || enemy.target == null) return false;
        var player = enemy.target.GetComponent<PlayerStats>();
        if (player == null || player.IsDead) return false;

        Vector3 origin = FirePoint();
        Vector3 delta = TargetPoint() - origin;
        if (delta.sqrMagnitude > maxRange * maxRange) return false;

        if (TryFirstHit(origin, delta.normalized, delta.magnitude, 0.05f, out RaycastHit hit))
            return hit.collider.GetComponentInParent<PlayerStats>() == player;
        return false;
    }

    protected override void StartAttack()
    {
        if (IsBusy || beamMaterial == null || !CanContinue() || !HasLineOfSight()) return;
        routine = StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        // Let StartCoroutine return before assigning routine.
        yield return null;
        if (!CanContinue()) { Finish(); yield break; }

        enemy.movement.Stop();
        oldAgentRotation = agent.updateRotation;
        oldRootMotion = enemy.animator.applyRootMotion;
        motionLocked = true;
        agent.updateRotation = false;
        enemy.animator.applyRootMotion = false;
        enemy.animator.SetBool("isAttack", false);
        beam.enabled = true;
        core.enabled = true;
        previousTargetPosition = enemy.target.position;
        targetVelocity = Vector3.zero;
        SetBeamStyle(warningColor, 0.07f);

        float elapsed = 0f;
        Vector3 direction = transform.forward;
        bool attackAnimationStarted = false;
        while (elapsed < warningDuration)
        {
            if (!CanContinue()) { Finish(); yield break; }
            // attack00 winds up during the final part of the warning and
            // reaches its strike as the damaging beam appears.
            if (!attackAnimationStarted && elapsed >= warningDuration - 0.75f)
            {
                enemy.animator.SetBool("isAttack", true);
                attackAnimationStarted = true;
            }
            // Track only at the start of the warning. The remaining warning
            // shows the exact firing lane so the player can move out of it.
            if (elapsed < aimDuration)
            {
                Vector3 currentPosition = enemy.target.position;
                float dt = Mathf.Max(Time.deltaTime, 0.001f);
                Vector3 measuredVelocity = (currentPosition - previousTargetPosition) / dt;
                measuredVelocity.y = 0f;
                // Smooth animation and physics jitter without losing player turns.
                targetVelocity = Vector3.Lerp(targetVelocity, measuredVelocity,
                    1f - Mathf.Exp(-12f * dt));
                previousTargetPosition = currentPosition;
                Vector3 lead = Vector3.ClampMagnitude(
                    targetVelocity * predictionTime, maxPredictionDistance);
                Vector3 toTarget = TargetPoint() + lead - FirePoint();
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(toTarget.normalized);
                direction = transform.forward;
            }
            float charge = Mathf.Clamp01(elapsed / warningDuration);
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * (12f + charge * 20f));
            SetBeamStyle(Color.Lerp(warningColor,
                new Color(1f, 0.7f, 0.2f, 1f), charge * pulse),
                Mathf.Lerp(0.045f, 0.16f, charge * (0.65f + 0.35f * pulse)));
            muzzleLight.transform.position = FirePoint();
            muzzleLight.intensity = Mathf.Lerp(0.5f, 3f, charge * pulse);
            DrawBeam(FirePoint(), direction, 0f, false);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // The direction is fixed for the whole damaging phase.
        direction = transform.forward;
        victims.Clear();
        SetBeamStyle(fireColor, beamRadius * 2f);
        elapsed = 0f;
        while (elapsed < fireDuration)
        {
            if (!CanContinue()) { Finish(); yield break; }
            float flare = 0.85f + 0.15f * Mathf.Sin(elapsed * 65f);
            SetBeamStyle(fireColor, beamRadius * 2f * flare);
            muzzleLight.intensity = 5f * flare;
            impactLight.intensity = 4f * flare;
            DrawBeam(FirePoint(), direction, beamRadius, true);
            elapsed += Time.deltaTime;
            yield return null;
        }

        beam.enabled = false;
        core.enabled = false;
        muzzleLight.intensity = 0f;
        impactLight.intensity = 0f;
        enemy.animator.SetBool("isAttack", false);
        elapsed = 0f;
        while (elapsed < recoveryDuration)
        {
            if (!CanContinue()) { Finish(); yield break; }
            elapsed += Time.deltaTime;
            yield return null;
        }
        Finish();
    }

    private bool CanContinue()
    {
        if (!isActiveAndEnabled || enemy == null || enemy.state != EnemyState.Attack || enemy.target == null)
            return false;
        var player = enemy.target.GetComponent<PlayerStats>();
        return player != null && !player.IsDead && agent != null && agent.enabled && agent.isOnNavMesh;
    }

    private Vector3 FirePoint() => transform.TransformPoint(localFirePoint);

    private Vector3 TargetPoint() => enemy.target.position + Vector3.up * 1f;

    private void DrawBeam(Vector3 origin, Vector3 direction, float radius, bool dealDamage)
    {
        float distance = maxRange;
        if (TryFirstHit(origin, direction, maxRange, radius, out RaycastHit hit))
        {
            distance = hit.distance;
            if (dealDamage)
            {
                var player = hit.collider.GetComponentInParent<PlayerStats>();
                if (player != null && victims.Add(player))
                {
                    player.TakeDamage(new DamageInfo {
                        damage = enemy.data.attackPower,
                        hitPoint = hit.point,
                        hitDirection = direction,
                        cameraShake = 0.2f
                    });
                }
            }
        }
        beam.SetPosition(0, origin);
        beam.SetPosition(1, origin + direction * distance);
        core.SetPosition(0, origin);
        core.SetPosition(1, origin + direction * distance);
        impactLight.transform.position = origin + direction * distance;
    }

    private bool TryFirstHit(Vector3 origin, Vector3 direction, float distance, float radius, out RaycastHit closest)
    {
        closest = default;
        bool found = false;
        foreach (var hit in Physics.SphereCastAll(origin, Mathf.Max(0.01f, radius), direction,
                     distance, ~0, QueryTriggerInteraction.Ignore))
        {
            Collider collider = hit.collider;
            if (collider == null || collider.transform.IsChildOf(transform) ||
                collider.GetComponentInParent<Enemy>() != null ||
                collider.GetComponentInParent<BossHealth>() != null ||
                collider.GetComponentInParent<ExpOrb>() != null)
                continue;
            if (!found || hit.distance < closest.distance)
            {
                closest = hit;
                found = true;
            }
        }
        return found;
    }

    private void SetBeamStyle(Color color, float width)
    {
        beam.startWidth = width;
        beam.endWidth = width;
        beam.startColor = color;
        beam.endColor = color;
        core.startWidth = width * 0.3f;
        core.endWidth = width * 0.3f;
        Color coreColor = Color.Lerp(color, Color.white, 0.75f);
        core.startColor = coreColor;
        core.endColor = coreColor;
        properties.SetColor("_BaseColor", color);
        beam.SetPropertyBlock(properties);
        properties.SetColor("_BaseColor", coreColor);
        core.SetPropertyBlock(properties);
    }

    private static Light CreateLight(string name, Transform parent)
    {
        var visual = new GameObject(name);
        visual.layer = 2;
        if (parent != null) visual.transform.SetParent(parent, false);
        var light = visual.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.18f, 0.04f);
        light.range = 4f;
        light.intensity = 0f;
        light.shadows = LightShadows.None;
        return light;
    }

    private void Finish()
    {
        routine = null;
        lastAttackTime = Time.time;
        Restore();
    }

    public override void CancelAttack()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        victims.Clear();
        Restore();
    }

    private void Restore()
    {
        if (beam != null) beam.enabled = false;
        if (core != null) core.enabled = false;
        if (muzzleLight != null) muzzleLight.intensity = 0f;
        if (impactLight != null) impactLight.intensity = 0f;
        if (enemy != null && enemy.animator != null)
            enemy.animator.SetBool("isAttack", false);
        if (!motionLocked) return;
        motionLocked = false;
        if (agent != null) agent.updateRotation = oldAgentRotation;
        if (enemy != null && enemy.animator != null)
            enemy.animator.applyRootMotion = oldRootMotion;
    }

    // Old ranged animation events must not apply a second hit.
    public void PerformAttack() { }
}
