using UnityEngine;

/// <summary>
/// 드래곤 보스 연출 (2026-11-20) — 화면 흔들림 · 공격 이펙트. 판정 · 패턴에는 손대지 않는다 (값은 DragonBossSettings.Staging).
///
///   화면 흔들림 — 세기만 다른 충격 하나였던 것을 세 단계로 나눈다 (무기 연출의 ImpactFx 부품을 쓴다)
///     작게 Jolt   : 발걸음 · 불덩이 발사 · 불비 한 발            — 짧은 떨림
///     중간 Shove  : 물기 · 꼬리 · 돌진 출발 · 브레스 시작 · 포효 — 공격이 간 쪽으로 화면이 밀렸다 돌아온다
///     크게 Quake  : 땅 물어뜯기 · 벽 충돌 · 도약 착지 · 전환 포효 — 아래로 쿵 + 줌 + 가장자리 번쩍
///     이어지는 떨림 Tremor (브레스 · 돌진 · 포효 동안) · 거리 감쇠 (멀면 절반) · 발걸음 (가까울수록 크게)
///
///   공격 이펙트 — 맞는 순간에 무언가 터지고, 지나간 자리에 흔적이 남는다 (무기 연출의 입자 재료 FxKit). 과하다 싶을 만큼.
///   몸 — 2페이즈 입가의 불티 · 3페이즈 몸 둘레의 열기 · 마지막 발악의 달아오름 · 큰 한 방에 번쩍 + 움찔
///
/// 전후 비교: DragonBoss.StagingOff = true 면 2026-11-20 이전 그대로 (세기만 다른 흔들림 · 이펙트 없음).
/// </summary>
public partial class DragonBoss
{
    /// <summary>켜면 연출 개선 전으로 돌아간다 (전후 비교 캡처용).</summary>
    public static bool StagingOff;

    static readonly Color FireColor = new Color(1f, 0.5f, 0.14f);
    static readonly Color HotColor = new Color(1f, 0.84f, 0.52f);
    static readonly Color EmberColor = new Color(1f, 0.34f, 0.07f);
    static readonly Color DustColor = new Color(0.56f, 0.47f, 0.39f);
    static readonly Color SootColor = new Color(0.16f, 0.1f, 0.08f);
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    DragonBossSettings.Staging stage = new DragonBossSettings.Staging();
    float nextTremor, quietUntil;
    float flashUntil, nudgeUntil, lastFlinch = -99f;
    int seenHp = -1;
    Vector3 modelHome, nudge;
    bool modelHomeKnown;
    Renderer[] bodyRenderers;
    MaterialPropertyBlock bodyTint;
    bool leftStep;

    /// <summary>지금 쓰는 연출 값.</summary>
    public DragonBossSettings.Staging Stage => stage;
    /// <summary>연출 개선이 켜져 있는가.</summary>
    public bool Staged => !StagingOff && stage.enabled;
    /// <summary>마지막 발악 중인가 (남은 체력이 enrageAt 아래).</summary>
    public bool Enraged { get; private set; }
    /// <summary>싸우는 중인가 (등장을 마쳤고 아직 살아 있다).</summary>
    public bool Fighting => state != State.Waiting && state != State.Dead;
    /// <summary>바닥 예고에 쓰는 재질 (반투명 단색) — 발밑 위험 고리도 같은 것을 쓴다.</summary>
    public Material TelegraphMaterial => telegraphMaterial;
    /// <summary>지금 떠 있는 바닥 예고들.</summary>
    public System.Collections.Generic.IReadOnlyList<DragonTelegraph> Telegraphs => telegraphs;

    bool FxOn => Staged && stage.attackFx && FxKit.Ready;
    float FxScale => stage.fxScale;
    /// <summary>바닥 높이로 내린 자리.</summary>
    Vector3 Ground(Vector3 at) => new Vector3(at.x, transform.position.y, at.z);

    /// <summary>기존 효과 프리팹을 씬 단위 FxPool에서 재사용한다. 색은 새로 만들 때만 입혀 반복 재생 시 퇴색하지 않게 한다.</summary>
    void PrewarmBossVfx(GameObject prefab, Color tint, int count)
    {
        if (prefab == null) return;
        FxPool.Prewarm(prefab, FxPool.ColorKey(tint), count,
            fx => ConfigureBossVfx(fx, tint), transform.position);
    }

    bool PlayBossVfx(GameObject prefab, Vector3 at, Color tint, float scale, float lifetime)
    {
        if (!Staged || !stage.attackFx || prefab == null) return false;
        FxPool.Spawn(prefab, FxPool.ColorKey(tint), at + Vector3.up * 0.3f, Quaternion.identity,
            scale * FxScale, lifetime, fx => ConfigureBossVfx(fx, tint));
        return true;
    }

