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

/// <summary>Selection without duplicate upgrades, used by the practice room.</summary>
public enum HeldWeaponSelectionResult { Invalid, NotReady, Unchanged, Equipped, Swapped, Replaced }

/// <summary>
/// 플레이어의 무기를 소유하고 구동한다.
///
/// 장착 정책 — 보조 무기 (무기-개편-개발-계획, 사수 2026-11-10)
///   · 손에는 <b>주무기 하나</b>만 든다 (로비에서 고른 것). 바꿔 들기는 없다.
///   · 그 밖에 얻은 총은 전부 <b>보조 무기</b> — 몸 둘레(왼쪽 · 오른쪽 · 뒤)에 떠서 스스로 적을 골라 쏜다.
///     드론 전용 무기(전격 · 충전 레이저 · 샷건)는 드론 몸체에 실린다. 세기는 피해 ×0.6 · 간격 ×1.33 (상위 무기 ×0.5 · ×1.4 — 2026-11-12 보조 칸이 차면 너무 쉽게 녹아 낮춤).
///   · 보조 칸은 3칸 — 드론(SubUnit)도 이 칸을 같이 쓴다. 드론은 원래처럼 스스로 궤도를 돈다.
///   · 근접 무기(검사, 2026-11-10)도 떠서 싸운다 — 사거리 + 뛰쳐나갈 거리 안의 적에게 날아가 찌르기 · 휘두르기 · 내려찍기 후 돌아온다.
///   · 패링 연계 — 패링에 성공하면 떠 있는 근접 무기가 막은 방향으로 일제히 찌르고, 주무기의 다음 한 방은 확정 치명.
///
/// 예전 방식 (W3, useSupportWeapons 를 끈 캐릭터)
///   · 손에 드는 무기(투사체/근접)는 여러 개 보유하지만 **한 번에 하나만 활성**이다. 스왑으로 바꾼다.
///   · 드론(SubUnit)은 스왑과 무관하게 **항상 동작**한다.
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
    [Min(1)] [SerializeField] private int maxHeldSlots = 4;

    [Tooltip("서브유닛(드론) 보유 가능 수")]
    [Min(1)] [SerializeField] private int maxSubUnitSlots = 2;

    [Header("Support Weapons (보조 무기)")]
    [Tooltip("보조 무기 칸 수 — 주무기 1 + 이만큼. 드론도 이 칸을 같이 쓴다")]
    [Min(0)] [SerializeField] private int supportSlots = 3;

    [Tooltip("보조 무기 세기 — 피해 배율 · 공격 간격 배율 (손에 든 같은 무기의 약 45%, 2026-11-12 0.8 → 0.6)")]
    [SerializeField] private float supportDamageMul = 0.6f;
    [SerializeField] private float supportIntervalMul = 1.33f;

    [Tooltip("보조 칸의 상위 무기 세기 — 연출은 그대로, 피해 · 간격만 (손에 든 상위 무기의 약 36%, 2026-11-12 0.7 → 0.5)")]
    [SerializeField] private float evolvedSupportDamageMul = 0.5f;
    [SerializeField] private float evolvedSupportIntervalMul = 1.4f;

    [Tooltip("보조 무기 자리 (플레이어 기준). 왼쪽 · 오른쪽 · 뒤. " +
             "높이는 주무기 총구(약 1.1m) 근처로 — 탄 · 빔은 쏜 높이 그대로 날아가므로 너무 높으면 작은 적 머리 위로 지나간다")]
    [SerializeField] private Vector3[] supportOffsets =
    {
        new Vector3(-0.95f, 1.15f, -0.15f),
        new Vector3(0.95f, 1.15f, -0.15f),
        new Vector3(0f, 1.3f, -1.1f),
    };

    [Tooltip("떠 있는 총 크기 (손에 들 때 대비)")]
    [SerializeField] private float floatingWeaponScale = 0.85f;

    [Tooltip("드론 전용 무기의 몸체 — 기존 드론 프리팹(Model 만 쓴다). 총 모형은 숨기고 드론 총구에서 쏜다. 비우면 총만 뜬다")]
    [SerializeField] private GameObject droneBodyPrefab;

    [Tooltip("드론 몸체 크기 (기존 드론 대비) — 보통 드론보다 조금 크게 해 무기 드론임을 보인다")]
    [SerializeField] private float droneBodyScale = 1.5f;

    [Tooltip("떠 있는 근접 무기 — 칼 길이 상한 (m). 손에 든 대검 · 창은 길어서 그대로 띄우면 화면을 가린다")]
    [SerializeField] private float floatingMeleeMaxLength = 1.8f;

    [Tooltip("떠 있는 근접 무기가 뛰쳐나갈 수 있는 거리 (m) — 무기 사거리에 더한다")]
    [SerializeField] private float meleeLunge = 3.5f;

    [Tooltip("떠 있는 근접 무기의 기울기 (도) — 칼끝을 조금 아래로")]
    [SerializeField] private float meleeTilt = 12f;

    [Tooltip("패링 연계 — 패링 모션이 끝난 뒤 이 시간(초) 안의 주무기 다음 한 방은 확정 치명")]
    [SerializeField] private float parryCritWindow = 0.5f;

    [Tooltip("보조 무기 화면 흔들림 배율 — 같은 무기를 손에 들 때보다 약하게 (2026-11-12, 예전엔 0 — 아예 안 흔들었다)")]
    [Range(0f, 1f)] [SerializeField] private float supportShakeMul = 0.4f;

    [Tooltip("보조 무기끼리 흔들림 간격(초) — 이 안에 다시 오면 더 센 것만 (셋이 같이 쏴도 화면이 계속 떨리지 않게)")]
    [Min(0f)] [SerializeField] private float supportShakeInterval = 0.2f;

    public float SupportShakeMul => supportShakeMul;
    public float SupportShakeInterval => supportShakeInterval;

    [Header("Support Weapon Sounds (보조 무기 소리, 2026-11-12)")]
    [Tooltip("떠 있는 근접 무기가 뛰쳐나갈 때 '슝' — 여럿이 같이 나가도 최소 간격으로 한 번")]
    [SerializeField] private GameSoundSet.Entry supportLaunchSound = new GameSoundSet.Entry();

    [Tooltip("떠 있는 근접 무기가 제자리로 돌아올 때 '슈욱'")]
    [SerializeField] private GameSoundSet.Entry supportReturnSound = new GameSoundSet.Entry();

    [Tooltip("패링 연계 일제 찌르기 — 큰 '챙' + 패링 여운. 여러 개가 찔러도 한 번")]
    [SerializeField] private GameSoundSet.Entry parryCounterSound = new GameSoundSet.Entry();

    /// <summary>떠 있는 근접 무기가 뛰쳐나간다 (MeleeWeapon 이 부른다).</summary>
    public void PlaySupportLaunch() => PlaySupportSound(supportLaunchSound);

    /// <summary>떠 있는 근접 무기가 돌아온다.</summary>
    public void PlaySupportReturn() => PlaySupportSound(supportReturnSound);

    static void PlaySupportSound(GameSoundSet.Entry entry)
    {
        if (entry != null && SoundManager.Instance != null) SoundManager.Instance.Play(entry, SoundManager.Instance.SfxGroup);
    }

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
    private bool hasStarterOverride;
    private WeaponData starterOverride;
    public bool IsInitialized { get; private set; }
    private Animator anim;

    /// <summary>지금까지 받은 범용 업그레이드 누적분. 새로 얻는 무기에 그대로 얹어 준다.</summary>
    private WeaponModifier globalModifier = WeaponModifier.Identity;

    // 캐릭터 규칙 (CharacterLoadout 이 설정). 비어 있으면 모든 손 무기를 허용한다
    private WeaponKind[] allowedHeldKinds;
    private WeaponData characterStarter;

    // 보조 무기 방식인가 (사수). 끄면 예전 방식 — 손 무기 여럿 · 스왑 (검사, 근접 보조 무기 전까지)
    private bool useSupportWeapons = true;

    // 보조 무기 자리 (만들어 두고 다시 쓴다)
    private readonly List<SupportMount> mounts = new List<SupportMount>();
    private readonly SupportTargeting.Provider supportTargets = new SupportTargeting.Provider();
    private readonly Dictionary<WeaponBase, Transform> supportAim = new Dictionary<WeaponBase, Transform>();
    private readonly Dictionary<WeaponBase, List<Renderer>> hiddenModels = new Dictionary<WeaponBase, List<Renderer>>();

    // 패링 모션 동안 손 무기 발사를 멈춘다
    private float suppressFireUntil;

    // 서브 능력 칸 (검사). 있으면 E 는 그쪽이 쓴다
    private SubAbilitySlot abilitySlot;

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

    /// <summary>
    /// 손 무기 목록에 하나 더 넣을 수 있는가. 보조 무기 방식이면 주무기가 없을 때 · 보조 칸이 남았을 때.
    /// </summary>
    public bool HasFreeHeldSlot => useSupportWeapons
        ? held.Count == 0 || (HasFreeSupportSlot && held.Count < MaxHeldSlots)
        : held.Count < maxHeldSlots;

    public bool HasFreeSubUnitSlot => subUnits.Count < maxSubUnitSlots && (!useSupportWeapons || HasFreeSupportSlot);

    /// <summary>손 무기 슬롯 수 (주무기 + 보조). 슬롯 UI 가 빈 칸을 그리는 데 쓴다.</summary>
    public int MaxHeldSlots => useSupportWeapons ? Mathf.Min(maxHeldSlots, 1 + supportSlots) : maxHeldSlots;

    /// <summary>보조 무기 방식인가 (사수). 꺼져 있으면 예전 방식 — 손 무기 스왑.</summary>
    public bool UsesSupportWeapons => useSupportWeapons;

    /// <summary>보조 칸 수.</summary>
    public int SupportSlots => supportSlots;

    /// <summary>보조 칸을 쓰는 무기 수 — 주무기를 뺀 손 무기 + 드론.</summary>
    public int SupportCount => (held.Count > 0 ? held.Count - 1 : 0) + subUnits.Count;

    /// <summary>보조 칸이 남았는가.</summary>
    public bool HasFreeSupportSlot => SupportCount < supportSlots;

    /// <summary>주무기 (손에 든 무기). ActiveWeapon 과 같다.</summary>
    public IWeapon MainWeapon => ActiveWeapon;

    /// <summary>
    /// 보조 무기 하나를 뺀다 (테스트 룸 "보조 비우기" · 가득할 때 바꾸기). 주무기는 빼지 않는다.
    /// 게임에서는 쓰지 않는다 — 보조 칸이 가득하면 새 무기가 나오지 않고 가진 무기 강화만 나온다.
    /// </summary>
    public bool RemoveSupport(IWeapon weapon)
    {
        if (!(weapon is WeaponBase w)) return false;

        int index = held.IndexOf(w);
        if (index >= 0)
        {
            if (index == activeHeldIndex) return false;

            SupportMount mount = w.IsSupport ? MountOf(w) : null;
            w.SetActive(false);
            held.RemoveAt(index);
            if (index < activeHeldIndex) activeHeldIndex--;
            FreeMount(mount);
        }
        else if (subUnits.Remove(w)) w.SetActive(false);
        else return false;

        supportAim.Remove(w);
        hiddenModels.Remove(w);
        Destroy(w.gameObject);

        RaiseLegacyStats();
        GameEvents.OnWeaponsChanged?.Invoke();
        OnAttachmentsChanged?.Invoke();
        return true;
    }

    /// <summary>보조 무기를 모두 뺀다 (테스트 룸). 뺀 수를 돌려준다.</summary>
    public int RemoveAllSupports()
    {
        int removed = 0;
        for (int i = held.Count - 1; i >= 0; i--)
            if (i != activeHeldIndex && RemoveSupport(held[i])) removed++;
        for (int i = subUnits.Count - 1; i >= 0; i--)
            if (RemoveSupport(subUnits[i])) removed++;
        return removed;
    }

    /// <summary>
    /// 보조 칸의 그 무기를 다른 무기로 바꾼다 — 같은 자리 · 보조 그대로 (테스트 룸 상위 무기 보조판).
    /// 새 무기는 레벨 1 · 부착물 없음이다.
    /// </summary>
    public bool ReplaceSupport(IWeapon support, WeaponData data)
    {
        if (!(support is WeaponBase w) || !IsValidHeldWeapon(data)) return false;

        int index = held.IndexOf(w);
        if (index < 0 || index == activeHeldIndex) return false;
        return ReplaceHeldAt(index, data, keepRole: true);
    }

    /// <summary>서브유닛 슬롯 수.</summary>
    public int MaxSubUnitSlots => maxSubUnitSlots;

    public bool Has(WeaponData data) => Find(data) != null;

    /// <summary>
    /// 그 무기를 찾는다. 진화한 무기는 원래 무기로도 찾힌다 — 보상 · 정비 창이 소총을 "이미 가진 무기"로 보게
    /// (처형자를 든 사수에게 소총을 새로 팔지 않는다).
    /// </summary>
    public IWeapon Find(WeaponData data)
    {
        if (data == null) return null;

        for (int i = 0; i < held.Count; i++)
            if (held[i].Data == data) return held[i];

        for (int i = 0; i < subUnits.Count; i++)
            if (subUnits[i].Data == data) return subUnits[i];

        for (int i = 0; i < held.Count; i++)
            if (held[i].Data != null && held[i].Data.IsEvolved && held[i].Data.Root == data) return held[i];

        for (int i = 0; i < subUnits.Count; i++)
            if (subUnits[i].Data != null && subUnits[i].Data.IsEvolved && subUnits[i].Data.Root == data) return subUnits[i];

        return null;
    }

    /// <summary>무기가 상위 무기로 바뀌었을 때 (진화 연출 · 테스트). 새 무기와 진화 데이터를 넘긴다.</summary>
    public event System.Action<IWeapon, WeaponEvolutionData> OnEvolved;

    /// <summary>
    /// 상위 무기로 바꾼다 (상위무기-계획.md 10-3). 조건은 부르는 쪽(EvolutionService)이 본다.
    ///   · 같은 칸에서 바뀐다 — 손에 들고 있었으면 바로 든다
    ///   · 레벨 · 부착물(잠긴 개조 포함) · 하부 능력(쿨타임 그대로) · 범용 강화를 넘겨받는다
    /// </summary>
    public bool Evolve(WeaponEvolutionData evolution)
    {
        if (evolution == null || evolution.from == null || evolution.result == null) return false;
        if (isDead) return false;

        WeaponBase previous = null;
        List<WeaponBase> list = null;
        int index = -1;

        for (int i = 0; i < held.Count && previous == null; i++)
            if (held[i].Data == evolution.from) { previous = held[i]; list = held; index = i; }

        for (int i = 0; i < subUnits.Count && previous == null; i++)
            if (subUnits[i].Data == evolution.from) { previous = subUnits[i]; list = subUnits; index = i; }

        if (previous == null) return false;

        WeaponBase next = SpawnWeapon(evolution.result);
        if (next == null) return false;

        next.InheritFrom(previous);   // 범용 강화 누적분도 원래 무기 것을 그대로 받는다

        bool wasActive = list == held && index == activeHeldIndex;
        bool wasSupport = list == held && previous.IsSupport;
        SupportMount mount = wasSupport ? MountOf(previous) : null;

        previous.SetActive(false);              // 장전 · 휘두르기 · 충전 소리 · 빔을 끊는다
        previous.gameObject.SetActive(false);
        list[index] = next;
        supportAim.Remove(previous);
        hiddenModels.Remove(previous);
        Destroy(previous.gameObject);

        // 보조 칸에서 진화하면 같은 자리에서 상위 무기 보조판으로 (연출 그대로 · 피해 ×0.5 · 간격 ×1.4)
        if (wasSupport) AssignSupport(next, mount);
        else next.SetActive(list == subUnits || wasActive);

        ApplyUpperBodyLayers();
        RaiseLegacyStats();
        GameEvents.OnWeaponsChanged?.Invoke();
        if (wasActive) GameEvents.OnWeaponSwapped?.Invoke(ActiveWeapon);
        OnAttachmentsChanged?.Invoke();
        OnEvolved?.Invoke(next, evolution);
        return true;
    }

    /// <summary>
    /// 이 무기를 지금 획득할 수 있는지. 업그레이드 카드 후보를 걸러내는 데 쓴다.
    /// (슬롯이 꽉 찼는데 카드가 나와 EXP 만 소모되는 상황을 막기 위함)
    /// </summary>
    public bool CanAcquire(WeaponData data)
    {
        if (data == null) return false;

        // 캐릭터가 쓸 수 없는 종류는 거른다 (검사에게 소총, 사수에게 검이 나오지 않게).
        // ⚠ Acquire 에는 넣지 않는다 — 테스트 룸 픽업은 SelectHeldWeapon → Acquire 로 바로 가므로
        //    여기에만 두어야 테스트 룸에서 모든 무기를 시험할 수 있다 (설계 4-4).
        if (!IsAllowedForCharacter(data)) return false;

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
            GameEvents.OnWeaponsChanged?.Invoke();

            return WeaponAcquireResult.LeveledUp;
        }

        bool isSub = data.IsAlwaysActive;

        if (isSub && !HasFreeSubUnitSlot) return WeaponAcquireResult.NoFreeSlot;
        if (!isSub && !HasFreeHeldSlot) return WeaponAcquireResult.NoFreeSlot;

        WeaponBase weapon = SpawnWeapon(data);
        if (weapon == null) return WeaponAcquireResult.Invalid;

        // 이미 받아 둔 범용 강화를 새 무기에도 적용한다
        weapon.ApplyModifier(globalModifier);

        if (isSub)
        {
            subUnits.Add(weapon);
        }
        else
        {
            held.Add(weapon);

            // 첫 무기는 자동으로 손에 들린다 (주무기).
            // 그 뒤에 얻은 무기는 보조 무기 방식이면 몸 둘레에 떠서 스스로 싸우고, 아니면 보유만 하고 스왑으로 꺼낸다.
            if (activeHeldIndex < 0) SwapTo(held.Count - 1);
            else if (CanBeSupport(weapon)) AssignSupport(weapon, null);
            else weapon.SetActive(false);
        }

        RaiseLegacyStats();
        GameEvents.OnWeaponsChanged?.Invoke();

        return WeaponAcquireResult.Equipped;
    }

    /// <summary>
    /// 캐릭터 규칙 적용 (CharacterLoadout, Start 전에 부른다).
    ///   · allowed : 보상 · 획득으로 얻을 수 있는 손 무기 종류. 비우면 전부 허용
    ///   · starter : 로비 선택이 없거나 이 캐릭터가 쓸 수 없는 무기일 때 대신 들 무기
    /// </summary>
    public void ApplyCharacterRules(WeaponKind[] allowed, WeaponData starter, bool supportWeapons = true, int supportSlotCount = -1)
    {
        allowedHeldKinds = allowed;
        characterStarter = starter;
        useSupportWeapons = supportWeapons;
        if (supportSlotCount >= 0) supportSlots = supportSlotCount;
    }

    /// <summary>
    /// 이 캐릭터가 쓸 수 있는 무기인지 — 검사는 검사 무기만, 사수는 사수 무기만 (2026-11-11).
    /// 드론(서브유닛)은 사수 무기다 — 총을 쓰는 캐릭터만 얻는다.
    /// 정비 창 · 보상 카드가 이것으로 거른다. 테스트 룸 선반 · 구역은 거르지 않고 모든 무기를 시험할 수 있다.
    /// </summary>
    public bool IsAllowedForCharacter(WeaponData data)
    {
        if (data == null) return false;
        if (allowedHeldKinds == null || allowedHeldKinds.Length == 0) return true;

        WeaponKind kind = data.IsAlwaysActive ? WeaponKind.Projectile : data.Kind;
        return System.Array.IndexOf(allowedHeldKinds, kind) >= 0;
    }

    /// <summary>duration 초 동안 손 무기를 쏘지 않는다. 쿨다운은 그대로 흐른다 (패링 모션용).</summary>
    public void SuppressFiring(float duration)
    {
        suppressFireUntil = Mathf.Max(suppressFireUntil, Time.time + duration);
    }

    /// <summary>Set before Start. Scene-owned test players need not change the lobby selection.</summary>
    public bool ConfigureStartingWeapon(WeaponData data)
    {
        if (IsInitialized || !IsValidHeldWeapon(data)) return false;
        hasStarterOverride = true;
        starterOverride = data;
        return true;
    }

    /// <summary>Acquire and equip, select an owned weapon, or replace only the active full slot.</summary>
    public HeldWeaponSelectionResult SelectHeldWeapon(WeaponData data)
    {
        if (!IsInitialized || isDead) return HeldWeaponSelectionResult.NotReady;
        if (!IsValidHeldWeapon(data)) return HeldWeaponSelectionResult.Invalid;

        // 드론 전용 무기는 손에 들지 않는다 — 보조 칸에 넣는다 (가득하면 마지막 보조 칸을 바꾼다)
        if (useSupportWeapons && data.SupportOnly && activeHeldIndex >= 0)
            return SelectSupportWeapon(data);

        for (int i = 0; i < held.Count; i++)
        {
            if (held[i].Data != data) continue;
            if (i == activeHeldIndex) return HeldWeaponSelectionResult.Unchanged;
            SwapTo(i);
            return HeldWeaponSelectionResult.Swapped;
        }

        // 연습용 선택: 진화한 무기를 가진 채 원래 무기를 고르면 그 칸을 원래 무기로 되돌린다 (원래 · 상위 비교 시험)
        for (int i = 0; i < held.Count; i++)
        {
            WeaponData owned = held[i].Data;
            if (owned == null || !owned.IsEvolved || owned.Root != data) continue;
            return ReplaceHeldAt(i, data) ? HeldWeaponSelectionResult.Replaced : HeldWeaponSelectionResult.Invalid;
        }

        if (HasFreeHeldSlot)
        {
            if (Acquire(data) != WeaponAcquireResult.Equipped) return HeldWeaponSelectionResult.Invalid;
            SwapTo(held.Count - 1);
            return HeldWeaponSelectionResult.Equipped;
        }

        if (activeHeldIndex < 0 || activeHeldIndex >= held.Count) return HeldWeaponSelectionResult.Invalid;
        return ReplaceHeldAt(activeHeldIndex, data) ? HeldWeaponSelectionResult.Replaced : HeldWeaponSelectionResult.Invalid;
    }

    /// <summary>드론 전용 무기 고르기 (테스트 룸) — 이미 있으면 그대로, 칸이 남으면 보조로 얻고, 가득하면 마지막 보조 칸을 바꾼다.</summary>
    HeldWeaponSelectionResult SelectSupportWeapon(WeaponData data)
    {
        if (Find(data) != null) return HeldWeaponSelectionResult.Unchanged;

        if (HasFreeHeldSlot)
            return Acquire(data) == WeaponAcquireResult.Equipped ? HeldWeaponSelectionResult.Equipped : HeldWeaponSelectionResult.Invalid;

        for (int i = held.Count - 1; i >= 0; i--)
        {
            if (i == activeHeldIndex) continue;
            return ReplaceHeldAt(i, data, keepRole: true) ? HeldWeaponSelectionResult.Replaced : HeldWeaponSelectionResult.Invalid;
        }

        return HeldWeaponSelectionResult.Invalid;
    }

    /// <summary>
    /// 그 칸의 무기를 새 무기(레벨 1 · 부착물 없음)로 바꾸고 손에 든다. 실패하면 이전 무기를 그대로 둔다.
    /// keepRole 이면 손에 들지 않고 그 칸의 역할(보조)을 그대로 잇는다.
    /// </summary>
    bool ReplaceHeldAt(int index, WeaponData data, bool keepRole = false)
    {
        // Validate and create first: failure must preserve the previous loadout.
        WeaponBase replacement = SpawnWeapon(data);
        if (replacement == null) return false;
        replacement.ApplyModifier(globalModifier);
        WeaponBase previous = held[index];
        SupportMount previousMount = previous.IsSupport ? MountOf(previous) : null;
        previous.SetActive(false); // cancels reload, pending melee hits, charge audio and beams
        previous.gameObject.SetActive(false);
        held[index] = replacement;
        replacement.SetActive(false);
        supportAim.Remove(previous);
        hiddenModels.Remove(previous);
        Destroy(previous.gameObject);

        if (keepRole && index != activeHeldIndex)
        {
            if (CanBeSupport(replacement)) AssignSupport(replacement, previousMount);
            else FreeMount(previousMount);
            GameEvents.OnWeaponsChanged?.Invoke();
            return true;
        }

        FreeMount(previousMount);

        if (index != activeHeldIndex)
        {
            SwapTo(index);
            GameEvents.OnWeaponsChanged?.Invoke();
            return true;
        }

        // SwapTo returns early for the same index, so complete this replacement explicitly.
        replacement.SetActive(true);
        ApplyUpperBodyLayers();
        RaiseLegacyStats();
        GameEvents.OnWeaponsChanged?.Invoke();
        GameEvents.OnWeaponSwapped?.Invoke(ActiveWeapon);
        return true;
    }

    public static bool IsValidHeldWeapon(WeaponData data)
    {
        if (data == null || data.IsAlwaysActive || data.weaponPrefab == null) return false;
        WeaponBase component = data.weaponPrefab.GetComponent<WeaponBase>();
        return (data is ProjectileWeaponData && component is ProjectileWeapon) ||
               (data is MeleeWeaponData && component is MeleeWeapon) ||
               (data is ChainBeamWeaponData && component is ChainBeamWeapon) ||
               (data is ChargeBeamWeaponData && component is ChargeBeamWeapon);
    }

    /// <summary>
    /// 모든 무기에 업그레이드 증분을 적용한다 (공격력·연사·치명타 등 범용 강화).
    ///
    /// 누적분을 따로 보관해 **나중에 얻는 무기에도 같은 강화가 적용되도록** 한다.
    /// 보관하지 않으면 "공격력을 먼저 올리고 무기를 나중에 얻은" 경우 그 무기만 약해진다.
    /// </summary>
    public void ApplyGlobalModifier(in WeaponModifier modifier)
    {
        globalModifier = WeaponModifier.Combine(globalModifier, modifier);

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

    /// <summary>부착물이 바뀔 때 (장착 · 교체). 장비 HUD · 정비 창 내 장비 판이 다시 그린다.</summary>
    public event System.Action OnAttachmentsChanged;

    /// <summary>
    /// 가진 무기에 부착물을 단다 (커스터마이징-구현계획.md 4-6). 같은 부위의 이전 부착물은 replaced 로 돌려주고 사라진다.
    /// 성공하면 HUD 에 스탯 변경을 알린다.
    /// </summary>
    public bool TryAttach(WeaponData target, AttachmentData attachment, out AttachmentData replaced)
    {
        replaced = null;

        IWeapon w = Find(target);
        if (w == null || !w.TryAttach(attachment, out replaced)) return false;

        RaiseLegacyStats();
        OnAttachmentsChanged?.Invoke();
        return true;
    }

    /// <summary>가진 무기의 부착물을 모두 뗀다 (테스트 룸). 잠긴 개조 칸(상위 무기)은 남는다. 뗀 개수를 돌려준다.</summary>
    public int DetachAll(WeaponData target)
    {
        if (!(Find(target) is WeaponBase w)) return 0;

        int removed = 0;
        foreach (AttachmentSlot slot in System.Enum.GetValues(typeof(AttachmentSlot)))
            if (w.Detach(slot)) removed++;

        if (removed > 0)
        {
            RaiseLegacyStats();
            OnAttachmentsChanged?.Invoke();
        }
        return removed;
    }

    // ───────── 몬스터 상태 이상 (monster 브랜치 2026-10-11, 병합 11-04) ─────────

    private float externalFireIntervalMultiplier = 1f;

    /// <summary>외부 상태효과가 적용한 발사 간격 배율. 1.3이면 발사 속도가 약 23% 느려진다.</summary>
    public float ExternalFireIntervalMultiplier => externalFireIntervalMultiplier;

    /// <summary>MonsterF 저주 — 켜면 발사 속도를 fireRateSlow 만큼 늦춘다 (WeaponBase.Stats 가 발사 간격에 곱한다).</summary>
    public void SetFireRateDebuff(bool enabled, float fireRateSlow = 0.23f)
    {
        float next = enabled
            ? 1f / Mathf.Clamp(1f - fireRateSlow, 0.05f, 1f)
            : 1f;

        if (Mathf.Approximately(externalFireIntervalMultiplier, next))
            return;

        externalFireIntervalMultiplier = next;
        RaiseLegacyStats();
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

        if (useSupportWeapons)
        {
            SwapMain(index);
            return;
        }

        for (int i = 0; i < held.Count; i++)
            held[i].SetActive(i == index);

        activeHeldIndex = index;

        // 근접 무기를 들면 상체 사격 레이어를 꺼야 소총 자세가 덮어쓰지 않는다
        ApplyUpperBodyLayers();

        RaiseLegacyStats();
        GameEvents.OnWeaponSwapped?.Invoke(ActiveWeapon);
    }

    // ───────── 보조 무기 ─────────

    /// <summary>
    /// 주무기를 바꾼다 (보조 무기 방식). 게임에서는 쓰지 않고 테스트 룸 · 진화 시험 · 조합 시험이 부른다.
    /// 새 주무기는 손으로, 원래 주무기는 그 자리(보조 칸)로 간다 — 서로 자리를 바꾼다.
    /// </summary>
    void SwapMain(int index)
    {
        WeaponBase next = held[index];
        WeaponBase previous = activeHeldIndex >= 0 && activeHeldIndex < held.Count ? held[activeHeldIndex] : null;

        SupportMount freed = next.IsSupport ? MountOf(next) : null;
        AttachToHand(next);
        next.SetSupportRole(false, null, WeaponModifier.Identity);
        next.SetActive(true);
        activeHeldIndex = index;

        if (previous != null && previous != next)
        {
            if (CanBeSupport(previous)) AssignSupport(previous, freed);
            else
            {
                FreeMount(freed);
                previous.SetActive(false);
            }
        }
        else FreeMount(freed);

        ApplyUpperBodyLayers();
        RaiseLegacyStats();
        GameEvents.OnWeaponSwapped?.Invoke(ActiveWeapon);
    }

    /// <summary>보조 무기로 띄울 수 있는가 — 보조 무기 방식이고, 드론이 아닌 손 무기 (총 · 근접).</summary>
    bool CanBeSupport(WeaponBase weapon) =>
        useSupportWeapons && weapon != null && weapon.Data != null &&
        (weapon.Data.Kind == WeaponKind.Projectile || weapon.Data.Kind == WeaponKind.Melee);

    /// <summary>보조 무기 세기 — 보통 무기 ×0.6 · ×1.33, 상위 무기 ×0.5 · ×1.4.</summary>
    WeaponModifier SupportRoleFor(WeaponData data)
    {
        bool evolved = data != null && data.IsEvolved;
        WeaponModifier m = WeaponModifier.Identity;
        m.damageMul = evolved ? evolvedSupportDamageMul : supportDamageMul;
        m.fireIntervalMul = evolved ? evolvedSupportIntervalMul : supportIntervalMul;
        return m;
    }

    /// <summary>
    /// 몸 둘레에 띄운다. preferred 자리가 비어 있으면 거기에, 아니면 빈 자리에.
    /// 드론 전용 무기는 드론 몸체 아래에 작게 매단다.
    /// </summary>
    void AssignSupport(WeaponBase weapon, SupportMount preferred)
    {
        SupportMount mount = preferred != null && !IsMountUsed(preferred, weapon) ? preferred : FreeMountSlot(weapon);

        bool drone = weapon.Data != null && weapon.Data.SupportOnly && droneBodyPrefab != null;
        int slot = mounts.IndexOf(mount);
        Vector3 offset = supportOffsets != null && supportOffsets.Length > 0
            ? supportOffsets[Mathf.Clamp(slot, 0, supportOffsets.Length - 1)]
            : new Vector3(0f, 1.6f, -0.6f);

        Transform t = weapon.transform;
        Vector3 handScale = ResolveSocket(weapon.Data.socket).lossyScale;

        // 근접 무기는 칼 길이를 상한까지만 — 손에 든 대검 · 창은 길어서 그대로 띄우면 화면을 가린다
        float scale = floatingWeaponScale;
        MeleeWeapon melee = weapon as MeleeWeapon;
        if (melee != null && melee.BladeLocalLength > 0.01f)
        {
            // 종류마다 상한을 조금씩 — 창이 가장 길고, 대검 · 검 순 (모두 같으면 창이 검처럼 짧아 보인다)
            float cap = floatingMeleeMaxLength * (melee.Style == MeleeWeapon.SwingStyle.Polearm ? 1.35f
                                                : melee.Style == MeleeWeapon.SwingStyle.TwoHanded ? 1.15f : 1f);
            float handLength = melee.BladeLocalLength * Mathf.Abs(handScale.x * PrefabScale(weapon).x);
            if (handLength * scale > cap) scale = cap / handLength;
        }

        mount.gameObject.SetActive(true);
        mount.Setup(transform, offset, slot, drone ? droneBodyPrefab : null, scale,
                    droneBodyScale, weapon.Data != null ? weapon.Data.SupportTint : Color.clear);

        t.SetParent(mount.Holder, false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Divide(Vector3.Scale(handScale, PrefabScale(weapon)), mount.transform.lossyScale);

        // 총구가 자리의 앞을 보게 돌린다 — 손에서는 손목 각도가 총구를 앞으로 돌려 준다.
        // 근접 무기는 칼끝이 앞(조금 아래)을 보게 — 뛰쳐나갈 때 자리째 돌아 칼끝이 적을 향한다
        Transform muzzle = weapon.MuzzlePoint;
        if (melee != null)
            t.rotation = mount.Holder.rotation * Quaternion.Euler(meleeTilt, 0f, 0f) * Quaternion.FromToRotation(melee.BladeAxisLocal, Vector3.forward);
        else if (muzzle != null && muzzle != t)
        {
            Quaternion rel = Quaternion.Inverse(t.rotation) * muzzle.rotation;
            t.rotation = mount.Holder.rotation * Quaternion.Inverse(rel);
        }

        // 드론: 총 모형은 숨기고 총구를 드론 총구에 — 탄 · 빔 · 충전 빛은 드론에서 나온다
        if (drone)
        {
            ShowModel(weapon, false);
            if (muzzle != null && mount.BodyMuzzle != null) t.position += mount.BodyMuzzle.position - muzzle.position;
            weapon.SetSupportRole(true, mount.transform, SupportRoleFor(weapon.Data));
            weapon.SetActive(true);
            return;
        }

        ShowModel(weapon, true);

        // 총 모형의 가운데가 자리에 오게 (총마다 피벗이 손잡이 · 개머리판 등 제각각이다)
        bool any = false;
        Bounds bounds = default;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        if (any) t.position += mount.Holder.position - bounds.center;

        weapon.SetSupportRole(true, mount.transform, SupportRoleFor(weapon.Data));
        weapon.SetActive(true);
    }

    /// <summary>손 소켓으로 되돌린다 (보조 → 주무기).</summary>
    void AttachToHand(WeaponBase weapon)
    {
        Transform t = weapon.transform;
        Transform socket = ResolveSocket(weapon.Data.socket);
        if (t.parent == socket) return;

        t.SetParent(socket, false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = PrefabScale(weapon);
        ShowModel(weapon, true);
    }

    /// <summary>
    /// 총 모형(메시)만 숨기거나 되살린다 — 탄 · 빔 · 입자 · 선은 그대로 둔다.
    /// 숨길 때 켜져 있던 것만 기억해 두었다가 그것만 다시 켠다 (충전 빛처럼 무기가 스스로 켜고 끄는 메시를 건드리지 않게).
    /// </summary>
    void ShowModel(WeaponBase weapon, bool show)
    {
        if (show)
        {
            if (!hiddenModels.TryGetValue(weapon, out List<Renderer> list)) return;
            foreach (Renderer r in list) if (r != null) r.enabled = true;
            hiddenModels.Remove(weapon);
            return;
        }

        if (hiddenModels.ContainsKey(weapon)) return;

        var hidden = new List<Renderer>();
        foreach (Renderer r in weapon.GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled) continue;
            r.enabled = false;
            hidden.Add(r);
        }
        hiddenModels[weapon] = hidden;
    }

    static Vector3 PrefabScale(WeaponBase weapon) =>
        weapon.Data != null && weapon.Data.weaponPrefab != null ? weapon.Data.weaponPrefab.transform.localScale : Vector3.one;

    static Vector3 Divide(Vector3 a, Vector3 b) =>
        new Vector3(a.x / NonZero(b.x), a.y / NonZero(b.y), a.z / NonZero(b.z));

    static float NonZero(float v) => Mathf.Abs(v) < 0.0001f ? 1f : v;

    /// <summary>그 보조 무기가 떠 있는 자리.</summary>
    SupportMount MountOf(WeaponBase weapon)
    {
        if (weapon == null || weapon.SupportMount == null) return null;
        return weapon.SupportMount.GetComponent<SupportMount>();
    }

    bool IsMountUsed(SupportMount mount, WeaponBase except)
    {
        for (int i = 0; i < held.Count; i++)
            if (held[i] != except && held[i] != null && held[i].IsSupport && held[i].SupportMount == mount.transform) return true;
        return false;
    }

    /// <summary>빈 자리. 없으면 하나 더 만든다.</summary>
    SupportMount FreeMountSlot(WeaponBase forWeapon)
    {
        for (int i = 0; i < mounts.Count; i++)
            if (mounts[i] != null && !IsMountUsed(mounts[i], forWeapon)) return mounts[i];

        Transform root = transform.Find("SupportWeapons");
        if (root == null)
        {
            root = new GameObject("SupportWeapons").transform;
            root.SetParent(transform, false);
        }

        var go = new GameObject($"SupportMount{mounts.Count}");
        go.transform.SetParent(root, false);
        SupportMount mount = go.AddComponent<SupportMount>();
        mounts.Add(mount);
        return mount;
    }

    /// <summary>빈 자리는 숨긴다 (드론 몸체가 혼자 떠 있지 않게).</summary>
    void FreeMount(SupportMount mount)
    {
        if (mount == null || IsMountUsed(mount, null)) return;
        mount.gameObject.SetActive(false);
    }

    /// <summary>
    /// 보조 무기 하나를 쏜다 — 규칙대로 적을 고르고, 그쪽으로 돌아, 그 적만 보이는 적 정보로 쏜다.
    /// 무기 코드(투사체 · 빔 · 상위 무기 동작)는 주무기와 똑같이 돈다.
    /// </summary>
    void FireSupport(WeaponBase weapon)
    {
        // 빔이 이어지는 동안(프리즘 노바 · 태양창)은 노린 적을 계속 본다 — 주무기가 마우스를 따라가듯
        if (weapon.InSequence)
        {
            if (supportAim.TryGetValue(weapon, out Transform aimed) && aimed != null && CombatFx.IsAlive(aimed))
            {
                SupportMount m = MountOf(weapon);
                if (m != null) m.FaceNow(aimed.position - m.transform.position);
            }
            return;
        }

        if (!weapon.CanFire || weapon.Data == null) return;
        if (weapon.Data.requiresTarget && (detector == null || !detector.HasTarget)) return;

        SupportMount mount = MountOf(weapon);
        if (mount == null) return;

        if (mount.IsStriking) return;   // 뛰쳐나가 있는 동안은 다음 적을 고르지 않는다

        Vector3 from = mount.transform.position;
        WeaponRuntimeStats s = weapon.Stats;
        float range = weapon is MeleeWeapon ? s.Range + meleeLunge   // 근접은 뛰쳐나갈 거리까지
                    : s.Range > 0.01f ? s.Range
                    : s.ProjectileSpeed * s.ProjectileLifeTime > 0.01f ? s.ProjectileSpeed * s.ProjectileLifeTime
                    : float.PositiveInfinity;

        Transform target = SupportTargeting.Pick(weapon.Data.SupportTargetRule, detector, from, range, transform);
        if (target == null) return;   // 노릴 적이 없으면 쿨다운을 쓰지 않고 기다린다

        mount.FaceNow(target.position - from);
        supportAim[weapon] = target;

        WeaponFireContext ctx = new WeaponFireContext(supportTargets.Set(target, detector), mount.transform);
        if (!weapon.HasTargetInReach(in ctx)) return;

        weapon.Fire(in ctx);
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
        // 로비에서 고른 주 무기를 쓰고, 없으면 프리팹에 지정된 무기로 떨어진다.
        //
        // 폴백이 중요하다 — 에디터에서 Stage 씬을 직접 Play 하면 로비를 거치지 않아
        // 선택값이 없다. 이때 무기 없이 시작하면 테스트가 막힌다.
        //
        // 생성 직후에 갈아 끼우지 않고 여기서 읽는 이유:
        // Instantiate 는 Awake 만 즉시 실행하고 Start 는 프레임 끝에 돈다.
        // 생성 직후 바꿔 놔도 뒤늦게 돈 Start 가 starterWeapon 을 또 장착해 버린다.
        WeaponData starter = starterWeapon;

        if (GameAppManager.Instance != null && GameAppManager.Instance.SelectedWeapon != null)
            starter = GameAppManager.Instance.SelectedWeapon;

        if (hasStarterOverride) starter = starterOverride;

        // 캐릭터가 쓸 수 없는 무기로 시작하지 않게 한다 — 스테이지를 바로 Play 한 검사가
        // 프리팹 기본값(소총)을 드는 경우, 테스트 룸이 검사에게 소총을 쥐여 주는 경우.
        // 테스트 룸 픽업은 이 규칙과 무관하게 모든 무기를 집을 수 있다.
        if (characterStarter != null && (starter == null || !IsAllowedForCharacter(starter)))
            starter = characterStarter;

        // 드론 전용 무기(전격 · 레이저 · 샷건)는 주무기가 될 수 없다 — 예전 로비 선택이 남아 있어도 기본 무기로
        if (useSupportWeapons && starter != null && starter.SupportOnly && !hasStarterOverride && characterStarter != null)
            starter = characterStarter;

        if (starter != null)
            Acquire(starter);

        // CharacterLoadout(실행 순서 −100)가 Awake 에서 붙인다 — 여기서는 이미 있다
        abilitySlot = GetComponent<SubAbilitySlot>();

        // 패링 연계 (검사) — 프리팹에 늘 붙어 있다. 패링 캐릭터가 아니면 성공 알림이 오지 않는다
        parry = GetComponent<ParryController>();
        if (parry != null) parry.OnParryResolved += OnParryResolved;

        IsInitialized = true;
    }

    void OnDestroy()
    {
        if (parry != null) parry.OnParryResolved -= OnParryResolved;
    }

    private ParryController parry;

    /// <summary>패링 연계로 찌른 수 (테스트).</summary>
    public int ParryCounters { get; private set; }

    /// <summary>
    /// 패링 연계 — 성공하면 떠 있는 근접 무기가 막은 방향으로 일제히 찌르고,
    /// 주무기의 다음 한 방(패링 모션이 끝난 뒤 parryCritWindow 초 안)은 무조건 치명타. 쿨다운도 비워 곧바로 반격한다.
    /// </summary>
    void OnParryResolved(bool success)
    {
        if (!success || isDead) return;

        Vector3 dir = transform.forward;
        dir.y = 0f;

        int thrusts = 0;
        for (int i = 0; i < held.Count; i++)
            if (held[i].IsSupport && held[i] is MeleeWeapon melee && melee.CounterThrust(dir)) { ParryCounters++; thrusts++; }

        if (thrusts > 0) PlaySupportSound(parryCounterSound);   // 한 번만 — 찌르는 수만큼 겹치지 않게

        if (activeHeldIndex >= 0 && activeHeldIndex < held.Count)
            held[activeHeldIndex].GrantForcedCrit(Mathf.Max(0f, suppressFireUntil - Time.time) + parryCritWindow);
    }

    void OnPlayerDead()
    {
        isDead = true;
        SuppressUpperBodyLayers();
        CancelActiveReload();   // 사망 연출 중에 장전이 끝나 HUD 가 갱신되지 않게 한다

        for (int i = 0; i < subUnits.Count; i++)
            subUnits[i].SetActive(false);

        for (int i = 0; i < held.Count; i++)
        {
            if (held[i].Ability != null) held[i].Ability.Cancel();
            if (held[i].IsSupport) held[i].SetActive(false);   // 떠 있는 보조 무기 · 빔 · 충전 소리를 끈다
        }
    }

    void Update()
    {
        if (isDead) return;

        HandleSwapInput();
        HandleReloadInput();
        HandleAbilityInput();

        float dt = Time.deltaTime;
        WeaponFireContext ctx = new WeaponFireContext(detector, transform);

        // 손에 든 무기: 활성 1개만 발사. 비활성도 Tick 은 돌려 쿨다운을 진행시킨다.
        // 보조 무기는 스스로 적을 골라 쏜다 (패링 중에도 — 손이 아니라 몸 둘레에 떠 있다)
        for (int i = 0; i < held.Count; i++)
        {
            WeaponBase w = held[i];
            w.Tick(dt);

            if (i != activeHeldIndex)
            {
                if (w.IsSupport) FireSupport(w);
                continue;
            }

            // 패링 모션 중에는 휘두르지 않는다 (쿨다운 Tick 은 위에서 이미 돌았다)
            if (Time.time < suppressFireUntil) continue;

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

    /// <summary>
    /// 사수의 E — 지금 든 총의 하부 부착물 능력 (커스터마이징-구현계획.md 4-4 · 6장).
    /// 서브 능력 칸이 있는 캐릭터(검사)는 SubAbilitySlot 이 E 를 쓴다. 시간이 멈춘 동안(카드 · 정비 · 설정창)은 받지 않는다.
    /// </summary>
    void HandleAbilityInput()
    {
        if (Time.timeScale < 0.01f) return;
        if (abilitySlot != null && abilitySlot.SlotCount > 0) return;

        SubAbility ability = activeHeldIndex >= 0 && activeHeldIndex < held.Count ? held[activeHeldIndex].Ability : null;
        if (ability == null) return;

        UnityEngine.InputSystem.Key key = KeyBindings.Current.ability1;

        if (KeyBindings.Pressed(key)) ability.OnPressed();
        else if (KeyBindings.Held(key)) ability.OnHeld(Time.deltaTime);

        if (KeyBindings.Released(key) || (ability.IsHolding && !KeyBindings.Held(key)))
            ability.OnReleased();
    }

    /// <summary>지금 든 총의 E 능력을 쓴다 (입력 없이 — 검증 · 다른 시스템용).</summary>
    public bool UseActiveAbility()
    {
        SubAbility ability = activeHeldIndex >= 0 && activeHeldIndex < held.Count ? held[activeHeldIndex].Ability : null;
        if (ability == null || isDead) return false;

        ability.OnPressed();
        return true;
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
        if (useSupportWeapons) return;   // 주무기는 로비에서 고른 하나 — 바꿔 들지 않는다
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

    /// <summary>그 소켓에 연결된 부착점. 지정되지 않았으면 null (로비 미리보기가 무기 모형을 붙일 때 쓴다).</summary>
    public Transform FindSocket(WeaponSocket socket)
    {
        if (sockets == null) return null;

        for (int i = 0; i < sockets.Length; i++)
        {
            if (sockets[i].socket == socket && sockets[i].point != null)
                return sockets[i].point;
        }

        return null;
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
