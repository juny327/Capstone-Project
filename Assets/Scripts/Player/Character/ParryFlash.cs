using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 패링 연출 — 금속끼리 부딪혀 불똥이 튀는 느낌 (세키로 · 몬스터 헌터의 튕겨내기처럼).
///
///  · 번쩍임 : 십자 광채(Glint) + 부드러운 섬광(Flash) + 주황 조명 한 번
///  · 불똥   : 길게 늘어진 불꽃이 부채꼴로 튀고, 칼면을 따라 잠깐 더 뿜어진다(마찰). 중력으로 떨어져 바닥에서 튄다
///  · 잔불   : 작은 불씨가 조금 더 오래 흩날리다 꺼진다
///  · 고리   : 성공을 알리는 짧은 충격파 (본 패링에만)
///
/// 파티클 설정(색 · 크기 · 중력 · 바닥 충돌)은 CharacterSetup 이 만든 프리팹(Prefabs/Effect/Parry/ParryFlash)에 있고,
/// 여기서는 어디서 어느 방향으로 몇 개를 뿜을지만 정한다. 패링은 5초에 한 번이라 풀링하지 않고
/// ParryController 가 하나를 만들어 두고 다시 쓴다. 파티클은 월드 공간이라 플레이어가 달려도 제자리에 남는다.
/// </summary>
public class ParryFlash : MonoBehaviour
{
    [SerializeField] private ParticleSystem flash;
    [SerializeField] private ParticleSystem glint;
    [SerializeField] private ParticleSystem ring;
    [SerializeField] private ParticleSystem sparks;
    [SerializeField] private ParticleSystem embers;
    [SerializeField] private Light flashLight;

    [Tooltip("불똥이 떨어져 튀는 바닥면 — 불꽃 파티클의 충돌 평면. 막을 때마다 발 높이로 옮긴다")]
    [SerializeField] private Transform floor;

    [Header("Parry — 막은 순간 (몸 앞)")]
    [Min(0)] [SerializeField] private int sparkBurst = 34;
    [SerializeField] private Vector2 sparkSpeed = new Vector2(6f, 15f);

    [Tooltip("칼면을 따라 이어서 뿜는 마찰 불똥 — 초당 수")]
    [Min(0f)] [SerializeField] private float scrapeRate = 240f;
    [Min(0f)] [SerializeField] private float scrapeDuration = 0.13f;
    [SerializeField] private Vector2 scrapeSpeed = new Vector2(9f, 19f);

    [Min(0)] [SerializeField] private int emberBurst = 14;

    [Header("Clash — 밀어낸 적마다 (맞닿은 자리)")]
    [Min(0)] [SerializeField] private int clashSparks = 16;
    [Min(0f)] [SerializeField] private float clashScrapeDuration = 0.08f;

    [Header("Deflect — 쳐낸 투사체마다")]
    [Min(0)] [SerializeField] private int deflectSparks = 16;

    [Tooltip("칼면을 긁고 지나가는 방향으로 뿜는 불똥")]
    [Min(0)] [SerializeField] private int deflectScrapeSparks = 8;

    [Header("Light")]
    [Min(0f)] [SerializeField] private float lightIntensity = 6f;
    [Min(0.01f)] [SerializeField] private float lightDuration = 0.16f;

    struct Scrape
    {
        public Vector3 position;
        public Vector3 forward;
        public Vector3 tangent;
        public float timeLeft;
        public float carry;
    }

    private readonly List<Scrape> scrapes = new List<Scrape>();
    private float lightTimer;

