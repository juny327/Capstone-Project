using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Monster D's warning, charge, and self-destruct attack.
/// The optional Devil Bulldog visual is configured only on MonsterD.prefab.
/// </summary>
public class SuicideAttack : EnemyAttack
{
    [Header("Charge")]
    [Min(0f)] public float explosionDelay = 0.9f;
    [Min(0.1f)] public float chargeSpeed = 10f;
    [Min(0.1f)] public float chargeDuration = 0.8f;
    [Min(0f)] public float chargeStopDistance = 1.4f;
    [Min(0.1f)] public float explosionRadius = 2.25f;
    public GameObject explosionEffect;

    [Header("Monster D Visual")]
    public GameObject visualPrefab;
    [Min(0.1f)] public float visualHeight = 1.5f;

    private bool exploded;
    private Coroutine attackRoutine;
    private GameObject visualInstance;
    private Animator visualAnimator;
    private NavMeshAgent agent;
    private bool originalAgentRotationSetting;
    private Vector3 chargeDirection;
    private float chargeTravelDistance;
    private LineRenderer warningLine;
    private Material warningMaterial;
    private float baseChargeSpeed;

    public override bool IsBusy => attackRoutine != null;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        baseChargeSpeed = chargeSpeed;

        // The original D rig remains available for its death animation and
        // health components, but its root motion must not move the replacement.
        Animator originalAnimator = GetComponent<Animator>();
        if (originalAnimator != null)
            originalAnimator.applyRootMotion = false;

