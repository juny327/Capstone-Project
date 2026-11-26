#ifndef REALSPELLS_FLIPBOOK_INCLUDED
#define REALSPELLS_FLIPBOOK_INCLUDED

// Real Spells flipbook particle (URP). Sheet format: docs/sheet-schema.md (realspells.sheet/1).
// Vertex streams (set by SpellParticleBuilder): POSITION, COLOR, TEXCOORD0 = (uv.x, uv.y, agePercent, stableRandom),
// TEXCOORD1 = (custom1.x intensity over life, custom1.y alpha over life).
// Blend One OneMinusSrcAlpha for every mode: RGB is premultiplied here, the output alpha is A * _Occlusion
// (alpha: 1, additive: 0, glow: k), which gives the three equations of the sheet format.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_MotionTex);
SAMPLER(sampler_MotionTex);
TEXTURE2D(_FrameLut);
SAMPLER(sampler_FrameLut);
SAMPLER(rs_point_clamp_sampler);

// dev diagnostics (global, 0 in normal use): 1 = solid magenta quads, 2 = soft particles off, 3 = show alpha only
float _RsDebug;

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_TexelSize;
    float4 _Grid;        // cols, rows, frameCount, loop (0/1)
    float4 _Cell;        // gutter/cell, content/cell, content px, mipMax
    float4 _Motion;      // scale, strength, enabled (0/1), step (0/1: frames cut hard, no blend)
    half4 _Tint;
    float _Intensity;
    float _Occlusion;
    float _Softness;
    float _Lit;
    float _LoopRate;
    float _NearFade;
    float _Fade;
    float _LayerIntensity;
    float _ManualTime;   // >= 0: normalized schedule time set per renderer (segment playback), else age / loop
    float _StartOffset;  // one-shot: max random start into the schedule (normalized), the end stays at 1
    float _DepthOffset;  // metres the quad is pulled toward the camera along the view ray (same pixels, nearer depth)
    float _AgeEase;      // exponent on the one-shot schedule time (> 1: slow gather accelerating into the release)
    float4 _Strip;       // beam / ring meshes: x = repeats of the sheet along u (0 = off), y = scroll offset, z = end fade share
CBUFFER_END

struct RsAttributes
{
    float4 positionOS : POSITION;
    half4 color : COLOR;
    float4 uvAgeRand : TEXCOORD0;
    float4 custom1 : TEXCOORD1;
};

struct RsVaryings
{
    float4 positionCS : SV_POSITION;
    half4 color : COLOR;
    float4 uvAgeRand : TEXCOORD0;
    float4 custom1 : TEXCOORD1;
    float4 screenPos : TEXCOORD2;
    float3 positionWS : TEXCOORD3;
};

RsVaryings RsVert(RsAttributes v)
{
    RsVaryings o;
    float3 ws = TransformObjectToWorld(v.positionOS.xyz);
    // pull toward the camera along the view ray: the sprite covers the same pixels but sits in front of the body it
    // explodes on, and the soft depth fade then melts it into that body instead of the body cutting it with a hard line
    float3 toCam = unity_OrthoParams.w > 0.5 ? -GetViewForwardDir() : GetCameraPositionWS() - ws;
    float camDist = max(length(toCam), 1e-4);
    ws += toCam / camDist * min(_DepthOffset, camDist * 0.5);
    o.positionCS = TransformWorldToHClip(ws);
    o.positionWS = ws;
    o.color = v.color;
    o.uvAgeRand = v.uvAgeRand;
    o.custom1 = v.custom1;
    o.screenPos = ComputeScreenPos(o.positionCS);
    return o;
}

// Fractional frame index from the schedule lookup (time -> frame), one-shot by age, loop by time.
float RsFrameIndex(float age, float rand)
{
    // single exit (FXC reports early returns as "potentially uninitialized")
    bool loop = _Grid.w > 0.5;
    float so = frac(rand * 43.758 + 0.31) * _StartOffset;
    float ageE = pow(saturate(age), max(_AgeEase, 0.05));
    float u = loop ? frac(_Time.y * _LoopRate + rand) : saturate(so + ageE * (1.0 - so));
    u = _ManualTime >= 0.0 ? saturate(_ManualTime) : u;
    float x = loop ? (u * 512.0 + 0.5) / 512.0 : (u * 511.0 + 0.5) / 512.0;
    return SAMPLE_TEXTURE2D_LOD(_FrameLut, sampler_FrameLut, float2(x, 0.5), 0).r;
}

// Content UV (0..1 inside the gutter-free content rect, v up) of frame i -> sheet UV.
float2 RsCellUV(float i, float2 c)
{
    float cols = _Grid.x;
    float rows = _Grid.y;
    float col = fmod(i, cols);
    float row = floor(i / cols);
    float2 inCell = _Cell.x + c * _Cell.y;
    return float2((col + inCell.x) / cols, 1.0 - (row + 1.0 - inCell.y) / rows);
}

float2 RsClampContent(float2 c)
{
    float h = 0.5 / max(_Cell.z, 1.0);
    return clamp(c, h, 1.0 - h);
}