    static void ConfigureBossVfx(GameObject fx, Color tint)
    {
        EvoStrikes.Tint(fx, tint);
        EvoStrikes.ThinSmoke(fx, 0.4f, 0.55f);
    }

    void LoadStaging()
    {
        var settings = Resources.Load<DragonBossSettings>(DragonBossSettings.ResourcePath);
        if (settings != null && settings.staging != null) stage = settings.staging;
    }

    // ───────── 화면 흔들림 ─────────

    /// <summary>거리 감쇠 — 플레이어가 그 자리에서 멀수록 약하다 (nearRange 안 1 → farRange 밖 farShake).</summary>
    float Near(Vector3 at)
    {
        if (target == null) return 1f;
        float d = Flat(target.position - at).magnitude;
        return Mathf.Lerp(1f, stage.farShake, Mathf.InverseLerp(stage.nearRange, stage.farRange, d));
    }

    /// <summary>작게 — 짧은 떨림. legacy = 예전 세기 (0 이면 예전에는 없던 것).</summary>
    void Jolt(float force, Vector3 at, float legacy)
    {
        if (!Staged) { if (legacy > 0f) Shake(legacy); return; }
        Shake(force * stage.shakeScale * Near(at));
    }

    /// <summary>중간 — 공격이 간 쪽(from → at)으로 화면이 밀렸다 돌아온다.</summary>
    void Shove(float force, Vector3 at, Vector3 from, float legacy)
    {
        if (!Staged) { if (legacy > 0f) Shake(legacy); return; }
        float amount = force * stage.shakeScale * Near(at);
        Vector3 push = Flat(at - from);
        if (push.sqrMagnitude > 0.01f) ImpactHost.Get().Kick(-push, amount);
        Shake(amount * (push.sqrMagnitude > 0.01f ? 0.4f : 1f));   // 밀림만으로는 가볍다 — 짧은 떨림을 섞는다
    }

    /// <summary>크게 — 아래로 쿵 내려앉는다 + 시야를 당긴다 + 가장자리가 불빛으로 물든다.</summary>
    void Quake(float force, Vector3 at, float legacy)
    {
        if (!Staged) { if (legacy > 0f) Shake(legacy); return; }
        float near = Near(at);
        ImpactHost host = ImpactHost.Get();
        host.Thud(force * stage.shakeScale * near);
        Shake(force * 0.45f * stage.shakeScale * near);
        if (stage.zoom > 0f) host.Zoom(stage.zoom * near);
        if (stage.edgeFlash > 0f) host.Flash(HotColor, stage.edgeFlash * near);
    }

    /// <summary>이어지는 떨림 — 프레임마다 불러도 0.1초에 한 번만 낸다. scale 은 tremor 값에 곱한다.</summary>
    void Tremor(Vector3 at, float scale = 1f)
    {
        if (!Staged || stage.tremor <= 0f || Time.time < nextTremor || Time.time < quietUntil) return;
        nextTremor = Time.time + 0.1f;
        ImpactHost.Get().Rumble(stage.tremor * scale * stage.shakeScale * Near(at));
    }

    /// <summary>정적 — 큰 한 방 직전에 떨림을 잠깐 멈춘다 (대비가 있어야 무겁다).</summary>
    void Hush(float seconds) => quietUntil = Mathf.Max(quietUntil, Time.time + seconds);

    /// <summary>발걸음 — 가까울수록 크게 흔들리고 발밑에 먼지가 인다. 소리 크기를 돌려준다 (가까울수록 크다).</summary>
    float StepFx(bool left)
    {
        if (!Staged || target == null) return 1f;
        float d = FlatDistance(target.position);
        float near = 1f - Mathf.Clamp01(d / Mathf.Max(1f, stage.stepRange));
        if (near > 0f) Shake(Mathf.Lerp(stage.stepShake.x, stage.stepShake.y, near) * stage.shakeScale);
        if (FxOn)
        {
            Vector3 foot = transform.position + transform.right * ((left ? -1.1f : 1.1f) * bodyScale) + transform.forward * (0.8f * bodyScale);
            FxKit.Mist(foot + Vector3.up * 0.3f, 0.9f * bodyScale, DustColor, 0.34f, 2f * FxScale, 0.7f);
            FxKit.Shockwave(foot, 2.6f * bodyScale, DustColor, 0.26f, 0.45f);
        }
        return Mathf.Lerp(0.6f, 1.15f, near);
    }

