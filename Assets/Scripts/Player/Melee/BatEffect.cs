using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 방망이 · 도깨비 방망이의 동작 (2026-11-20 근접 신규 무기).
///  · 맞힌 적을 날려 보낸다 (넉백 2.5m, 마무리 풀스윙은 5m — 보스는 없음). 날아가다 다른 적과 부딪히면 둘 다 50%, 벽에 박히면 50% + 잠깐 기절
///  · 기절 · 느려진 적은 확정 치명 — 무기 데이터의 critOnWeakened (MeleeWeapon 이 판정한다)
///  · 도깨비 방망이 (dokkaebi) — 맞힌 휘두르기 다섯 번째마다 '뚝딱!' 코인 1 (스테이지마다 12개까지).
///    맴도는 도깨비불이 맞힐 때마다 하나씩 늘어 다섯이 되면 뚝딱 (카운터 겸용)
///  · 도깨비 방망이 (ghostfire) — 마무리 풀스윙이 도깨비불 타구를 날린다 (곧게 · 첫 적에서 폭발 2.5m · 멀리 갈수록 최대 2배 ·
///    맞은 적 2초 느려짐 → 확정 치명과 이어진다)
/// 기본 방망이는 단촐하게 (바닥을 긁는 꼬리 · 부딪힘 섬광), 도깨비 방망이는 화려하게 (연두 도깨비불 · 금빛 코인 · 큰 순간).
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Melee Effects/Bat", fileName = "AE_Bat")]
public class BatEffect : AttachmentEffect
{
    [Header("넉백 (보스는 밀리지 않는다)")]
    [Min(0f)] public float knockback = 2.5f;
    [Tooltip("마무리 풀스윙")]
    [Min(0f)] public float finisherKnockback = 5f;
    [Tooltip("날아가는 시간 (초) — 넉백 거리를 이 시간에 나눠 민다")]
    [Min(0.05f)] public float knockTime = 0.22f;
    [Tooltip("날아가는 적이 다른 적과 부딪히는 거리 (m)")]
    [Min(0.2f)] public float collideRadius = 0.9f;
    [Tooltip("부딪힌 두 적에게 = 무기 공격력 × 이 값")]
    [Min(0f)] public float collideDamageMul = 0.5f;
    [Tooltip("벽에 박힌 적에게 = 무기 공격력 × 이 값")]
    [Min(0f)] public float wallDamageMul = 0.5f;
    [Tooltip("벽에 박힌 적의 기절 (초) — 확정 치명과 이어진다")]
    [Min(0f)] public float wallStun = 0.5f;

    [Header("모습")]
    [Tooltip("선 · 고리 재질 (Assets/Prefabs/Attachment/FxLine.mat)")]
    public Material material;
    public Color color = new Color(0.55f, 0.85f, 1f, 1f);
    [Tooltip("날아가는 적의 꼬리 · 벽 먼지 색")]
    public Color dustColor = new Color(0.78f, 0.72f, 0.62f, 1f);
    [Tooltip("공통 재료(FxKit)로 화려하게 — 도깨비 방망이")]
    public bool kit;

    [Header("도깨비 방망이 — 뚝딱")]
    public bool dokkaebi;
    [Tooltip("맞힌 휘두르기 이 횟수째마다 '뚝딱!'")]
    [Min(1)] public int tukEvery = 5;
    [Tooltip("뚝딱마다 떨어지는 코인")]
    [Min(0)] public int tukCoins = 1;
    [Tooltip("스테이지마다 뚝딱 코인 최대 (넘으면 뚝딱하지 않는다)")]
    [Min(0)] public int tukStageCap = 12;
    public Color ghostColor = new Color(0.62f, 1f, 0.25f, 1f);
    public Color goldColor = new Color(1f, 0.84f, 0.32f, 1f);
    [Tooltip("'뚝딱!' 글자 글꼴 (한글). 비우면 장비 HUD 의 글꼴을 빌린다")]
    public TMPro.TMP_FontAsset font;
    public string tukWord = "뚝딱!";
    [Tooltip("맴도는 도깨비불 반경 · 높이 (m)")]
    public Vector2 wispOrbit = new Vector2(1.15f, 1.6f);

