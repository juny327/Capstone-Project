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

/// <summary>무기 하나에 달린 부착물 동작의 실행 상태. WeaponBase 가 만들고 부르고 치운다.</summary>
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

    public virtual void OnReloadStarted() { }

    /// <summary>손에 들림 / 빠짐.</summary>
    public virtual void OnActiveChanged(bool active) { }

    /// <summary>떼어 낼 때 · 무기가 사라질 때. 만든 오브젝트를 치운다.</summary>
    public virtual void Dispose() { }
}
