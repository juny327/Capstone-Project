Shader "NoName/EnemyProjectile"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.3,0.9,1,1)
        _UseVertexColor ("Use trail color", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _UseVertexColor;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = lerp(half4(1,1,1,1), input.color, _UseVertexColor);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return _BaseColor * input.color; }
            ENDHLSL
        }
    }
}
