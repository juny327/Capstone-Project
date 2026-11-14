using UnityEngine;

/// <summary>
/// 근접 무기 데이터 (검).
///
/// 권장 초기값 (07번 문서 3-4)
///   damage 18, fireInterval 0.70, crit 0.10/2.0
///   range 3, arcAngle 120, maxTargets 5, hitDelay 0.25
/// </summary>
[CreateAssetMenu(menuName = "Weapon/Melee Weapon Data", order = 1)]
public class MeleeWeaponData : WeaponData
{
    [Header("Melee")]
    [Tooltip("사거리(m). EnemyDetector 감지 범위보다 작게 잡고, 무기가 자체 사거리를 다시 확인한다")]
    [Min(0.1f)] public float range = 3f;

    [Tooltip("정면 기준 좌우 합산 각도. 360이면 검이 아니라 오라가 된다")]
    [Range(10f, 360f)] public float arcAngle = 120f;

    [Tooltip("한 번 휘둘러 때릴 수 있는 최대 적 수")]
    [Min(1)] public int maxTargets = 5;

    [Tooltip("휘두르기 시작부터 판정까지의 지연(초). 모션과 타격 시점을 맞춘다")]
    [Min(0f)] public float hitDelay = 0.25f;

    [Tooltip("적 레이어만 포함할 것")]
    public LayerMask hitLayers = ~0;

    [Tooltip("피격 시 카메라 흔들림. 근접은 흔들어야 타격감이 난다")]
    [Min(0f)] public float cameraShake = 0.15f;

    [Header("Effects")]
    public GameObject slashEffectPrefab;

    [Header("베기 궤적 (2026-11-14) — slashEffectPrefab 이 비어 있을 때 코드가 그린다")]
    [Tooltip("궤적 재질 (더하기 선 재질 — Assets/Prefabs/Attachment/FxLine.mat). 비우면 궤적 · 불꽃 없음")]
    public Material slashMaterial;
    [Tooltip("궤적 몸통 색 (알파 = 세기)")]
    public Color slashColor = new Color(1f, 0.8f, 0.45f, 0.9f);
    [Tooltip("바깥 날을 따라 도는 가는 심지 색")]
    public Color slashCoreColor = Color.white;
    [Tooltip("가장 두꺼운 곳의 두께(m). 찌르기는 줄기 굵기")]
    [Min(0.02f)] public float slashWidth = 0.45f;
    [Tooltip("궤적 반경 = 사거리 × 이 값")]
    [Range(0.3f, 1.2f)] public float slashReach = 0.9f;
    [Tooltip("그어지는 시간(초)")]
    [Min(0.01f)] public float slashSweep = 0.07f;
    [Tooltip("걷히는 시간(초)")]
    [Min(0.01f)] public float slashFade = 0.2f;
    [Tooltip("켜면 호 대신 앞으로 뻗는 줄기 (창 찌르기)")]
    public bool slashThrust;
    [Tooltip("맞은 자리에 짧은 베인 자국 + 고리")]
    public bool hitSpark = true;

    [Header("Sound")]
    [Tooltip("휘두르기 시작 '휙' — 허공을 쳐도 난다 (2026-11-02). 베는 소리(fireSounds)는 판정이 적에게 들어간 순간에 난다. 비우면 휘두를 때 소리 없음")]
    public AudioClip[] swingSounds;
    [Range(0f, 1f)] public float swingVolume = 0.45f;
    public Vector2 swingPitchRange = new Vector2(0.94f, 1.06f);

    public override WeaponKind Kind => WeaponKind.Melee;

    public override WeaponRuntimeStats Compose(in WeaponModifier modifier)
    {
        WeaponRuntimeStats s = base.Compose(modifier);
        WeaponModifier m = modifier.Sanitized();

        s.Range = Mathf.Max(0.1f, range + m.rangeAdd);
        s.ArcAngle = Mathf.Clamp(arcAngle, 10f, 360f);
        s.MaxTargets = Mathf.Max(1, maxTargets + m.projectileCountAdd);   // 탄 수 업그레이드를 타격 수로 재사용

        return s;
    }
}
