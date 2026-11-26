using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealSpellsDemo
{
    /// <summary>
    /// Turns one <see cref="SpellLayer"/> into a configured Unity ParticleSystem (main, emission, shape, velocity,
    /// size, rotation, custom data for the HDR intensity and alpha curves, renderer with the flipbook vertex streams).
    /// The same code runs at run time (spells loaded from disk) and in the editor builder (prefabs), so both paths draw
    /// identically.
    /// </summary>
    public static class SpellParticleBuilder
    {
        /// <summary>Lifetime used for loop layers without an explicit life: they live until their phase ends.</summary>
        public const float UntilStopped = 100000f;

        static readonly List<ParticleSystemVertexStream> Streams = new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position,
            ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.AgePercent,
            ParticleSystemVertexStream.StableRandomX,
            ParticleSystemVertexStream.Custom1XY
        };

        /// <summary>Particle quad size per metre of visible size (the sheet's extent box fills the requested size).</summary>
        public static float QuadScale(SpellSheet sheet) { return sheet != null ? 1f / sheet.CoveredSize : 1f; }

        /// <summary>Renderer pivot (in particle sizes) that puts the sheet's pivot point on the particle position.</summary>
        public static Vector3 Pivot(SpellSheet sheet)
        {
            if (sheet == null) return Vector3.zero;
            return new Vector3(0.5f - sheet.pivot.x, 0.5f - sheet.pivot.y, 0f) * PivotSign;
        }

        /// <summary>Unity's renderer pivot direction (measured by the engine test RS|TEST|pivot).</summary>
        public static float PivotSign = 1f;

        static Mesh stripMesh, ringMesh;

        /// <summary>Beam strip: a unit quad in the XZ plane, x -0.5..0.5 (u across = v), z 0..1 along the beam (u).</summary>
        public static Mesh StripMesh
        {
            get
            {
                if (stripMesh != null) return stripMesh;
                stripMesh = new Mesh { name = "RealSpellsDemo Beam Strip", hideFlags = HideFlags.DontSave };
                stripMesh.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 0f, 1f), new Vector3(0.5f, 0f, 1f) };
                stripMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(1f, 1f) };
                stripMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                stripMesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
                stripMesh.RecalculateBounds();
                return stripMesh;
            }
        }

        /// <summary>The run-time mesh of a beam or ring layer (shared strip / band, or a tapered strip of its own).</summary>
        public static Mesh MeshFor(SpellLayer layer)
        {
            return layer.orient == SpellOrient.Ring ? RingMesh : layer.hasWidthCurve ? TaperedStrip(layer.widthCurve) : StripMesh;
        }

        static float CurvePeak(AnimationCurve c)
        {
            float m = 0.01f;
            for (int i = 0; i <= 32; i++) m = Mathf.Max(m, c.Evaluate(i / 32f));
            return m;
        }

        /// <summary>Beam strip whose width follows a curve along its length (0 = anchor, 1 = impact), normalised to the
        /// curve's peak (the particle's x size = layer size x peak).</summary>
        public static Mesh TaperedStrip(AnimationCurve width)
        {
            const int seg = 24;
            float peak = CurvePeak(width);
            var v = new Vector3[(seg + 1) * 2];
            var uv = new Vector2[v.Length];
            var n = new Vector3[v.Length];
            var tri = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float z = i / (float)seg;
                float hw = 0.5f * Mathf.Max(0f, width.Evaluate(z)) / peak;
                v[i * 2] = new Vector3(-hw, 0f, z);
                v[i * 2 + 1] = new Vector3(hw, 0f, z);
                uv[i * 2] = new Vector2(z, 0f);
                uv[i * 2 + 1] = new Vector2(z, 1f);
                n[i * 2] = n[i * 2 + 1] = Vector3.up;
                if (i == seg) break;
                int t = i * 6, b = i * 2;
                tri[t] = b; tri[t + 1] = b + 2; tri[t + 2] = b + 1;
                tri[t + 3] = b + 1; tri[t + 4] = b + 2; tri[t + 5] = b + 3;
            }
            var m = new Mesh { name = "RealSpellsDemo Beam Strip (tapered)", hideFlags = HideFlags.DontSave, vertices = v, uv = uv, normals = n, triangles = tri };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Ring band: an open cylinder of radius 0.5 and height 1 standing on y = 0; u runs around, v up.</summary>
        public static Mesh RingMesh
        {
            get
            {
                if (ringMesh != null) return ringMesh;
                const int seg = 64;
                var v = new Vector3[(seg + 1) * 2];
                var uv = new Vector2[v.Length];
                var n = new Vector3[v.Length];
                var tri = new int[seg * 6];
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    v[i * 2] = d * 0.5f;
                    v[i * 2 + 1] = d * 0.5f + Vector3.up;
                    uv[i * 2] = new Vector2(i / (float)seg, 0f);
                    uv[i * 2 + 1] = new Vector2(i / (float)seg, 1f);
                    n[i * 2] = n[i * 2 + 1] = d;
                    if (i == seg) break;
                    int t = i * 6, b = i * 2;
                    tri[t] = b; tri[t + 1] = b + 1; tri[t + 2] = b + 2;
                    tri[t + 3] = b + 1; tri[t + 4] = b + 3; tri[t + 5] = b + 2;
                }
                ringMesh = new Mesh { name = "RealSpellsDemo Ring Band", hideFlags = HideFlags.DontSave, vertices = v, uv = uv, normals = n, triangles = tri };
                ringMesh.RecalculateBounds();
                return ringMesh;
            }
        }

        public static ParticleSystem Configure(GameObject go, SpellLayer layer, SpellSheet sheet, uint seed)
        {
            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null) ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed = false;
            ps.randomSeed = seed;

            bool loopLayer = layer.playback == SpellPlayback.Loop || layer.playback == SpellPlayback.Segments;
            bool strip = layer.orient == SpellOrient.Beam || layer.orient == SpellOrient.Ring;
            float quad = strip ? 1f : QuadScale(sheet);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startDelay = layer.spawn.delay;
            main.simulationSpace = layer.space == SpellSpace.Local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startLifetime = loopLayer && !layer.hasLife ? new ParticleSystem.MinMaxCurve(UntilStopped) : layer.life.ToCurve();
            main.startSize = layer.size.ToCurve(quad);
            main.startRotation = layer.rotation.ToCurve(Mathf.Deg2Rad);
            main.startColor = Color.white;
            main.gravityModifier = layer.velocity.enabled ? layer.velocity.gravity : 0f;
            main.startSpeed = layer.velocity.enabled ? layer.velocity.speed.ToCurve() : new ParticleSystem.MinMaxCurve(0f);
            main.stopAction = ParticleSystemStopAction.None;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new ParticleSystem.Burst[0]);
            int maxParticles = 64;
            float lifeMax = loopLayer && !layer.hasLife ? 1f : Mathf.Max(0.05f, layer.life.max);
            switch (layer.spawn.mode)
            {
                case SpellSpawnMode.Single:
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1) });
                    maxParticles = 4;
                    break;
                case SpellSpawnMode.Burst:
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, layer.spawn.count.ToCurve()) });
                    maxParticles = Mathf.CeilToInt(layer.spawn.count.max) + 4;
                    break;
                case SpellSpawnMode.Rate:
                    emission.rateOverTime = layer.spawn.rate;
                    if (layer.spawn.duration > 0f) main.duration = layer.spawn.duration; else main.loop = true;
                    maxParticles = Mathf.CeilToInt(layer.spawn.rate * lifeMax * 1.3f) + 8;
                    break;
                case SpellSpawnMode.Beam:
                case SpellSpawnMode.Ring:
                    // emitted by SpellPlayer (along the beam / on the growing ring)
                    main.loop = true;
                    maxParticles = Mathf.CeilToInt(layer.spawn.rate * lifeMax * 1.3f) + 8;
                    break;
                case SpellSpawnMode.Stream:
                    // emitted by SpellPlayer at the anchor, flying to the impact point (up to 40 m of stream alive at once)
                    main.loop = true;
                    maxParticles = Mathf.CeilToInt(layer.spawn.rate * 40f / Mathf.Max(0.5f, layer.spawn.speed) * 1.3f) + 16;
                    break;
                case SpellSpawnMode.Front:
                    // emitted by SpellPlayer on the current wave front: density x circumference cards are alive at once
                    main.loop = true;
                    maxParticles = Mathf.CeilToInt(layer.spawn.density * 2f * Mathf.PI * Mathf.Max(0.1f, layer.spawn.radius) * 1.5f) + 16;
                    break;
                case SpellSpawnMode.Trail:
                    // emitted by SpellPlayer.EmitTrails (jittered spacing, lateral offset, size jitter, drift)
                    main.loop = true;
                    maxParticles = Mathf.CeilToInt(40f / Mathf.Max(0.01f, layer.spawn.spacing) * lifeMax) + 16;
                    break;
            }
            main.maxParticles = Mathf.Clamp(maxParticles, 1, 4000);

            var shape = ps.shape;
            SpellVelocity v = layer.velocity;
            float radius = Mathf.Max(0.0001f, layer.spawn.radius);
            if (v.enabled && v.directionMode == SpellDirection.Vector)
            {
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = Mathf.Clamp(v.spread * 0.5f, 0f, 90f);
                shape.radius = radius;
                shape.radiusThickness = 1f;
                shape.arc = 360f;
                shape.rotation = Quaternion.FromToRotation(Vector3.forward, v.direction.sqrMagnitude > 0f ? v.direction.normalized : Vector3.up).eulerAngles;
                shape.position = Vector3.zero;
            }
            else if ((v.enabled && v.speed.max > 0f) || layer.spawn.radius > 0f)
            {
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = radius;
                shape.radiusThickness = 1f;
                shape.rotation = Vector3.zero;
                shape.position = Vector3.zero;
            }
            else shape.enabled = false;

            var inherit = ps.inheritVelocity;
            inherit.enabled = v.enabled && v.inherit != 0f && layer.space == SpellSpace.World && layer.spawn.mode != SpellSpawnMode.Trail;
            if (inherit.enabled)
            {
                inherit.mode = ParticleSystemInheritVelocityMode.Initial;
                inherit.curve = new ParticleSystem.MinMaxCurve(v.inherit);
            }

            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = v.enabled && v.drag > 0f;
            if (limit.enabled)
            {
                limit.limit = new ParticleSystem.MinMaxCurve(1000f);
                limit.dampen = 0f;
                limit.drag = new ParticleSystem.MinMaxCurve(v.drag);
                limit.multiplyDragByParticleSize = false;
                limit.multiplyDragByParticleVelocity = false;
            }

            var sol = ps.sizeOverLifetime;
            sol.enabled = !loopLayer;
            sol.separateAxes = false;
            if (sol.enabled) sol.size = new ParticleSystem.MinMaxCurve(1f, layer.sizeCurve);
            if (strip)
            {
                // beams: x = width (the player sets the length per frame); rings: x = z = diameter growing by sizeCurve,
                // y = band height; both turn in 3D (rings: a random turn around the up axis from 'rotation')
                main.startSize3D = true;
                main.startSizeX = layer.orient == SpellOrient.Beam && layer.hasWidthCurve ? layer.size.ToCurve(CurvePeak(layer.widthCurve)) : layer.size.ToCurve();
                main.startSizeY = layer.orient == SpellOrient.Ring ? new ParticleSystem.MinMaxCurve(layer.ringHeight) : new ParticleSystem.MinMaxCurve(1f);
                main.startSizeZ = layer.orient == SpellOrient.Ring ? layer.size.ToCurve() : new ParticleSystem.MinMaxCurve(1f);
                main.startRotation3D = true;
                main.startRotationX = 0f;
                main.startRotationY = layer.orient == SpellOrient.Ring ? layer.rotation.ToCurve(Mathf.Deg2Rad) : new ParticleSystem.MinMaxCurve(0f);
                main.startRotationZ = 0f;
                if (sol.enabled)
                {
                    sol.separateAxes = true;
                    sol.x = new ParticleSystem.MinMaxCurve(1f, layer.sizeCurve);
                    sol.y = new ParticleSystem.MinMaxCurve(1f);
                    sol.z = new ParticleSystem.MinMaxCurve(1f, layer.sizeCurve);
                }
            }
            else if (layer.orient == SpellOrient.Billboard && Mathf.Abs(layer.aspect - 1f) > 1e-3f)
            {
                // 'aspect' = width / height: the wider side keeps 'size', the other shrinks (flat fog: aspect 2-3 with
                // rotation 0, since a rotated quad carries its squash with it)
                float sx = layer.aspect >= 1f ? 1f : layer.aspect, sy = layer.aspect >= 1f ? 1f / layer.aspect : 1f;
                main.startSize3D = true;
                main.startSizeX = layer.size.ToCurve(quad * sx);
                main.startSizeY = layer.size.ToCurve(quad * sy);
                main.startSizeZ = layer.size.ToCurve(quad);
                main.startRotation3D = false;
            }
            else
            {
                main.startSize3D = false;
                main.startRotation3D = false;
            }

            var rol = ps.rotationOverLifetime;
            rol.enabled = !strip && (layer.spin.min != 0f || layer.spin.max != 0f);
            if (rol.enabled) rol.z = layer.spin.ToCurve(Mathf.Deg2Rad);

            var custom = ps.customData;
            custom.enabled = true;
            custom.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
            custom.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 2);
            if (loopLayer)
            {
                // loop layers: intensity/alpha curves run on seconds since start, driven per frame by SpellPlayer
                custom.SetVector(ParticleSystemCustomData.Custom1, 0, new ParticleSystem.MinMaxCurve(1f));
                custom.SetVector(ParticleSystemCustomData.Custom1, 1, new ParticleSystem.MinMaxCurve(1f));
            }
            else
            {
                custom.SetVector(ParticleSystemCustomData.Custom1, 0, new ParticleSystem.MinMaxCurve(1f, layer.intensity));
                custom.SetVector(ParticleSystemCustomData.Custom1, 1, new ParticleSystem.MinMaxCurve(1f, layer.alpha));
            }

            var r = go.GetComponent<ParticleSystemRenderer>();
            switch (layer.orient)
            {
                case SpellOrient.Ground: r.renderMode = ParticleSystemRenderMode.HorizontalBillboard; break;
                case SpellOrient.Velocity:
                    r.renderMode = ParticleSystemRenderMode.Stretch;
                    r.velocityScale = layer.stretch;
                    r.lengthScale = 1f;
                    r.cameraVelocityScale = 0f;
                    break;
                case SpellOrient.Beam:
                case SpellOrient.Ring:
                    r.renderMode = ParticleSystemRenderMode.Mesh;
                    r.mesh = layer.mesh != null ? layer.mesh : MeshFor(layer);
                    break;
                default: r.renderMode = ParticleSystemRenderMode.Billboard; break;
            }
            r.alignment = strip ? ParticleSystemRenderSpace.World : ParticleSystemRenderSpace.View;
            r.sharedMaterial = layer.material;
            r.minParticleSize = 0f;
            r.maxParticleSize = 100f;
            r.pivot = layer.orient == SpellOrient.Velocity || strip ? Vector3.zero : Pivot(sheet);
            r.flip = strip ? Vector3.zero : layer.mirror ? new Vector3(1f, 0f, 0f) : new Vector3(layer.spawn.flip, layer.spawn.flip, 0f);
            // stretched billboards: Unity lays the texture's -u along the velocity, so a sheet drawn flying to +u
            // (sheet.json forward [1, 0]) is mirrored along the stretch axis and one drawn to -u is not; spawn.flip no
            // longer decides that axis (recipes that mirrored by hand with flip 1 keep their look: u mirrored, v as before)
            if (layer.orient == SpellOrient.Velocity && sheet != null && sheet.hasForward && Mathf.Abs(sheet.forward.x) > 0.5f)
                r.flip = new Vector3(sheet.forward.x > 0f ? 1f : 0f, layer.spawn.flip, 0f);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.sortingFudge = 0f;
            r.allowRoll = true;
            r.SetActiveVertexStreams(Streams);
            return ps;
        }
    }
}
