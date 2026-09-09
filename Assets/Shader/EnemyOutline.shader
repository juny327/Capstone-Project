Shader "Custom/EnemyOutline"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineThickness("Outline Thickness", Float) = 0.02
    }

    SubShader
    {
        // 불투명 오브젝트 이후에 그려지도록 설정
        Tags { "RenderType"="Opaque" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Outline"
            
            // ------------------------------------------------------------
            // 🔥 핵심: 스텐실 값이 1(본체)과 같지 않은 영역에만 그린다!
            // ------------------------------------------------------------
            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Keep
            }

            Cull Back         // 뒷면만 그려서 본체 앞을 가리지 않게 함
            ZWrite Off        // 외곽선이 깊이 버퍼를 갱신하지 않게 함
            ZTest LEqual      // 일반적인 깊이 판정

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            //GPU Instancing 활성화
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID //인스턴스 ID 입력받기
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID // ID 전달 (필요 시)
            };
            
            CBUFFER_START(UnityPerMaterial)
            float _OutlineThickness;
            float4 _OutlineColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                //인스턴스 데이터 설정
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                // 1.PNG의 노드 로직: 정점 + (노멀 * 두께)
                float3 posOS = input.positionOS.xyz + (input.normalOS * _OutlineThickness);
                output.positionCS = TransformObjectToHClip(posOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); // 인스턴스 데이터 사용 준비
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}