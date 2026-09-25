using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 충전 레이저 — 모았다가 굵은 관통 빔을 한 방 쏜다 (아이작 *혈사포* 계열).
///
/// **충전은 별도 상태 기계가 아니다.** WeaponBase 가 발사 후 걸어 주는 쿨다운
/// (= `fireInterval`)이 그대로 충전 시간이고, 여기서는 그 진행도를 보여 주기만 한다.
/// 덕분에 "충전 중에 적이 사라지면 어떻게 되나" 같은 문제가 생기지 않는다 —
/// 쿨다운은 적과 무관하게 흐르므로 충전이 끊기지 않는다.
///
/// 판정은 <see cref="Physics.SphereCastNonAlloc"/> 한 번이다.
/// 중복 제거는 <see cref="MeleeWeapon"/> 과 같은 HashSet 방식을 쓴다.
/// </summary>
public class ChargeBeamWeapon : WeaponBase
{
    private ChargeBeamWeaponData Config => (ChargeBeamWeaponData)data;

    protected override bool AutoReload => Config == null || Config.autoReload;

    // 할당 없이 재사용하는 버퍼
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private readonly HashSet<Transform> struck = new HashSet<Transform>();

    private LineRenderer beam;      // 발사된 빔
    private LineRenderer charge;    // 총구에 모이는 빛
    private Material runtimeMaterial;

    private float beamHideAt = -1f;

    /// <summary>
    /// 사거리 안에 적이 있을 때만 쏜다.
    ///
    /// 감지 범위(10m)와 빔 사거리(18m)가 달라서, 확인하지 않으면
    /// 허공에 쏘면서 2초짜리 충전을 날린다.
    /// </summary>
    public override bool HasTargetInReach(in WeaponFireContext context)
    {
        if (context.Targets == null) return false;

        return context.Targets.GetNearest(context.Origin, Stats.Range) != null;
    }

    protected override void OnFire(in WeaponRuntimeStats stats, in WeaponFireContext context)
    {
        ChargeBeamWeaponData cfg = Config;

        if (cfg == null)
        {
            Debug.LogError($"[ChargeBeamWeapon] ChargeBeamWeaponData 가 아닙니다. ({name})", this);
            enabled = false;
            return;
        }

        // 모으던 소리를 끊는다 — 쏘는 순간 멎어야 "다 모아서 쐈다" 로 들린다
        StopChargeSound();

        Vector3 origin = Muzzle.position;
        Vector3 dir = context.AimDirection;

        ApplyBeamDamage(cfg, in stats, origin, dir);
        ShowBeam(cfg, origin, origin + dir * stats.Range);

        SpawnFx(cfg.muzzleFlashPrefab, origin, Quaternion.LookRotation(dir));

        // 다음 발을 위한 충전이 곧바로 시작된다.
        // 적이 있을 때만 쏘므로, 이 소리도 전투 중에만 난다.
        StartChargeSound(cfg, stats.FireInterval);
    }

    // ───────── 충전 소리 ─────────

    private AudioSource chargeSource;

    /// <summary>
    /// 충전음을 **끝에서부터 맞춰** 재생한다.
    ///
    /// 받아 온 클립(8초)이 충전 시간(2.2초)보다 훨씬 길다. 앞부분부터 틀면
    /// 한창 올라가는 중에 발사되어 "다 모았다" 는 느낌이 나지 않는다.
    /// 클립의 **마지막 `duration` 초**만 틀면 고조되는 지점이 발사 순간과 맞는다.
    /// </summary>
    void StartChargeSound(ChargeBeamWeaponData cfg, float duration)
    {
        if (cfg.chargeSound == null || duration <= 0.05f) return;

        EnsureChargeSource(cfg);

        if (chargeSource == null) return;

        chargeSource.clip = cfg.chargeSound;
        chargeSource.volume = cfg.chargeVolume;

        float offset = cfg.chargeSound.length - duration;

        // 클립이 충전 시간보다 짧으면 처음부터 (먼저 끝나도 어색하지 않다)
        chargeSource.time = Mathf.Max(0f, offset);
        chargeSource.Play();
    }

    void StopChargeSound()
    {
        if (chargeSource != null && chargeSource.isPlaying)
            chargeSource.Stop();
    }

    void EnsureChargeSource(ChargeBeamWeaponData cfg)
    {
        if (chargeSource != null) return;

        // 전용 AudioSource 를 둔다. SoundManager 풀은 재생 중에 끊을 수단이 없다.
        GameObject go = new GameObject("ChargeSound");
        go.transform.SetParent(transform, false);

        chargeSource = go.AddComponent<AudioSource>();
        chargeSource.playOnAwake = false;
        chargeSource.loop = false;
        chargeSource.spatialBlend = 0f;   // 플레이어 무기라 2D

        if (SoundManager.Instance != null)
            chargeSource.outputAudioMixerGroup = SoundManager.Instance.SfxGroup;
    }

