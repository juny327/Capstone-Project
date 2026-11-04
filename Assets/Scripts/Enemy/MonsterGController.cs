using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Stage 3 support enemy. It stays in place and grants a short-range purple aura
/// buff to the nearby combat enemies.
/// </summary>
public sealed class MonsterGController : MonoBehaviour
{
    [Header("Temporary Visual")]
    [SerializeField] private GameObject visualPrefab;
    [Min(0.1f)] [SerializeField] private float visualHeight = 1.8f;

    [Header("Buff Aura")]
    [Min(0.1f)] public float auraRadius = 12f;
    [Range(0f, 1f)] public float attackSpeedBonus = 0.2f;
    [Range(0f, 1f)] public float movementSpeedBonus = 0.15f;
    [Min(0.05f)] public float updateInterval = 0.2f;

    [Header("Aura Visual")]
    public Color auraColor = new Color(0.58f, 0.28f, 0.9f, 1f);
    [Range(0f, 0.3f)] public float fillOpacity = 0.08f;
    [Min(0.01f)] public float ringWidth = 0.09f;

    private Enemy enemy;
    private float nextUpdateTime;
    private GameObject auraVisual;
    private Mesh auraMesh;
    private Material auraMaterial;
    private GameObject visualInstance;
    private Renderer[] originalRenderers;
    private readonly HashSet<Enemy> currentlyBuffed = new HashSet<Enemy>();
    private readonly HashSet<Enemy> nextBuffed = new HashSet<Enemy>();
    private readonly List<Enemy> removeBuffer = new List<Enemy>();

    void Awake()
    {
        // Keep the MonsterF-derived mesh hidden while preserving its gameplay
        // components, health bar and aura renderers.
        originalRenderers = GetComponentsInChildren<Renderer>(true);
        CreateAuraVisual();
    }

    public void Initialize(Enemy owner)
    {
        enemy = owner;
        nextUpdateTime = 0f;
        ClearBuffs();
        EnsureVisual();
        if (auraVisual != null)
            auraVisual.SetActive(true);
    }

    void EnsureVisual()
    {
        if (visualPrefab == null || visualInstance != null)
            return;

        if (originalRenderers != null)
        {
            foreach (Renderer originalRenderer in originalRenderers)
            {
                if (originalRenderer != null)
                    originalRenderer.enabled = false;
            }
        }

        visualInstance = Instantiate(visualPrefab, transform, false);
        visualInstance.name = "MonsterG_ZombieVisual";
        visualInstance.transform.localPosition = Vector3.zero;
        visualInstance.transform.localRotation = Quaternion.identity;

        Animator visualAnimator = visualInstance.GetComponentInChildren<Animator>(true);
        if (visualAnimator != null)
            visualAnimator.applyRootMotion = false;

        Renderer[] renderers = visualInstance.GetComponentsInChildren<Renderer>(true);
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

        if (!hasBounds || bounds.size.y <= 0.001f)
            return;

        visualInstance.transform.localScale = Vector3.one * (visualHeight / bounds.size.y);
        bounds = GetVisualBounds(renderers);
        Vector3 localBottom = transform.InverseTransformPoint(
            new Vector3(transform.position.x, bounds.min.y, transform.position.z));
        visualInstance.transform.localPosition = Vector3.up * -localBottom.y;
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

    public void Tick()
    {
        if (enemy == null || enemy.state == EnemyState.Dead)
        {
            ClearEffects();
            return;
        }

        // G is a stationary support unit. Its EnemyBrain routes to this method
        // instead of the normal chase and attack states.
        if (Time.time < nextUpdateTime)
            return;

        nextUpdateTime = Time.time + updateInterval;
        RefreshBuffs();
    }

    void RefreshBuffs()
    {
        nextBuffed.Clear();
        IReadOnlyList<Enemy> enemies = EnemyManager.Instance != null
            ? EnemyManager.Instance.ActiveEnemies
            : null;
        if (enemies != null)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy candidate = enemies[i];
                if (candidate == null || candidate == enemy ||
                    !candidate.gameObject.activeInHierarchy || candidate.state == EnemyState.Dead)
                    continue;

                bool isMoveType;
                if (!HasBuffableAttack(candidate, out isMoveType))
                    continue;

                if (Vector3.Distance(transform.position, candidate.transform.position) > auraRadius)
                    continue;

                ApplyBuff(candidate, isMoveType);
                nextBuffed.Add(candidate);
            }
        }

        removeBuffer.Clear();
        foreach (Enemy previous in currentlyBuffed)
        {
            if (previous == null || !nextBuffed.Contains(previous))
                removeBuffer.Add(previous);
        }

        foreach (Enemy previous in removeBuffer)
        {
            if (previous != null)
                RemoveBuff(previous);
            currentlyBuffed.Remove(previous);
        }

