using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 하부 부착물 "유탄 발사기" — 사수의 E (커스터마이징-구현계획.md 4-4).
/// 커서 쪽으로 유탄을 포물선으로 던지고, 떨어진 자리에서 반경 폭발. 뭉친 적을 정리한다.
/// 피해 = 들고 있는 총의 공격력 × 5 (최소 10) — 약한 총에도 쓸모가 있게 최소값을 둔다.
/// </summary>
public class GrenadeLauncherAbility : SubAbility
{
    [Header("유탄")]
    [Min(1f)] [SerializeField] float range = 8f;
    [Min(1f)] [SerializeField] float minRange = 3f;
    [Min(0.3f)] [SerializeField] float radius = 2.5f;
    [Min(0f)] [SerializeField] float damageRatio = 5f;
    [Min(0f)] [SerializeField] float minDamage = 10f;
    [Min(0.05f)] [SerializeField] float flightTime = 0.45f;
    [Min(0f)] [SerializeField] float arcHeight = 2.2f;
    [Min(0f)] [SerializeField] float cameraShake = 0.25f;
    [Min(0f)] [SerializeField] float cooldownSeconds = 6f;

    [Header("연출")]
    [SerializeField] Transform shell;
    [SerializeField] Material fxMaterial;
    [SerializeField] Color blastColor = new Color(1f, 0.55f, 0.18f, 0.95f);
    [SerializeField] ParryFlash sparkPrefab;
    [SerializeField] GameSoundSet.Entry fireSound = new GameSoundSet.Entry();
    [SerializeField] GameSoundSet.Entry blastSound = new GameSoundSet.Entry();

    float cooldown;
    bool flying;
    Vector3 from;
    Vector3 to;
    float progress;
    float damage;
    ParryFlash sparks;
    readonly List<Transform> targets = new List<Transform>();

    public override float CooldownRemaining => Mathf.Max(0f, cooldown);
    public override float CooldownTotal => cooldownSeconds;

    /// <summary>마지막 폭발에 맞은 적 수 (검증용).</summary>
    public int LastHitCount { get; private set; }

    /// <summary>유탄이 날아가는 중인가.</summary>
    public bool IsFlying => flying;

    public override void Equip(GameObject owner, SubAbilityData data, int level)
    {
        base.Equip(owner, data, level);
        if (shell != null) shell.gameObject.SetActive(false);
    }

    public override void OnPressed()
    {
        if (cooldown > 0f || flying || Owner == null) return;
        if (Move != null && (Move.IsRolling || Move.IsDashing)) return;

        Vector3 origin = Owner.transform.position;
        from = origin + Vector3.up * 1.2f + Forward * 0.6f;
        to = origin + Forward * CursorDistance(minRange, range);

        damage = Mathf.Max(minDamage, WeaponDamage(minDamage) * damageRatio);
        progress = 0f;
        flying = true;
        cooldown = cooldownSeconds;

        if (shell != null)
        {
            shell.gameObject.SetActive(true);
            shell.position = from;
        }

        PlaySound(fireSound);
    }

    // 날아가는 유탄은 무기를 바꿔도 끝까지 간다 — 이미 쏜 것이다
    public override void Cancel() { }

    void Update()
    {
        if (cooldown > 0f) cooldown -= Time.deltaTime;
        if (!flying) return;

        progress += Time.deltaTime / flightTime;
        float t = Mathf.Clamp01(progress);

        if (shell != null)
            shell.position = Vector3.Lerp(from, to, t) + Vector3.up * (arcHeight * 4f * t * (1f - t));

        if (t >= 1f) Explode();
    }

    void Explode()
    {
        flying = false;
        if (shell != null) shell.gameObject.SetActive(false);

        CombatFx.FindEnemies(to, radius, targets);
        bool first = true;
        foreach (Transform target in targets)
        {
            Vector3 dir = target.position - to;
            dir.y = 0f;
            CombatFx.Damage(target, damage, CombatFx.Body(target), dir.sqrMagnitude > 0.0001f ? dir.normalized : Forward,
                false, first ? cameraShake : 0f);
            first = false;
        }

        LastHitCount = targets.Count;

        FxLine.Ring(fxMaterial, to, radius * 0.2f, radius, blastColor, 0.4f, 0.45f);
        FxLine.Ring(fxMaterial, to, radius * 0.1f, radius * 0.6f, new Color(1f, 0.9f, 0.6f, 0.9f), 0.6f, 0.25f);

        if (sparkPrefab != null)
        {
            if (sparks == null) sparks = Instantiate(sparkPrefab);
            sparks.Clash(to + Vector3.up * 0.5f, Vector3.up);
        }

        PlaySound(blastSound);
    }

    void OnDestroy()
    {
        if (sparks != null) Destroy(sparks.gameObject);
    }
}
