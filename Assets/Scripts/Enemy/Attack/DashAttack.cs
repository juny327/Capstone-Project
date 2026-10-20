using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(CapsuleCollider))]
public class DashAttack : EnemyAttack
{
    [Header("Dash")]
    [Min(0.1f)] public float warningDuration = 0.9f;
    [Min(0.1f)] public float dashDistance = 6f;
    [Min(0.1f)] public float dashSpeed = 8f;
    [Min(0f)] public float recoveryDuration = 1f;
    [Min(0.1f)] public float pathWidth = 1.4f;
    public LayerMask obstacleMask = ~0;
    [Header("Telegraph")]
    public Material telegraphMaterial;
    public Color warningColor = new Color(1f, 0.25f, 0.05f, 0.18f);
    public Color fillColor = new Color(1f, 0.45f, 0.05f, 0.65f);
    public Color edgeColor = new Color(1f, 0.8f, 0.15f, 0.95f);
    [Range(0f, 1f)] public float cueVolume = 0.35f;

    public override bool IsBusy => routine != null;
    Coroutine routine;
    NavMeshAgent agent;
    CapsuleCollider body;
    AudioSource audioSource;
    AudioClip cue;
    bool savedRootMotion, savedRotation, motionLocked;
    Vector3 origin, bodyOrigin, direction;
    Quaternion facing;
    float radius, width, height, travel;
    DashFootprint footprint;
    GameObject visual;
    Transform fill;
    readonly List<Mesh> meshes = new List<Mesh>();
    readonly HashSet<PlayerStats> victims = new HashSet<PlayerStats>();
    static readonly WaitForFixedUpdate PhysicsStep = new WaitForFixedUpdate();

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        body = GetComponent<CapsuleCollider>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.5f;
        audioSource.minDistance = 4f;
        audioSource.maxDistance = 25f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        // Short synthesized cue: no external sound asset is required.
        const int sampleRate = 22050;
        float[] samples = new float[(int)(sampleRate * 0.12f)];
        float phase = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / (samples.Length - 1);
            phase += 2f * Mathf.PI * Mathf.Lerp(650f, 1100f, t) / sampleRate;
            samples[i] = Mathf.Sin(phase) * Mathf.Sin(Mathf.PI * t) * 0.4f;
        }
        cue = AudioClip.Create("Monster B dash cue", samples.Length, 1, sampleRate, false);
        cue.SetData(samples, 0);
    }

    void OnEnable() { GameEvents.OnPlayerDeadStart += CancelAttack; }
    void OnDisable()
    {
        GameEvents.OnPlayerDeadStart -= CancelAttack;
        CancelAttack();
    }

    public override void Initialize(Enemy owner)
    {
        CancelAttack();
        base.Initialize(owner);
        lastAttackTime = Time.time - attackCooldown;
    }

    protected override void StartAttack()
    {
        if (IsBusy || enemy.target == null || enemy.state != EnemyState.Attack) return;
        var player = enemy.target.GetComponent<PlayerStats>();
        if (player != null && player.currentHp <= 0) return;
        if (telegraphMaterial == null || !agent.enabled || !agent.isOnNavMesh) return;
        // Delay the coroutine's first work until routine has been assigned.
        routine = StartCoroutine(AttackRoutine());
    }

    IEnumerator AttackRoutine()
    {
        yield return null;
        if (!CanContinue()) { Finish(); yield break; }
        direction = enemy.target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) direction = transform.forward;
        direction.Normalize();
        facing = Quaternion.LookRotation(direction, Vector3.up);
        origin = transform.position;
        enemy.movement.Stop();
        savedRootMotion = enemy.animator.applyRootMotion;
        savedRotation = agent.updateRotation;
        motionLocked = true;
        enemy.animator.applyRootMotion = false;
        agent.updateRotation = false;
        enemy.animator.SetBool("isAttack", false);
        transform.rotation = facing;
        Physics.SyncTransforms();
        bodyOrigin = body.bounds.center;
        radius = body.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        width = Mathf.Max(pathWidth, radius * 2f);
        height = body.bounds.size.y;
        travel = AvailableTravel(bodyOrigin, Mathf.Max(0f, dashDistance));
        NavMeshHit edge;
        if (agent.Raycast(origin + direction * travel, out edge))
            travel = Mathf.Min(travel, Mathf.Max(0f, Vector3.ProjectOnPlane(edge.position - origin, Vector3.up).magnitude - 0.05f));
        if (travel < 0.1f) { Finish(); yield break; }
        footprint = new DashFootprint(width, radius, travel);
        CreateVisual();
        victims.Clear();
        float elapsed = 0f;
        bool playedCue = false;
        while (elapsed < warningDuration)
        {
            if (!CanContinue()) { Finish(); yield break; }
            transform.rotation = facing;
            float progress = Mathf.Clamp01(elapsed / warningDuration);
            SetFill(progress);
            if (!playedCue && warningDuration - elapsed <= 0.15f)
            {
                audioSource.PlayOneShot(cue, cueVolume);
                playedCue = true;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (!CanContinue()) { Finish(); yield break; }
        if (!playedCue) audioSource.PlayOneShot(cue, cueVolume);
        SetFill(1f);
        enemy.animator.SetBool("isMove", true);
        enemy.animator.SetFloat("Speed", dashSpeed);
        float moved = 0f;
        while (moved < travel)
        {
            yield return PhysicsStep;
            if (!CanContinue()) { Finish(); yield break; }
            Physics.SyncTransforms();
            float step = Mathf.Min(dashSpeed * Time.fixedDeltaTime, travel - moved);
            step = Mathf.Min(step, AvailableTravel(bodyOrigin + direction * moved, step));
            if (step <= 0.0001f) break;
            Vector3 destination = origin + direction * (moved + step);
            // Warp follows the fixed straight line rather than steering around obstacles.
            if (!agent.Warp(destination)) break;
            agent.isStopped = true;
            transform.rotation = facing;
            Physics.SyncTransforms();
            ApplySweptDamage(moved, moved + step);
            moved += step;
        }
        if (visual != null) visual.SetActive(false);
        enemy.animator.SetBool("isMove", false);
        enemy.animator.SetFloat("Speed", 0f);
        elapsed = 0f;
        while (elapsed < recoveryDuration)
        {
            if (!CanContinue()) { Finish(); yield break; }
            elapsed += Time.deltaTime;
            yield return null;
        }
        Finish();
    }

    bool CanContinue()
    {
        return enemy != null && enemy.state == EnemyState.Attack && enemy.target != null
            && isActiveAndEnabled && agent.enabled && agent.isOnNavMesh;
    }

    bool IsObstacle(Collider candidate)
    {
        return candidate != null && !candidate.transform.IsChildOf(transform)
            && candidate.GetComponentInParent<Enemy>() == null
            && candidate.GetComponentInParent<IDamageable>() == null
            && (candidate.attachedRigidbody == null || candidate.attachedRigidbody.isKinematic);
    }

    float AvailableTravel(Vector3 center, float distance)
    {
        // Lift the lower face above the floor so the floor is not treated as a wall.
        Vector3 half = new Vector3(width * 0.5f, Mathf.Max(0.05f, height * 0.5f - 0.12f), radius);
        foreach (var collider in Physics.OverlapBox(center, half, facing, obstacleMask, QueryTriggerInteraction.Ignore))
            if (IsObstacle(collider)) return 0f;
        float result = distance;
        foreach (var hit in Physics.BoxCastAll(center, half, direction, facing, distance + 0.05f, obstacleMask, QueryTriggerInteraction.Ignore))
            if (IsObstacle(hit.collider)) result = Mathf.Min(result, Mathf.Max(0f, hit.distance - 0.05f));
        return result;
    }

    void ApplySweptDamage(float from, float to)
    {
        // This swept box is always contained in the displayed rectangle:
        // width = width, longitudinal limits = [-radius, travel + radius].
        footprint.GetSweep(from, to, out float centerOffset, out float halfLength);
        Vector3 center = bodyOrigin + direction * centerOffset;
        Vector3 half = new Vector3(footprint.HalfWidth, height * 0.5f, halfLength);
        foreach (var hit in Physics.OverlapBox(center, half, facing, ~0, QueryTriggerInteraction.Ignore))
        {
            var player = hit.GetComponentInParent<PlayerStats>();
            if (player == null || player.currentHp <= 0 || !victims.Add(player)) continue;
            player.TakeDamage(new DamageInfo {
                damage = enemy.data.attackPower,
                hitPoint = hit.ClosestPoint(center),
                hitDirection = direction,
                cameraShake = 0.35f
            });
        }
    }

    // Compatibility with animation events left in the shared melee animator.
    // Dash damage is exclusively handled by ApplySweptDamage.
    public void PerformAttack() { }

    void CreateVisual()
    {
        DestroyVisual();
        visual = new GameObject("Monster B fixed dash warning");
        visual.layer = 2;
        float ground = body.bounds.min.y;
        RaycastHit floor;
        if (Physics.Raycast(bodyOrigin, Vector3.down, out floor, height + 2f, LayerMask.GetMask("Ground"), QueryTriggerInteraction.Ignore))
            ground = floor.point.y;
        visual.transform.SetPositionAndRotation(new Vector3(bodyOrigin.x, ground + 0.035f, bodyOrigin.z), facing);
        float rear = footprint.Rear;
        float front = footprint.Front;
        float length = front - rear;
        AddQuad("Range", -width * 0.5f, width * 0.5f, rear, front, 0f, warningColor);
        fill = AddQuad("Countdown", -width * 0.5f, width * 0.5f, 0f, 1f, 0f, fillColor).transform;
        fill.localPosition = new Vector3(0f, 0.002f, rear);
        float border = Mathf.Min(0.045f, width * 0.1f);
        AddQuad("Left", -width * 0.5f, -width * 0.5f + border, rear, front, 0.004f, edgeColor);
        AddQuad("Right", width * 0.5f - border, width * 0.5f, rear, front, 0.004f, edgeColor);
        AddQuad("Rear", -width * 0.5f, width * 0.5f, rear, rear + border, 0.004f, edgeColor);
        AddQuad("Front", -width * 0.5f, width * 0.5f, front - border, front, 0.004f, edgeColor);
        float tip = front - border * 2f;
        float arrowLength = Mathf.Min(0.7f, length * 0.4f);
        AddMesh("Direction", new[] {
            new Vector3(-width * 0.32f, 0.008f, tip - arrowLength),
            new Vector3(0f, 0.008f, tip),
            new Vector3(width * 0.32f, 0.008f, tip - arrowLength)
        }, new[] { 0, 1, 2 }, edgeColor);
        SetFill(0f);
    }

    void SetFill(float progress)
    {
        if (fill != null) fill.localScale = new Vector3(1f, 1f, footprint.Length * Mathf.Clamp01(progress));
    }

    GameObject AddQuad(string label, float left, float right, float rear, float front, float y, Color color)
    {
        return AddMesh(label, new[] { new Vector3(left,y,rear), new Vector3(left,y,front),
            new Vector3(right,y,front), new Vector3(right,y,rear) }, new[] { 0,1,2,0,2,3 }, color);
    }

    GameObject AddMesh(string label, Vector3[] vertices, int[] triangles, Color color)
    {
        var part = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
        part.layer = 2;
        part.transform.SetParent(visual.transform, false);
        var mesh = new Mesh { name = label, vertices = vertices, triangles = triangles };
        mesh.RecalculateBounds();
        meshes.Add(mesh);
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = telegraphMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var properties = new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(properties);
        return part;
    }

    void Finish()
    {
        routine = null;
        lastAttackTime = Time.time;
        RestoreMotion();
        if (visual != null) visual.SetActive(false);
    }

    public override void CancelAttack()
    {
        bool wasBusy = routine != null;
        if (wasBusy) StopCoroutine(routine);
        routine = null;
        if (wasBusy) lastAttackTime = Time.time;
        RestoreMotion();
        if (visual != null) visual.SetActive(false);
        if (audioSource != null) audioSource.Stop();
        victims.Clear();
    }

    void RestoreMotion()
    {
        if (!motionLocked) return;
        motionLocked = false;
        if (agent != null) agent.updateRotation = savedRotation;
        if (enemy != null && enemy.animator != null)
        {
            enemy.animator.applyRootMotion = savedRootMotion;
            enemy.animator.SetBool("isMove", false);
            enemy.animator.SetFloat("Speed", 0f);
        }
    }

    void DestroyVisual()
    {
        if (visual != null) { visual.SetActive(false); Destroy(visual); }
        foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
        meshes.Clear();
        fill = null;
    }

    void OnDestroy()
    {
        DestroyVisual();
        if (cue != null) Destroy(cue);
    }
}


