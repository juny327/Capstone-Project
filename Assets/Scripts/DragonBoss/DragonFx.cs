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
}