    void ApplyBeamDamage(ChargeBeamWeaponData cfg, in WeaponRuntimeStats stats,
        Vector3 origin, Vector3 dir)
    {
        struck.Clear();

        int count = Physics.SphereCastNonAlloc(
            origin, cfg.beamRadius, dir, hits, stats.Range,
            cfg.hitLayers, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider col = hits[i].collider;

            if (col == null) continue;

            // Bullet 의 기존 규약과 맞춘다 — 태그로 적을 가린다
            if (!col.CompareTag("Enemy") && !col.CompareTag("Boss")) continue;

            IDamageable target = col.GetComponentInParent<IDamageable>();

            if (target == null) continue;

            Component component = target as Component;
            Transform root = component != null ? component.transform : col.transform;

            // 자식 히트박스가 여러 개인 적을 한 번만 때린다
            if (!struck.Add(root)) continue;

            // SphereCast 가 시작 지점에 이미 겹친 콜라이더를 반환하면 point 가 0 이 된다
            Vector3 point = hits[i].point;
            if (point == Vector3.zero) point = col.ClosestPoint(origin);

            bool isCritical = RollCritical(in stats);

            target.TakeDamage(new DamageInfo
            {
                damage = ApplyCritical(in stats, isCritical),
                isCritical = isCritical,
                hitPoint = point,
                hitDirection = dir,
                cameraShake = cfg.cameraShake,
            });

            SpawnFx(cfg.hitEffectPrefab, point, Quaternion.LookRotation(-dir));

            // 관통이라 한 프레임에 여러 명이 들어온다. 소리는 프레임 끝에 한 번만.
            if (SoundManager.Instance != null)
                SoundManager.Instance.ReportHit(isCritical);
        }
    }

    protected override void OnTick(float deltaTime)
    {
        ChargeBeamWeaponData cfg = Config;

        if (cfg == null) return;

        // 빔은 잠깐 보여 주고 끈다. 판정은 이미 발사 순간에 끝났다.
        if (beam != null && beam.enabled && Time.time >= beamHideAt)
            beam.enabled = false;

        UpdateChargeVisual(cfg);
    }

    /// <summary>
    /// 총구에 빛이 점점 커지는 표시.
    ///
    /// 이게 없으면 2초 넘게 아무 일도 일어나지 않아 고장 난 것처럼 보인다.
    /// </summary>
    void UpdateChargeVisual(ChargeBeamWeaponData cfg)
    {
        if (!IsActive || IsReloading)
        {
            if (charge != null) charge.enabled = false;
            return;
        }

        float interval = Stats.FireInterval;

        if (interval <= 0.01f) return;

        // 쿨다운이 줄어드는 만큼 충전이 찬다
        float progress = Mathf.Clamp01(1f - CooldownRemaining / interval);

        EnsureCharge(cfg);

        if (charge == null) return;

        charge.enabled = progress > 0.05f;

        if (!charge.enabled) return;

        Vector3 origin = Muzzle.position;
        Vector3 forward = Muzzle.forward;

        charge.SetPosition(0, origin);
        charge.SetPosition(1, origin + forward * 0.15f);

        // 다 차면 가장 굵고 진하다 — 언제 나갈지 예측할 수 있어야 한다
        float width = cfg.chargeWidth * progress;
        charge.startWidth = width;
        charge.endWidth = width;

        Color c = cfg.beamColor;
        c.a = progress;
        charge.startColor = c;
        charge.endColor = c;
    }

    void ShowBeam(ChargeBeamWeaponData cfg, Vector3 from, Vector3 to)
    {
        EnsureBeam(cfg);

        if (beam == null) return;

        beam.SetPosition(0, from);
        beam.SetPosition(1, to);
        beam.startWidth = cfg.beamWidth;
        beam.endWidth = cfg.beamWidth * 0.6f;
        beam.startColor = cfg.beamColor;
        beam.endColor = cfg.beamColor;
        beam.enabled = true;

        beamHideAt = Time.time + cfg.beamDuration;

        // 발사했으니 충전 표시는 즉시 지운다
        if (charge != null) charge.enabled = false;
    }

    // ───────── 연출 준비 ─────────

    void EnsureBeam(ChargeBeamWeaponData cfg)
    {
        if (beam != null) return;

        beam = CreateLine("Beam", cfg);
    }

    void EnsureCharge(ChargeBeamWeaponData cfg)
    {
        if (charge != null) return;

        charge = CreateLine("ChargeGlow", cfg);
    }

    LineRenderer CreateLine(string childName, ChargeBeamWeaponData cfg)
    {
        GameObject go = new GameObject(childName);
        go.transform.SetParent(transform, false);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;          // 무기가 회전해도 빔은 쏜 방향 그대로
        line.numCapVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;

        line.material = ResolveMaterial(cfg);

        return line;
    }

    Material ResolveMaterial(ChargeBeamWeaponData cfg)
    {
        if (cfg.beamMaterial != null) return cfg.beamMaterial;

        if (runtimeMaterial != null) return runtimeMaterial;

        // 데이터에 머티리얼이 없을 때의 폴백. 에디터 도구가 만들어 넣어 주는 것이 정상이다.
        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            Debug.LogWarning($"[ChargeBeamWeapon] 빔 머티리얼이 없습니다. ({name})", this);
            return null;
        }

        runtimeMaterial = new Material(shader);

        return runtimeMaterial;
    }

    void SpawnFx(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null || PoolManager.Instance == null) return;

        GameObject fx = PoolManager.Instance.Get(prefab);

        if (fx == null) return;

        fx.transform.SetPositionAndRotation(position, rotation);
    }

    public override void SetActive(bool active)
    {
        base.SetActive(active);

        // 스왑으로 손에서 빠지면 연출도 같이 꺼야 한다.
        // SetActive 가 자식을 전부 끄지만, 다시 들었을 때 빔이 남아 있으면 안 된다.
        if (active) return;

        if (beam != null) beam.enabled = false;
        if (charge != null) charge.enabled = false;

        // 손에 없는 무기가 계속 충전음을 내면 안 된다
        StopChargeSound();
    }

    protected override void OnReloadStarted(float duration)
    {
        // 장전 중에는 충전이 멈춘다
        StopChargeSound();
    }

    void OnDestroy()
    {
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }
}
