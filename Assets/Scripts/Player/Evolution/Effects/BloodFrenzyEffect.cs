using UnityEngine;

/// <summary>
/// 상위 무기 "혈검" (검 + 흡혈 — 팀원 스킬, 상위무기-계획.md 16-12).
///
///  · 혈기 — 무기로 직접 쓰러뜨릴 때마다 쌓인다 (흡혈 카드가 많을수록 더). HP 가 가득이면 배로 — 버려지던 흡혈이 혈기가 된다.
///    보스는 쓰러뜨릴 일이 드물어 맞힐 때마다 조금씩 쌓인다.
///  · 혈기가 차면 폭주 — 공격 속도 · 사거리가 늘고, 맞힌 피해의 일부를 회복한다 (초당 상한). 폭주 중 처치하면 조금 늘어난다.
///  · 발밑의 붉은 고리가 혈기 (차오르는 호) — 폭주 동안은 가득 차서 맥박처럼 뛰고, 회복할 때 적에게서 피 줄기가 날아온다.
/// 흡혈 스킬(Skill/Drain)은 그대로 돈다 — 여기서는 회복량을 읽기만 한다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Effects/Blood Frenzy", fileName = "AE_EVO_BloodFrenzy")]
public class BloodFrenzyEffect : AttachmentEffect
{
    [Header("혈기")]
    [Min(1f)] public float maxGauge = 100f;

    [Tooltip("처치 한 번에 쌓이는 기본 혈기")]
    [Min(0f)] public float killGain = 10f;

    [Tooltip("흡혈 회복량 1 당 더 쌓이는 혈기 (흡혈 카드 1장 = 회복 3 → 10 + 3 × 1.7 ≈ 15)")]
    [Min(0f)] public float gainPerHeal = 1.7f;

    [Tooltip("HP 가 가득일 때 처치하면 이 배수 — 흡혈이 버려지지 않게")]
    [Min(1f)] public float fullHpMultiplier = 2f;

    [Tooltip("보스를 맞힐 때마다 (보스는 처치가 없다)")]
    [Min(0f)] public float bossHitGain = 2f;

    [Header("폭주")]
    [Min(0.1f)] public float frenzyDuration = 6f;

    [Tooltip("폭주 중 처치하면 늘어나는 시간")]
    [Min(0f)] public float killExtend = 0.5f;

    [Tooltip("한 번의 폭주가 늘어나도 이 시간을 넘지 않는다")]
    [Min(0.1f)] public float maxFrenzy = 10f;

    [Tooltip("폭주 동안 공격 속도 배율")]
    [Min(1f)] public float attackSpeed = 1.5f;

    [Tooltip("폭주 동안 사거리에 더한다 (m)")]
    [Min(0f)] public float rangeBonus = 1f;

    [Tooltip("폭주 동안 맞힌 피해의 이 비율만큼 회복 (검 한 번 약 7 — 여럿을 베면 초당 상한에 닿는다)")]
    [Range(0f, 1f)] public float lifesteal = 0.1f;

    [Tooltip("폭주 회복 상한 (초당 HP)")]
    [Min(0f)] public float lifestealPerSecond = 8f;

    [Header("연출")]
    public Material material;
    public Color color = new Color(0.9f, 0.08f, 0.14f, 1f);

    [Tooltip("발밑 혈기 고리 반경")]
    [Min(0.1f)] public float ringRadius = 0.95f;

    public GameSoundSet.Entry frenzySound = new GameSoundSet.Entry();

    // ── 테스트용 (손에 든 혈검 기준) ──
    public static float Gauge { get; private set; }
    public static bool Frenzied { get; private set; }
    public static float FrenzyLeft { get; private set; }
    public static int Frenzies { get; private set; }
    public static float LastGain { get; private set; }
    public static int Healed { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Gauge = 0f; Frenzied = false; FrenzyLeft = 0f; Frenzies = 0; LastGain = 0f; Healed = 0;
    }

    public override AttachmentRuntime CreateRuntime() => new Runtime(this);