    [Header("도깨비 방망이 — 도깨비불 타구 (마무리 풀스윙)")]
    public bool ghostfire;
    [Min(1f)] public float ghostSpeed = 18f;
    [Tooltip("최대 거리 (m) — 벽에서는 거기까지. 끝까지 가면 그 자리에서 터진다")]
    [Min(1f)] public float ghostDistance = 14f;
    [Min(0.5f)] public float ghostBlastRadius = 2.5f;
    [Tooltip("피해 = 무기 공격력 × (바로 앞 x ~ 최대 거리 y)")]
    public Vector2 ghostDamage = new Vector2(1f, 2f);
    [Tooltip("홀린 적의 이동 속도 배율")]
    [Range(0.1f, 1f)] public float slowFactor = 0.5f;
    [Min(0f)] public float slowSeconds = 2f;

    [Header("소리")]
    public GameSoundSet.Entry collideSound = new GameSoundSet.Entry();
    public GameSoundSet.Entry wallSound = new GameSoundSet.Entry();
    public GameSoundSet.Entry tukSound = new GameSoundSet.Entry();
    public GameSoundSet.Entry ghostLaunchSound = new GameSoundSet.Entry();
    public GameSoundSet.Entry ghostBlastSound = new GameSoundSet.Entry();

    public bool UsesKit => kit && FxKit.Ready;

