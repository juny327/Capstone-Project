using UnityEngine;

/// <summary>무기가 적을 맞혔다는 알림 (커스터마이징-구현계획.md 9-1). 탄 · 빔이 피해를 준 직후에 보낸다.</summary>
public struct WeaponHit
{
    /// <summary>맞은 적의 루트 (IDamageable 을 가진 오브젝트).</summary>
    public Transform Target;
    public IDamageable Damageable;
    public Vector3 Point;
    public Vector3 Direction;

    /// <summary>치명타까지 반영된 실제 피해.</summary>
    public float Damage;
    public bool IsCritical;

    /// <summary>이 피해로 적이 죽었는가 (맞기 전에 살아 있었고 지금은 죽었다).</summary>
    public bool Killed;
}

/// <summary>
/// 무기가 공격했다는 알림 (상위무기-계획.md 10-2) — 총은 쏘는 순간, 근접은 휘두른 판정 순간.
/// 맞혔는지와 상관없이 온다. 검기 · 중력장처럼 "휘두를 때마다" 나오는 동작이 쓴다.
/// </summary>
public struct WeaponAttack
{
    public Vector3 Origin;
    public Vector3 Direction;

    /// <summary>근접 내려찍기인가 (총은 항상 false).</summary>
    public bool Vertical;

    /// <summary>사거리.</summary>
    public float Range;
}

/// <summary>
/// 부착물의 동작 — 수치로 안 되는 것 (조준선 · 명중 효과 · 장전 방전).
///
/// 에셋 하나가 여러 무기에 달릴 수 있으므로 상태는 여기에 두지 않는다.
/// 무기에 달 때마다 <see cref="CreateRuntime"/> 으로 그 무기 전용 실행 상태를 만든다.
/// </summary>
public abstract class AttachmentEffect : ScriptableObject
{
    public abstract AttachmentRuntime CreateRuntime();
}

/// <summary>
/// 무기 하나에 달린 부착물 동작의 실행 상태. WeaponBase 가 만들고 부르고 치운다.
/// 상위 무기의 고유 동작(WeaponData.innateEffects)도 같은 통로로 돈다 — 그때 Data 는 null 이다.
/// </summary>
public abstract class AttachmentRuntime
{
    protected WeaponBase Weapon { get; private set; }
    protected AttachmentData Data { get; private set; }

    public void Bind(WeaponBase weapon, AttachmentData data)
    {
        Weapon = weapon;
        Data = data;
        OnBind();
    }

    protected virtual void OnBind() { }

    /// <summary>매 프레임 (손에 없을 때도). 연출만 하는 효과는 IsActive 를 보고 숨긴다.</summary>
    public virtual void Tick(float deltaTime) { }

    /// <summary>탄 · 빔이 적을 맞혔을 때. 여기서 주는 추가 피해는 다시 알림을 보내지 않는다 (연쇄 폭주 방지).</summary>
    public virtual void OnHit(in WeaponHit hit) { }

    /// <summary>맞힌 적이 그 피해로 죽었을 때 (OnHit 다음에 온다). 상위 무기 "처형자"가 쓴다.</summary>
    public virtual void OnKill(in WeaponHit hit) { }

    /// <summary>쏘거나 휘둘렀을 때 — 맞혔는지와 상관없이.</summary>
    public virtual void OnAttack(in WeaponAttack attack) { }

    public virtual void OnReloadStarted() { }

    /// <summary>쏜 탄이 사거리 끝에서 사라졌을 때 (맞히고 멈춘 탄은 오지 않는다).</summary>
    public virtual void OnProjectileExpired(Vector3 position, Vector3 direction) { }

    /// <summary>손에 들림 / 빠짐.</summary>
    public virtual void OnActiveChanged(bool active) { }

    /// <summary>떼어 낼 때 · 무기가 사라질 때. 만든 오브젝트를 치운다.</summary>
    public virtual void Dispose() { }
}
