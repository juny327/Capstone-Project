using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 서브 능력 칸 — E(1번) · F(2번) (커스터마이징-구현계획.md 5-1).
///
/// 캐릭터 데이터에 칸 수가 있으면 CharacterLoadout 이 플레이어에 붙인다 (검사 2칸, 사수 0칸).
/// 능력은 정비(상점)에서 얻는다 — 처음 얻은 능력은 E, 두 번째는 F. 같은 능력을 다시 얻으면 레벨업.
/// 두 칸이 차면 새 능력은 나오지 않고 레벨업만 나온다 (무기 슬롯과 같은 규칙).
///
/// 한 번에 하나만 동작한다. 다른 능력 키 · 패링을 누르면 지금 능력을 끊는다 (방패를 내린다).
/// 시간이 멈춰 있으면(카드 · 정비 · 설정창) 입력을 받지 않는다.
/// </summary>
[DisallowMultipleComponent]
public class SubAbilitySlot : MonoBehaviour
{
    public const int MaxSlots = 2;

    [SerializeField, Range(0, MaxSlots)] int slotCount;

    readonly SubAbility[] equipped = new SubAbility[MaxSlots];

    Transform holder;
    ParryController parry;
    bool wasParrying;
    bool dead;

    /// <summary>지금 플레이어의 슬롯 (HUD 가 찾는다). 없으면 null.</summary>
    public static SubAbilitySlot Current { get; private set; }

    /// <summary>장착 · 레벨업 · 칸 수가 바뀔 때.</summary>
    public event Action OnChanged;

    public int SlotCount => slotCount;

    public SubAbility Get(int index) => index >= 0 && index < slotCount ? equipped[index] : null;

    public bool HasFreeSlot
    {
        get
        {
            for (int i = 0; i < slotCount; i++)
                if (equipped[i] == null) return true;
            return false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Current = null;

    public static SubAbilitySlot Ensure(GameObject player)
    {
        SubAbilitySlot slot = player.GetComponent<SubAbilitySlot>();
        if (slot == null) slot = player.AddComponent<SubAbilitySlot>();
        return slot;
    }

    public void Configure(int count)
    {
        slotCount = Mathf.Clamp(count, 0, MaxSlots);
        OnChanged?.Invoke();
    }

    public int IndexOf(SubAbilityData data)
    {
        if (data == null) return -1;

        for (int i = 0; i < slotCount; i++)
            if (equipped[i] != null && equipped[i].Data == data) return i;

        return -1;
    }

    /// <summary>얻을 수 있는가 — 가진 능력이면 최대 레벨 전, 없으면 빈 칸이 있을 때.</summary>
    public bool CanAcquire(SubAbilityData data)
    {
        if (dead || data == null || data.prefab == null) return false;

        int i = IndexOf(data);
        if (i >= 0) return equipped[i].Level < Mathf.Max(1, data.maxLevel);

        return HasFreeSlot;
    }

    /// <summary>능력을 얻는다 — 가진 능력이면 레벨업, 없으면 빈 칸(E → F)에 장착.</summary>
    public bool Acquire(SubAbilityData data)
    {
        if (!CanAcquire(data)) return false;

        int i = IndexOf(data);
        if (i >= 0)
        {
            equipped[i].SetLevel(equipped[i].Level + 1);
            OnChanged?.Invoke();
            return true;
        }

        for (int s = 0; s < slotCount; s++)
        {
            if (equipped[s] != null) continue;

            if (holder == null)
            {
                holder = new GameObject("SubAbilities").transform;
                holder.SetParent(transform, false);
            }

            SubAbility ability = Instantiate(data.prefab, holder);
            ability.name = data.prefab.name;
            ability.transform.localPosition = Vector3.zero;
            ability.transform.localRotation = Quaternion.identity;
            ability.Equip(gameObject, data, 1);

            equipped[s] = ability;
            OnChanged?.Invoke();
            return true;
        }

        return false;
    }

    void Awake()
    {
        parry = GetComponent<ParryController>();
    }

    void OnEnable()
    {
        Current = this;
        GameEvents.OnPlayerDeadStart += OnPlayerDead;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerDeadStart -= OnPlayerDead;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        CancelAll();
        if (Current == this) Current = null;
    }

    void OnPlayerDead()
    {
        dead = true;
        CancelAll();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => CancelAll();

    void Update()
    {
        if (dead || slotCount == 0) return;

        // 카드 · 정비 · 설정창이 시간을 멈춘 동안은 입력을 받지 않는다
        if (Time.timeScale < 0.01f) return;

        // 패링을 시작하면 지금 능력을 끊는다 (방패를 내리고 패링)
        if (parry != null)
        {
            bool parrying = parry.IsParrying;
            if (parrying && !wasParrying) CancelAll();
            wasParrying = parrying;
        }

        KeyBindings keys = KeyBindings.Current;

        for (int i = 0; i < slotCount; i++)
        {
            SubAbility ability = equipped[i];
            if (ability == null) continue;

            UnityEngine.InputSystem.Key key = keys.AbilityKey(i);

            if (KeyBindings.Pressed(key))
            {
                CancelOthers(i);
                ability.OnPressed();
            }
            else if (KeyBindings.Held(key))
            {
                ability.OnHeld(Time.deltaTime);
            }

            // 뗀 순간을 놓쳤으면(시간이 멈춘 사이 뗐다) 지금 뗀 것으로 친다
            if (KeyBindings.Released(key) || (ability.IsHolding && !KeyBindings.Held(key)))
                ability.OnReleased();
        }
    }

    void CancelOthers(int keep)
    {
        for (int i = 0; i < slotCount; i++)
            if (i != keep && equipped[i] != null && (equipped[i].IsBusy || equipped[i].IsHolding)) equipped[i].Cancel();
    }

    public void CancelAll()
    {
        for (int i = 0; i < MaxSlots; i++)
            if (equipped[i] != null) equipped[i].Cancel();
    }
}