        foreach (Enemy current in nextBuffed)
            currentlyBuffed.Add(current);
    }

    bool HasBuffableAttack(Enemy candidate, out bool isMoveType)
    {
        isMoveType = false;
        if (candidate.attack is RangedAttack || candidate.attack is LaserAttack)
            return true;

        if (candidate.attack is MeleeAttack || candidate.attack is DashAttack ||
            candidate.attack is SuicideAttack)
        {
            isMoveType = true;
            return true;
        }

        return false;
    }

    void ApplyBuff(Enemy target, bool moveType)
    {
        if (moveType)
        {
            float multiplier = 1f + movementSpeedBonus;
            target.movement?.SetSpeedMultiplier(multiplier);
            target.attack?.SetMovementSpeedMultiplier(multiplier);
            target.attack?.SetAttackSpeedBonus(0f);
        }
        else
        {
            target.movement?.SetSpeedMultiplier(1f);
            target.attack?.SetMovementSpeedMultiplier(1f);
            target.attack?.SetAttackSpeedBonus(attackSpeedBonus);
        }
    }

    void RemoveBuff(Enemy target)
    {
        target.movement?.SetSpeedMultiplier(1f);
        target.attack?.ResetBuffModifiers();
    }

    void ClearBuffs()
    {
        foreach (Enemy target in currentlyBuffed)
        {
            if (target != null)
                RemoveBuff(target);
        }
        currentlyBuffed.Clear();
        nextBuffed.Clear();
        removeBuffer.Clear();
    }

    public void ClearEffects()
    {
        ClearBuffs();
        if (auraVisual != null)
            auraVisual.SetActive(false);
    }

    void CreateAuraVisual()
    {
        auraVisual = new GameObject("MonsterG_PurpleBuffAura");
        auraVisual.transform.SetParent(transform, false);
        auraVisual.transform.localPosition = new Vector3(0f, 0.035f, 0f);

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader != null)
        {
            auraMaterial = new Material(shader);
            auraMaterial.color = Color.white;
            auraMaterial.renderQueue = (int)RenderQueue.Transparent;
            CreateAuraDisk();
        }

        CreateAuraRing("AuraBoundary", auraRadius, 0.28f);
        CreateAuraRing("AuraInnerRing", auraRadius * 0.94f, 0.12f);
    }

    void CreateAuraDisk()
    {
        GameObject disk = new GameObject("AuraSoftFill");
        disk.transform.SetParent(auraVisual.transform, false);
        MeshFilter filter = disk.AddComponent<MeshFilter>();
        MeshRenderer renderer = disk.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = auraMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        const int segments = 64;
        Vector3[] vertices = new Vector3[segments + 1];
        Color[] colors = new Color[segments + 1];
        int[] triangles = new int[segments * 3];
        vertices[0] = Vector3.zero;
        colors[0] = new Color(auraColor.r, auraColor.g, auraColor.b, fillOpacity);
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * auraRadius, 0f,
                Mathf.Sin(angle) * auraRadius);
            colors[i + 1] = new Color(auraColor.r, auraColor.g, auraColor.b, fillOpacity * 0.18f);

            int triangle = i * 3;
            int current = i + 1;
            int next = (i + 1) % segments + 1;
            triangles[triangle] = 0;
            triangles[triangle + 1] = next;
            triangles[triangle + 2] = current;
        }

        auraMesh = new Mesh { name = "MonsterG_AuraDisk" };
        auraMesh.vertices = vertices;
        auraMesh.colors = colors;
        auraMesh.triangles = triangles;
        auraMesh.RecalculateNormals();
        filter.sharedMesh = auraMesh;
    }

    void CreateAuraRing(string ringName, float radius, float alpha)
    {
        GameObject ringObject = new GameObject(ringName);
        ringObject.transform.SetParent(auraVisual.transform, false);
        LineRenderer line = ringObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.alignment = LineAlignment.View;
        line.positionCount = 97;
        line.widthMultiplier = ringWidth;
        line.numCapVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        Color color = new Color(auraColor.r, auraColor.g, auraColor.b, alpha);
        line.startColor = color;
        line.endColor = color;
        if (auraMaterial != null)
            line.sharedMaterial = auraMaterial;

        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / (line.positionCount - 1);
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0.01f,
                Mathf.Sin(angle) * radius));
        }
    }

    void OnDisable()
    {
        ClearBuffs();
        enemy = null;
        if (auraVisual != null)
            auraVisual.SetActive(false);
    }

    void OnDestroy()
    {
        if (auraMesh != null)
            Destroy(auraMesh);
        if (auraMaterial != null)
            Destroy(auraMaterial);
    }
}