    // ───────── 공격 이펙트 ─────────

    /// <summary>물기 — 턱이 닫히는 자리: 섬광 · 이빨 자국 · 튀는 불티.</summary>
    void FxBite(Vector3 origin, Vector3 forward, bool chain)
    {
        if (!FxOn) return;
        Vector3 f = Flat(forward).normalized;
        Vector3 at = origin + f * Mathf.Lerp(biteNear, biteFar, 0.8f) + Vector3.up * 1.1f;
        float s = FxScale * (chain ? 0.85f : 1f);
        bool prefabImpact = !chain && current != Pattern.GroundBite &&
            PlayBossVfx(impactVfxPrefab, at, HotColor, 2.8f * bodyScale, 1.6f);
        // 프리팹 타격 섬광 위에 짧은 중심 플래시를 겹쳐 턱이 닫히는 순간을 강조한다.
        FxKit.EnergyBurst(Ground(at), (prefabImpact ? 1.8f : 2.6f) * bodyScale, HotColor, (prefabImpact ? 0.9f : 1.2f) * s);
        FxKit.ClawMark(at, 6.5f * bodyScale * s, FireColor, 0.3f);
        FxKit.ClawMark(at + Vector3.up * 0.4f, 4.5f * bodyScale * s, Color.white, 0.18f);
        FxKit.Spray(at, f, EmberColor, 34f * s, 13f, 0.85f);
        FxKit.Spray(at, Vector3.up * 0.8f - f * 0.4f, HotColor, 18f * s, 8f, 1f);
        FxKit.Shockwave(Ground(at), 10f * bodyScale * s, HotColor, 0.3f, 0.85f);
        FxKit.Wave(Ground(at), 12f * bodyScale * s, FireColor, 0.45f, 0.5f);
        FxKit.Debris(Ground(at), 1.6f * bodyScale, 6f * s);
        FxKit.Glints(at, 2.4f * bodyScale, Color.white, 14f * s, 0.9f, 0.5f);
    }

    /// <summary>땅 물어뜯기 — 땅이 깨진다: 폭발 · 충격파 겹겹 · 파편 · 먼지 기둥 · 그을음.</summary>
    void FxGroundSlam(Vector3 slam, float radius)
    {
        if (!FxOn) return;
        float s = FxScale;
        FxKit.Explosion(slam, radius * 0.5f, FireColor, 1.15f * s);
        FxKit.BigShockwave(slam, radius * 0.75f, HotColor);
        // 기존 충격파의 지름과 같은 크기로 펼쳐져 공격 범위가 화면에서 읽히게 한다.
        if (!PlayBossVfx(shockwaveVfxPrefab, Ground(slam), HotColor, radius * 2.3f, 1.8f))
            FxKit.Shockwave(slam, radius * 2.3f, Color.white, 0.4f, 0.9f);
        FxKit.Wave(slam, radius * 3f, FireColor, 0.65f, 0.6f);
        FxKit.Debris(slam, radius * 0.7f, 14f * s);
        FxKit.Mist(slam + Vector3.up * 0.8f, radius * 0.8f, DustColor, 0.5f, 10f * s, 1.4f);
        FxKit.Pillar(slam, radius * 0.45f, 6.5f, DustColor, 0.55f, 7);
        FxKit.FlameRing(slam, radius * 0.85f, FireColor, 12f * s, 2.6f, 0.5f);
        FxKit.Scorch(slam, radius * 1.8f, 1.6f);
    }

    /// <summary>브레스가 터져 나오는 순간 — 입 앞 폭풍.</summary>
    void FxBreathStart(Vector3 apex, Vector3 forward)
    {
        if (!FxOn) return;
        Vector3 mouth = Mouth();
        FxKit.MuzzleBlast(mouth, forward, 2.2f * bodyScale * FxScale, FireColor, transform.position.y);
        FxKit.Shockwave(Ground(apex), 7f * bodyScale, HotColor, 0.34f, 0.8f);
    }

