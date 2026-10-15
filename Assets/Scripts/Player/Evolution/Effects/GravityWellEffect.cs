using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상위 무기 "중력 대검" (대검 + 그랩 훅) — 내려찍은 자리에 중력장이 생겨 주변 적을 빨아들이고,
/// 끝날 때 붕괴 폭발한다 (상위무기-계획.md 5장 · 14-5).
///
///  · 내려찍기(세로베기)에서만 생긴다 — 가로베기는 그대로. 한 번에 하나 (이미 있으면 새로 만들지 않는다)
///  · 끌기는 그랩과 같은 길(EnemyShove — 내비메시 위에서만)이고, 끌리는 동안 경직을 건다. 보스는 끌지 않는다
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Gravity Well", fileName = "AE_EVO_GravityWell")]
public class GravityWellEffect : AttachmentEffect
{
    [Header("중력장")]
    [Tooltip("휘두른 방향으로 사거리 × 이 값 앞에 생긴다")]
    [Range(0f, 1.5f)] public float forwardRatio = 0.75f;
    [Min(0.1f)] public float duration = 1.2f;
    [Min(0.5f)] public float pullRadius = 6f;
    [Tooltip("끌려와 멈추는 자리 — 중심에서 이 반경 안 무작위 (한 점에 겹치지 않게)")]
    [Min(0f)] public float gatherRadius = 0.8f;
    [Min(0.05f)] public float scanInterval = 0.2f;

    [Header("붕괴")]
    [Min(0.3f)] public float collapseRadius = 3f;
    [Tooltip("붕괴 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float collapseDamageMul = 2f;
    [Min(0f)] public float collapseStun = 0.6f;

    [Header("연출")]
    public Material ringMaterial;
    [Tooltip("바닥에 도는 소용돌이 원판 재질 (비우면 고리만)")]
    public Material swirlMaterial;
    public Color color = new Color(0.72f, 0.3f, 1f, 1f);
    public GameObject collapsePrefab;
    [Min(0f)] public float collapseScale = 1.2f;
    [Min(0f)] public float cameraShake = 0.6f;
    public GameSoundSet.Entry openSound = new GameSoundSet.Entry();
    public GameSoundSet.Entry collapseSound = new GameSoundSet.Entry();

    [Tooltip("열릴 때 함께 — 끌어당기는 동안 낮게 깔리는 울림 (엘든링 중력 마법처럼, 2026-10-13)")]
    public GameSoundSet.Entry openRumbleSound = new GameSoundSet.Entry();

    [Tooltip("붕괴할 때 함께 — 깊은 \"쿵\"")]
    public GameSoundSet.Entry collapseBassSound = new GameSoundSet.Entry();

    /// <summary>만든 중력장 수 (테스트).</summary>
    public static int Spawned { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Spawned = 0;

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly GravityWellEffect cfg;
        GravityWell active;

        public Runtime(GravityWellEffect cfg) => this.cfg = cfg;

        public override void OnAttack(in WeaponAttack attack)
        {
            if (!attack.Vertical || Weapon == null) return;
            if (active != null) return;

            Vector3 dir = attack.Direction;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;

            Vector3 at = attack.Origin + dir.normalized * attack.Range * cfg.forwardRatio;
            active = GravityWell.Spawn(cfg, at, Weapon.Stats.Damage * cfg.collapseDamageMul);
            Spawned++;
        }

        public override void Dispose()
        {
            // 무기가 사라져도 열린 중력장은 끝까지 간다 (스스로 지운다)
            active = null;
        }
    }
}
