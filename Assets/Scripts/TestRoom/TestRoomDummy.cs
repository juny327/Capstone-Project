using UnityEngine;

/// <summary>
/// The skeleton's sole IDamageable. Reuses its model/hitbox/flash, without Enemy AI or death logic.
/// Weapon projectile, melee and beam paths remain exactly the same as in normal stages.
/// </summary>
[RequireComponent(typeof(HitFlashController))]
public class TestRoomDummy : MonoBehaviour, IDamageable
{
    [SerializeField] GameObject criticalDecalPrefab;
    HitFlashController hitFlash;
    public TestRoomDamageStats Stats { get; } = new TestRoomDamageStats();

    void Awake() => hitFlash = GetComponent<HitFlashController>();
    void Update() => Stats.Tick(Time.timeAsDouble);

    public void TakeDamage(DamageInfo info)
    {
        if (!isActiveAndEnabled || info.damage <= 0 || float.IsNaN(info.damage) || float.IsInfinity(info.damage)) return;
        Stats.Record(info, Time.timeAsDouble);
        hitFlash.HitFlash();
        if (DamageTextManager.Instance != null)
            DamageTextManager.Instance.ShowDamage((int)Mathf.Min(info.damage, int.MaxValue - 128f),
                transform.position + Vector3.up * 2f, info.isCritical);

        if (info.isCritical && criticalDecalPrefab != null && PoolManager.Instance != null)
        {
            GameObject fx = PoolManager.Instance.Get(criticalDecalPrefab);
            if (fx != null && fx.TryGetComponent<CriticalDecal>(out var decal))
                decal.Initialize(transform, 1f);
        }
        // Hit sound is reported by the attacking weapon. Do not report it a second time here.
    }
}
