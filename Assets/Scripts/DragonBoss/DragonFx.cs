using UnityEngine;

/// <summary>
/// 드래곤 보스 이펙트 (2026-11-13) — 파티클을 코드로 만든다 (재질만 프리팹에서 받는다).
///  · 브레스: 입에서 앞 · 아래로 퍼지는 불 (부채꼴 예고와 같은 범위까지 닿게 속도 · 수명을 맞춘다)
///  · 먼지: 돌진할 때 발밑에서 뒤로 남는 흙먼지
/// </summary>
public static class DragonFx
{
    /// <summary>불 브레스 — reach(m) 까지 닿고 ±halfAngle 로 퍼진다. 위치 · 방향은 매 프레임 입에 맞춰 옮긴다.</summary>
    public static ParticleSystem Breath(Transform parent, Material fire, float reach, float halfAngle)
    {
        var go = new GameObject("BreathFx");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        const float life = 0.62f;
        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.85f, life);
        // 끝에선 거의 투명해지므로 보이는 길이가 예고 끝에 닿게 조금 더 빨리
        main.startSpeed = new ParticleSystem.MinMaxCurve(reach / life * 1.15f, reach / life * 1.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.45f), new Color(1f, 0.55f, 0.15f));
        main.maxParticles = 600;
        main.gravityModifier = -0.15f;   // 살짝 떠오른다

        var emission = ps.emission;
        emission.rateOverTime = 260f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = halfAngle * 0.8f;   // 가장자리 입자는 예고 끝보다 조금 안쪽
        shape.radius = 0.35f;
        shape.scale = new Vector3(1f, 0.35f, 1f);   // 위아래로 납작하게 — 바닥 부채꼴처럼 옆으로 퍼진다

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.5f, 1.8f), new Keyframe(1f, 2.4f)));

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.12f), 0.35f),
                    new GradientColorKey(new Color(0.85f, 0.18f, 0.05f), 0.75f), new GradientColorKey(new Color(0.25f, 0.08f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.08f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.8f;
        noise.frequency = 0.9f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = fire;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingFudge = -10f;
        return ps;
    }

    /// <summary>돌진 먼지 — 몸을 따라가며 뿌리면 월드에 남는다.</summary>
    public static ParticleSystem Dust(Transform parent, Material smoke, float width)
    {
        var go = new GameObject("DustFx");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.5f, 0.48f, 0.55f), new Color(0.35f, 0.32f, 0.32f, 0.45f));
        main.maxParticles = 300;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 4f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width, 0.2f, 1.5f);

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.8f)));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = smoke;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    // ───────── 2 · 3페이즈 (2026-11-14) ─────────

    static Gradient FireGradient(float peakAlpha)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.12f), 0.4f),
                    new GradientColorKey(new Color(0.8f, 0.16f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.12f), new GradientAlphaKey(peakAlpha * 0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }

    static ParticleSystemRenderer FireRenderer(GameObject go, Material fire)
    {
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = fire;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    /// <summary>불 장판 위로 피어오르는 불꽃 — 반지름 radius 원 안에서 계속 올라온다. 만들자마자 켜진다.</summary>
    public static ParticleSystem Flames(Transform parent, Material fire, float radius)
    {
        var go = new GameObject("FlamesFx");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.45f), new Color(1f, 0.5f, 0.15f));
        main.maxParticles = 160;

        var emission = ps.emission;
        emission.rateOverTime = Mathf.Clamp(radius * radius * 9f, 18f, 70f);

        // 원판에서 위로 — 원뿔 각도 0 이면 밑면(반지름 radius) 전체에서 곧게 올라온다
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 4f;
        shape.radius = radius * 0.9f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.4f, 1.2f), new Keyframe(1f, 0.5f)));

        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = FireGradient(0.85f);

        FireRenderer(go, fire);
        ps.Play(true);
        return ps;
    }

    /// <summary>한 번 터지는 불 — 불비가 떨어진 자리 · 부하가 나오는 자리. 스스로 사라진다.</summary>
    public static void Burst(Vector3 at, Material fire, float radius)
    {
        if (fire == null) return;
        var go = new GameObject("BurstFx");
        go.transform.position = at;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 1.4f, radius * 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.7f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.55f), new Color(1f, 0.5f, 0.15f));
        main.gravityModifier = -0.1f;
        main.maxParticles = 60;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 34) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.3f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = 0.2f;
        limit.limit = radius * 2f;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(1f, 1.9f)));

        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = FireGradient(0.95f);

        FireRenderer(go, fire);
        ps.Play(true);
    }

    /// <summary>떨어지는 불덩이 꼬리 — 움직이는 물체에 달면 지나간 길에 불이 남는다.</summary>
    public static ParticleSystem MeteorTrail(Transform parent, Material fire)
    {
        var go = new GameObject("MeteorFx");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.6f), new Color(1f, 0.55f, 0.15f));
        main.maxParticles = 120;

        var emission = ps.emission;
        emission.rateOverTime = 40f;
        emission.rateOverDistance = 3f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));

        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = FireGradient(0.95f);

        FireRenderer(go, fire);
        return ps;
    }
}
