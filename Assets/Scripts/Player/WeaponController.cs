using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>무기 획득 시도 결과. 업그레이드 시스템이 성공/실패를 구분하는 데 쓴다.</summary>
public enum WeaponAcquireResult
{
    /// <summary>새 무기를 장착했다.</summary>
    Equipped,

    /// <summary>이미 가진 무기라 레벨업했다.</summary>
    LeveledUp,

    /// <summary>슬롯이 가득 찼다.</summary>
    NoFreeSlot,

    /// <summary>이미 최대 레벨이다.</summary>
    MaxLevel,

    /// <summary>데이터가 잘못됐다.</summary>
    Invalid,
}

/// <summary>
/// 플레이어의 무기를 소유하고 구동한다.
///
/// 장착 정책 (W3)
///   · 손에 드는 무기(투사체/근접)는 여러 개 보유하지만 **한 번에 하나만 활성**이다.
///     스왑으로 활성 무기를 바꾼다. 비활성 무기는 모델이 숨겨지고 발사되지 않는다.
///   · 드론(SubUnit)은 스왑과 무관하게 **항상 동작**한다. 손에 드는 무기가 아니기 때문이다.
///
/// AutoAttack 을 대체한다. 둘을 동시에 켜두면 이중 발사가 되므로 Awake 에서 경고한다.
/// </summary>
public class WeaponController : MonoBehaviour
{
    [System.Serializable]
    public struct SocketBinding
    {
        public WeaponSocket socket;
        [Tooltip("무기 모델이 붙을 Transform. Hand_Right 아래 빈 오브젝트를 만들어 지정할 것")]
        public Transform point;
    }

    [Header("Slots")]
    [Tooltip("손에 드는 무기(투사체/근접) 보유 가능 수")]
    [Min(1)] [SerializeField] private int maxHeldSlots = 3;

    [Tooltip("서브유닛(드론·오브·방전장 등) 보유 가능 수")]
    [Min(1)] [SerializeField] private int maxSubUnitSlots = 3;

    [Header("Start")]
    [Tooltip("게임 시작 시 장착할 무기. 비워두면 무기 없이 시작한다")]
    [SerializeField] private WeaponData starterWeapon;

    [Header("References")]
    [Tooltip("비우면 자식에서 찾는다")]
    [SerializeField] private EnemyDetector detector;

    [Tooltip("무기 모델 부착점. Hand_Right 아래 소켓을 만들어 연결할 것")]
    [SerializeField] private SocketBinding[] sockets;

    [Header("Swap Input")]
    [Tooltip("임시 직접 입력. 나중에 PlayerInput 의 SwapWeapon 액션으로 옮길 것")]
    [SerializeField] private bool enableDirectSwapInput = true;
    [SerializeField] private Key swapKey = Key.Q;
    [SerializeField] private bool swapWithScrollWheel = true;

    [Tooltip("수동 장전 키. 탄창이 남아 있어도 장전할 수 있다")]
    [SerializeField] private Key reloadKey = Key.R;

    [Header("Legacy HUD")]
    [Tooltip("PlayerStatsUI 가 구독하는 기존 이벤트를 활성 무기 기준으로 발행한다")]
    [SerializeField] private bool raiseLegacyStatEvents = true;

    private readonly List<WeaponBase> held = new List<WeaponBase>();
    private readonly List<WeaponBase> subUnits = new List<WeaponBase>();

    private int activeHeldIndex = -1;
    private bool isDead;
    private Animator anim;

    // 상체 레이어 인덱스 (없으면 -1). 총은 ShootLayer, 근접 무기는 MeleeLayer 를 켠다
    private int shootLayer = -1;
    private int meleeLayer = -1;

    // ───────── 공개 API (업그레이드 시스템이 호출) ─────────

    /// <summary>손에 드는 무기 목록 (스왑 대상).</summary>
    public IReadOnlyList<IWeapon> HeldWeapons => held;

    /// <summary>서브유닛 목록 (항상 동작).</summary>
    public IReadOnlyList<IWeapon> SubUnits => subUnits;

    /// <summary>현재 손에 든 무기. 없으면 null.</summary>
    public IWeapon ActiveWeapon =>
        (activeHeldIndex >= 0 && activeHeldIndex < held.Count) ? held[activeHeldIndex] : null;

    public int ActiveIndex => activeHeldIndex;

    public bool HasFreeHeldSlot => held.Count < maxHeldSlots;
    public bool HasFreeSubUnitSlot => subUnits.Count < maxSubUnitSlots;

    public bool Has(WeaponData data) => Find(data) != null;

