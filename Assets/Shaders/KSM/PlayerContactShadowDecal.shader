// 플레이어 발밑 타원형 그림자 - 깊이 기반 투영(데칼).
// 발 주변을 감싸는 상자를 그리고, 화면의 각 픽셀이 가리키는 "실제 바닥 위치"를 깊이 버퍼에서 복원해
// 그 위치가 타원 안이면 어둡게 칠한다. 그래서 계단처럼 단차가 있는 바닥에서도 메시를 이어 붙이지 않아 갈라지지 않는다.
//  - 높이: 서 있는 바닥보다 위(윗단·캐릭터 몸)는 빨리 사라지고, 아래(아랫단)는 천천히 사라진다.
//  - 수직면(계단 챌판·벽)은 법선으로 걸러 낸다.
// URP의 Depth Texture가 켜져 있어야 한다(현재 URP 에셋에서 켜짐).
Shader "CoopDemo/PlayerContactShadowDecal"
{
    Properties
    {
        _Color ("Shadow Color", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-50" }
        Pass
        {
            Name "ContactShadowDecal"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _Center;   // xyz = 서 있는 바닥 위 기준점(월드), w = 진하기(0~1)
                float4 _Axes;     // xy = 타원의 오른쪽 방향(월드 xz, 정규화), zw = 반지름(오른쪽, 앞)
                float4 _Fade;     // x = 위로 허용하는 높이, y = 위쪽 사라지는 구간, z = 아래쪽 사라지는 구간
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 screenPos : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.screenPos.xy / input.screenPos.w;
                float depth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (depth <= 0.0) return 0;   // 하늘/없음
                #else
                    if (depth >= 1.0) return 0;
                #endif
                float3 wp = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);

                float3 rel = wp - _Center.xyz;
                float2 right = _Axes.xy;
                float2 fwd = float2(-right.y, right.x);
                float2 local = float2(dot(rel.xz, right), dot(rel.xz, fwd)) / max(_Axes.zw, 0.0001);
                float e = length(local);
                // 가운데는 진하고 가장자리는 부드럽게 0으로
                float a = (1.0 - smoothstep(0.25, 1.0, e));
                a *= a;

                // 높이 창: 서 있는 바닥은 1, 윗단/몸은 빨리 0, 아랫단은 천천히 0
                float up = 1.0 - saturate((rel.y - _Fade.x) / max(_Fade.y, 0.0001));
                float down = 1.0 - saturate(-rel.y / max(_Fade.z, 0.0001));
                a *= (rel.y > 0 ? up : down);

                // 수직면(챌판·벽) 제외: 화면 미분으로 표면 법선의 y 성분을 구한다
                float3 n = normalize(cross(ddy(wp), ddx(wp)));
                a *= smoothstep(0.55, 0.85, abs(n.y));

                return half4(_Color.rgb, _Color.a * saturate(a) * _Center.w);
            }
            ENDHLSL
        }
    }
}
