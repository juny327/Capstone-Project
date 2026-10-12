using UnityEngine;

/// <summary>
/// 상위 무기 "폭풍 코일" (전격 소총 + 과부하 코일) — 연쇄된 적마다 chance 확률로 하늘에서 낙뢰.
/// 상위 무기 데이터가 notifyEveryChain 을 켜서 연쇄된 적마다 명중 알림이 온다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Lightning Strike", fileName = "AE_EVO_Lightning")]
public class LightningStrikeEffect : AttachmentEffect
{
    [Range(0f, 1f)] public float chance = 0.2f;
    [Min(0.3f)] public float radius = 1.5f;
    [Tooltip("낙뢰 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float damageMul = 2f;
    [Min(0f)] public float stun = 0.2f;
    [Tooltip("낙뢰가 시작하는 높이")]
    [Min(2f)] public float height = 9f;
    [Range(1, 32)] public int maxPerFrame = 6;

    public Material material;
    public Color color = new Color(0.75f, 0.9f, 1f, 1f);
    public GameObject impactPrefab;
    public GameSoundSet.Entry sound = new GameSoundSet.Entry();

    public static int Strikes { get; private set; }
    static float lastSound;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Strikes = 0; lastSound = 0f; }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly LightningStrikeEffect cfg;
        public Runtime(LightningStrikeEffect cfg) => this.cfg = cfg;

        public override void OnHit(in WeaponHit hit)
        {
            if (hit.Target == null || Weapon == null) return;
            if (Random.value >= cfg.chance || !EvoStrikes.Take(cfg.maxPerFrame)) return;

            Vector3 at = hit.Target.position;
            Vector3 sky = at + Vector3.up * cfg.height + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));
            EvoStrikes.Lightning(cfg.material, cfg.color, sky, at, cfg.radius, Weapon.Stats.Damage * cfg.damageMul, cfg.stun, cfg.impactPrefab);
            Strikes++;

            if (Time.time - lastSound > 0.08f)
            {
                lastSound = Time.time;
                CombatFx.PlaySound(cfg.sound);
            }
        }
    }
}
