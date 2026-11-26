Shader "DeepSpace/Cosmos"
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
        // 2026-11-25 — 바닥 위 우주 창 판(ARMOURED VIEWPORT)이 바닥보다 8cm 높아 그 위의 몬스터 돌진 경고 · 블랙홀 장판 · 바닥 효과를 가렸다.
        // 바닥(Geometry) 다음에 그리고 깊이는 쓰지 않는다 — 판은 그대로 보이고, 위를 걷는 캐릭터는 판 위에, 투명한 경고 · 효과는 판 위에 그려진다
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+450" }
        Pass
        {
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint, _Secondary;
            float _Seed, _Intensity;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7))+_Seed*21)*43758.5453);}
            float noise(float2 p){float2 n=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(n),hash(n+float2(1,0)),f.x),lerp(hash(n+float2(0,1)),hash(n+1),f.x),f.y);}
            float fbm(float2 p){float v=0,a=.5;for(int k=0;k<5;k++){v+=a*noise(p);p=p*2.03+13.7;a*=.5;}return v;}
            half4 frag(V i):SV_Target
            {
                float2 uv=i.uv;
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
                float2 planet=(uv-float2(.73,.68))*float2(1,1.3);
                float d=length(planet), pr=.14;
                float rim=exp(-abs(d-pr)*230);
                col+=rim*_Tint.rgb*1.8;
                if(d<pr)
                {
                    float3 n=float3(planet/pr,sqrt(saturate(1-d*d/(pr*pr))));
                    float lit=saturate(dot(n,normalize(float3(-.7,.4,.55))));
                    col=_Tint.rgb*(.03+lit*.6)*( .5+fbm(planet*50+_Seed)*.7)+rim*_Tint.rgb;
                }
                return half4(col*_Intensity,1);
            }
            ENDHLSL
        }
    }
}