    /// <summary>브레스를 뿜는 동안 (프레임마다) — 불 가장자리를 따라 솟는 불길 · 지나간 바닥의 그을음과 잔불.</summary>
    void FxBreathTick(Vector3 apex, Vector3 forward, float radius, float halfAngle)
    {
        Tremor(apex + forward * (radius * 0.5f));
        if (!FxOn) return;
        float dt = Time.deltaTime, s = FxScale;
        forward = Flat(forward).normalized;
        // 가장자리 두 줄 — 좌우 끝을 따라 불길이 솟는다
        if (Random.value < 26f * dt * s)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            Vector3 edge = Quaternion.Euler(0f, side * halfAngle * Random.Range(0.86f, 1f), 0f) * forward;
            Vector3 at = apex + edge * Random.Range(0.25f, 1f) * radius;
            FxKit.FlameRing(at, 0.5f, FireColor, 2f, Random.Range(1.6f, 2.8f), 0.45f);
        }
        // 안쪽 — 불티가 피어오르고 바닥이 그을린다
        Vector3 inside = apex + Quaternion.Euler(0f, Random.Range(-halfAngle, halfAngle) * 0.85f, 0f) * forward * (Random.Range(0.2f, 0.95f) * radius);
        FxKit.Aura(inside, 1.4f, EmberColor, 22f * dt * s, 1.1f);
        if (Random.value < 9f * dt) FxKit.Scorch(inside, Random.Range(2.4f, 3.8f), 1.5f);
    }

    /// <summary>불덩이 한 발 — 입 앞 폭풍 (날아가는 꼬리 · 착탄 폭발은 탄에 붙인 DragonFireballFx 가 낸다).</summary>
    void FxFireballLaunch(GameObject projectile, Vector3 mouth, Vector3 dir)
    {
        if (!FxOn) return;
        FxKit.MuzzleBlast(mouth, dir, 1.5f * bodyScale * FxScale, FireColor, transform.position.y);
        FxKit.Glints(mouth, 1.2f * bodyScale, HotColor, 6f * FxScale, 0.6f, 0.4f);
        if (projectile == null) return;
        var fx = projectile.GetComponent<DragonFireballFx>();
        if (fx == null) fx = projectile.AddComponent<DragonFireballFx>();
        fx.Arm(transform.position.y, FxScale);
    }

    /// <summary>돌진 준비 (프레임마다) — 발밑으로 기운이 빨려든다.</summary>
    void FxChargeWindup(float progress)
    {
        if (!FxOn) return;
        float dt = Time.deltaTime;
        FxKit.Inflow(transform.position, 5f * bodyScale, FireColor, (30f + 60f * progress) * dt * FxScale);
        if (Random.value < 5f * dt) FxKit.Swirl(transform.position, 7f * bodyScale, EmberColor, 0.4f, 2, 0.6f);
    }

    /// <summary>돌진 출발 — 발밑이 터지며 튀어 나간다.</summary>
    void FxChargeLaunch(Vector3 forward)
    {
        if (!FxOn) return;
        Vector3 at = transform.position;
        FxKit.EnergyBurst(at, 2.4f * bodyScale, FireColor, 1.1f * FxScale);
        FxKit.Shockwave(at, 9f * bodyScale, HotColor, 0.34f, 0.85f);
        FxKit.WindStreaks(at, forward, 10f, 2.2f * bodyScale, HotColor, 22f * FxScale, 38f);
        FxKit.Mist(at - forward * (1.5f * bodyScale) + Vector3.up * 0.5f, 2.2f * bodyScale, DustColor, 0.5f, 7f * FxScale, 1.1f);
    }

    /// <summary>돌진으로 달리는 동안 (프레임마다) — 양옆으로 튀는 파편과 불꽃 · 바닥에 긁힌 자국.</summary>
    void FxChargeRun(Vector3 forward, float step)
    {
        Tremor(transform.position, 1.2f);
        if (!FxOn) return;
        float s = FxScale;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 head = transform.position + forward * (chargeFront * 0.55f);
        float count = step * 3.2f * s;   // 1m 에 세 줄쯤
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 at = head + right * (side * chargeWidth * 0.42f) + Vector3.up * 0.4f;
            FxKit.Spray(at, right * side + Vector3.up * 0.7f - forward * 0.3f, EmberColor, count * 3f, 12f, 0.6f);
            if (Random.value < step * 0.5f) FxKit.Debris(at, 1.5f, 4f * s);
            if (Random.value < step * 0.3f) FxKit.Pop(Ground(at), 1.5f, FireColor, 0.9f * s);            // 발톱이 긁고 간 자리가 터진다
            if (Random.value < step * 0.45f) FxKit.FlameRing(Ground(at), 0.7f, FireColor, 3f, 2.6f, 0.4f);
        }
        if (Random.value < step * 0.4f) FxKit.Scorch(transform.position, chargeWidth * 0.9f, 1.4f);
        FxKit.WindStreaks(head, forward, 8f, chargeWidth * 0.5f, HotColor, step * 3f * s, 44f);
        FxKit.Mist(transform.position - forward * (2f * bodyScale) + Vector3.up * 0.5f, 1.6f * bodyScale, DustColor, 0.4f, step * 0.9f * s, 1f);
    }

    /// <summary>벽에 박았다 — 큰 폭발 · 파편 · 충격파 겹겹.</summary>
    void FxChargeCrash(Vector3 forward)
    {
        if (!FxOn) return;
        float s = FxScale;
        Vector3 at = transform.position + forward * (chargeFront * 0.8f);
        if (!PlayBossVfx(explosionVfxPrefab, at, FireColor, 4f * bodyScale, 3.2f))
            FxKit.Explosion(at, 3.2f * bodyScale, FireColor, 1.25f * s);
        FxKit.EnergyBurst(at, 2.6f * bodyScale, HotColor, 1.25f * s, true);
        FxKit.BigShockwave(at, 4.2f * bodyScale, HotColor);
        FxKit.Debris(at, 2.6f * bodyScale, 18f * s);
        FxKit.Spray(at + Vector3.up, -forward + Vector3.up * 0.6f, HotColor, 26f * s, 12f, 1f);
        FxKit.Mist(at + Vector3.up * 0.8f, 3.4f * bodyScale, DustColor, 0.55f, 12f * s, 1.5f);
        FxKit.Scorch(at, 8f * bodyScale, 1.6f);
    }

    /// <summary>불비 한 발이 떨어지기 시작한다 — 하늘에서 내리꽂히는 빛줄기.</summary>
    void FxRainFall(Vector3 point)
    {
        if (!FxOn) return;
        FxKit.Pillar(point, rainRadius * 0.8f, 18f, FireColor, 0.5f, 9);
        FxKit.Disc(point, rainRadius * 3f, HotColor, 0.5f, 0.85f);
    }

    /// <summary>불비 착탄 — 폭발 · 그을음.</summary>
    void FxRainLand(Vector3 point)
    {
        if (!FxOn) return;
        float s = FxScale;
        FxKit.Explosion(point, rainRadius * 1.35f, FireColor, 1.2f * s);
        FxKit.FlameRing(point, rainRadius * 1.15f, FireColor, 10f * s, 2.8f, 0.5f);
        FxKit.Shockwave(point, rainRadius * 5.2f, HotColor, 0.42f, 0.95f);
        FxKit.Debris(point, rainRadius, 10f * s);
    }

    /// <summary>꼬리가 돌기 시작한다 — 꼬리를 따라 도는 소용돌이 · 큰 베기 호 · 밀려 나가는 먼지.</summary>
    void FxTailSweep(Vector3 center, float spinSign)
    {
        if (!FxOn) return;
        float s = FxScale, r = tailRadius;
        FxKit.Swirl(center, r * 2.2f, FireColor, 0.5f, 3, 0.9f);
        FxKit.Swirl(center, r * 1.5f, HotColor, 0.4f, 2, 0.8f, 0.08f);
        FxKit.Shockwave(center, r * 2.3f, HotColor, 0.38f, 0.9f);
        FxKit.Wave(center, r * 2.8f, FireColor, 0.55f, 0.55f);
        // 꼬리 끝이 지나가는 둘레에 베기 호 여섯 — 도는 차례대로
        for (int i = 0; i < 6; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, transform.eulerAngles.y + 180f + spinSign * i * 60f, 0f) * Vector3.forward;
            FxKit.SlashArc(center + dir * (r * 0.7f) + Vector3.up * 0.9f, dir, r * 1.6f * s, FireColor, 4f, 0.3f, i * 0.055f);
            FxKit.SlashArc(center + dir * (r * 0.75f) + Vector3.up * 1.1f, dir, r * 1.1f * s, Color.white, 4f, 0.2f, i * 0.055f);
            FxKit.Mist(center + dir * (r * 0.9f) + Vector3.up * 0.4f, r * 0.34f, DustColor, 0.45f, 3f * s, 1.1f);
            FxKit.Spray(center + dir * (r * 0.95f) + Vector3.up * 0.5f, dir + Vector3.up * 0.5f, EmberColor, 8f * s, 9f, 0.7f);
        }
        FxKit.FlameRing(center, r * 0.95f, FireColor, 18f * s, 2.2f, 0.45f);
        FxKit.Debris(center, r * 0.8f, 12f * s);
    }

    /// <summary>포효 — big 은 페이즈 전환 (빛 폭발 · 겹겹의 큰 충격파 · 바닥이 어두워짐 · 솟는 불티), 아니면 부르는 포효 (불비 · 소환 · 벽 · 고리).</summary>
    void FxRoar(Vector3 center, bool big)
    {
        if (!FxOn) return;
        float s = FxScale;
        Vector3 head = Mouth();
        if (big)
        {
            FxKit.EnergyBurst(center, 2.4f * bodyScale, FireColor, 1.15f * s, true);   // 더 키우면 섬광 그림이 화면 위쪽을 하얗게 덮는다
            FxKit.BigShockwave(center, 9f * areaScale, HotColor);
            FxKit.Shade(center, 30f * areaScale, 0.55f, 1.6f);
            FxKit.Pillar(center, 3f * bodyScale, 11f, FireColor, 0.8f, 10);
            FxKit.FlameRing(center, 5.5f * bodyScale, FireColor, 20f * s, 4.5f, 0.7f);
            FxKit.Aura(center, 6f * bodyScale, EmberColor, 34f * s, 1.6f);
            FxKit.Debris(center, 4f * bodyScale, 14f * s);
            FxKit.Mist(center + Vector3.up, 5f * bodyScale, SootColor, 0.45f, 10f * s, 1.8f);
        }
        else
        {
            FxKit.EnergyBurst(center, 2.6f * bodyScale, FireColor, 1.1f * s);
            FxKit.Shockwave(center, 14f * areaScale, HotColor, 0.45f, 0.85f);
            FxKit.Wave(center, 17f * areaScale, FireColor, 0.7f, 0.5f);
            FxKit.Aura(center, 4f * bodyScale, EmberColor, 16f * s, 1.3f);
        }
        FxKit.Spray(head, Vector3.up, HotColor, (big ? 30f : 14f) * s, 9f, 1.1f);
    }

    /// <summary>포효하는 동안 (프레임마다) — 입에서 불티가 솟는다.</summary>
    void FxRoarTick(float scale = 1f)
    {
        Tremor(transform.position, scale);
        if (!FxOn) return;
        Vector3 head = Mouth();
        FxKit.Aura(Ground(head), 1.6f * bodyScale, EmberColor, 26f * Time.deltaTime * FxScale * scale, 1.2f);
    }

    /// <summary>부하가 나오는 자리 — 빛기둥.</summary>
    void FxSummon(Vector3 point)
    {
        if (!FxOn) return;
        FxKit.Pillar(point, 1.3f, 7f, FireColor, 0.5f, 6);
        FxKit.EnergyBurst(point, 1.3f, EmberColor, 0.9f * FxScale);
    }

    /// <summary>물러나는 날갯짓 — 앞으로 밀려 나가는 바람과 먼지.</summary>
    void FxRetreat(Vector3 from, Vector3 back)
    {
        if (!FxOn) return;
        Vector3 front = -back;
        FxKit.WindStreaks(from, front, 9f, 3.2f * bodyScale, DustColor, 26f * FxScale, 26f);
        FxKit.Mist(from + front * (2f * bodyScale) + Vector3.up * 0.5f, 3f * bodyScale, DustColor, 0.5f, 9f * FxScale, 1.2f);
        FxKit.Shockwave(from, 9f * bodyScale, DustColor, 0.36f, 0.7f);
    }

    /// <summary>불의 벽이 선다 — 벽을 따라 불길이 달려 솟는다.</summary>
    void FxWallRise(Vector3 from, Vector3 to)
    {
        if (!FxOn) return;
        FxKit.FireLine(from, to, wallThickness * 1.3f, FireColor, 60f);
        Vector3 mid = (from + to) * 0.5f;
        FxKit.Shockwave(mid, Flat(to - from).magnitude * 0.9f, HotColor, 0.34f, 0.6f);
    }

    /// <summary>고리가 나간다 — 몸에서 터져 나오는 빛과 충격파.</summary>
    void FxRingLaunch(Vector3 center, bool first)
    {
        if (!FxOn) return;
        float s = FxScale * (first ? 1f : 0.8f);
        FxKit.EnergyBurst(center, 2.2f * bodyScale, FireColor, 1.1f * s, first);
        FxKit.FlameRing(center, ringStart + 1.5f, FireColor, 18f * s, 3.2f, 0.5f);
        FxKit.Shockwave(center, 10f * bodyScale, Color.white, 0.3f, 0.9f);
        if (first) FxKit.Debris(center, 3f * bodyScale, 10f * s);
    }

    /// <summary>도약 — 뛰어오르는 순간의 먼지와 바람.</summary>
    void FxLeapTakeoff(Vector3 at)
    {
        if (!FxOn) return;
        float s = FxScale;
        FxKit.EnergyBurst(at, 2.6f * bodyScale, FireColor, 1.1f * s);
        FxKit.Mist(at + Vector3.up * 0.5f, 3.4f * bodyScale, DustColor, 0.55f, 16f * s, 1.4f);
        FxKit.Shockwave(at, 12f * bodyScale, HotColor, 0.4f, 0.85f);
        FxKit.Swirl(at, 10f * bodyScale, DustColor, 0.5f, 3, 0.8f);
        FxKit.FlameRing(at, 2.6f * bodyScale, FireColor, 12f * s, 3f, 0.5f);
        FxKit.Debris(at, 2.6f * bodyScale, 12f * s);
    }

    /// <summary>도약 — 떨어질 자리에 그늘이 진다 (떠 있는 동안, 프레임마다).</summary>
    void FxLeapShadow(Vector3 spot, float progress)
    {
        if (!FxOn) return;
        if (Random.value < 14f * Time.deltaTime) FxKit.Shade(spot, leapRadius * (2.2f + progress), 0.3f + 0.25f * progress, 0.3f);
        FxKit.Inflow(spot, leapRadius * 1.2f, FireColor, 30f * progress * Time.deltaTime * FxScale);
    }

    /// <summary>도약 — 내려찍는 순간: 큰 폭발 · 충격파 겹겹 · 파편 · 먼지 · 그을음.</summary>
    void FxLeapLand(Vector3 spot)
    {
        if (!FxOn) return;
        float s = FxScale, r = leapRadius;
        FxKit.Explosion(spot, r * 0.8f, FireColor, 1.3f * s);
        FxKit.BigShockwave(spot, r * 1.2f, HotColor);
        if (!PlayBossVfx(shockwaveVfxPrefab, Ground(spot), HotColor, r * 3.4f, 1.8f))
            FxKit.Shockwave(spot, r * 3.4f, Color.white, 0.42f, 0.9f);
        FxKit.Wave(spot, r * 4.2f, FireColor, 0.7f, 0.6f);
        FxKit.Debris(spot, r * 0.9f, 20f * s);
        FxKit.FlameRing(spot, r * 0.95f, FireColor, 16f * s, 3.4f, 0.55f);
        FxKit.Mist(spot + Vector3.up * 0.8f, r, DustColor, 0.55f, 14f * s, 1.6f);
        FxKit.Scorch(spot, r * 2.4f, 1.8f);
    }

    // ───────── 몸 — 페이즈 표시 · 맞을 때 ─────────

    /// <summary>프레임마다 (살아 있는 동안) — 페이즈가 몸에 보인다 · 번쩍임과 움찔을 되돌린다.</summary>
    void TickStaging()
    {
        TickFlinch();
        // 마지막 발악 — 속도(Tuning.enrageTempo)는 연출 스위치와 상관없이 걸린다
        Enraged = stage.enrageAt > 0f && health != null && health.maxHp > 0 && seenHp > 0 && seenHp <= health.maxHp * stage.enrageAt;
        DragonTelegraph.Blink = Staged && stage.telegraphBlink;
        if (!FxOn || !stage.bodyMarks || state == State.Waiting) return;
        float dt = Time.deltaTime, s = FxScale;
        if (phase >= 2)
        {
            // 입가의 불티 — 2페이즈부터
            Vector3 mouth = Mouth();
            FxKit.TrailDust(mouth, EmberColor, (phase >= 3 ? 26f : 14f) * dt * s, 0.16f * bodyScale, false, 0.5f);
        }
        if (phase >= 3 || Enraged)
        {
            // 몸 둘레의 열기 — 3페이즈 · 마지막 발악은 더 세다
            float heat = Enraged ? 2.2f : 1f;
            FxKit.Aura(transform.position, 3.2f * bodyScale, Enraged ? HotColor : EmberColor, 14f * heat * dt * s, 1.3f * bodyScale);
            if (Enraged && Random.value < 3f * dt) FxKit.Swirl(transform.position, 8f * bodyScale, FireColor, 0.5f, 2, 0.55f);
        }
    }

    /// <summary>체력이 깎였다 — 큰 한 방이면 몸이 번쩍이고 움찔한다 (패턴은 끊지 않는다).</summary>
    void OnHurtStaging(int hp, int max)
    {
        int before = seenHp < 0 ? max : seenHp;
        seenHp = hp;
        if (!Staged || !stage.flinch || max <= 0 || hp <= 0 || state == State.Dead) return;
        int lost = before - hp;
        if (lost < max * stage.flinchDamage || Time.time - lastFlinch < 0.25f) return;
        lastFlinch = Time.time;
        float weight = Mathf.Clamp01(lost / (max * stage.flinchDamage * 4f));   // 큰 한 방일수록 세게

        // 번쩍 — 몸 재질을 잠깐 밝힌다
        if (bodyRenderers == null)
        {
            bodyRenderers = new Renderer[0];
            if (animator != null) bodyRenderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>();
            bodyTint = new MaterialPropertyBlock();
        }
        float bright = Mathf.Lerp(1.7f, 2.8f, weight);
        bodyTint.SetColor(BaseColorId, new Color(bright, bright * 0.93f, bright * 0.82f, 1f));
        foreach (Renderer r in bodyRenderers) if (r != null) r.SetPropertyBlock(bodyTint);
        flashUntil = Time.time + 0.09f;

        // 움찔 — 맞은 반대쪽(플레이어 반대쪽)으로 모델만 살짝 밀렸다 돌아온다. 판정 · 위치는 그대로다
        if (animator != null && animator.transform != transform && !airborne)
        {
            if (!modelHomeKnown) { modelHome = animator.transform.localPosition; modelHomeKnown = true; }
            Vector3 away = target != null ? Flat(transform.position - target.position).normalized : -transform.forward;
            nudge = transform.InverseTransformDirection(away) * (Mathf.Lerp(0.18f, 0.45f, weight) * bodyScale);
            nudgeUntil = Time.time + 0.16f;
        }
        if (FxOn)
        {
            Vector3 at = transform.position + Vector3.up * (1.6f * bodyScale);
            FxKit.Glints(at, 2.4f * bodyScale, Color.white, Mathf.Lerp(6f, 16f, weight) * FxScale, 0.7f, 0.6f);
            if (weight > 0.5f) FxKit.Shockwave(transform.position, 7f * bodyScale, Color.white, 0.24f, 0.6f);
        }
    }

    void TickFlinch()
    {
        if (flashUntil > 0f && Time.time >= flashUntil)
        {
            flashUntil = 0f;
            if (bodyRenderers != null) foreach (Renderer r in bodyRenderers) if (r != null) r.SetPropertyBlock(null);
        }
        if (!modelHomeKnown || animator == null) return;
        if (nudgeUntil > 0f)
        {
            float left = Mathf.Clamp01((nudgeUntil - Time.time) / 0.16f);
            animator.transform.localPosition = modelHome + nudge * (left * left);
            if (left <= 0f) { nudgeUntil = 0f; animator.transform.localPosition = modelHome; }
        }
    }

    /// <summary>연출을 원래대로 — 쓰러질 때 · 꺼질 때.</summary>
    void ResetStaging()
    {
        flashUntil = 0f;
        if (bodyRenderers != null) foreach (Renderer r in bodyRenderers) if (r != null) r.SetPropertyBlock(null);
        if (modelHomeKnown && animator != null) animator.transform.localPosition = modelHome;
        nudgeUntil = 0f;
    }
}

