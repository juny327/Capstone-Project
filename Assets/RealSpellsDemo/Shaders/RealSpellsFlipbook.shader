// Real Spells flipbook particle for URP: scheduled frames (lookup texture), motion-vector frame blending,
// soft particles, HDR intensity, alpha / additive / glow through one premultiplied blend.
// Loaded at run time with Resources.Load (never Shader.Find), so player builds always contain it.
Shader "RealSpellsDemo/Flipbook"
{
    Properties
    {
        _MainTex ("Sheet (sRGB, straight alpha)", 2D) = "black" {}
        _MotionTex ("Motion Vectors (linear RG)", 2D) = "gray" {}
        _FrameLut ("Schedule Lookup", 2D) = "black" {}
        _Grid ("Grid (cols, rows, frames, loop)", Vector) = (1, 1, 1, 0)
        _Cell ("Cell (gutter, content, content px, mip max)", Vector) = (0, 1, 512, 3)
        _Motion ("Motion (scale, strength, on)", Vector) = (0, 1, 0, 0)
        [HDR] _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 1
        _Occlusion ("Occlusion (0 additive, 1 alpha)", Range(0, 1)) = 0
        _Softness ("Soft Particle Distance", Float) = 0.3
        _Lit ("Scene Lighting", Range(0, 1)) = 0
        _LoopRate ("Loop Rate (cycles per second)", Float) = 0
        _NearFade ("Near Camera Fade", Float) = 0.3
        _Fade ("Fade", Range(0, 1)) = 1
        _LayerIntensity ("Layer Intensity", Float) = 1
        _ManualTime ("Manual Time (segments, -1 off)", Float) = -1
        _StartOffset ("Random Start Offset (normalized)", Float) = 0
        _DepthOffset ("Depth Offset toward camera (m)", Float) = 0
        _AgeEase ("Age Ease exponent", Float) = 1
        _Strip ("Strip tiles, scroll, end fade", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Pass
        {
            Name "RealSpellsFlipbook"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex RsVert
            #pragma fragment RsFrag
            #pragma multi_compile_local _ RS_MOTION
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "RealSpellsFlipbook.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