    class Runtime : AttachmentRuntime
    {
        const int Segments = 48;

        readonly BloodFrenzyEffect cfg;
        PlayerStats stats;

        float gauge;
        float frenzyLeft;
        float frenzyTotal;    // 늘어난 것까지 센 이번 폭주 길이 (상한용)
        float healCarry;      // 1 이 안 되는 회복을 모은다
        float healWindow;     // 이번 1초 동안 회복한 양
        float windowLeft;
        float pulseTimer;

        LineRenderer back;    // 빈 고리 (흐리게)
        LineRenderer fill;    // 찬 만큼

        public Runtime(BloodFrenzyEffect cfg) => this.cfg = cfg;

        bool Frenzy => frenzyLeft > 0f;

        Transform OwnerRoot => Weapon.Controller != null ? Weapon.Controller.transform : null;

        protected override void OnBind()
        {
            stats = Weapon.Controller != null ? Weapon.Controller.GetComponent<PlayerStats>() : null;
        }

        public override void OnHit(in WeaponHit hit)
        {
            if (hit.Target != null && hit.Target.TryGetComponent(out BossHealth _)) Add(cfg.bossHitGain);
            if (Frenzy) Steal(hit.Damage, hit.Point);
        }

        public override void OnKill(in WeaponHit hit)
        {
            if (Frenzy)
            {
                float extend = Mathf.Min(cfg.killExtend, Mathf.Max(0f, cfg.maxFrenzy - frenzyTotal));
                frenzyLeft += extend;
                frenzyTotal += extend;
                Publish();
                return;
            }

            // 흡혈(팀원 스킬)이 먼저 회복한 뒤에 온다 — 회복할 자리가 없었으면(가득) 혈기를 배로
            DrainSkill drain = DrainSkill.Active;
            float gain = cfg.killGain + (drain != null ? drain.HealAmount * cfg.gainPerHeal : 0f);
            if (stats != null && stats.currentHp >= stats.maxHp) gain *= cfg.fullHpMultiplier;

            LastGain = gain;
            Add(gain);
        }

        void Add(float amount)
        {
            if (Frenzy || amount <= 0f) return;

            gauge = Mathf.Min(cfg.maxGauge, gauge + amount);
            if (gauge >= cfg.maxGauge) StartFrenzy();
            Publish();
        }

        void Publish()
        {
            if (!Weapon.IsActive) return;
            Gauge = gauge;
            Frenzied = Frenzy;
            FrenzyLeft = Mathf.Max(0f, frenzyLeft);
        }

        void StartFrenzy()
        {
            gauge = 0f;
            frenzyLeft = frenzyTotal = cfg.frenzyDuration;
            pulseTimer = 0f;
            Weapon.FireRateBoost = cfg.attackSpeed;
            Weapon.RangeBonus = cfg.rangeBonus;
            Frenzies++;

            CombatFx.PlaySound(cfg.frenzySound);
            Transform owner = OwnerRoot;
            if (owner != null && cfg.material != null)
            {
                FxLine.Ring(cfg.material, owner.position, 0.3f, 3.5f, cfg.color, 0.28f, 0.45f);
                FxLine.Ring(cfg.material, owner.position, 0.2f, 2.2f, Color.white, 0.12f, 0.3f);
            }
        }

        void EndFrenzy()
        {
            frenzyLeft = 0f;
            frenzyTotal = 0f;
            Weapon.FireRateBoost = 1f;
            Weapon.RangeBonus = 0f;
            Publish();
        }

        /// <summary>폭주 회복 — 맞힌 피해 × 비율, 초당 상한. 회복하면 적에게서 피 줄기가 날아온다.</summary>
        void Steal(float damage, Vector3 from)
        {
            if (stats == null || stats.IsDead || cfg.lifesteal <= 0f) return;

            float room = Mathf.Max(0f, cfg.lifestealPerSecond - healWindow);
            float amount = Mathf.Min(damage * cfg.lifesteal, room);
            if (amount <= 0f) return;

            healWindow += amount;
            healCarry += amount;

            Transform owner = OwnerRoot;
            if (owner != null && cfg.material != null)
                FxLine.Line(cfg.material, from, owner.position + Vector3.up * 1.1f, cfg.color, 0.06f, 0.18f);

            int whole = Mathf.FloorToInt(healCarry);
            if (whole <= 0) return;

            healCarry -= whole;
            int before = stats.currentHp;
            stats.AddHP(whole);
            Healed += stats.currentHp - before;
        }

