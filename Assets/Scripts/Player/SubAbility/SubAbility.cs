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

    public void SetLevel(int level)
    {
        int max = Data != null ? Mathf.Max(1, Data.maxLevel) : 3;
        Level = Mathf.Clamp(level, 1, max);
        OnLevelChanged();
    }

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

    /// <summary>들고 있는 무기의 공격력 — 능력 피해의 기준.</summary>
    protected float WeaponDamage(float fallback)
    {
        IWeapon w = Weapons != null ? Weapons.ActiveWeapon : null;
        return w != null ? w.Stats.Damage : fallback;
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
