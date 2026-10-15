using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상위 무기 "혈검" (검 + 흡혈 — 팀원 스킬, 상위무기-계획.md 16-12).
///
///  · 혈기 — 무기로 직접 쓰러뜨릴 때마다 쌓인다 (흡혈 카드가 많을수록 더). HP 가 가득이면 배로 — 버려지던 흡혈이 혈기가 된다.
///    보스는 쓰러뜨릴 일이 드물어 맞힐 때마다 조금씩 쌓인다.
///  · 혈기가 차면 폭주 — 공격 속도 · 사거리가 늘고, 맞힌 피해의 일부를 회복한다 (초당 상한). 폭주 중 처치하면 조금 늘어난다.
///  · 발밑의 붉은 고리가 혈기 (차오르는 호) — 폭주 동안은 가득 차서 맥박처럼 뛰고, 회복할 때 적에게서 피 줄기가 날아온다.
///  · <b>혈풍 난무</b> (2026-10-13 — 엘든링 말레니아 "물새난격"처럼) — 폭주 동안 붉은 검격이 <b>360° 사방에서 무작위로</b> 난무한다.
///    시작할 때 한꺼번에 쏟아지고, 그 뒤 폭주가 끝날 때까지 stormWaveInterval 마다 한 차례씩 몰아친다.
///    검격마다 방향 · 크기 · 길이 · 기울기 · 높이가 무작위 — 날아가지 않고 내 둘레를 사선으로 베는 호 (거울칼날 검기와 다르게).
///    검의 직접 공격으로 센다 — 흡혈 · 폭주 회복 · 처치로 폭주 연장이 같이 된다.
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

    [Header("혈풍 난무 (폭주 — 말레니아 물새난격처럼 사방 무작위 피 검격)")]
    [Tooltip("폭주가 시작될 때 한꺼번에 쏟아지는 검격 수")]
    [Min(0)] public int flurryCount = 12;

    [Tooltip("그 검격들이 다 나오는 시간(초) — 사이 간격은 무작위로 흔들린다")]
    [Min(0.05f)] public float flurryTime = 0.7f;

    [Tooltip("폭주 동안 이 간격(초)마다 검격이 한 차례 더 몰아친다 (±25% 무작위)")]
    [Min(0.1f)] public float stormWaveInterval = 0.8f;

    [Tooltip("한 차례에 나오는 검격 수 (0 이면 시작할 때만)")]
    [Min(0)] public int stormWaveCount = 6;

    [Tooltip("한 차례가 다 나오는 시간(초)")]
    [Min(0.05f)] public float stormWaveTime = 0.4f;

    [Tooltip("폭주 중 이 번째 휘두르기마다 앞쪽 반원에 검기 (0 이면 안 나온다 — 사방 난무가 맡는다)")]
    [Min(0)] public int swingSlashEvery = 0;

    [Tooltip("그때 나오는 검기 수 (앞쪽 반원에 고르게)")]
    [Min(0)] public int swingSlashCount = 3;

    [Tooltip("검격이 닿는 반경 (m) — 모습은 이 안에서 무작위 크기")]
    [Min(0.5f)] public float slashRadius = 4f;

    [Tooltip("검격 한 줄이 베는 각도 (도) — 줄마다 60 ~ 100% 사이 무작위")]
    [Range(10f, 180f)] public float slashArc = 110f;

    [Tooltip("검격을 바닥에서 비스듬히 기울이는 최대 각도 (도) — 칼로 사선으로 벤 자국")]
    [Range(0f, 80f)] public float slashTilt = 55f;

    [Tooltip("검격 한 줄 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float slashDamageMul = 0.4f;   // 촘촘한 만큼 한 줄은 약하게 (폭주 한 번 합계는 비슷)

    public Color slashCoreColor = new Color(1f, 0.55f, 0.55f, 1f);
    public GameSoundSet.Entry slashSound = new GameSoundSet.Entry();
    public GameSoundSet.Entry bloodSound = new GameSoundSet.Entry();

    // ── 테스트용 (손에 든 혈검 기준) ──
    public static float Gauge { get; private set; }
    public static bool Frenzied { get; private set; }
    public static float FrenzyLeft { get; private set; }
    public static int Frenzies { get; private set; }
    public static float LastGain { get; private set; }
    public static int Healed { get; private set; }
    public static int Slashes { get; private set; }
    public static int SlashHits { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Gauge = 0f; Frenzied = false; FrenzyLeft = 0f; Frenzies = 0; LastGain = 0f; Healed = 0; Slashes = 0; SlashHits = 0;
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

        // 혈풍 난무 — 지금 몰아치는 차례에 남은 검격 · 다음 검격까지 · 다음 차례까지
        int pending;
        float pendingTimer;
        float pendingStep;
        float waveTimer;
        int frenzySwings;
        readonly List<Transform> found = new List<Transform>();

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

        public override void OnAttack(in WeaponAttack attack)
        {
            if (!Frenzy || cfg.swingSlashEvery <= 0 || cfg.swingSlashCount <= 0) return;
            if (++frenzySwings % cfg.swingSlashEvery != 0) return;

            Vector3 forward = attack.Direction;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = OwnerRoot != null ? OwnerRoot.forward : Vector3.forward;
            forward.Normalize();

            int n = cfg.swingSlashCount;
            for (int i = 0; i < n; i++)
            {
                float yaw = n > 1 ? -60f + 120f * i / (n - 1) : 0f;
                Slash(Quaternion.Euler(0f, yaw, 0f) * forward);
            }
        }

        /// <summary>검격 한 줄 — 몸 둘레 dir 쪽을 사선으로 벤다 (크기 · 길이 · 기울기 · 높이 무작위). 붉은 호 두 겹 · 피 방울 · 피해 · 회복.</summary>
        void Slash(Vector3 dir)
        {
            Transform owner = OwnerRoot;
            if (owner == null) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();

            Vector3 center = owner.position;
            float r = cfg.slashRadius * Random.Range(0.55f, 0.95f);
            float half = cfg.slashArc * 0.5f * Random.Range(0.6f, 1f);
            float tilt = Random.Range(-cfg.slashTilt, cfg.slashTilt);
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 body = center + Vector3.up * Random.Range(0.5f, 1.5f) + side * Random.Range(-0.5f, 0.5f);
            Slashes++;

            if (cfg.material != null)
            {
                FxLine.SlashArc(cfg.material, body, dir, r, half, tilt, cfg.color, Random.Range(0.36f, 0.52f), Random.Range(0.28f, 0.4f));
                FxLine.SlashArc(cfg.material, body, dir, r * 0.9f, half * 0.85f, tilt, cfg.slashCoreColor, 0.13f, 0.2f);

                // 피 방울 — 호에서 바깥 · 아래로 튀는 짧은 줄기
                Color drop = new Color(cfg.color.r * 0.7f, 0f, 0.02f, 1f);
                for (int i = 0; i < 4; i++)
                {
                    float yaw = Random.Range(-half, half);
                    Vector3 outward = Quaternion.Euler(0f, yaw, 0f) * dir;
                    Vector3 from = body + outward * r;
                    FxLine.Line(cfg.material, from, from + outward * Random.Range(0.4f, 0.9f) + Vector3.down * Random.Range(0.3f, 0.7f), drop, 0.06f, 0.3f);
                }
            }

            CombatFx.PlaySound(cfg.slashSound);
            CombatFx.PlaySound(cfg.bloodSound);

            float damage = Weapon.Stats.Damage * cfg.slashDamageMul;
            if (damage <= 0f) return;

            CombatFx.FindEnemies(center, cfg.slashRadius, found);
            foreach (Transform t in found)
            {
                Vector3 to = t.position - center;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(dir, to) > half) continue;

                bool wasAlive = CombatFx.IsAlive(t);
                Vector3 point = CombatFx.Body(t);
                CombatFx.Damage(t, damage, point, to.sqrMagnitude > 0.0001f ? to.normalized : dir);
                SlashHits++;

                // 검의 직접 공격 — 흡혈(팀원 스킬) · 폭주 회복 · 처치로 폭주 연장
                if (Frenzy) Steal(damage, point);
                if (WeaponBase.CheckDirectKill(t, wasAlive) && Frenzy) ExtendFrenzy();
            }
        }

        public override void OnKill(in WeaponHit hit)
        {
            if (Frenzy)
            {
                ExtendFrenzy();
                return;
            }

            // 흡혈(팀원 스킬)이 먼저 회복한 뒤에 온다 — 회복할 자리가 없었으면(가득) 혈기를 배로
            DrainSkill drain = DrainSkill.Active;
            float gain = cfg.killGain + (drain != null ? drain.HealAmount * cfg.gainPerHeal : 0f);
            if (stats != null && stats.currentHp >= stats.maxHp) gain *= cfg.fullHpMultiplier;

            LastGain = gain;
            Add(gain);
        }

        void ExtendFrenzy()
        {
            float extend = Mathf.Min(cfg.killExtend, Mathf.Max(0f, cfg.maxFrenzy - frenzyTotal));
            frenzyLeft += extend;
            frenzyTotal += extend;
            Publish();
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

            // 혈풍 난무 — 시작에 한꺼번에 쏟아지고, 다 나오면 stormWaveInterval 마다 한 차례씩
            pending = cfg.flurryCount;
            pendingStep = cfg.flurryTime / Mathf.Max(1, cfg.flurryCount);
            pendingTimer = 0f;
            waveTimer = cfg.stormWaveInterval;
            frenzySwings = 0;

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
            pending = 0;
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

            // 혈풍 난무 — 폭주 동안 360° 사방 무작위 (방향 · 간격 모두 흔든다)
            if (Frenzy && Weapon.IsActive)
            {
                if (pending == 0 && cfg.stormWaveCount > 0 && (waveTimer -= deltaTime) <= 0f)
                {
                    pending = cfg.stormWaveCount;
                    pendingStep = cfg.stormWaveTime / cfg.stormWaveCount;
                    pendingTimer = 0f;
                    waveTimer = cfg.stormWaveInterval * Random.Range(0.75f, 1.25f);
                }

                if (pending > 0)
                {
                    pendingTimer -= deltaTime;
                    while (pendingTimer <= 0f && pending > 0)
                    {
                        Slash(Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward);
                        pending--;
                        pendingTimer += pendingStep * Random.Range(0.4f, 1.6f);
                    }
                }
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