half4 RsSampleLinear(float2 uv, float lod)
{
    return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, lod);
}

// Mip level from the content-UV footprint (continuous over the quad), capped at the sheet's mipMax.
float RsLod(float2 c)
{
    float2 px = c * _Cell.z;
    float2 dx = ddx(px);
    float2 dy = ddy(px);
    float d = max(dot(dx, dx), dot(dy, dy));
    return clamp(0.5 * log2(max(d, 1e-8)), 0.0, _Cell.w);
}

half4 RsSampleFlipbook(float2 uv, float age, float rand)
{
    float n = max(_Grid.z, 1.0);
    float fi = RsFrameIndex(age, rand);
    float i0 = floor(fi);
    float f = _Motion.w > 0.5 ? 0.0 : fi - i0;
    float i1 = i0 + 1.0;
    if (_Grid.w > 0.5) { i0 = fmod(i0, n); i1 = fmod(i1, n); }
    else { i0 = min(i0, n - 1.0); i1 = min(i1, n - 1.0); }

    // beams and rings repeat the sheet along u; the mip level follows the continuous (unwrapped) coordinate
    float2 cl = _Strip.x > 0.0 ? float2(uv.x * _Strip.x + _Strip.y, uv.y) : uv;
    float2 c = _Strip.x > 0.0 ? float2(frac(cl.x), uv.y) : uv;
#if defined(RS_PIXEL)
    float iNear = floor(fi + 1e-4);
    if (_Grid.w > 0.5) iNear = fmod(iNear, n); else iNear = min(iNear, n - 1.0);
    half4 s = SAMPLE_TEXTURE2D_LOD(_MainTex, rs_point_clamp_sampler, RsCellUV(iNear, RsClampContent(c)), 0);
    return half4(s.rgb * s.a, s.a);
#else
    float lod = RsLod(cl);
    float2 cA = c;
    float2 cB = c;
    #if defined(RS_MOTION)
    {
        float2 mv = (SAMPLE_TEXTURE2D_LOD(_MotionTex, sampler_MotionTex, RsCellUV(i0, RsClampContent(c)), 0).rg * 2.0 - 1.0) * _Motion.x;
        cA = c - mv * f * _Motion.y;
        cB = c + mv * (1.0 - f) * _Motion.y;
    }
    #endif
    half4 a = RsSampleLinear(RsCellUV(i0, RsClampContent(cA)), lod);
    half4 b = RsSampleLinear(RsCellUV(i1, RsClampContent(cB)), lod);
    // blend premultiplied so a frame's empty area does not pull the other's colour toward black
    half4 pa = half4(a.rgb * a.a, a.a);
    half4 pb = half4(b.rgb * b.a, b.a);
    return lerp(pa, pb, (half)f);
#endif
}

half3 RsLighting(float3 positionWS, float4 screenPos)
{
    half3 light = SampleSH(half3(0, 1, 0));
    Light mainLight = GetMainLight();
    light += mainLight.color * 0.5;
#if defined(_ADDITIONAL_LIGHTS)
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalizedScreenSpaceUV = screenPos.xy / max(screenPos.w, 1e-5);
    uint lightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightCount)
        Light l = GetAdditionalLight(lightIndex, positionWS);
        light += l.color * l.distanceAttenuation;
    LIGHT_LOOP_END
#endif
    return light;
}

half4 RsFrag(RsVaryings i) : SV_Target
{
    float age = i.uvAgeRand.z;
    float rand = i.uvAgeRand.w;
    half4 s = RsSampleFlipbook(i.uvAgeRand.xy, age, rand);

    float mul = i.color.a * saturate(i.custom1.y) * _Fade;
    if (_Strip.z > 1e-4) mul *= saturate(i.uvAgeRand.x / _Strip.z) * saturate((1.0 - i.uvAgeRand.x) / _Strip.z);
    // soft particles against the scene depth, and a fade right in front of the camera
    float2 suv = i.screenPos.xy / max(i.screenPos.w, 1e-5);
    float sceneZ = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
    float partZ = i.screenPos.w;
    if (_Softness > 1e-4 && _RsDebug != 2.0) mul *= saturate((sceneZ - partZ) / _Softness);
    if (_RsDebug == 1.0) return half4(1, 0, 1, 1);
    if (_RsDebug == 3.0) return half4(s.a, i.custom1.x * 0.25, i.custom1.y, 1);
    if (_NearFade > 1e-4) mul *= saturate((partZ - _ProjectionParams.y) / _NearFade);

    // s is premultiplied (rgb * a): scaling rgb and a by the same factor keeps it premultiplied
    half3 rgb = s.rgb * _Tint.rgb * i.color.rgb;
    if (_Lit > 1e-3) rgb *= lerp((half3)1.0, RsLighting(i.positionWS, i.screenPos), (half)_Lit);
    float k = _Intensity * _LayerIntensity * max(i.custom1.x, 0.0);
    half3 outRgb = rgb * (half)(k * mul);
    float alpha = s.a * mul;
    return half4(outRgb, (half)(alpha * _Occlusion));
}

#endif
