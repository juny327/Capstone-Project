using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 바닥에 깔려 수명 동안 주기적으로 데미지를 주는 장판 (11번 문서 B2).
/// 나노 산성 장판이 쓰고, 나중에 중력 특이점 같은 지속 영역에도 재사용한다.
///
/// 풀링 대상이다. 보스의 StunZone 은 Destroy 를 쓰지만, 장판은 몇 초마다 반복해서 깔리므로
/// 풀에서 꺼내 쓰고 수명이 끝나면 돌려보낸다.
///
/// 연출(자식 파티클)은 루프로 켜 두고 이 컴포넌트가 수명을 관리한다.
/// ⚠ 자식 파티클에 EffectAutoReturn 을 붙이면 안 된다 — 루프 파티클은 스스로 멈추지 않아
///    OnParticleSystemStopped 가 오지 않고, 풀 개체가 계속 늘어난다 (11번 3-3).
/// </summary>
[RequireComponent(typeof(Poolable))]
public class DamageZone : MonoBehaviour, IPoolable
{
    [Header("Hit")]
    [Tooltip("판정 레이어. 검(WD_Sword)과 같은 Default + Enemy 로 둔다")]
    [SerializeField] private LayerMask hitLayers = ~0;

    [Tooltip("틱마다 흔들 세기. 항상 도는 서브유닛은 0 을 권장")]
    [SerializeField] private float cameraShake = 0f;

    [Header("Visual")]
    [Tooltip("자식 연출이 스케일 1 일 때 덮는 반지름(m). 판정 반경에 맞춰 크기를 조절하는 기준값")]
    [Min(0.01f)] [SerializeField] private float fxBaseRadius = 1f;

    [Tooltip("수명이 끝나기 이 시간 전에 파티클 방출을 멈춰 서서히 사라지게 한다")]
    [Min(0f)] [SerializeField] private float fadeOutTime = 0.5f;

    private readonly List<ParticleSystem> particles = new List<ParticleSystem>();
    private Poolable poolable;

    private float radius;
    private float damage;
    private float critChance;
    private float critMultiplier;
    private float tickInterval;

    private float endTime;
    private float nextTickTime;
    private bool running;
    private bool fading;

    /// <summary>판정 반경(m). 무기가 Init 으로 정한다.</summary>
    public float Radius => radius;

    void Awake()
    {
        poolable = GetComponent<Poolable>();
        GetComponentsInChildren(true, particles);
    }

    /// <summary>
    /// 무기가 장판을 꺼낸 직후 호출한다. 첫 틱은 즉시 들어간다.
    /// 크리티컬 값까지 받아 두는 이유: 장판은 무기보다 오래 살아남으므로
    /// 생성 시점의 스탯을 복사해 두고 쓴다.
    /// </summary>
    public void Init(Vector3 position, float radius, float duration, float tickInterval,
                     float damage, float critChance, float critMultiplier)
    {
        transform.position = position;

        this.radius = Mathf.Max(0.01f, radius);
        this.damage = damage;
        this.critChance = critChance;
        this.critMultiplier = critMultiplier;
        this.tickInterval = Mathf.Max(0.05f, tickInterval);

        // 보이는 크기를 판정 반경에 맞춘다
        float scale = this.radius / Mathf.Max(0.01f, fxBaseRadius);
        transform.localScale = new Vector3(scale, scale, scale);

        endTime = Time.time + Mathf.Max(0.1f, duration);
        nextTickTime = Time.time;
        fading = false;
        running = true;

        PlayParticles();
    }

    void Update()
    {
        if (!running) return;

        if (Time.time >= nextTickTime)
        {
            AreaDamage.Apply(
                transform.position, radius,
                damage, critChance, critMultiplier,
                hitLayers, 0, null, 0f, cameraShake);

            nextTickTime += tickInterval;

            // 프레임이 크게 밀렸을 때 틱이 몰아서 들어가지 않게 한다
            if (nextTickTime < Time.time)
                nextTickTime = Time.time + tickInterval;
        }

        if (!fading && fadeOutTime > 0f && Time.time >= endTime - fadeOutTime)
        {
            fading = true;
            StopParticleEmission();
        }

        if (Time.time >= endTime)
        {
            running = false;
            poolable.ReturnToPool();
        }
    }

    public void OnSpawn()
    {
        // Init 을 받기 전에는 판정하지 않는다 (꺼낸 프레임에 Update 가 먼저 돌 수 있다)
        running = false;
        fading = false;
    }

    public void OnDespawn()
    {
        running = false;
        StopParticles();
    }

    void PlayParticles()
    {
        for (int i = 0; i < particles.Count; i++)
        {
            if (particles[i] == null) continue;

            particles[i].Clear(true);
            particles[i].Play(true);
        }
    }

    /// <summary>방출만 멈춘다. 이미 떠 있는 입자는 수명대로 사라진다.</summary>
    void StopParticleEmission()
    {
        for (int i = 0; i < particles.Count; i++)
        {
            if (particles[i] == null) continue;

            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    void StopParticles()
    {
        for (int i = 0; i < particles.Count; i++)
        {
            if (particles[i] == null) continue;

            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius > 0f ? radius : fxBaseRadius);
    }
}