    /// <summary>막은 자리(몸 앞)에서 크게 터뜨린다. facing 쪽으로 불똥이 튀고, 칼면(facing 에 수직)을 따라 마찰 불똥이 뿜어진다.</summary>
    public void Play(Vector3 position, Vector3 facing, float floorHeight)
    {
        Vector3 forward = Flat(facing);
        Vector3 tangent = Vector3.Cross(Vector3.up, forward);

        transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));

        if (floor != null)
            floor.SetPositionAndRotation(new Vector3(position.x, floorHeight, position.z), Quaternion.identity);

        EmitAt(flash, position, 1f);
        EmitAt(glint, position, 1f);
        EmitAt(ring, position, 1f);

        Spray(position, forward + Vector3.up * 0.3f, 55f, sparkBurst, sparkSpeed, 0.12f);
        StartScrape(position, forward, tangent, scrapeDuration);
        Embers(position, emberBurst);

        FlashLight(position);
    }

    /// <summary>밀어낸 적과 맞닿은 자리에서 작게 부딪힌다. direction = 적 쪽.</summary>
    public void Clash(Vector3 position, Vector3 direction)
    {
        Vector3 forward = Flat(direction);

        EmitAt(flash, position, 0.6f);
        EmitAt(glint, position, 0.7f);

        Spray(position, forward + Vector3.up * 0.3f, 45f, clashSparks, sparkSpeed, 0.08f);
        StartScrape(position, forward, Vector3.Cross(Vector3.up, forward), clashScrapeDuration);
        Embers(position, emberBurst / 3);
    }

    /// <summary>
    /// 쳐낸 투사체 자리에서 튄다. outgoing = 튕겨 나가는 방향, along = 칼면을 따라 긁고 지나가는 방향(없으면 0).
    /// </summary>
    public void Deflect(Vector3 position, Vector3 outgoing, Vector3 along)
    {
        EmitAt(flash, position, 0.45f);
        EmitAt(glint, position, 0.6f);

        Spray(position, Flat(outgoing) + Vector3.up * 0.2f, 28f, deflectSparks, sparkSpeed, 0.05f);

        along.y = 0f;
        if (along.sqrMagnitude > 0.01f)
            Spray(position, along.normalized + Vector3.up * 0.15f, 15f, deflectScrapeSparks, scrapeSpeed, 0.05f);

        Embers(position, 3);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        for (int i = scrapes.Count - 1; i >= 0; i--)
        {
            Scrape s = scrapes[i];

            s.carry += scrapeRate * Mathf.Min(dt, s.timeLeft);
            int count = (int)s.carry;
            s.carry -= count;
            EmitScrape(s, count);

            s.timeLeft -= dt;
            if (s.timeLeft <= 0f) scrapes.RemoveAt(i);
            else scrapes[i] = s;
        }

        if (lightTimer > 0f && flashLight != null)
        {
            lightTimer -= dt;
            float t = Mathf.Clamp01(lightTimer / lightDuration);
            flashLight.intensity = lightIntensity * t * t;

            if (lightTimer <= 0f) flashLight.enabled = false;
        }
    }

    // ───────── 뿜기 ─────────

    static void EmitAt(ParticleSystem ps, Vector3 position, float sizeScale)
    {
        if (ps == null) return;

        var p = new ParticleSystem.EmitParams
        {
            position = position,
            applyShapeToPosition = false,
            startSize = ps.main.startSize.constant * sizeScale,
        };
        ps.Emit(p, 1);
    }

    /// <summary>direction 을 축으로 한 원뿔(반각 spread) 안으로 불똥을 뿜는다.</summary>
    void Spray(Vector3 position, Vector3 direction, float spread, int count, Vector2 speed, float jitter)
    {
        if (sparks == null || count <= 0) return;

        Quaternion basis = Quaternion.LookRotation(direction.normalized);
        var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };

        for (int i = 0; i < count; i++)
        {
            p.position = position + Random.insideUnitSphere * jitter;
            p.velocity = basis * InCone(spread) * Random.Range(speed.x, speed.y);
            sparks.Emit(p, 1);
        }
    }

    // 칼면을 따라 좌우로 긁히며 튀는 불똥. 한쪽으로 쏠리게(70%) 해서 칼이 미끄러지는 느낌을 준다
    void StartScrape(Vector3 position, Vector3 forward, Vector3 tangent, float duration)
    {
        if (duration <= 0f || scrapeRate <= 0f) return;

        if (Random.value < 0.5f) tangent = -tangent;

        var s = new Scrape { position = position, forward = forward, tangent = tangent, timeLeft = duration };
        EmitScrape(s, Mathf.CeilToInt(scrapeRate * 0.03f));   // 첫 프레임부터 보이게 (히트 스톱으로 시간이 거의 멈춰 있다)
        scrapes.Add(s);
    }

    void EmitScrape(Scrape s, int count)
    {
        if (sparks == null || count <= 0) return;

        var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };

        for (int i = 0; i < count; i++)
        {
            float side = Random.value < 0.7f ? 1f : -1f;
            Vector3 along = s.tangent * side;
            Vector3 direction = (along * 0.85f + s.forward * 0.45f + Vector3.up * 0.3f).normalized;

            p.position = s.position + along * Random.Range(0f, 0.35f) + Random.insideUnitSphere * 0.05f;
            p.velocity = Quaternion.LookRotation(direction) * InCone(18f) * Random.Range(scrapeSpeed.x, scrapeSpeed.y);
            sparks.Emit(p, 1);
        }
    }

    void Embers(Vector3 position, int count)
    {
        if (embers == null || count <= 0) return;

        var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };

        for (int i = 0; i < count; i++)
        {
            Vector3 d = Random.onUnitSphere;
            d.y = Mathf.Abs(d.y) * 0.8f + 0.2f;

            p.position = position + Random.insideUnitSphere * 0.2f;
            p.velocity = d.normalized * Random.Range(1f, 3.5f);
            embers.Emit(p, 1);
        }
    }

    void FlashLight(Vector3 position)
    {
        if (flashLight == null || lightIntensity <= 0f) return;

        flashLight.transform.position = position;
        flashLight.intensity = lightIntensity;
        flashLight.enabled = true;
        lightTimer = lightDuration;
    }

    // ───────── 도움 ─────────

    static Vector3 InCone(float halfAngle)
    {
        Quaternion tilt = Quaternion.AngleAxis(Random.Range(0f, halfAngle), Vector3.right);
        Quaternion roll = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);
        return roll * (tilt * Vector3.forward);
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }
}