    public IWeapon Find(WeaponData data)
    {
        if (data == null) return null;

        for (int i = 0; i < held.Count; i++)
            if (held[i].Data == data) return held[i];

        for (int i = 0; i < subUnits.Count; i++)
            if (subUnits[i].Data == data) return subUnits[i];

        return null;
    }

    /// <summary>
    /// 이 무기를 지금 획득할 수 있는지. 업그레이드 카드 후보를 걸러내는 데 쓴다.
    /// (슬롯이 꽉 찼는데 카드가 나와 EXP 만 소모되는 상황을 막기 위함)
    /// </summary>
    public bool CanAcquire(WeaponData data)
    {
        if (data == null) return false;

        IWeapon existing = Find(data);

        if (existing != null)
            return existing.Level < Mathf.Max(1, data.maxLevel);

        return data.IsAlwaysActive ? HasFreeSubUnitSlot : HasFreeHeldSlot;
    }

    /// <summary>
    /// 무기를 획득한다. 이미 있으면 레벨업한다.
    /// 반환값으로 성공/실패를 구분할 수 있으므로, 실패 시 EXP 를 소모하지 않도록 처리할 것.
    /// </summary>
    public WeaponAcquireResult Acquire(WeaponData data)
    {
        if (data == null) return WeaponAcquireResult.Invalid;

        IWeapon existing = Find(data);

        if (existing != null)
        {
            int max = Mathf.Max(1, data.maxLevel);
            if (existing.Level >= max) return WeaponAcquireResult.MaxLevel;

            existing.SetLevel(existing.Level + 1);
            RaiseLegacyStats();
            return WeaponAcquireResult.LeveledUp;
        }

        bool isSub = data.IsAlwaysActive;

        if (isSub && !HasFreeSubUnitSlot) return WeaponAcquireResult.NoFreeSlot;
        if (!isSub && !HasFreeHeldSlot) return WeaponAcquireResult.NoFreeSlot;

        WeaponBase weapon = SpawnWeapon(data);
        if (weapon == null) return WeaponAcquireResult.Invalid;

        if (isSub)
        {
            subUnits.Add(weapon);
        }
        else
        {
            held.Add(weapon);

            // 첫 무기는 자동으로 손에 들린다. 그 외에는 보유만 하고 스왑으로 꺼낸다.
            if (activeHeldIndex < 0) SwapTo(held.Count - 1);
            else weapon.SetActive(false);
        }

        RaiseLegacyStats();
        return WeaponAcquireResult.Equipped;
    }

    /// <summary>게임 시작 시 장착하도록 지정된 기본 무기. 초기화에 쓴다.</summary>
    public WeaponData StarterWeapon => starterWeapon;

    /// <summary>
    /// 보유 무기를 전부 지우고 기본 무기만 다시 장착한다 (테스트용).
    ///
    /// 서브유닛은 하나도 남기지 않는다. 드론·오브처럼 개체를 만드는 무기는
    /// SetActive(false) 로 개체를 먼저 치운 뒤 지운다 — 그냥 Destroy 하면
    /// 플레이어 밑에 개체만 남아 유령처럼 계속 돈다.
    /// </summary>
    public void ResetWeapons(WeaponData starter)
    {
        for (int i = held.Count - 1; i >= 0; i--)
            DestroyWeapon(held[i]);

        held.Clear();

        for (int i = subUnits.Count - 1; i >= 0; i--)
            DestroyWeapon(subUnits[i]);

        subUnits.Clear();

        activeHeldIndex = -1;
        SuppressUpperBodyLayers();

        if (starter != null)
        {
            Acquire(starter);
            return;
        }

        RaiseLegacyStats();
        GameEvents.OnWeaponSwapped?.Invoke(null);
    }

    void DestroyWeapon(WeaponBase weapon)
    {
        if (weapon == null) return;

        weapon.SetActive(false);   // 드론·오브 등 개체 정리
        Destroy(weapon.gameObject);
    }

    /// <summary>모든 무기에 업그레이드 증분을 적용한다 (연사·치명타 등 범용 강화).</summary>
    public void ApplyGlobalModifier(in WeaponModifier modifier)
    {
        for (int i = 0; i < held.Count; i++)
            held[i].ApplyModifier(modifier);

        for (int i = 0; i < subUnits.Count; i++)
            subUnits[i].ApplyModifier(modifier);

        RaiseLegacyStats();
    }

    /// <summary>특정 무기에만 증분을 적용한다.</summary>
    public bool ApplyModifierTo(WeaponData data, in WeaponModifier modifier)
    {
        IWeapon w = Find(data);
        if (w == null) return false;

        w.ApplyModifier(modifier);
        RaiseLegacyStats();
        return true;
    }

    // ───────── 스왑 ─────────

