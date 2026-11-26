// Real Spells - hit flash: the target's renderers wear this for a few frames when a spell hits them. It keeps the
// model's own colour and shading (base map and colour copied from the original material, main light + ambient +
// per-object additional lights, Lambert) and leans it toward the flash colour by _Amount (0.6 at the peak, then eased
// out by SpellReactive); it never renders darker than the model was, and the model keeps casting its shadow. Before 2026-11-13 it was a flat colour: a white silhouette that read as a rendering glitch.
Shader "RealSpellsDemo/Hit Flash"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _Color ("Flash Color", Color) = (1, 1, 1, 1)
        _Amount ("Amount", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "HitFlash"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _Color;
                half _Amount;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; };

            V Vert(A v)
            {
                V o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                half3 light = SampleSH(n);
                Light main = GetMainLight();
                light += main.color * saturate(dot(n, main.direction));
                uint count = GetAdditionalLightsCount();
                for (uint k = 0u; k < count; k++)
                {
                    Light l = GetAdditionalLight(k, i.positionWS);
                    light += l.color * (l.distanceAttenuation * saturate(dot(n, l.direction)));
                }
                half3 lit = albedo * light;
                // the flash: the same form on a paler surface in the flash colour, under the scene's own light held
                // between a floor (it reads at night) and a cap (an impact light's spike does not blow it to white);
                // the mix keeps 1 - _Amount of the model as it was. A flash never darkens: in daylight a bright model is
                // already above the capped flash, so the mix is lifted back to the model's own brightness plus a
                // share of the amount (2026-11-13: by day the old form read as a grey fade, not a flash)
                half3 flash = _Color.rgb * lerp(albedo, half3(1.0, 1.0, 1.0), 0.6) * clamp(light, half3(0.45, 0.45, 0.45), half3(1.5, 1.5, 1.5));
                half a = saturate(_Amount);
                half3 mixed = lerp(lit, flash, a);
                half lumLit = dot(lit, half3(0.2126, 0.7152, 0.0722));
                half lumMix = dot(mixed, half3(0.2126, 0.7152, 0.0722));
                mixed *= max(1.0, lumLit * (1.0 + 0.35 * a) / max(lumMix, 1e-4));
                return half4(mixed, 1.0);
            }
            ENDHLSL
        }
        Pass
        {
            // the target keeps its shadow during the flash frames
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _Color;
                half _Amount;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; };

            V Vert(A v)
            {
                V o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = positionCS;
                return o;
            }

            half4 Frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _Color;
                half _Amount;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };

            V Vert(A v)
            {
                V o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half Frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
