Shader "DeepSpace/Sky"
{
    Properties
    {
        _Tint("Nebula", Color) = (.06,.2,.5,1)
        _Secondary("Dust", Color) = (.4,.07,.45,1)
        _Seed("Sector", Float) = 1
        _Intensity("Brightness", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Background" "Queue"="Background" "PreviewType"="Skybox" }
        Pass
        {
            Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint, _Secondary;
            float _Seed, _Intensity;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; };
            V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.direction=i.positionOS.xyz;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7))+_Seed*21)*43758.5453);}
            float noise(float2 p){float2 n=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(n),hash(n+float2(1,0)),f.x),lerp(hash(n+float2(0,1)),hash(n+1),f.x),f.y);}
            float fbm(float2 p){float v=0,a=.5;for(int k=0;k<5;k++){v+=a*noise(p);p=p*2.03+13.7;a*=.5;}return v;}
            half4 frag(V i):SV_Target
            {
                float3 direction=normalize(i.direction);
                float2 uv=float2(atan2(direction.x,direction.z)/6.2831853+.5,asin(clamp(direction.y,-1,1))/3.14159265+.5);
                float2 p=uv*4+_Seed;
                float dust=fbm(p+float2(fbm(p+4),fbm(p-7))*1.8);
                float band=exp(-pow((uv.y-.48)+.16*sin(uv.x*7+_Seed),2)*19);
                half3 col=half3(.003,.006,.018)+_Tint.rgb*pow(dust,2)*band*.75;
                col+=_Secondary.rgb*pow(fbm(p*1.7+22),4)*band*.9;
                float2 grid=uv*145; float2 id=floor(grid), f=frac(grid)-.5;
                float r=hash(id);
                float stars=(1-smoothstep(.012,.06,length(f)))*step(.94,r);
                float twinkle=.85+.15*sin(_Time.y*.6+r*90);
                col+=stars*twinkle*lerp(half3(.5,.7,1),half3(1,.85,.64),r)*1.8;
                return half4(col*_Intensity,1);
            }
            ENDHLSL
        }
    }
}