    public void SwapNext()
    {
        if (held.Count <= 1) return;
        SwapTo((activeHeldIndex + 1) % held.Count);
    }

    public void SwapPrevious()
    {
        if (held.Count <= 1) return;
        SwapTo((activeHeldIndex - 1 + held.Count) % held.Count);
    }

    public void SwapTo(int index)
    {
        if (index < 0 || index >= held.Count) return;
        if (index == activeHeldIndex) return;

        for (int i = 0; i < held.Count; i++)
            held[i].SetActive(i == index);

        activeHeldIndex = index;

        // 근접 무기를 들면 상체 사격 레이어를 꺼야 소총 자세가 덮어쓰지 않는다
        ApplyUpperBodyLayers();

        RaiseLegacyStats();
        GameEvents.OnWeaponSwapped?.Invoke(ActiveWeapon);
    }

    // ───────── 생명주기 ─────────

    void Awake()
    {
        anim = GetComponent<Animator>();

        if (anim != null)
        {
            shootLayer = anim.GetLayerIndex("ShootLayer");
            meleeLayer = anim.GetLayerIndex("MeleeLayer");

            // 이름이 바뀌었어도 기존처럼 두 번째 레이어를 사격 레이어로 본다
            if (shootLayer < 0 && anim.layerCount >= 2)
                shootLayer = 1;
        }

        if (detector == null)
            detector = GetComponentInChildren<EnemyDetector>();

        if (detector == null)
            Debug.LogError("[WeaponController] EnemyDetector 를 찾지 못했습니다.", this);

        // AutoAttack 과 동시에 켜져 있으면 총알이 두 번 나간다
        AutoAttack legacy = GetComponent<AutoAttack>();
        if (legacy != null && legacy.enabled)
        {
            Debug.LogWarning(
                "[WeaponController] AutoAttack 이 켜져 있습니다. 이중 발사가 되므로 " +
                "WeaponController 로 전환했다면 AutoAttack 컴포넌트를 비활성화하세요.", this);
        }
    }

    void OnEnable()
    {
        GameEvents.OnPlayerDeadStart += OnPlayerDead;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerDeadStart -= OnPlayerDead;
    }

    void Start()
    {
        if (starterWeapon != null)
            Acquire(starterWeapon);
    }

    void OnPlayerDead()
    {
        isDead = true;
        SuppressUpperBodyLayers();
        CancelActiveReload();   // 사망 연출 중에 장전이 끝나 HUD 가 갱신되지 않게 한다

        for (int i = 0; i < subUnits.Count; i++)
            subUnits[i].SetActive(false);
    }

    void Update()
    {
        if (isDead) return;

        HandleSwapInput();
        HandleReloadInput();

        float dt = Time.deltaTime;
        WeaponFireContext ctx = new WeaponFireContext(detector, transform);

        // 손에 든 무기: 활성 1개만 발사. 비활성도 Tick 은 돌려 쿨다운을 진행시킨다.
        for (int i = 0; i < held.Count; i++)
        {
            WeaponBase w = held[i];
            w.Tick(dt);

            if (i != activeHeldIndex) continue;

            if (!w.CanFire)
            {
                // 탄이 떨어졌으면 자동 장전을 시작한다. 손에 든 무기만 장전하므로
                // 스왑으로 장전 시간을 회피할 수 없다.
                w.TryAutoReload();
                continue;
            }

            if (!ShouldFire(w)) continue;
            if (!w.HasTargetInReach(in ctx)) continue;   // 사거리 밖이면 쿨다운을 쓰지 않고 기다린다

            w.Fire(in ctx);
        }

        // 서브유닛: 스왑과 무관하게 전부 발사
        for (int i = 0; i < subUnits.Count; i++)
        {
            WeaponBase w = subUnits[i];
            w.Tick(dt);

            if (!w.CanFire) continue;
            if (!ShouldFire(w)) continue;
            if (!w.HasTargetInReach(in ctx)) continue;   // 사거리 밖이면 쿨다운을 쓰지 않고 기다린다

            w.Fire(in ctx);
        }
    }

    bool ShouldFire(WeaponBase weapon)
    {
        if (weapon.Data == null) return false;

        // 오라·장판형처럼 적이 없어도 도는 무기를 허용하기 위한 분기
        if (!weapon.Data.requiresTarget) return true;

        return detector != null && detector.HasTarget;
    }

    /// <summary>수동 장전 (12번 R1). 탄창이 남아 있어도 누르면 장전한다.</summary>
    void HandleReloadInput()
    {
        if (!enableDirectSwapInput) return;
        if (Keyboard.current == null) return;
        if (!Keyboard.current[reloadKey].wasPressedThisFrame) return;

        ReloadActiveWeapon();
    }