        SetupReplacementVisual();
        SetupWarningLine();
    }

    public override void SetMovementSpeedMultiplier(float multiplier)
    {
        chargeSpeed = baseChargeSpeed * Mathf.Max(0f, multiplier);
    }

    void SetupReplacementVisual()
    {
        if (visualPrefab == null || visualInstance != null)
            return;

        // Hide only MonsterD's old rendered model. Canvas UI uses CanvasRenderer
        // and remains visible; MonsterE has no SuicideAttack component changes.
        Renderer[] oldRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer oldRenderer in oldRenderers)
            oldRenderer.enabled = false;

        visualInstance = Instantiate(visualPrefab, transform);
        visualInstance.name = "MonsterD_DevilBulldogVisual";
        visualInstance.transform.localPosition = Vector3.zero;
        visualInstance.transform.localRotation = Quaternion.identity;
        visualInstance.transform.localScale = Vector3.one;
        visualAnimator = visualInstance.GetComponentInChildren<Animator>(true);
        if (visualAnimator != null)
            visualAnimator.applyRootMotion = false;

        // Normalize against the imported renderer bounds so a differently
        // authored model keeps a predictable in-game size.
        Renderer[] modelRenderers = visualInstance.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = default;
        foreach (Renderer modelRenderer in modelRenderers)
        {
            if (!(modelRenderer is MeshRenderer) && !(modelRenderer is SkinnedMeshRenderer))
                continue;

            if (!hasBounds)
            {
                bounds = modelRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(modelRenderer.bounds);
            }
        }

        if (!hasBounds || bounds.size.y <= 0.001f)
        {
            Debug.LogWarning("MonsterD Devil Bulldog visual has no usable mesh bounds.", this);
            return;
        }

        visualInstance.transform.localScale = Vector3.one * (visualHeight / bounds.size.y);
        bounds = GetVisualBounds(modelRenderers);
        Vector3 localBottom = transform.InverseTransformPoint(
            new Vector3(transform.position.x, bounds.min.y, transform.position.z));
        visualInstance.transform.localPosition = Vector3.up * -localBottom.y;
    }

    void SetupWarningLine()
    {
        GameObject warningObject = new GameObject("ChargeDirectionWarning");
        warningObject.transform.SetParent(transform, false);
        warningLine = warningObject.AddComponent<LineRenderer>();
        warningLine.positionCount = 2;
        warningLine.useWorldSpace = true;
        warningLine.widthMultiplier = 0.18f;
        warningLine.numCapVertices = 4;
        warningLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        warningLine.receiveShadows = false;
        warningLine.startColor = new Color(1f, 0.12f, 0.08f, 0.9f);
        warningLine.endColor = new Color(1f, 0.12f, 0.08f, 0.15f);
        warningLine.enabled = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            warningMaterial = new Material(shader);
            warningLine.material = warningMaterial;
        }
    }

    Bounds GetVisualBounds(Renderer[] renderers)
    {
        bool hasBounds = false;
        Bounds bounds = default;
        foreach (Renderer modelRenderer in renderers)
        {
            if (!(modelRenderer is MeshRenderer) && !(modelRenderer is SkinnedMeshRenderer))
                continue;

            if (!hasBounds)
            {
                bounds = modelRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(modelRenderer.bounds);
            }
        }
        return bounds;
    }

    protected override void StartAttack()
    {
        if (exploded || enemy == null)
            return;

        exploded = true;
        CaptureChargeDirection();
        attackRoutine = enemy.StartCoroutine(ChargeAndExplodeRoutine());
    }

    void CaptureChargeDirection()
    {
        Vector3 direction = enemy.target != null
            ? enemy.target.position - enemy.transform.position
            : enemy.transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            direction = enemy.transform.forward;

        chargeDirection = direction.normalized;
        chargeTravelDistance = Mathf.Max(0f, direction.magnitude - chargeStopDistance);
        enemy.transform.rotation = Quaternion.LookRotation(chargeDirection, Vector3.up);

        if (warningLine != null)
        {
            Vector3 start = enemy.transform.position + Vector3.up * 0.08f;
            warningLine.SetPosition(0, start);
            warningLine.SetPosition(1, start + chargeDirection * chargeTravelDistance);
            warningLine.enabled = true;
        }
    }

    IEnumerator ChargeAndExplodeRoutine()
    {
        enemy.movement.Stop();
        PlayAttackSound();
        PlayVisualAnimation("Roar");

        // Brief, readable warning before the lunge.
        yield return new WaitForSeconds(explosionDelay);

        if (warningLine != null)
            warningLine.enabled = false;
        yield return ChargeRoutine();

        // The existing explosion prefab is reused; only the damage radius is
        // separated from the larger distance used to start the charge.
        if (explosionEffect != null && PoolManager.Instance != null)
        {
            GameObject fx = PoolManager.Instance.Get(explosionEffect);
            fx.transform.position = enemy.transform.position;
        }

        Collider[] hits = Physics.OverlapSphere(enemy.transform.position, explosionRadius);
        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag("Player"))
                continue;

            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable == null)
                continue;

            DamageInfo info = new DamageInfo
            {
                damage = enemy.data.attackPower,
                isCritical = false,
                hitPoint = hit.transform.position,
                hitDirection = (hit.transform.position - enemy.transform.position).normalized,
                cameraShake = 0.3f
            };
            damageable.TakeDamage(info);
        }

        enemy.OnDeath?.Invoke(enemy);
        attackRoutine = null;
        PoolManager.Instance.Return(enemy.gameObject);
    }

    IEnumerator ChargeRoutine()
    {
        Transform target = enemy.target;
        if (target == null)
            yield break;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            originalAgentRotationSetting = agent.updateRotation;
            agent.updateRotation = false;
            agent.isStopped = false;
        }

        float maxTravel = chargeTravelDistance;
        float travelled = 0f;
        float elapsed = 0f;

        while (elapsed < chargeDuration && travelled < maxTravel)
        {
            if (enemy == null || enemy.state == EnemyState.Dead)
                yield break;

            float step = Mathf.Min(chargeSpeed * Time.deltaTime, maxTravel - travelled);
            Vector3 displacement = chargeDirection * step;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.Move(displacement);
            else
                enemy.transform.position += displacement;

            travelled += step;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.updateRotation = originalAgentRotationSetting;
        }
    }

    void PlayVisualAnimation(string stateName)
    {
        if (visualAnimator == null)
            return;

        int stateHash = Animator.StringToHash(stateName);
        if (visualAnimator.HasState(0, stateHash))
            visualAnimator.CrossFadeInFixedTime(stateHash, 0.08f);
    }

    public override void CancelAttack()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (warningLine != null)
            warningLine.enabled = false;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.updateRotation = originalAgentRotationSetting;
        }

        exploded = false;
    }

    void OnDestroy()
    {
        if (warningMaterial != null)
            Destroy(warningMaterial);
    }
}