/// <summary>
/// 보스 불덩이의 연출 (2026-11-20) — 날아가는 불 꼬리와 착탄 폭발. 탄 자체(RangedProjectile — 몬스터와 같이 쓴다)는 건드리지 않고
/// 보스가 쏜 탄에만 붙인다. 풀로 돌아갈 때(맞았거나 수명이 다했을 때) 그 자리에서 터진다.
/// </summary>
public class DragonFireballFx : MonoBehaviour
{
    static readonly Color Fire = new Color(1f, 0.5f, 0.14f);
    static readonly Color Ember = new Color(1f, 0.34f, 0.07f);

    bool armed;
    float groundY, scale, age;
    Vector3 last;

    public void Arm(float groundY, float scale)
    {
        this.groundY = groundY;
        this.scale = scale;
        armed = true;
        age = 0f;
        last = transform.position;
    }

    void Update()
    {
        if (!armed || !FxKit.Ready) return;
        age += Time.deltaTime;
        last = transform.position;
        FxKit.TrailDust(last, Fire, 130f * Time.deltaTime * scale, 0.6f, false, 1f);
        FxKit.TrailDust(last, Ember, 60f * Time.deltaTime * scale, 0.32f, true, 0f);
    }

    void OnDisable()
    {
        if (!armed) return;
        armed = false;
        if (age < 0.03f || !FxKit.Ready || DragonBoss.StagingOff) return;
        Vector3 ground = new Vector3(last.x, groundY, last.z);
        FxKit.Explosion(ground, 2.2f, Fire, 0.95f * scale);
        FxKit.FlameRing(ground, 1.6f, Fire, 7f * scale, 2.4f, 0.45f);
        FxKit.Shockwave(ground, 9f, new Color(1f, 0.84f, 0.52f), 0.32f, 0.85f);
        FxKit.Debris(ground, 1.4f, 6f * scale);
    }
}
