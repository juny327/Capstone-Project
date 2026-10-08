using UnityEngine;

/// <summary>
/// 소이탄 · 용의 숨결 — 맞은 적에 화상 (커스터마이징-구현계획.md 4-4).
/// 초당 (맞힌 피해 × dpsRatio) 를 duration 초. 다시 맞으면 쌓지 않고 <b>센 쪽 · 긴 쪽으로 갱신</b>한다 —
/// 쌓으면 산탄 8발 · 기관단총이 터무니없이 세진다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Attachment Effects/Burn On Hit", fileName = "AE_Burn")]
public class BurnOnHitEffect : AttachmentEffect
{
    [Min(0.1f)] public float duration = 3f;
    [Tooltip("초당 화상 피해 = 맞힌 피해 × 이 값")]
    [Min(0f)] public float dpsRatio = 0.25f;
    [Range(0f, 1f)] public float chance = 1f;

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly BurnOnHitEffect cfg;
        public Runtime(BurnOnHitEffect cfg) => this.cfg = cfg;

        public override void OnHit(in WeaponHit hit)
        {
            if (hit.Target == null || Random.value > cfg.chance) return;
            BurnTicker.Apply(hit.Target, hit.Damage * cfg.dpsRatio, cfg.duration);
        }
    }
}
