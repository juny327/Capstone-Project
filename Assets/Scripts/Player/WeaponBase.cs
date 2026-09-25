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

    // 탄창 (12번 문서). 탄창 크기 0 인 무기(근접·서브유닛)는 이 값들을 쓰지 않는다.
    private int ammo;
    private float reloadRemaining;

    /// <summary>이 무기를 소유한 컨트롤러. 플레이어 루트 참조용.</summary>
    protected WeaponController Owner { get; private set; }

    public WeaponData Data => data;
    public int Level => level;

    /// <summary>쿨다운이 끝나고, 장전 중이 아니며, 탄약이 남아 있어야 쏠 수 있다.</summary>
    public bool CanFire => cooldown <= 0f && IsActive && !IsReloading && HasAmmo;

    public bool IsActive { get; private set; } = true;

    // ───────── 탄창 ─────────

    /// <summary>탄창 크기. 0 이면 무제한이라 장전 자체를 하지 않는다.</summary>
    public int MagazineSize => data != null ? Stats.MagazineSize : 0;

    public int Ammo => ammo;

    public bool IsReloading => reloadRemaining > 0f;

    /// <summary>탄창을 쓰지 않는 무기이거나, 탄이 남아 있는가.</summary>
    public bool HasAmmo => MagazineSize <= 0 || ammo > 0;

    /// <summary>
    /// 다음 발사까지 남은 시간(초). 0 이하면 쏠 수 있다.
    ///
    /// 충전형 무기가 "얼마나 모였는지"를 연출하는 데 쓴다 —
    /// 발사 간격이 곧 충전 시간이므로 별도 상태를 들 필요가 없다.
    /// </summary>
    protected float CooldownRemaining => cooldown;

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

        // 탄창을 가득 채운 상태로 시작한다
        ammo = Stats.MagazineSize;
        reloadRemaining = 0f;
    }

    public void Tick(float deltaTime)
    {
        if (cooldown > 0f)
            cooldown -= deltaTime;

        if (reloadRemaining > 0f)
        {
            reloadRemaining -= deltaTime;

            if (reloadRemaining <= 0f)
            {
                reloadRemaining = 0f;
                ammo = Stats.MagazineSize;

                PlaySound(data != null ? data.reloadEndSound : null,
                    data != null ? data.reloadVolume : 1f);

                OnReloadFinished();
                RaiseAmmoChanged();
            }
        }

        OnTick(deltaTime);
    }

    public void Fire(in WeaponFireContext context)
    {
        WeaponRuntimeStats stats = Stats;

        OnFire(in stats, in context);

        cooldown = stats.FireInterval;

        PlayFireSound();

        // 산탄이어도 1회 발사는 탄약 1 소모다 (12번 R3).
        // 탄 수만큼 깎으면 산탄 업그레이드가 곧 탄약 소모 증가가 되어 벌칙이 된다.
        if (stats.MagazineSize > 0)
        {
            ammo = Mathf.Max(0, ammo - 1);
            RaiseAmmoChanged();
        }
    }

    /// <summary>
    /// 장전을 시작한다. 탄창이 가득이거나 이미 장전 중이면 아무 일도 하지 않는다.
    /// 탄이 남아 있어도 수동으로 장전할 수 있다 (12번 R1).
    /// </summary>
    public void StartReload()
    {
        WeaponRuntimeStats stats = Stats;

        if (stats.MagazineSize <= 0) return;   // 탄창을 쓰지 않는 무기
        if (IsReloading) return;
        if (ammo >= stats.MagazineSize) return;
        if (!IsActive) return;

        reloadRemaining = stats.ReloadTime;

        PlayReloadStartSound(stats.ReloadTime);

        OnReloadStarted(stats.ReloadTime);
        RaiseAmmoChanged();
    }

    /// <summary>탄이 떨어졌고 자동 장전을 쓰는 무기면 장전을 시작한다.</summary>
    public void TryAutoReload()
    {
        if (IsReloading || HasAmmo) return;
        if (!AutoReload) return;

        StartReload();
    }

    /// <summary>장전을 취소한다. 스왑·구르기·사망에서 부른다. 탄약은 그대로 둔다 (12번 R4).</summary>
    public void CancelReload()
    {
        if (!IsReloading) return;

        reloadRemaining = 0f;

        OnReloadCanceled();
        RaiseAmmoChanged();
    }

    /// <summary>자동 장전 여부. 투사체 무기가 데이터 값으로 재정의한다.</summary>
    protected virtual bool AutoReload => true;

    // ───────── 소리 ─────────

    void PlayFireSound()
    {
        if (data == null) return;

        AudioClip clip = data.PickFireSound();

        if (clip == null) return;

        float min = Mathf.Min(data.firePitchRange.x, data.firePitchRange.y);
        float max = Mathf.Max(data.firePitchRange.x, data.firePitchRange.y);

        if (min <= 0f) min = 1f;
        if (max <= 0f) max = 1f;

        PlaySound(clip, data.fireVolume, Random.Range(min, max));
    }

    /// <summary>
    /// 장전 소리를 **장전 시간에 맞춰 배속 재생**한다.
    ///
    /// 장전 시간이 무기마다 다른데(소총 2.0 · 기관단총 1.8 · 스나이퍼 2.6초)
    /// 클립 길이는 하나다. 애니메이션을 `ReloadSpeed` 로 맞춘 것과 같은 방식이다.
    /// </summary>
    void PlayReloadStartSound(float duration)
    {
        if (data == null || data.reloadStartSound == null) return;

        float pitch = 1f;
        float length = data.reloadStartSound.length;

        // 너무 크게 늘이거나 줄이면 소리가 우스워진다
        if (duration > 0.05f && length > 0.05f)
            pitch = Mathf.Clamp(length / duration, 0.6f, 1.8f);

        PlaySound(data.reloadStartSound, data.reloadVolume, pitch);
    }

    /// <summary>
    /// 플레이어 무기 소리는 2D 로 낸다 — 탑다운 카메라라 거리 감쇠가 어색하다.
    /// `SoundManager` 가 없으면 조용히 넘어간다 (씬 구성이 덜 된 경우).
    /// </summary>
    protected void PlaySound(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null || SoundManager.Instance == null) return;

        SoundManager.Instance.PlayClip(clip, volume, pitch);
    }

    /// <summary>
    /// 손에 든 무기의 탄약 변화를 HUD 에 알린다.
    /// 서브유닛은 스왑 대상이 아니므로 발행하지 않는다 — 발행하면 HUD 숫자가 드론 것으로 덮인다.
    /// </summary>
    protected void RaiseAmmoChanged()
    {
        if (!IsActive) return;
        if (data == null || data.IsAlwaysActive) return;

        GameEvents.OnAmmoChanged?.Invoke(IsReloading ? 0 : ammo, MagazineSize);
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
        {
            cooldown = 0f;       // 스왑 직후 바로 쏠 수 있게
            RaiseAmmoChanged();  // HUD 를 이 무기의 탄약으로 바꾼다
            return;
        }

        // 손에서 빠질 때 장전을 취소한다. 탄약은 유지되므로 다시 들면 그 상태 그대로다 (12번 R4).
        // 취소하지 않으면 무기를 바꿔 둔 사이에 장전이 끝난다.
        CancelReload();
    }

    // ───────── 파생 클래스가 구현/확장하는 부분 ─────────

    /// <summary>
    /// 지금 쏠(휘두를) 대상이 이 무기의 사거리 안에 있는지. 기본은 항상 true 다.
    /// 근접 무기처럼 사거리가 감지 범위보다 짧은 무기가 재정의해, 허공에 휘두르며 쿨다운을 쓰지 않게 한다.
    /// </summary>
    public virtual bool HasTargetInReach(in WeaponFireContext context) => true;

    /// <summary>실제 발사 동작.</summary>
    protected abstract void OnFire(in WeaponRuntimeStats stats, in WeaponFireContext context);

    /// <summary>매 프레임 추가 처리가 필요한 무기용 (드론 궤도 등).</summary>
    protected virtual void OnTick(float deltaTime) { }

    /// <summary>장전이 시작될 때. 투사체 무기가 애니메이션을 재생한다.</summary>
    protected virtual void OnReloadStarted(float duration) { }

    /// <summary>장전이 끝나 탄창이 찼을 때.</summary>
    protected virtual void OnReloadFinished() { }

    /// <summary>장전이 도중에 취소됐을 때 (스왑·구르기·사망).</summary>
    protected virtual void OnReloadCanceled() { }

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
