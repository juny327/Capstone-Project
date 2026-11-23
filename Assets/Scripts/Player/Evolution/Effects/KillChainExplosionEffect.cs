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

    [Header("연쇄 단마다 커지는 모습 (2026-11-14) — 판정 반경 · 피해는 그대로")]
    [Tooltip("연쇄 한 단마다 불꽃 · 바닥 빛 · 조명 · 폭발 효과의 크기와 밝기에 곱한다 (1 이면 단마다 같다). 3단이면 1 → 1.2 → 1.44")]
    [Range(1f, 1.6f)] public float depthGrow = 1.2f;
    [Tooltip("밖으로 튀는 불꽃 줄 수 (첫 단). 단마다 2줄씩 는다. 0 이면 없음")]
    [Range(0, 12)] public int flameLines = 6;
    [Tooltip("폭발 효과의 검은 연기 진하기 (0 = 없음 · 1 = 원래대로). 진하면 적이 가려진다")]
    [Range(0f, 1f)] public float smokeAlpha = 0.45f;
    [Tooltip("검은 연기 수명 배율")]
    [Range(0.2f, 1f)] public float smokeLife = 0.6f;

    [Header("공통 재료 연출 (2026-11-19 연출 개선) — 판정 · 피해와 무관")]
    [Tooltip("켜면 폭발 프리팹(흰 연기 덩어리)과 불꽃 줄 대신 공통 재료(FxKit)로 그린다 — 불 덩어리 · 검붉은 연기 · 튀는 불꽃 · 파편 · 잔불 · 충격파 · 그을음. " +
             "설정 에셋(Resources/Fx/FxKitSet)이 없으면 예전 그대로")]
    public bool kitExplosion = true;
    [Tooltip("연쇄 단마다 커지는 정도를 공통 재료 폭발에는 이만큼 더 세게 건다 (1 = depthGrow 그대로). 연쇄가 이어질수록 폭발이 눈에 띄게 커진다")]
    [Range(1f, 2.5f)] public float kitGrowBoost = 1.6f;

    /// <summary>공통 재료로 그리는가.</summary>
    public bool UsesKit => kitExplosion && FxKit.Ready;

    [Header("큰 연쇄 (2026-11-15) — 큰 순간(히트 스톱 · 쿵 · 줌 · 번쩍)을 내는 조건")]
    [Tooltip("폭발 하나가 이만큼 이상을 한꺼번에 쓰러뜨리면, 그 자리들에서 다음 단이 터지는 순간이 큰 순간이 된다. (예전 조건 '연쇄 3단째'는 거의 안 나왔다 — 폭발 피해가 단마다 줄어 2단 폭발이 적을 못 잡는다)")]
    [Range(2, 8)] public int finaleKills = 3;
    [Tooltip("큰 순간을 다시 내기까지의 시간(초) — 약한 무리를 계속 잡을 때 폭발마다 화면이 멈칫하지 않게")]
    [Min(0f)] public float finaleCooldown = 1.5f;

    static float lastSound;
    static int frameStamp = -1;
    static int frameCount;

    /// <summary>터진 횟수 · 가장 깊이 간 연쇄 단 (테스트).</summary>
    public static int Explosions { get; private set; }
    public static int DeepestChain { get; private set; }
    /// <summary>단마다 터진 횟수 · 그 폭발로 쓰러뜨린 수 (테스트) — [1] 이 첫 폭발.</summary>
    public static readonly int[] ExplosionsByDepth = new int[8];
    public static readonly int[] KillsByDepth = new int[8];
    /// <summary>큰 연쇄로 큰 순간을 낸 횟수 (테스트).</summary>
    public static int Finales { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        lastSound = 0f;
        frameStamp = -1;
        frameCount = 0;
        Explosions = 0;
        DeepestChain = 0;
        System.Array.Clear(ExplosionsByDepth, 0, ExplosionsByDepth.Length);
        System.Array.Clear(KillsByDepth, 0, KillsByDepth.Length);
        Finales = 0;
    }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    /// <summary>폭발 효과 색 — 새로 만들 때 한 번만 (FxPool). 불꽃은 붉게 · 연기는 검게, 옅고 짧게.</summary>
    void SetupExplosion(GameObject fx)
    {
        EvoStrikes.Tint(fx, Color.Lerp(color, new Color(1f, 0.4f, 0.1f, 1f), 0.4f));   // 불꽃은 붉은 주황 — 테마 색 그대로면 분홍으로 보인다
        EvoStrikes.ThinSmoke(fx, smokeAlpha, smokeLife);
    }

    /// <summary>폭발 효과를 미리 만들어 둔다 — 입장 가림막 아래에서 (StageWarmup, 2026-11-18). 연쇄 한 번에 여럿이 터진다.</summary>
    public void Prewarm(int count, Vector3? wakeAt = null)
    {
        // 공통 재료로 그리면 폭발 프리팹은 쓰지 않는다 — 채널만 깨워 둔다 (2026-11-19)
        if (UsesKit) { FxKit.Prewarm(wakeAt ?? Vector3.zero); return; }
        if (explosionPrefab != null) FxPool.Prewarm(explosionPrefab, GetInstanceID(), count, SetupExplosion, wakeAt);
    }

    class Runtime : AttachmentRuntime
    {
        readonly KillChainExplosionEffect cfg;
        readonly List<Transform> found = new List<Transform>();
        float lastFinaleAt = -99f;

        public Runtime(KillChainExplosionEffect cfg) => this.cfg = cfg;

        public override void OnKill(in WeaponHit hit)
        {
            if (Weapon == null || hit.Target == null) return;

            Vector3 at = hit.Target.position;
            Weapon.StartCoroutine(Explode(at, hit.EffectDamage * cfg.damageRatio, 1, cfg.implodeTime));
        }

        /// <param name="finale">앞 단 폭발이 여럿(finaleKills 이상)을 한꺼번에 쓰러뜨렸다 — 이 폭발이 터지는 순간이 큰 순간.</param>
        IEnumerator Explode(Vector3 at, float damage, int depth, float delay, bool finale = false)
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

            int slot = Mathf.Clamp(depth, 0, ExplosionsByDepth.Length - 1);
            ExplosionsByDepth[slot]++;
            KillsByDepth[slot] += killed.Count;

            // 연쇄 단마다 커진다 (모습만 — 판정 반경을 그리는 첫 고리는 그대로). 보조 칸 배율은 EvoFxScale (기본 1 — 줄이지 않는다)
            DeepestChain = Mathf.Max(DeepestChain, depth);
            float grow = Mathf.Pow(cfg.depthGrow, depth - 1) * (Weapon != null ? Weapon.EvoFxScale : 1f);

            FxLine.Ring(cfg.material, at, 0.3f, cfg.radius * 1.1f, cfg.color, 0.4f, 0.32f);
            FxLine.Ring(cfg.material, at, 0.1f, cfg.radius * 0.55f, Color.white, 0.14f, 0.18f);
            if (depth > 1) FxLine.Ring(cfg.material, at, cfg.radius * 0.5f, cfg.radius * 1.1f * grow, Color.Lerp(cfg.color, Color.white, 0.35f), 0.1f, 0.4f);
            // 공통 재료로 그릴 때는 붉은 바닥 빛을 낮춘다 — 불 덩어리가 분홍 빛에 묻히지 않게 (2026-11-19)
            GroundGlow.Flash(at, cfg.radius * 1.4f * grow, cfg.color, 0.26f, Mathf.Min(1.1f, 0.7f * grow) * (cfg.UsesKit ? 0.6f : 1f));
            FlashLight.Flash(at, cfg.color, Mathf.Max(4f, cfg.radius * 2.5f) * grow, 0.16f, Mathf.Min(1.5f, grow));
            if (cfg.UsesKit)
            {
                // 공통 재료 (2026-11-19) — 연쇄 단마다 더 크게: 1 → 1.32 → 1.70 (depthGrow 1.2 · kitGrowBoost 1.6)
                FxKit.Explosion(at, cfg.radius, cfg.color, 1f + (grow - 1f) * cfg.kitGrowBoost);
            }
            else
            {
                Flames(at, depth, grow);
                SpawnExplosion(at, grow);
            }

            if (Time.time - lastSound > 0.07f)
            {
                lastSound = Time.time;
                CombatFx.PlaySound(cfg.sound, SoundKind.Explosion);
            }

            if (depth == 1) WeaponBase.ShakeCamera(Weapon, cfg.cameraShake);   // 보조 무기는 약하게 (WeaponBase.ShakeCamera)

            // 큰 연쇄 — 앞 단 폭발이 여럿을 한꺼번에 쓰러뜨려 여러 군데가 같이 터지는 순간 (또는 마지막 단까지 간 연쇄).
            // 큰 순간(히트 스톱 · 쿵 · 줌 · 번쩍)은 같이 터져도 한 번만 (ImpactFx.repeatGap), 그리고 finaleCooldown 에 한 번만
            if (finale || (depth >= cfg.maxDepth && depth > 1)) Finale(at);

            if (depth >= cfg.maxDepth || Weapon == null) yield break;

            float next = damage * cfg.chainFalloff;
            bool big = killed.Count >= cfg.finaleKills;
            foreach (Vector3 p in killed)
                Weapon.StartCoroutine(Explode(p, next, depth + 1, cfg.chainDelay + cfg.implodeTime * 0.5f, big));
        }

        void Finale(Vector3 at)
        {
            if (Time.time - lastFinaleAt < cfg.finaleCooldown) return;
            lastFinaleAt = Time.time;
            Finales++;
            ImpactFx.Play(ImpactMoment.KillChainFinale, Weapon, cfg.color);
            if (cfg.UsesKit) FxKit.BigShockwave(at, cfg.radius, cfg.color);   // 큰 연쇄의 마지막 — 넓게 퍼지는 충격파
        }

        /// <summary>붉은 불꽃 — 가운데 뜨거운 덩어리 + 밖으로 튀는 줄기. 깊은 단일수록 많고 길고 밝다.</summary>
        void Flames(Vector3 at, int depth, float grow)
        {
            if (cfg.material == null || cfg.flameLines <= 0) return;

            // 연쇄 여덟 군데가 한꺼번에 터진다 — 가운데 덩어리는 작고 짧게 (크면 화면이 통째로 하얗게 뜬다)
            Color hot = Color.Lerp(cfg.color, new Color(1f, 0.6f, 0.25f, 1f), Mathf.Clamp01(0.3f + 0.15f * (depth - 1)));
            GroundGlow.Flash(at + Vector3.up * 0.6f, cfg.radius * 0.4f * grow, hot, 0.12f, 1.3f);

            int n = cfg.flameLines + 2 * (depth - 1);
            float turn = Random.Range(0f, 360f);
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.AngleAxis(turn + i * 360f / n + Random.Range(-14f, 14f), Vector3.up) * Vector3.forward;
                Vector3 from = at + Vector3.up * 0.4f + d * cfg.radius * 0.15f;
                Vector3 to = from + (d + Vector3.up * Random.Range(0f, 0.4f)) * cfg.radius * Random.Range(0.55f, 0.95f) * grow;
                FxLine.Line(cfg.material, from, to, i % 3 == 0 ? hot : cfg.color, 0.14f * grow, 0.2f);
            }
        }

        void SpawnExplosion(Vector3 at, float grow)
        {
            if (cfg.explosionPrefab == null) return;

            // 2026-11-18 성능 — 처치마다 만들고 지우지 않고 돌려 쓴다 (FxPool, 색은 새로 만들 때 한 번 — SetupExplosion)
            setupExplosion ??= cfg.SetupExplosion;
            FxPool.Spawn(cfg.explosionPrefab, cfg.GetInstanceID(), at + Vector3.up * 0.3f, Quaternion.identity,
                         cfg.explosionScale * grow, 2.5f, setupExplosion);
        }

        System.Action<GameObject> setupExplosion;

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
