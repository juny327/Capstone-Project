/// <summary>
/// 무기 공통 계약.
///
/// 손에 드는 무기(투사체/근접)와 서브유닛(드론)이 같은 계약을 쓴다.
/// WeaponController 는 이 인터페이스만 알고 구체 타입을 모른다.
/// </summary>
public interface IWeapon
{
    WeaponData Data { get; }

    int Level { get; }

    /// <summary>기본값 + 누적 모디파이어를 합성한 실제 스탯.</summary>
    WeaponRuntimeStats Stats { get; }

    /// <summary>쿨다운이 끝나 발사 가능한지.</summary>
    bool CanFire { get; }

    /// <summary>쿨다운 진행. 비활성(스왑되어 손에 없는) 상태에서도 호출된다.</summary>
    void Tick(float deltaTime);

    /// <summary>실제 발사. 호출 후 쿨다운이 초기화된다.</summary>
    void Fire(in WeaponFireContext context);

    /// <summary>업그레이드 증분을 누적한다.</summary>
    void ApplyModifier(in WeaponModifier modifier);

    /// <summary>레벨 설정. 드론은 이 값이 드론 수가 된다.</summary>
    void SetLevel(int level);

    /// <summary>
    /// 스왑으로 손에 들렸는지 여부. 서브유닛은 항상 true 로 유지된다.
    /// 비활성 무기는 모델이 숨겨지고 발사되지 않는다.
    /// </summary>
    bool IsActive { get; }

    void SetActive(bool active);
}
