using UnityEngine;

/// <summary>
/// 플레이어가 생성될 때 고른 캐릭터의 설정을 적용한다 (설계 4-2 · 4-7).
///
/// 다른 컴포넌트의 Awake · Start 보다 먼저 돌아야 한다.
///   · PlayerStats.Awake 가 currentHp = maxHp 를 한다
///   · WeaponController.Start 가 시작 무기를 장착한다
/// 같은 오브젝트의 Awake 순서는 보장되지 않으므로 실행 순서를 앞당기고,
/// 각 컴포넌트의 ApplyCharacter 는 순서와 무관하게 결과가 같도록 값 전부를 다시 쓴다.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class CharacterLoadout : MonoBehaviour
{
    [Tooltip("로비를 거치지 않았을 때 쓸 캐릭터 (사수)")]
    [SerializeField] private CharacterData fallback;

    [Tooltip("프리팹 기본 몸 머티리얼. 캐릭터 머티리얼로 바꿀 때 이것만 골라 바꾼다 (무기 등은 건드리지 않는다)")]
    [SerializeField] private Material defaultBodyMaterial;

    /// <summary>적용된 캐릭터. HUD 가 회피 아이콘을 고르는 데 쓴다.</summary>
    public CharacterData Data { get; private set; }

    void Awake()
    {
        Data = CharacterSelection.Current != null ? CharacterSelection.Current : fallback;

        if (Data == null)
        {
            Debug.LogWarning("[CharacterLoadout] 캐릭터 데이터가 없습니다. 프리팹 기본값으로 시작합니다.", this);
            return;
        }

        PlayerStats stats = GetComponent<PlayerStats>();
        if (stats != null)
            stats.ApplyCharacter(Data.maxHp, Data.damageTakenMultiplier);

        PlayerMove move = GetComponent<PlayerMove>();
        if (move != null)
            move.ApplyCharacter(Data.moveSpeedMultiplier, Data.maxStamina, Data.dodge);

        WeaponController weapons = GetComponent<WeaponController>();
        if (weapons != null)
            weapons.ApplyCharacterRules(Data.allowedHeldKinds, Data.FirstStartWeapon);

        ApplyBodyMaterial();
    }

    void ApplyBodyMaterial()
    {
        if (Data.bodyMaterial == null || defaultBodyMaterial == null) return;
        if (Data.bodyMaterial == defaultBodyMaterial) return;

        foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != defaultBodyMaterial) continue;

                mats[i] = Data.bodyMaterial;
                changed = true;
            }

            if (changed)
                r.sharedMaterials = mats;
        }
    }
}
