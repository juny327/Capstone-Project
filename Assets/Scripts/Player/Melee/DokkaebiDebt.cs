using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도깨비 빚 (2026-11-22 뚝딱 활용 B3) — 도깨비 방망이를 갖춘 동안(주무기 · 보조 모두) 맞으면
/// 피해 일부를 지갑 코인이 대신 맞는다. 다 막지는 않는다 — 코인 일부 + 체력 일부를 잃는다 (체력은 언제나 1 이상 깎인다).
/// 대신 낸 코인은 '도깨비가 꾼 돈'이 되어 다음 정비 때 돌려받는다 (설화처럼 건망증 탓에 가끔 더 갚는다).
///
/// 플레이어에 붙는다 (지갑처럼 — 죽거나 로비로 가면 함께 사라진다). 수치는 도깨비 방망이 효과(AE_EVO_Dokkaebi)의 '도깨비 빚' 칸.
/// 방패(ShieldAbility)가 다 막은 피해에는 코인을 쓰지 않게 가로채기 중 맨 마지막에 묻는다.
/// </summary>
[DisallowMultipleComponent]
public class DokkaebiDebt : MonoBehaviour, IDamageFilter
{
    PlayerStats stats;
    Wallet wallet;
    readonly List<BatEffect> sources = new List<BatEffect>();   // 지금 갖춘 도깨비 방망이 (같은 설정이 두 번 들어올 수 있다 — 주무기 + 보조)
    BatEffect lastSource;                                       // 돌려줄 때 쓸 설정 (방망이를 판 뒤에도 빚은 갚는다)

    /// <summary>도깨비가 꾼 코인 — 다음 정비 때 돌려받는다.</summary>
    public int Borrowed { get; private set; }

