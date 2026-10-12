using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상위 무기 "처형자" (소총 + 지정사수) — 탄으로 적을 처치하면 그 자리가 터지고,
/// 그 폭발로 죽은 적도 다시 터진다 (상위무기-계획.md 4장 · 14-5).
///
///  · 터지기 전 고리가 안으로 줄어든다 (implodeTime) — 어디가 터질지 보인다
///  · 연쇄는 chainDelay 씩 늦게 — 도미노가 눈에 보이게. 깊이 maxDepth 에서 멈춘다
///  · 한 프레임에 터지는 수를 막는다 (maxPerFrame) — 넘치면 다음 프레임으로 미룬다
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Kill Chain Explosion", fileName = "AE_EVO_KillChain")]
public class KillChainExplosionEffect : AttachmentEffect
{
    [Header("폭발")]
    [Min(0.3f)] public float radius = 2f;

    [Tooltip("폭발 피해 = 처치한 탄의 피해 × 이 값")]
    [Min(0f)] public float damageRatio = 1.2f;

    [Tooltip("연쇄마다 피해에 곱한다")]
    [Range(0.1f, 1f)] public float chainFalloff = 0.85f;

    [Tooltip("처치 → 폭발 → 처치 … 몇 단까지")]
    [Range(1, 6)] public int maxDepth = 3;

    [Header("타이밍")]
    [Min(0f)] public float implodeTime = 0.15f;
    [Min(0f)] public float chainDelay = 0.08f;
    [Range(1, 32)] public int maxPerFrame = 8;

    [Header("연출")]
    public Material material;
    public Color color = new Color(1f, 0.18f, 0.25f, 1f);
    [Tooltip("터질 때 만드는 효과 (없으면 고리만). 파티클 색은 color 로 맞춘다")]
    public GameObject explosionPrefab;
    [Min(0f)] public float explosionScale = 1.3f;
    [Min(0f)] public float cameraShake = 0.25f;
    public GameSoundSet.Entry sound = new GameSoundSet.Entry();

    static float lastSound;
    static int frameStamp = -1;
    static int frameCount;

    /// <summary>터진 횟수 (테스트).</summary>
    public static int Explosions { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        lastSound = 0f;
        frameStamp = -1;
        frameCount = 0;
        Explosions = 0;
    }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly KillChainExplosionEffect cfg;
        readonly List<Transform> found = new List<Transform>();

        public Runtime(KillChainExplosionEffect cfg) => this.cfg = cfg;

        public override void OnKill(in WeaponHit hit)
        {
            if (Weapon == null || hit.Target == null) return;

            Vector3 at = hit.Target.position;
            Weapon.StartCoroutine(Explode(at, hit.Damage * cfg.damageRatio, 1, cfg.implodeTime));
        }

        IEnumerator Explode(Vector3 at, float damage, int depth, float delay)
        {
            // 안으로 줄어드는 고리 — "여기 터진다"
            FxLine.Ring(cfg.material, at, cfg.radius * 0.9f, 0.15f, cfg.color, 0.12f, Mathf.Max(0.05f, delay));
            if (delay > 0f) yield return new WaitForSeconds(delay);

            // 한 프레임에 너무 많이 터지면 다음 프레임으로
            while (!TakeFrameSlot()) yield return null;

            Explosions++;

            CombatFx.FindEnemies(at, cfg.radius, found);
            var killed = new List<Vector3>();

            foreach (Transform t in found)
            {
                bool wasAlive = CombatFx.IsAlive(t);
                Vector3 dir = t.position - at;
                dir.y = 0f;

                CombatFx.Damage(t, damage, CombatFx.Body(t), dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward);

                if (wasAlive && !CombatFx.IsAlive(t)) killed.Add(t.position);
            }

            FxLine.Ring(cfg.material, at, 0.3f, cfg.radius * 1.1f, cfg.color, 0.4f, 0.32f);
            FxLine.Ring(cfg.material, at, 0.1f, cfg.radius * 0.55f, Color.white, 0.14f, 0.18f);
            SpawnExplosion(at);

            if (Time.time - lastSound > 0.07f)
            {
                lastSound = Time.time;
                CombatFx.PlaySound(cfg.sound);
            }

            if (cfg.cameraShake > 0f && depth == 1 && CameraShakeManager.Instance != null)
                CameraShakeManager.Instance.Shake(cfg.cameraShake);

            if (depth >= cfg.maxDepth || Weapon == null) yield break;

            float next = damage * cfg.chainFalloff;
            foreach (Vector3 p in killed)
                Weapon.StartCoroutine(Explode(p, next, depth + 1, cfg.chainDelay + cfg.implodeTime * 0.5f));
        }

        void SpawnExplosion(Vector3 at)
        {
            if (cfg.explosionPrefab == null) return;

            GameObject fx = Object.Instantiate(cfg.explosionPrefab, at + Vector3.up * 0.3f, Quaternion.identity);
            fx.transform.localScale = Vector3.one * cfg.explosionScale;

            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(cfg.color, Color.white, 0.25f));
            }

            Object.Destroy(fx, 2.5f);
        }

        bool TakeFrameSlot()
        {
            if (frameStamp != Time.frameCount)
            {
                frameStamp = Time.frameCount;
                frameCount = 0;
            }

            if (frameCount >= cfg.maxPerFrame) return false;
            frameCount++;
            return true;
        }
    }
}
