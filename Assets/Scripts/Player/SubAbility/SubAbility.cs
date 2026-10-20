using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 서브 능력 공통 (커스터마이징-구현계획.md 9-2). 능력 프리팹의 루트에 붙는다.
///
/// <see cref="SubAbilitySlot"/> 이 키 입력을 넘겨준다 — 누름(OnPressed) · 누르고 있음(OnHeld) · 뗌(OnReleased).
/// 적 코드는 고치지 않는다. 공개된 Enemy.ApplyShock · ChangeState · TakeDamage 와 NavMeshAgent.Move 만 쓴다 (패링과 같은 방식).
/// </summary>
public abstract class SubAbility : MonoBehaviour
{
    public SubAbilityData Data { get; private set; }
    public int Level { get; private set; } = 1;

    /// <summary>사수의 하부 부착물에서 온 E 능력이면 그 부착물 (유탄 발사기 등). 검사 서브 능력이면 null.</summary>
    public AttachmentData Source { get; private set; }

    /// <summary>HUD 그림 — 부착물에서 왔으면 부착물 그림.</summary>
    public Sprite Icon => Source != null ? Source.icon : Data != null ? Data.icon : null;

    public string DisplayName => Source != null ? Source.displayName : Data != null ? Data.displayName : name;

    protected GameObject Owner { get; private set; }
    protected PlayerMove Move { get; private set; }
    protected PlayerStats Stats { get; private set; }
    protected WeaponController Weapons { get; private set; }
    protected Animator Anim { get; private set; }

    readonly Dictionary<string, bool> animatorParams = new Dictionary<string, bool>();

    /// <summary>다시 쓸 수 있게 되기까지 남은 시간(초). HUD 덮개.</summary>
    public abstract float CooldownRemaining { get; }

    /// <summary>쿨타임 전체 시간(초). HUD 비율.</summary>
    public abstract float CooldownTotal { get; }

    /// <summary>동작 중인지 — 다른 능력 키를 누르면 이것을 끊는다.</summary>
    public virtual bool IsBusy => false;

    /// <summary>누르고 있는 동안 유지되는 능력인지 (방패). 시간이 멈춘 사이 키를 뗐으면 슬롯이 대신 뗀다.</summary>
    public virtual bool IsHolding => false;

    /// <summary>0 ~ 1 게이지 (방패 내구도). 없으면 −1.</summary>
    public virtual float Gauge => -1f;

    public virtual void Equip(GameObject owner, SubAbilityData data, int level)
    {
        Owner = owner;
        Data = data;
        Move = owner.GetComponent<PlayerMove>();
        Stats = owner.GetComponent<PlayerStats>();
        Weapons = owner.GetComponent<WeaponController>();
        Anim = owner.GetComponent<Animator>();
        SetLevel(level);
    }

    /// <summary>
    /// 사수 하부 부착물의 E 능력으로 단다 (커스터마이징-구현계획.md 4-4). 무기마다 하나씩 생겨 쿨타임을 따로 든다.
    /// 입력은 WeaponController 가 지금 든 총의 능력에만 넘긴다.
    /// </summary>
    public void EquipAttachment(GameObject owner, AttachmentData source)
    {
        Source = source;
        Equip(owner, null, 1);
    }

    public void SetLevel(int level)
    {
        int max = Data != null ? Mathf.Max(1, Data.maxLevel) : 3;
        Level = Mathf.Clamp(level, 1, max);
        OnLevelChanged();
    }

    // ───────── 레벨업 변형 (트라이포드 — Lv2 · Lv3 에서 A / B 중 하나) ─────────

    // [레벨] = 고른 쪽 (0 = A · 1 = B · −1 = 없음). 0 · 1 칸은 쓰지 않는다
    readonly int[] picks = { -1, -1, -1, -1 };

    /// <summary>한 단계 올리며 그 레벨의 변형을 고른다 (choice −1 = 변형 없이).</summary>
    public void LevelUp(int choice)
    {
        int before = Level;
        SetLevel(Level + 1);
        if (Level > before && Level < picks.Length) picks[Level] = choice;
        OnVariantsChanged();
    }

    /// <summary>그 레벨에서 고른 쪽 — 0 = A · 1 = B · −1 = 고르지 않음.</summary>
    public int Pick(int level) => level >= 0 && level < picks.Length ? picks[level] : -1;

    /// <summary>그 레벨의 그 변형을 골랐는가 (예: Has(2, 0) = Lv2 A).</summary>
    public bool Has(int level, int choice) => Pick(level) == choice;