    /// <summary>시험 집계 — 막은 횟수 · 대신 낸 코인 · 아낀 체력 · 돌려받은 코인 · 건망증 횟수.</summary>
    public static int Blocks, CoinsTaken, HpSaved, Repaid, Forgetful;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Blocks = CoinsTaken = HpSaved = Repaid = Forgetful = 0; }

    /// <summary>도깨비 방망이를 갖출 때 (BatEffect 가 부른다). 플레이어를 못 찾으면 false.</summary>
    public static bool Join(GameObject owner, BatEffect cfg)
    {
        if (owner == null || cfg == null) return false;
        PlayerStats s = owner.GetComponentInParent<PlayerStats>();
        if (s == null) return false;

        DokkaebiDebt debt = s.GetComponent<DokkaebiDebt>();
        if (debt == null) debt = s.gameObject.AddComponent<DokkaebiDebt>();
        debt.sources.Add(cfg);
        debt.lastSource = cfg;
        return true;
    }

    /// <summary>도깨비 방망이를 뗄 때 · 사라질 때.</summary>
    public static void Leave(GameObject owner, BatEffect cfg)
    {
        if (owner == null) return;
        PlayerStats s = owner.GetComponentInParent<PlayerStats>();
        DokkaebiDebt debt = s != null ? s.GetComponent<DokkaebiDebt>() : null;
        if (debt != null) debt.sources.Remove(cfg);
    }

    /// <summary>
    /// 정비를 열 때 (MaintenanceUI) — 꾼 코인을 돌려준다. 돌려준 게 있으면 창에 띄울 문구, 없으면 null.
    /// </summary>
    public static string Repay(GameObject player)
    {
        DokkaebiDebt debt = player != null ? player.GetComponentInChildren<DokkaebiDebt>() : null;
        if (debt == null || debt.Borrowed <= 0) return null;

        BatEffect cfg = debt.lastSource;
        int owed = debt.Borrowed;
        int amount = Mathf.CeilToInt(owed * (cfg != null ? cfg.debtRepayRate : 1f));
        bool forgot = cfg != null && Random.value < cfg.debtForgetChance;
        if (forgot)
        {
            amount = Mathf.CeilToInt(amount * cfg.debtForgetBonus);
            Forgetful++;
        }

        debt.Borrowed = 0;
        Wallet w = debt.ResolveWallet();
        if (w == null || amount <= 0) return null;

        w.Add(amount);
        Repaid += amount;
        return forgot
            ? $"도깨비가 꾼 돈을 갚았다 — 건망증 탓에 {owed} 대신 {amount} 을 돌려줬다"
            : $"도깨비가 꾼 돈 {amount} 을 갚았다";
    }

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        if (stats != null) stats.AddDamageFilter(this, askLast: true);
    }

    void OnDestroy()
    {
        if (stats != null) stats.RemoveDamageFilter(this);
    }

    Wallet ResolveWallet()
    {
        if (wallet == null) wallet = GetComponent<Wallet>();
        return wallet;
    }

    BatEffect Active()
    {
        for (int i = sources.Count - 1; i >= 0; i--)
        {
            if (sources[i] == null) { sources.RemoveAt(i); continue; }
            return sources[i];
        }
        return null;
    }

    public bool Absorb(ref DamageInfo info)
    {
        BatEffect cfg = Active();
        if (cfg == null || !cfg.debt || stats == null || info.damage < 1f) return false;

        Wallet w = ResolveWallet();
        if (w == null || w.Balance <= 0) return false;

        // PlayerStats 와 같은 셈으로 깎일 체력을 먼저 낸다 (캐릭터 배율 · 난이도)
        float scale = Mathf.Max(0.01f, stats.DamageTakenMultiplier * Difficulty.Values.enemyDamage);
        int full = Mathf.Max(1, (int)(info.damage * scale));
        if (full < 2) return false;   // 1 은 그대로 맞는다 — 다 막지 않는다

        int cover = Mathf.Clamp(Mathf.RoundToInt(full * cfg.debtShare), 0, full - 1);
        float perCoin = Mathf.Max(0.1f, cfg.debtHpPerCoin);
        int coins = Mathf.CeilToInt(cover / perCoin);
        if (coins > w.Balance)
        {
            coins = w.Balance;
            cover = Mathf.Min(cover, Mathf.FloorToInt(coins * perCoin));
        }
        if (cover <= 0 || coins <= 0 || !w.TrySpend(coins)) return false;

        Borrowed += coins;
        lastSource = cfg;
        Blocks++;
        CoinsTaken += coins;
        HpSaved += cover;

        // 남은 피해가 정확히 full - cover 가 되게 (0.5 를 더해 떨어지는 소수 오차를 막는다)
        info.damage = (full - cover + 0.5f) / scale;

        Fx(cfg, coins);
        return false;   // 막지 않는다 — 줄어든 피해가 체력에 들어간다
    }

    /// <summary>엽전이 몸에서 튀어 나간다 — 금빛 불티 · 바닥 빛 · '−3 냥' 글자.</summary>
    void Fx(BatEffect cfg, int coins)
    {
        Vector3 p = transform.position;
        Vector3 chest = p + Vector3.up * 1.1f;
        GroundGlow.Flash(p, 1.4f, cfg.goldColor, 0.22f, 0.8f);
        FlashLight.Flash(chest, cfg.goldColor, 5f, 0.14f, 0.7f);
        if (cfg.material != null) FxLine.Ring(cfg.material, p, 0.15f, 1.2f, cfg.goldColor, 0.1f, 0.22f);
        if (cfg.UsesKit)
        {
            FxKit.Glints(chest, 1.2f, cfg.goldColor, Mathf.Min(40f, 8f + coins * 4f), 0.7f, 0.6f);
            FxKit.EnergyBurst(chest, 0.6f, cfg.goldColor, 0.6f);
        }
        FloatingWord.Show(p + Vector3.up * 2.4f, string.Format(cfg.debtWord, coins), cfg.goldColor, cfg.font, 60f, 0.8f);
        CombatFx.PlaySound(cfg.debtSound);
    }
}
