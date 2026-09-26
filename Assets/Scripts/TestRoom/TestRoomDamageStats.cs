using System.Collections.Generic;

/// <summary>Full-precision target totals; independent of integer popups and batched hit audio.</summary>
public sealed class TestRoomDamageStats
{
    public const double WindowSeconds = 5;
    readonly Queue<(double time, float damage)> recent = new Queue<(double, float)>();
    double recentDamage;
    double firstHitTime = -1;
    public double TotalDamage { get; private set; }
    public float LastDamage { get; private set; }
    public bool LastWasCritical { get; private set; }
    public long HitCount { get; private set; }
    public long CriticalCount { get; private set; }
    public double Dps => recentDamage / WindowSeconds;

    public void Record(DamageInfo info, double now)
    {
        if (info.damage <= 0 || float.IsNaN(info.damage) || float.IsInfinity(info.damage)) return;
        if (firstHitTime < 0) firstHitTime = now;
        LastDamage = info.damage;
        LastWasCritical = info.isCritical;
        TotalDamage += info.damage;
        HitCount++;
        if (info.isCritical) CriticalCount++;
        recent.Enqueue((now, info.damage));
        recentDamage += info.damage;
        Tick(now);
    }

    public void Tick(double now)
    {
        while (recent.Count > 0 && recent.Peek().time <= now - WindowSeconds)
            recentDamage -= recent.Dequeue().damage;
        if (recent.Count == 0) recentDamage = 0;
    }

    public bool IsWarmingUp(double now) => firstHitTime >= 0 && now - firstHitTime < WindowSeconds;

    public void Reset()
    {
        recent.Clear();
        recentDamage = 0;
        firstHitTime = -1;
        TotalDamage = 0;
        LastDamage = 0;
        LastWasCritical = false;
        HitCount = 0;
        CriticalCount = 0;
    }
}
