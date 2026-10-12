using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상위 무기 "위성 방벽" (드론 + 에너지 방패) — 드론이 가까이 돌며
///  · 닿은 몬스터 탄을 막는다 (플레이어 바깥쪽으로 튕겨 내 적을 맞힌다 — 반사판과 같은 길)
///  · 부딪힌 적에게 피해 · 밀치기 (적마다 ramCooldown 초에 한 번)
/// 궤도 반경 · 속도는 상위 무기 데이터(DroneWeaponData)가 정한다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Satellite Guard", fileName = "AE_EVO_SatelliteGuard")]
public class SatelliteGuardEffect : AttachmentEffect
{
    [Header("탄 막기")]
    [Min(0.1f)] public float blockRadius = 0.9f;
    [Tooltip("튕겨 낸 탄 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float deflectDamageRatio = 1f;
    [Min(0.02f)] public float scanInterval = 0.05f;

    [Header("부딪히기")]
    [Min(0.1f)] public float ramRadius = 1f;
    [Tooltip("부딪힌 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float ramDamageMul = 2f;
    [Min(0.05f)] public float ramCooldown = 0.5f;
    [Min(0f)] public float ramPush = 1.5f;

    [Header("연출")]
    public Material material;
    public Color color = new Color(0.3f, 0.95f, 1f, 1f);
    public GameSoundSet.Entry blockSound = new GameSoundSet.Entry();

    public static int Blocked { get; private set; }
    public static int Rams { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Blocked = 0; Rams = 0; }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly SatelliteGuardEffect cfg;
        readonly List<Transform> found = new List<Transform>();
        readonly Dictionary<Transform, float> nextRam = new Dictionary<Transform, float>();
        readonly List<Transform> stale = new List<Transform>();
        float scanTimer;

        public Runtime(SatelliteGuardEffect cfg) => this.cfg = cfg;

        public override void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || Weapon is not DroneWeapon drones || drones.Units == null || drones.Units.Count == 0) return;

            Transform player = Weapon.Controller != null ? Weapon.Controller.transform : null;
            if (player == null) return;

            scanTimer -= deltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = cfg.scanInterval;
                BlockProjectiles(drones, player);
            }

            Ram(drones, player);
        }

        void BlockProjectiles(DroneWeapon drones, Transform player)
        {
            RangedProjectile[] flying = Object.FindObjectsByType<RangedProjectile>(FindObjectsSortMode.None);
            if (flying.Length == 0) return;

            float damage = Mathf.Max(1f, Weapon.Stats.Damage * cfg.deflectDamageRatio);
            float sqr = cfg.blockRadius * cfg.blockRadius;
            bool any = false;

            foreach (RangedProjectile p in flying)
            {
                if (p == null || !p.enabled || !p.gameObject.activeInHierarchy) continue;

                Vector3 pos = p.transform.position;
                foreach (DroneUnit unit in drones.Units)
                {
                    if (unit == null) continue;

                    Vector3 d = pos - unit.transform.position;
                    d.y = 0f;
                    if (d.sqrMagnitude > sqr) continue;

                    // 플레이어 바깥쪽으로 튕겨 낸다
                    Vector3 outward = EvoStrikes.Flat(unit.transform.position - player.position, -p.transform.forward);
                    if (!DeflectedProjectile.Launch(p, outward, 1.3f, 12f, damage, 1.5f)) break;

                    FxLine.Ring(cfg.material, unit.transform.position - Vector3.up * 0.3f, 0.1f, 0.9f, cfg.color, 0.12f, 0.18f);
                    Blocked++;
                    any = true;
                    break;
                }
            }

            if (any) CombatFx.PlaySound(cfg.blockSound);
        }

        void Ram(DroneWeapon drones, Transform player)
        {
            float now = Time.time;
            float damage = Weapon.Stats.Damage * cfg.ramDamageMul;

            foreach (DroneUnit unit in drones.Units)
            {
                if (unit == null) continue;

                Vector3 at = unit.transform.position;
                at.y = player.position.y;
                CombatFx.FindEnemies(at, cfg.ramRadius, found);

                foreach (Transform t in found)
                {
                    if (nextRam.TryGetValue(t, out float ready) && ready > now) continue;
                    nextRam[t] = now + cfg.ramCooldown;

                    Vector3 away = EvoStrikes.Flat(t.position - player.position, Vector3.forward);
                    CombatFx.Damage(t, damage, CombatFx.Body(t), away);
                    CombatFx.Push(Weapon, t, away, cfg.ramPush, 0.15f);
                    FxLine.Ring(cfg.material, t.position, 0.1f, 0.7f, cfg.color, 0.1f, 0.15f);
                    Rams++;
                }
            }

            // 오래된 기록은 지운다 (죽은 적이 쌓이지 않게)
            if (nextRam.Count > 64)
            {
                stale.Clear();
                foreach (KeyValuePair<Transform, float> kv in nextRam)
                    if (kv.Key == null || kv.Value < now) stale.Add(kv.Key);
                foreach (Transform t in stale) nextRam.Remove(t);
            }
        }
    }
}
