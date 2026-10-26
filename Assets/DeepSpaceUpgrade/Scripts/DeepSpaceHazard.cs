using UnityEngine;

/// <summary>A telegraphed arena zone. Uses the single player, not collider counts.</summary>
[DisallowMultipleComponent]
public sealed class DeepSpaceHazard : MonoBehaviour
{
    public enum Effect { Energy, Cryogenic, Thermal, Void }
    public enum Phase { Dormant, Warning, Active, Stopped }
    public Effect effect;
    [Min(.5f)] public float radius = 2.8f;
    [Min(0)] public float initialDelay = 8f;
    [Min(1)] public float restSeconds = 7f;
    [Min(1)] public float warningSeconds = 2.5f;
    [Min(.1f)] public float activeSeconds = 2f;
    [Min(0)] public float phaseOffset;
    [Min(0)] public float damage = 5;
    [Range(.1f, 1)] public float slowMultiplier = .72f;
    public Renderer[] indicators;
    public Transform progressRing;
    public ParticleSystem plume;
    public TextMesh readout;
    public Color activeColor = Color.cyan;
    public Phase CurrentPhase { get; private set; }
    public int DamageTicks { get; private set; }
    public bool HasAppliedSlow => slowedPlayer != null;

    MaterialPropertyBlock block;
    PlayerMove slowedPlayer;
    float startedAt, nextDamage, nextReadout;
    bool stopped;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int Emission = Shader.PropertyToID("_EmissionColor");

    void OnEnable()
    {
        startedAt = Time.time;
        stopped = false;
        nextDamage = 0;
        block = new MaterialPropertyBlock();
        GameEvents.OnPlayerDeadStart += Stop;
        GameEvents.OnGameWin += Stop;
        Refresh(Phase.Dormant, 0, initialDelay);
    }

    void OnDisable()
    {
        GameEvents.OnPlayerDeadStart -= Stop;
        GameEvents.OnGameWin -= Stop;
        ReleaseSlow();
        if (plume) plume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        CurrentPhase = Phase.Stopped;
    }

    public Phase PhaseAt(float elapsed, out float progress, out float remaining)
    {
        float start = initialDelay + phaseOffset;
        progress = 0;
        remaining = Mathf.Max(0, start - elapsed);
        if (elapsed < start) return Phase.Dormant;
        float rest = Mathf.Max(1, restSeconds), warning = Mathf.Max(1, warningSeconds);
        float active = Mathf.Max(.1f, activeSeconds);
        float t = Mathf.Repeat(elapsed - start, rest + warning + active);
        if (t < rest) { remaining = rest - t; return Phase.Dormant; }
        t -= rest;
        if (t < warning) { progress = t / warning; remaining = warning - t; return Phase.Warning; }
        remaining = active - (t - warning);
        progress = 1;
        return Phase.Active;
    }

    public bool Contains(Vector3 position)
    {
        var delta = position - transform.position;
        return Mathf.Abs(delta.y) < 3.5f && delta.x * delta.x + delta.z * delta.z <= radius * radius;
    }

    void Update()
    {
        if (stopped) return;
        var phase = PhaseAt(Time.time - startedAt, out float progress, out float remaining);
        Refresh(phase, progress, remaining);
        var player = GameEvents.Player;
        var stats = player ? player.GetComponent<PlayerStats>() : null;
        if (phase != Phase.Active || !player || !stats || stats.IsDead || !Contains(player.position))
        {
            ReleaseSlow();
            return;
        }
        if (effect == Effect.Cryogenic)
        {
            var move = player.GetComponent<PlayerMove>();
            if (slowedPlayer != move) { ReleaseSlow(); slowedPlayer = move; if (move) move.AddSpeedModifier(slowMultiplier); }
        }
        else if (Time.time >= nextDamage)
        {
            nextDamage = Time.time + 1f;
            stats.TakeDamage(new DamageInfo { damage = damage, hitPoint = player.position,
                hitDirection = (player.position - transform.position).normalized, cameraShake = 0 });
            DamageTicks++;
        }
    }

    void Refresh(Phase phase, float progress, float remaining)
    {
        bool wasActive = CurrentPhase == Phase.Active;
        CurrentPhase = phase;
        bool active = phase == Phase.Active;
        Color color = phase == Phase.Warning ? new Color(1, .58f, .08f) : activeColor;
        float strength = phase == Phase.Warning ? 1.1f + .5f * Mathf.Sin(Time.time * 9) : active ? 2.2f : .18f;
        block.SetColor(BaseColor, color * (active ? 1 : .65f));
        block.SetColor(Emission, color * strength);
        if (indicators != null) foreach (var r in indicators) if (r) r.SetPropertyBlock(block);
        if (progressRing)
        {
            progressRing.gameObject.SetActive(phase == Phase.Warning);
            progressRing.localScale = Vector3.one * (phase == Phase.Warning ? Mathf.Lerp(.12f, 1, progress) : 1);
        }
        if (plume && active != wasActive)
        {
            if (active) plume.Play(); else plume.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        if (readout && Time.time >= nextReadout)
        {
            nextReadout = Time.time + .15f;
            string title = effect == Effect.Cryogenic ? "CRYO VENT" : effect == Effect.Thermal ? "HEAT EXHAUST" : effect == Effect.Void ? "CORE PULSE" : "POWER RELAY";
            readout.text = title + "\n" + (active ? (effect == Effect.Cryogenic ? "SLOW FIELD" : "DANGER") : phase == Phase.Warning ? "CLEAR / " + remaining.ToString("0.0") : "STANDBY");
            readout.color = phase == Phase.Warning ? new Color(1, .75f, .22f) : Color.Lerp(activeColor, Color.white, .55f);
        }
    }

    void ReleaseSlow()
    {
        if (slowedPlayer) slowedPlayer.RemoveSpeedModifier(slowMultiplier);
        slowedPlayer = null;
    }

    public void Stop()
    {
        stopped = true;
        ReleaseSlow();
        Refresh(Phase.Stopped, 0, 0);
        if (plume) plume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (readout) readout.text = "SYSTEM OFFLINE";
    }
}
