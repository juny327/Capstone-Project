using UnityEngine;

/// <summary>
/// 상위 무기 "회전 기관포" (기관단총 + 이중 탄창) — 쏘는 동안 연사가 spinUpTime 에 걸쳐 maxBoost 배까지 오르고,
/// 멈추면 spinDownTime 에 걸쳐 내려간다. 무기의 다총열 부속(barrelName)이 연사에 맞춰 돈다.
/// 장전이 없는 것(탄창 무한)은 상위 무기 데이터의 탄창 0 이 맡는다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Spin Up", fileName = "AE_EVO_SpinUp")]
public class SpinUpEffect : AttachmentEffect
{
    [Min(1f)] public float maxBoost = 3f;
    [Min(0.1f)] public float spinUpTime = 2f;
    [Min(0.1f)] public float spinDownTime = 1f;
    [Tooltip("마지막 발 뒤 이만큼 지나면 '안 쏜다'로 본다")]
    [Min(0.05f)] public float idleGrace = 0.35f;

    [Header("다총열")]
    public string barrelName = "SpinBarrels";
    [Tooltip("최고 연사일 때 도는 속도 (도/초)")]
    public float barrelMaxSpeed = 1440f;

    /// <summary>마지막으로 걸린 연사 배율 (테스트).</summary>
    public static float LastBoost { get; private set; } = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => LastBoost = 1f;

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly SpinUpEffect cfg;
        float spin;
        float sinceAttack = 99f;
        Transform barrels;

        public Runtime(SpinUpEffect cfg) => this.cfg = cfg;

        protected override void OnBind() => barrels = FindDeep(Weapon.transform, cfg.barrelName);

        public override void OnAttack(in WeaponAttack attack) => sinceAttack = 0f;

        public override void Tick(float deltaTime)
        {
            sinceAttack += deltaTime;

            bool firing = Weapon.IsActive && sinceAttack < cfg.idleGrace;
            spin = Mathf.Clamp01(spin + (firing ? deltaTime / cfg.spinUpTime : -deltaTime / cfg.spinDownTime));

            float boost = 1f + (cfg.maxBoost - 1f) * spin;
            Weapon.FireRateBoost = boost;
            if (Weapon.IsActive) LastBoost = boost;

            if (barrels != null) barrels.Rotate(Vector3.forward, (90f + cfg.barrelMaxSpeed * spin) * deltaTime, Space.Self);
        }

        public override void OnActiveChanged(bool active)
        {
            if (active) return;
            spin = 0f;
            Weapon.FireRateBoost = 1f;
        }

        static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name)) return null;
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