    /// <summary>시험 집계 — 뚝딱 수 · 뚝딱 코인.</summary>
    public static int Tuks, TukCoinsDropped;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Tuks = TukCoinsDropped = 0; }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        readonly BatEffect cfg;
        int tuk;              // 맞힌 휘두르기 수 (뚝딱 차례)
        int stageCoins;       // 이번 스테이지에 뚝딱으로 떨어뜨린 코인
        int stageScene = -1;
        float wispAngle;
        float wispTimer;
        float lickTimer;

        public Runtime(BatEffect cfg) => this.cfg = cfg;

        float Scale => Weapon != null ? Weapon.EvoFxScale : 1f;

        /// <summary>지금 켜진 도깨비불 수 (0 ~ tukEvery-1) — 다 차면 뚝딱.</summary>
        int Lit => cfg.tukEvery > 0 ? tuk % cfg.tukEvery : 0;

        public override void OnHit(in WeaponHit hit)
        {
            if (hit.Target == null || Weapon == null) return;

            float distance = hit.Finisher ? cfg.finisherKnockback : cfg.knockback;
            BatKnockback.Push(cfg, Weapon, hit.Target, hit.Direction, distance, Weapon.Stats.Damage, hit.Finisher);
        }

        public override void OnAttack(in WeaponAttack attack)
        {
            if (cfg.dokkaebi && attack.Hits > 0) Tuk(in attack);
            if (cfg.ghostfire && attack.Finisher) Ghostfire(in attack);
        }

        /// <summary>뚝딱 — 맞힌 휘두르기 다섯 번째마다 코인 1 (스테이지마다 12개까지).</summary>
        void Tuk(in WeaponAttack attack)
        {
            int scene = SceneManager.GetActiveScene().handle;
            if (scene != stageScene) { stageScene = scene; stageCoins = 0; }

            if (stageCoins >= cfg.tukStageCap) return;   // 이번 스테이지 몫은 다 받았다 — 도깨비불도 더 늘지 않는다

            tuk++;
            if (cfg.tukEvery <= 0 || tuk % cfg.tukEvery != 0) return;

            Vector3 dir = EvoStrikes.Flat(attack.Direction, Vector3.forward);
            Vector3 at = attack.Origin + dir * Mathf.Min(2f, attack.Range * 0.6f);
            bool dropped = cfg.tukCoins > 0 && EconomySystem.Instance != null && EconomySystem.Instance.DropBonusCoins(at, cfg.tukCoins);
            if (dropped)
            {
                stageCoins += cfg.tukCoins;
                TukCoinsDropped += cfg.tukCoins;
            }
            Tuks++;
            TukFx(at);
        }

        /// <summary>뚝딱 — 맴돌던 도깨비불이 금빛으로 터지고 '뚝딱!' 글자가 뜬다.</summary>
        void TukFx(Vector3 at)
        {
            float s = Scale;
            if (cfg.material != null)
            {
                FxLine.Ring(cfg.material, at, 0.2f, 1.6f, cfg.goldColor, 0.14f, 0.28f);
                FxLine.Ring(cfg.material, at, 0.1f, 0.9f, Color.white, 0.07f, 0.2f);
            }
            GroundGlow.Flash(at, 2f, cfg.goldColor, 0.26f);
            FlashLight.Flash(at + Vector3.up, cfg.goldColor, 6f, 0.18f, 0.8f * s);
            if (cfg.UsesKit)
            {
                FxKit.EnergyBurst(at, 1f, cfg.goldColor, s);
                FxKit.Glints(at + Vector3.up * 1.2f, 1.4f, cfg.goldColor, 20f * s, 0.75f);
                FxKit.Pillar(at, 0.6f * s, 4f, cfg.goldColor, 0.4f, 5);
                FxKit.GhostFlameRing(at, 1f, cfg.ghostColor, 10f * s, 1.4f, 0.45f);
            }

            Vector3 head = Weapon != null && Weapon.Controller != null ? Weapon.Controller.transform.position : at;
            FloatingWord.Show(new Vector3(at.x, head.y, at.z) + Vector3.up * 2.6f, cfg.tukWord, cfg.goldColor, cfg.font);
            CombatFx.PlaySound(cfg.tukSound);
        }

        /// <summary>마무리 풀스윙 — 도깨비불 타구를 앞으로 날린다.</summary>
        void Ghostfire(in WeaponAttack attack)
        {
            if (Weapon == null) return;
            Vector3 dir = EvoStrikes.Flat(attack.Direction, Vector3.forward);
            float ground = Weapon.Controller != null ? Weapon.Controller.transform.position.y : attack.Origin.y;
            Vector3 start = attack.Origin + dir * 1.2f;
            start.y = ground + 1.1f;
            GhostfireBall.Launch(cfg, Weapon, start, dir, Weapon.Stats.Damage);
        }

        /// <summary>도깨비 방망이 — 둘레를 맴도는 도깨비불(뚝딱까지 남은 수) · 방망이 끝에서 일렁이는 불.</summary>
        public override void Tick(float deltaTime)
        {
            if (!cfg.dokkaebi || !cfg.UsesKit || Weapon == null || !Weapon.IsActive || Weapon.Controller == null) return;

            wispAngle += deltaTime * 2.4f;
            wispTimer -= deltaTime;
            lickTimer -= deltaTime;
            float s = Scale;

            // 맴도는 도깨비불 — 떠 있는 방망이는 제 자리 둘레, 손에 든 방망이는 플레이어 둘레
            int lit = Lit;
            if (lit > 0 && wispTimer <= 0f)
            {
                wispTimer = 0.035f;
                Transform center = Weapon.IsSupport && Weapon.SupportMount != null ? Weapon.SupportMount : Weapon.Controller.transform;
                float radius = Weapon.IsSupport ? cfg.wispOrbit.x * 0.6f : cfg.wispOrbit.x;
                float height = Weapon.Controller.transform.position.y + cfg.wispOrbit.y;
                for (int i = 0; i < lit; i++)
                {
                    float a = wispAngle + i * Mathf.PI * 2f / Mathf.Max(1, cfg.tukEvery - 1);
                    Vector3 p = center.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                    p.y = height + Mathf.Sin(wispAngle * 2f + i) * 0.12f;
                    FxKit.GhostPuff(p, Vector3.up * 0.5f, 0.42f * s, 0.24f, A(cfg.ghostColor, 0.9f));
                    if (Random.value < 0.25f) FxKit.Spot(p, Color.Lerp(cfg.ghostColor, Color.white, 0.6f), 0.3f, 0.12f);
                }
            }

            // 방망이 끝에서 일렁이는 도깨비불
            if (lickTimer <= 0f && Weapon is MeleeWeapon melee && !Weapon.HeldAway)
            {
                lickTimer = 0.09f;
                FxKit.GhostLick(melee.HeadWorld + Random.insideUnitSphere * 0.25f, 0.35f * s, 0.7f * s, 0.32f, A(cfg.ghostColor, 0.8f));
            }
        }

        static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
