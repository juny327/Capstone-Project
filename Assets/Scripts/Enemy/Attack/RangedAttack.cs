using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class RangedAttack : EnemyAttack
{
    [Min(0.1f)] public float warningDuration = 0.8f;
    [Range(0f, 80f)] public float spreadAngle = 25f;
    public Vector3 localFirePoint = new Vector3(0f, 1.1f, 0.65f);
    public GameObject projectilePrefab;
    public Material chargeMaterial;
    public override bool IsBusy => routine != null;
    Coroutine routine;
    NavMeshAgent agent;
    GameObject charge;
    Renderer chargeRenderer;
    MaterialPropertyBlock properties;
    bool locked, oldRotation, oldRootMotion;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        charge = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        charge.name = "Monster C charge";
        charge.layer = 2;
        charge.transform.SetParent(transform, false);
        charge.transform.localPosition = localFirePoint;
        var collider = charge.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        chargeRenderer = charge.GetComponent<Renderer>();
        chargeRenderer.sharedMaterial = chargeMaterial;
        chargeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        chargeRenderer.receiveShadows = false;
        properties = new MaterialPropertyBlock();
        charge.SetActive(false);
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

    Vector3 AimDirection()
    {
        Vector3 heading = enemy.target.position - transform.position;
        heading.y = 0f;
        return heading.sqrMagnitude > 0.001f ? heading.normalized : transform.forward;
    }

    Vector3 FirePoint(Vector3 heading)
    {
        return transform.position + Quaternion.LookRotation(heading) * Vector3.Scale(localFirePoint, transform.lossyScale);
    }

    public override bool HasLineOfSight()
    {
        if (enemy == null || enemy.target == null) return false;
        var player = enemy.target.GetComponent<PlayerStats>();
        if (player != null && player.currentHp <= 0) return false;
        Vector3 heading = AimDirection();
        Vector3 muzzle = FirePoint(heading);
        Vector3 center = transform.position + Vector3.up * (localFirePoint.y * transform.lossyScale.y);
        Vector3 target = new Vector3(enemy.target.position.x, muzzle.y, enemy.target.position.z);
        float radius = projectilePrefab != null ? projectilePrefab.GetComponent<RangedProjectile>().radius : 0.16f;
        return IsClear(center, muzzle, radius) && IsClear(muzzle, target, radius);
    }

    bool IsClear(Vector3 from, Vector3 to, float radius)
    {
        foreach (var collider in Physics.OverlapSphere(from, radius, ~0, QueryTriggerInteraction.Ignore))
            if (!RangedProjectile.IgnoreCollider(collider) && collider.GetComponentInParent<PlayerStats>() == null) return false;
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < 0.0001f) return true;
        foreach (var hit in Physics.SphereCastAll(from, radius, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (!RangedProjectile.IgnoreCollider(hit.collider) && hit.collider.GetComponentInParent<PlayerStats>() == null) return false;
        return true;
    }

    protected override void StartAttack()
    {
        if (IsBusy || projectilePrefab == null || chargeMaterial == null || !CanContinue() || !HasLineOfSight()) return;
        routine = StartCoroutine(AttackRoutine());
    }

    bool CanContinue()
    {
        if (!isActiveAndEnabled || enemy == null || enemy.state != EnemyState.Attack || enemy.target == null) return false;
        var player = enemy.target.GetComponent<PlayerStats>();
        return player == null || player.currentHp > 0;
    }

    IEnumerator AttackRoutine()
    {
        yield return null;
        if (!CanContinue()) { Finish(); yield break; }
        enemy.movement.Stop();
        oldRotation = agent.updateRotation;
        oldRootMotion = enemy.animator.applyRootMotion;
        locked = true;
        agent.updateRotation = false;
        enemy.animator.applyRootMotion = false;
        enemy.animator.SetBool("isAttack", false);
        charge.SetActive(true);
        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            if (!CanContinue()) { Finish(); yield break; }
            transform.rotation = Quaternion.LookRotation(AimDirection());
            float progress = Mathf.Clamp01(elapsed / warningDuration);
            charge.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 0.7f, progress);
            properties.SetColor("_BaseColor", Color.Lerp(new Color(1f, 0.35f, 0.05f, 0.8f), new Color(1f, 0.9f, 0.2f, 1f), progress));
            chargeRenderer.SetPropertyBlock(properties);
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (CanContinue() && Vector3.Distance(transform.position, enemy.target.position) <= enemy.data.attackRange && HasLineOfSight())
        {
            Vector3 heading = AimDirection();
            transform.rotation = Quaternion.LookRotation(heading);
            Vector3 muzzle = FirePoint(heading);
            var volley = new VolleyHitGroup();
            float speed = EnemyManager.Instance != null ? EnemyManager.Instance.rangedProjectileSpeed : 3.5f;
            for (int index = -1; index <= 1; index++)
            {
                if (!CanContinue()) break;
                var obj = PoolManager.Instance.Get(projectilePrefab);
                if (obj == null) continue;
                Vector3 direction = Quaternion.AngleAxis(index * spreadAngle, Vector3.up) * heading;
                obj.GetComponent<RangedProjectile>().Initialize(muzzle, direction, speed, enemy.data.attackPower, volley);
            }
        }
        Finish();
    }

    // Legacy melee animation events must not cause an extra instant hit.
    public void PerformAttack() { }

    void Finish()
    {
        routine = null;
        Restore();
        // Base class measures the 3-second interval from the warning's start.
    }

    public override void CancelAttack()
    {
        if (routine != null) { StopCoroutine(routine); lastAttackTime = Time.time; }
        routine = null;
        Restore();
    }

    void Restore()
    {
        if (charge != null) charge.SetActive(false);
        if (!locked) return;
        locked = false;
        if (agent != null) agent.updateRotation = oldRotation;
        if (enemy != null && enemy.animator != null) enemy.animator.applyRootMotion = oldRootMotion;
    }
}