    /// <summary>손에 든 무기를 장전한다. 다른 시스템(UI 버튼 등)에서도 부를 수 있다.</summary>
    public void ReloadActiveWeapon()
    {
        if (activeHeldIndex < 0 || activeHeldIndex >= held.Count) return;

        held[activeHeldIndex].StartReload();
    }

    /// <summary>진행 중인 장전을 취소한다. 구르기·사망에서 부른다 (12번 R2).</summary>
    public void CancelActiveReload()
    {
        if (activeHeldIndex < 0 || activeHeldIndex >= held.Count) return;

        held[activeHeldIndex].CancelReload();
    }

    void HandleSwapInput()
    {
        if (!enableDirectSwapInput) return;
        if (held.Count <= 1) return;

        if (Keyboard.current != null && Keyboard.current[swapKey].wasPressedThisFrame)
        {
            SwapNext();
            return;
        }

        if (swapWithScrollWheel && Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;

            if (scroll > 0.1f) SwapNext();
            else if (scroll < -0.1f) SwapPrevious();
        }
    }

    // ───────── 내부 ─────────

    WeaponBase SpawnWeapon(WeaponData data)
    {
        if (data.weaponPrefab == null)
        {
            Debug.LogError($"[WeaponController] '{data.weaponName}' 의 weaponPrefab 이 비어 있습니다.", this);
            return null;
        }

        Transform parent = ResolveSocket(data.socket);

        GameObject obj = Instantiate(data.weaponPrefab, parent);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;

        WeaponBase weapon = obj.GetComponent<WeaponBase>();

        if (weapon == null)
        {
            Debug.LogError(
                $"[WeaponController] '{data.weaponName}' 프리팹에 WeaponBase 파생 컴포넌트가 없습니다.", this);
            Destroy(obj);
            return null;
        }

        weapon.Initialize(data, this);
        return weapon;
    }

    Transform ResolveSocket(WeaponSocket socket)
    {
        if (sockets != null)
        {
            for (int i = 0; i < sockets.Length; i++)
            {
                if (sockets[i].socket == socket && sockets[i].point != null)
                    return sockets[i].point;
            }
        }

        Debug.LogWarning(
            $"[WeaponController] {socket} 소켓이 지정되지 않아 루트에 붙입니다. " +
            $"Hand_Right 아래에 빈 오브젝트를 만들어 sockets 에 연결하세요.", this);

        return transform;
    }

    /// <summary>
    /// 활성 무기 종류에 맞춰 상체 레이어를 켠다.
    ///   · 투사체 → ShootLayer (소총 조준 자세)
    ///   · 근접   → MeleeLayer (검 대기·휘두르기)
    /// 근접 무기를 들었는데 ShootLayer 가 켜져 있으면 상체가 소총 자세로 덮어써진다.
    /// 구르기가 끝날 때 PlayerMove 도 이것을 호출해 들고 있는 무기에 맞는 자세로 되돌린다.
    /// </summary>
    public void ApplyUpperBodyLayers()
    {
        if (anim == null) return;

        IWeapon active = ActiveWeapon;
        WeaponKind kind = active != null && active.Data != null ? active.Data.Kind : WeaponKind.Projectile;

        SetLayerWeight(shootLayer, kind == WeaponKind.Projectile ? 1f : 0f);
        SetLayerWeight(meleeLayer, kind == WeaponKind.Melee ? 1f : 0f);
    }

    /// <summary>구르기·사망처럼 전신 동작이 필요할 때 상체 레이어를 모두 끈다.</summary>
    public void SuppressUpperBodyLayers()
    {
        if (anim == null) return;

        SetLayerWeight(shootLayer, 0f);
        SetLayerWeight(meleeLayer, 0f);
    }

    void SetLayerWeight(int index, float weight)
    {
        if (index >= 0 && index < anim.layerCount)
            anim.SetLayerWeight(index, weight);
    }

    /// <summary>
    /// 기존 HUD(PlayerStatsUI)가 구독하는 이벤트를 활성 무기 기준으로 발행한다.
    /// 슬롯 UI 로 교체하기 전까지 HUD 를 깨뜨리지 않기 위한 임시 다리다.
    /// </summary>
    void RaiseLegacyStats()
    {
        GameEvents.OnWeaponStatsChanged?.Invoke(ActiveWeapon);

        if (!raiseLegacyStatEvents) return;

        IWeapon active = ActiveWeapon;
        if (active == null) return;

        WeaponRuntimeStats s = active.Stats;

        GameEvents.OnBulletDamageChanged?.Invoke(s.Damage);
        GameEvents.OnBulletSpeedChanged?.Invoke(s.ProjectileSpeed);
    }
}
