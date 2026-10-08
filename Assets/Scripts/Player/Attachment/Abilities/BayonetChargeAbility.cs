using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 하부 부착물 "총검 돌격" — 사수의 E (커스터마이징-구현계획.md 4-4).
/// 커서 쪽으로 짧게 돌진하며 찌른다. 맞은 적은 밀리고 잠깐 경직 · 돌진 중 무적 —
/// <b>둘러싸였을 때 빠져나가는</b> 사수의 근접 대응이다 (구르기는 피하기, 돌격은 뚫기).
/// 돌진은 그랩과 같은 플레이어 대시 통로(PlayerMove.TryDash)를 쓴다.
/// </summary>
public class BayonetChargeAbility : SubAbility
{
    [Header("돌진")]
    [Min(0.5f)] [SerializeField] float dashDistance = 3f;
    [Min(0.05f)] [SerializeField] float dashDuration = 0.18f;
    [Min(0f)] [SerializeField] float invulnerable = 0.3f;

    [Header("찌르기")]
    [Min(0.2f)] [SerializeField] float hitRadius = 1.1f;
    [Tooltip("판정 중심을 몸 앞으로 이만큼")]
    [Min(0f)] [SerializeField] float hitForward = 0.7f;
    [Min(0f)] [SerializeField] float damageRatio = 4f;
    [Min(0f)] [SerializeField] float minDamage = 8f;
    [Min(0f)] [SerializeField] float push = 1.5f;
    [Min(0.01f)] [SerializeField] float pushDuration = 0.15f;
    [Min(0f)] [SerializeField] float stun = 0.6f;
    [Min(0f)] [SerializeField] float cooldownSeconds = 5f;

    [Header("연출")]
    [SerializeField] Transform blade;
    [SerializeField] ParryFlash sparkPrefab;
    [SerializeField] GameSoundSet.Entry dashSound = new GameSoundSet.Entry();
    [SerializeField] GameSoundSet.Entry hitSound = new GameSoundSet.Entry();

    float cooldown;
    bool charging;
    float damage;
    Vector3 sweepFrom;   // 지난 프레임 판정 자리 — 이번 프레임까지의 구간을 훑는다
    ParryFlash sparks;
    readonly HashSet<Transform> struck = new HashSet<Transform>();
    readonly List<Transform> targets = new List<Transform>();

    public override float CooldownRemaining => Mathf.Max(0f, cooldown);
    public override float CooldownTotal => cooldownSeconds;
    public override bool IsBusy => charging;

    /// <summary>마지막 돌진에 맞은 적 수 (검증용).</summary>
    public int LastHitCount => struck.Count;

    public override void Equip(GameObject owner, SubAbilityData data, int level)
    {
        base.Equip(owner, data, level);
        ShowBlade(false);
    }

    public override void OnPressed()
    {
        if (cooldown > 0f || charging || Owner == null || Move == null) return;
        if (Move.IsRolling || Move.IsDashing) return;

        if (!Move.TryDash(Forward, dashDistance, dashDuration, invulnerable)) return;

        charging = true;
        struck.Clear();
        sweepFrom = Owner.transform.position + Forward * hitForward;
        damage = Mathf.Max(minDamage, WeaponDamage(minDamage) * damageRatio);
        cooldown = cooldownSeconds;

        Move.SetActionLock(this, true);
        if (Weapons != null) Weapons.SuppressFiring(dashDuration + 0.1f);

        ShowBlade(true);
        PlaySound(dashSound);
        Sweep();
    }

    public override void Cancel()
    {
        if (!charging) return;
        if (Move != null) Move.StopDash();
        Finish();
    }

    void Update()
    {
        if (cooldown > 0f) cooldown -= Time.deltaTime;
        if (!charging) return;

        Sweep();
        if (Move == null || !Move.IsDashing) Finish();
    }

    void LateUpdate()
    {
        if (!charging || blade == null || Owner == null) return;

        blade.position = Owner.transform.position + Vector3.up * 1.1f + Forward * 0.9f;
        blade.rotation = Quaternion.LookRotation(Forward, Vector3.up);
    }

    /// <summary>몸 앞 반경 안의 아직 안 찌른 적을 찌른다 — 지난 프레임부터 지나온 구간 전체를 본다.</summary>
    void Sweep()
    {
        Vector3 center = Owner.transform.position + Forward * hitForward;
        CombatFx.FindEnemiesAlong(sweepFrom, center, hitRadius, targets);
        sweepFrom = center;

        foreach (Transform target in targets)
        {
            if (!struck.Add(target)) continue;

            Vector3 at = CombatFx.Body(target);
            CombatFx.Damage(target, damage, at, Forward, false, 0.12f);
            CombatFx.Stun(target, stun);
            CombatFx.Push(this, target, Forward, push, pushDuration);

            if (sparkPrefab != null)
            {
                if (sparks == null) sparks = Instantiate(sparkPrefab);
                sparks.Clash(at, -Forward);
            }

            PlaySound(hitSound);
        }
    }

    void Finish()
    {
        charging = false;
        ShowBlade(false);
        if (Move != null) Move.SetActionLock(this, false);
    }

    void ShowBlade(bool on)
    {
        if (blade != null) blade.gameObject.SetActive(on);
    }

    void OnDisable()
    {
        if (Move != null) Move.SetActionLock(this, false);
    }

    void OnDestroy()
    {
        if (sparks != null) Destroy(sparks.gameObject);
    }
}
