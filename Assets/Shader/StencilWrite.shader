Shader "Custom/StencilWrite"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            ColorMask 0     // 색은 그리지 않음
            ZWrite On       // 깊이는 기록함
            
            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace // 이 오브젝트가 그려지는 곳을 1로 채움
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata v) {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                return o;
            }
            half4 frag (v2f i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}