        public override void Tick(float deltaTime)
        {
            windowLeft -= deltaTime;
            if (windowLeft <= 0f)
            {
                windowLeft = 1f;
                healWindow = 0f;
            }

            if (Frenzy)
            {
                frenzyLeft -= deltaTime;
                if (frenzyLeft <= 0f) EndFrenzy();
                else if (Weapon.IsActive && (pulseTimer -= deltaTime) <= 0f && OwnerRoot != null && cfg.material != null)
                {
                    pulseTimer = 0.5f;
                    FxLine.Ring(cfg.material, OwnerRoot.position, cfg.ringRadius, cfg.ringRadius + 1.3f, cfg.color, 0.12f, 0.4f);
                }
            }

            Publish();
            DrawRing();
        }

        public override void OnActiveChanged(bool active)
        {
            // 다른 무기로 바꾸면 폭주는 끝난다 (혈기는 남는다)
            if (!active && Frenzy) EndFrenzy();
            if (!active) SetVisible(false);
        }

        public override void Dispose()
        {
            if (Frenzy) EndFrenzy();
            if (back != null) Object.Destroy(back.gameObject);
            if (fill != null) Object.Destroy(fill.gameObject);
            back = fill = null;
        }

        // ───────── 발밑 혈기 고리 ─────────

        void DrawRing()
        {
            Transform owner = OwnerRoot;
            if (owner == null || !Weapon.IsActive || cfg.material == null)
            {
                SetVisible(false);
                return;
            }

            EnsureRing();
            SetVisible(true);

            Vector3 center = owner.position + Vector3.up * 0.06f;
            float amount = Frenzy ? 1f : gauge / cfg.maxGauge;

            Color dim = cfg.color;
            dim.a = 0.28f;
            back.startColor = back.endColor = dim;
            back.widthMultiplier = 0.07f;
            DrawArc(back, center, 1f);

            Color c = cfg.color;
            float beat = Frenzy ? 0.5f + 0.5f * Mathf.Sin(Time.time * 14f) : 0f;
            c.a = Frenzy ? 0.7f + 0.3f * beat : 0.95f;
            fill.startColor = fill.endColor = c;
            fill.widthMultiplier = Frenzy ? 0.18f + 0.06f * beat : 0.14f;
            DrawArc(fill, center, amount);
        }

        void DrawArc(LineRenderer line, Vector3 center, float amount)
        {
            if (amount <= 0.001f)
            {
                line.positionCount = 0;
                return;
            }

            int count = Mathf.Max(2, Mathf.CeilToInt(Segments * amount) + 1);
            line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                // 뒤(-z, 화면 아래 — 카메라 쪽)에서 시작해 찬다. 앞에서 시작하면 처음 조금이 캐릭터 몸에 가린다
                float a = (-90f - 360f * amount * i / (count - 1)) * Mathf.Deg2Rad;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * cfg.ringRadius);
            }
        }

        void EnsureRing()
        {
            if (back == null) back = MakeLine("BloodGauge_Back");
            if (fill == null) fill = MakeLine("BloodGauge_Fill");
        }

        LineRenderer MakeLine(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Weapon.transform, false);   // 플레이어와 같이 씬을 넘는다 · 무기가 꺼지면 같이 숨는다
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.numCapVertices = 2;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = cfg.material;
            return line;
        }

        void SetVisible(bool on)
        {
            if (back != null && back.enabled != on) back.enabled = on;
            if (fill != null && fill.enabled != on) fill.enabled = on;
        }
    }
}
