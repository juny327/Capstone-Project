using UnityEngine;

/// <summary>
/// 모든 무기의 공통 구현. 쿨다운, 스탯 합성, 크리티컬 판정, 이펙트 스폰을 담당한다.
/// 파생 클래스는 OnFire 만 구현하면 된다.
///
/// ⚠ WeaponData(SO)는 절대 수정하지 않는다. 업그레이드 누적분은 accumulated 에만 쌓고,
///    실제 사용값은 매번 data.Compose(TotalModifier) 로 만든다.
///    SO 를 직접 고치면 에디터에서 에셋에 영구 기록되어 플레이마다 수치가 누적된다.
/// </summary>
public abstract class WeaponBase : MonoBehaviour, IWeapon
{
    [Header("Data")]
    [SerializeField] protected WeaponData data;

    [Header("Transforms")]
    [Tooltip("투사체·머즐플래시가 나오는 지점. 비우면 자기 Transform 을 쓴다")]
    [SerializeField] protected Transform muzzle;

    [Tooltip("탄피가 나오는 지점. 없으면 탄피를 생성하지 않는다")]
    [SerializeField] protected Transform casingPoint;

    private WeaponModifier accumulated = WeaponModifier.Identity;
    private float cooldown;
    private int level = 1;

    /// <summary>이 무기를 소유한 컨트롤러. 플레이어 루트 참조용.</summary>
    protected WeaponController Owner { get; private set; }

    public WeaponData Data => data;
    public int Level => level;
    public bool CanFire => cooldown <= 0f && IsActive;
    public bool IsActive { get; private set; } = true;

    /// <summary>누적 업그레이드 + 레벨 보너스.</summary>
    protected WeaponModifier TotalModifier =>
        WeaponModifier.Combine(accumulated, data.perLevelBonus.Scaled(level - 1));

    public WeaponRuntimeStats Stats => data.Compose(TotalModifier);

    protected Transform Muzzle => muzzle != null ? muzzle : transform;

    /// <summary>WeaponController 가 장착 직후 호출한다.</summary>
    public virtual void Initialize(WeaponData weaponData, WeaponController owner)
    {
        if (weaponData != null)
            data = weaponData;

        Owner = owner;

        if (data == null)
        {
            Debug.LogError($"[{GetType().Name}] WeaponData 가 없습니다.", this);
            enabled = false;
            return;
        }

        // 서브유닛은 스왑과 무관하게 항상 동작한다
        IsActive = data.IsAlwaysActive;
        if (data.IsAlwaysActive)
            SetActive(true);

        cooldown = 0f;
    }

    public void Tick(float deltaTime)
    {
        if (cooldown > 0f)
            cooldown -= deltaTime;

        OnTick(deltaTime);
    }

    public void Fire(in WeaponFireContext context)
    {
        WeaponRuntimeStats stats = Stats;

        OnFire(in stats, in context);

        cooldown = stats.FireInterval;
    }

    public void ApplyModifier(in WeaponModifier modifier)
    {
        accumulated = WeaponModifier.Combine(accumulated, modifier);
        OnStatsChanged();
    }

    public virtual void SetLevel(int newLevel)
    {
        int max = data != null ? Mathf.Max(1, data.maxLevel) : 1;
        level = Mathf.Clamp(newLevel, 1, max);
        OnStatsChanged();
    }

    public virtual void SetActive(bool active)
    {
        // 서브유닛은 비활성화하지 않는다
        if (data != null && data.IsAlwaysActive)
            active = true;

        IsActive = active;

        // 모델 표시/숨김. 무기 프리팹 루트가 곧 이 컴포넌트의 오브젝트다.
        for (int i = 0; i < transform.childCount; i++)
            transform.GetChild(i).gameObject.SetActive(active);

        var renderers = GetComponents<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = active;

        if (active)
            cooldown = 0f;   // 스왑 직후 바로 쏠 수 있게
    }

    // ───────── 파생 클래스가 구현/확장하는 부분 ─────────

    /// <summary>실제 발사 동작.</summary>
    protected abstract void OnFire(in WeaponRuntimeStats stats, in WeaponFireContext context);

    /// <summary>매 프레임 추가 처리가 필요한 무기용 (드론 궤도 등).</summary>
    protected virtual void OnTick(float deltaTime) { }

    /// <summary>레벨·모디파이어가 바뀌었을 때. 드론이 수를 맞추는 데 쓴다.</summary>
    protected virtual void OnStatsChanged() { }

    // ───────── 공통 헬퍼 ─────────

    /// <summary>
    /// 크리티컬 판정. 무기가 판정 주체라는 점이 중요하다 —
    /// 투사체가 없는 무기(근접/드론)도 같은 규칙을 쓸 수 있다.
    /// </summary>
    protected bool RollCritical(in WeaponRuntimeStats stats)
    {
        return Random.value < stats.CritChance;
    }

    protected float ApplyCritical(in WeaponRuntimeStats stats, bool isCritical)
    {
        return isCritical ? stats.Damage * stats.CritMultiplier : stats.Damage;
    }

    protected void SpawnEffect(GameObject prefab, Transform at)
    {
        if (prefab == null || at == null) return;
        if (PoolManager.Instance == null) return;

        GameObject fx = PoolManager.Instance.Get(prefab);
        if (fx == null) return;

        fx.transform.SetPositionAndRotation(at.position, at.rotation);
    }

    /// <summary>조준 방향 결정: 타겟이 있으면 그쪽, 없으면 플레이어가 보는 방향.</summary>
    protected Vector3 ResolveFireDirection(in WeaponFireContext context, float maxRange, out Transform target)
    {
        target = context.Targets != null
            ? context.Targets.GetNearest(Muzzle.position, maxRange)
            : null;

        if (target == null)
            return context.AimDirection;

        Vector3 dir = target.position - Muzzle.position;
        dir.y = 0f;

        return dir.sqrMagnitude > 0.0001f ? dir.normalized : context.AimDirection;
    }

    /// <summary>index 번째 탄의 퍼짐을 적용한 방향. 탄이 1발이면 그대로 반환한다.</summary>
    protected static Vector3 ApplySpread(Vector3 direction, float spreadAngle, int index, int total)
    {
        if (spreadAngle <= 0f || total <= 1)
        {
            if (spreadAngle <= 0f) return direction;

            // 단발이라도 spread 가 있으면 무작위로 흔든다
            float jitter = Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f);
            return Quaternion.Euler(0f, jitter, 0f) * direction;
        }

        // 여러 발이면 부채꼴로 균등 분산
        float step = spreadAngle / (total - 1);
        float angle = -spreadAngle * 0.5f + step * index;

        return Quaternion.Euler(0f, angle, 0f) * direction;
    }
}