    /// <summary>고른 변형 이름들 — "작살 · 추격". 없으면 빈 문자열.</summary>
    public string VariantSummary
    {
        get
        {
            if (Data == null) return string.Empty;
            var names = new List<string>();
            for (int lv = 2; lv < picks.Length; lv++)
            {
                SubAbilityVariant v = picks[lv] >= 0 ? Data.Variant(lv, picks[lv]) : null;
                if (v != null) names.Add(v.name);
            }
            return string.Join(" · ", names);
        }
    }

    /// <summary>변형이 바뀌었을 때 (수치 다시 계산).</summary>
    protected virtual void OnVariantsChanged() { }

    protected virtual void OnLevelChanged() { }

    public virtual void Unequip() => Cancel();

    public abstract void OnPressed();

    public virtual void OnHeld(float deltaTime) { }

    public virtual void OnReleased() { }

    /// <summary>사망 · 패링 · 다른 능력 · 씬 이동 때 — 진행 중인 동작을 그 자리에서 멈춘다.</summary>
    public virtual void Cancel() { }

    // ───────── 공통 헬퍼 ─────────

    /// <summary>플레이어 정면(= 커서 방향). 패링 · 근접 무기와 같은 기준.</summary>
    protected Vector3 Forward
    {
        get
        {
            Vector3 f = Owner != null ? Owner.transform.forward : Vector3.forward;
            f.y = 0f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
        }
    }

    /// <summary>
    /// 이 E 능력을 주는 총 (사수 하부 부착물) — 작은 조합은 "같은 총"의 탄을 본다 (상위무기-계획.md 7장).
    /// 검사 서브 능력이면 null.
    /// </summary>
    protected IWeapon SourceWeapon
    {
        get
        {
            if (Source == null || Weapons == null) return null;
            foreach (IWeapon w in Weapons.HeldWeapons)
                if (w != null && w.Ability == this) return w;
            return null;
        }
    }

    /// <summary>들고 있는 무기의 공격력 — 능력 피해의 기준.</summary>
    protected float WeaponDamage(float fallback)
    {
        IWeapon w = Weapons != null ? Weapons.ActiveWeapon : null;
        return w != null ? w.Stats.Damage : fallback;
    }

    /// <summary>
    /// 정면(= 커서 방향)으로 커서까지의 거리 — 최소 minDistance · 최대 maxDistance.
    /// 방향은 플레이어가 보는 쪽 그대로 두고 거리만 커서에 맞춘다 (유탄이 커서 근처에 떨어진다). 마우스가 없으면 최대 거리.
    /// </summary>
    protected float CursorDistance(float minDistance, float maxDistance)
    {
        Camera cam = Camera.main;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (Owner == null || cam == null || mouse == null) return maxDistance;

        Vector3 origin = Owner.transform.position;
        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!new Plane(Vector3.up, origin).Raycast(ray, out float enter)) return maxDistance;

        Vector3 flat = ray.GetPoint(enter) - origin;
        flat.y = 0f;
        float along = Vector3.Dot(flat, Forward);
        return Mathf.Clamp(along, minDistance, maxDistance);
    }

    protected static void PlaySound(GameSoundSet.Entry entry)
    {
        if (entry != null && SoundManager.Instance != null) SoundManager.Instance.Play(entry);
    }

    /// <summary>애니메이터에 그 파라미터가 있을 때만 — 모션을 아직 안 넣었어도 경고 없이 넘어간다.</summary>
    protected void SetTrigger(string name)
    {
        if (HasParam(name, AnimatorControllerParameterType.Trigger)) Anim.SetTrigger(name);
    }

    protected void SetBool(string name, bool value)
    {
        if (HasParam(name, AnimatorControllerParameterType.Bool)) Anim.SetBool(name, value);
    }

    bool HasParam(string name, AnimatorControllerParameterType type)
    {
        if (Anim == null || Anim.runtimeAnimatorController == null) return false;
        if (animatorParams.TryGetValue(name, out bool has)) return has;

        has = false;
        foreach (AnimatorControllerParameter p in Anim.parameters)
            if (p.name == name && p.type == type) { has = true; break; }

        animatorParams[name] = has;
        return has;
    }

    protected static bool IsAlive(Enemy enemy)
    {
        return enemy != null && enemy.gameObject.activeInHierarchy && enemy.state != EnemyState.Dead;
    }
}
