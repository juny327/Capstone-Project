Shader "DeepSpace/PanelDeck"
{
    Properties
    {
        _BaseColor("Hull color", Color) = (.15,.21,.28,1)
        _Accent("Inset light", Color) = (.05,.6,.9,1)
        _PanelSize("Panel size", Float) = 2.5
        _Glow("Inset brightness", Float) = .3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor, _Accent;
            float _PanelSize, _Glow;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; };
            V vert(A i) { V o; o.world=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world); o.normal=TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 frag(V i):SV_Target
            {
                float2 uv=i.world.xz/max(.1,_PanelSize);
                float2 cell=floor(uv), f=frac(uv);
                float2 edge=min(f,1-f);
                float seam=1-smoothstep(.009,.025,min(edge.x,edge.y));
                float bevel=smoothstep(.025,.035,min(edge.x,edge.y))*(1-smoothstep(.035,.05,min(edge.x,edge.y)));
                float variation=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);
                float plate=smoothstep(.04,.08,min(edge.x,edge.y));
                float bolts=1-smoothstep(.012,.025,length(edge-float2(.075,.075)));
                float hatch=step(.84,f.x)*step(f.x,.91)*step(.2,f.y)*step(f.y,.8);
                float lights=step(.12,f.x)*step(f.x,.32)*step(.035,f.y)*step(f.y,.048)*step(.55,variation);
                half3 albedo=_BaseColor.rgb*(.85+variation*.18)*lerp(.32,1,plate);
                albedo=lerp(albedo,half3(.012,.021,.035),seam);
                albedo+=bevel*.055+bolts*.065;
                albedo*=1-hatch*.12;
                Light main=GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 normal=normalize(i.normal);
                half3 light=max(SampleSH(normal),half3(.22,.25,.32))+main.color*saturate(dot(normal,main.direction))*main.shadowAttenuation*.7;
                return half4(albedo*light+_Accent.rgb*lights*_Glow,1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
