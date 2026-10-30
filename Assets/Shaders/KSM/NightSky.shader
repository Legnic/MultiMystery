// 밤하늘 스카이박스 (KSM 야외 조명, 2026-10-30): 수평선→천정 그라디언트 + 별 + 달.
// 달 방향은 전역 벡터 _NightMoonDir(NightSkyMoon 컴포넌트가 달빛 방향광에서 읽어 넣는다)를 쓴다.
Shader "KSM/NightSky"
{
    Properties
    {
        _HorizonColor ("Horizon (안개 색과 맞출 것)", Color) = (0.035, 0.048, 0.095, 1)
        _ZenithColor ("Zenith", Color) = (0.004, 0.009, 0.03, 1)
        _GroundColor ("Below horizon", Color) = (0.012, 0.014, 0.025, 1)
        _HorizonFalloff ("Horizon falloff", Range(0.1, 2)) = 0.55
        _StarDensity ("Star density", Range(0, 0.05)) = 0.012
        _StarScale ("Star scale (클수록 작고 많음)", Float) = 90
        _StarBrightness ("Star brightness", Float) = 1.8
        _MoonColor ("Moon color", Color) = (0.82, 0.88, 1, 1)
        _MoonSize ("Moon angular radius (rad)", Range(0.005, 0.1)) = 0.035
        _MoonBrightness ("Moon brightness", Float) = 3.5
        _HaloBrightness ("Moon halo", Float) = 0.45
    }
    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "NightSky"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _HorizonColor;
                half4 _ZenithColor;
                half4 _GroundColor;
                half _HorizonFalloff;
                half _StarDensity;
                half _StarScale;
                half _StarBrightness;
                half4 _MoonColor;
                half _MoonSize;
                half _MoonBrightness;
                half _HaloBrightness;
            CBUFFER_END
            float4 _NightMoonDir;   // 전역: 달이 하늘에서 보이는 방향(월드, 정규화 안 돼도 됨)

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            float Hash13(float3 p3)
            {
                p3 = frac(p3 * 0.1031);
                p3 += dot(p3, p3.zyx + 31.32);
                return frac((p3.x + p3.y) * p3.z);
            }

            float3 Hash33(float3 p3)
            {
                p3 = frac(p3 * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yxz + 33.33);
                return frac((p3.xxy + p3.yxx) * p3.zyx);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;

                // 수평선 → 천정, 수평선 아래는 지면 색
                float3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(h), _HorizonFalloff));
                col = lerp(col, _GroundColor.rgb, smoothstep(0.0, -0.12, h));

                // 별: 방향을 격자로 나눠 칸마다 확률적으로 점 하나
                float3 p = d * _StarScale;
                float3 id = floor(p);
                float3 f = frac(p) - 0.5;
                float rnd = Hash13(id);
                float3 offset = (Hash33(id) - 0.5) * 0.5;
                float size = lerp(0.06, 0.16, Hash13(id + 7.3));
                float star = smoothstep(size, 0.0, length(f - offset)) * step(1.0 - _StarDensity, rnd);
                float twinkle = 0.75 + 0.25 * sin(_Time.y * (1.5 + rnd * 3.0) + rnd * 60.0);
                star *= twinkle * smoothstep(0.03, 0.3, h) * _StarBrightness;
                col += star * lerp(float3(0.8, 0.88, 1.0), float3(1.0, 0.92, 0.8), Hash13(id + 3.1));

                // 달: 원반 + 은은한 무늬 + 번짐
                float3 m = normalize(_NightMoonDir.xyz + float3(0, 0.0001, 0));
                float md = dot(d, m);
                float cosR = cos(_MoonSize);
                float disc = smoothstep(cosR - 0.00005, cosR, md) * smoothstep(-0.03, 0.03, h);
                float3 tx = normalize(cross(m, float3(0, 1, 0)) + float3(0.0001, 0, 0));
                float3 ty = cross(tx, m);
                float2 uv = float2(dot(d, tx), dot(d, ty)) / _MoonSize;
                float mare = 0.82 + 0.18 * sin(uv.x * 4.3 + sin(uv.y * 3.1) * 1.6) * sin(uv.y * 5.2 + uv.x * 1.7);
                float shade = 1.0 - 0.18 * saturate(dot(uv, float2(0.6, -0.5)));
                col += disc * _MoonColor.rgb * _MoonBrightness * mare * shade;
                float halo = pow(saturate(md), 140.0) * _HaloBrightness + pow(saturate(md), 18.0) * 0.05;
                col += halo * float3(0.55, 0.68, 1.0) * smoothstep(-0.05, 0.1, h);

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
