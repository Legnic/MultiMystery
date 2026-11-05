// 외곽선 합성 - 마스크(물체가 있는 곳 = 1)를 가우시안 가중 최댓값 팽창으로 바깥으로 번지게 해서
// "단단한 띠 + 부드러운 번짐" 모양의 밝은 외곽선을 카메라 화면 위에 얹는다. 물체 안쪽은 건드리지 않는다.
//  - Pass 0: 가로 방향 팽창 (마스크 -> 임시 텍스처)
//  - Pass 1: 세로 방향 팽창 + 색칠 (임시 텍스처 + 마스크 -> 카메라 색)
// 두 방향을 가우시안 가중으로 곱하면 번지는 모양이 둥글게 나온다(원형 팽창).
Shader "Hidden/KSM/OutlineComposite"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // x = 가우시안 시그마(픽셀), y = 띠 안쪽 문턱, z = 띠 바깥 문턱, w = 번짐 세기
        float4 _OutlineParams;
        half4 _OutlineColor;
        float _OutlineDebug;
        TEXTURE2D(_OutlineMaskTex);

        static const int TAPS = 20;   // 한쪽 방향 표본 수 (양쪽 합 41)

        // dir: 가로면 (1,0), 세로면 (0,1)
        half Dilate(TEXTURE2D_PARAM(tex, samp), float2 uv, float2 dir)
        {
            float sigma = max(_OutlineParams.x, 0.5);
            float radius = sigma * 3.0;
            float stepPx = radius / TAPS;
            float2 texel = dir * (_BlitTexture_TexelSize.xy);
            half best = 0;
            [unroll]
            for (int k = -TAPS; k <= TAPS; k++)
            {
                float d = k * stepPx;
                half w = (half)exp(-(d * d) / (2.0 * sigma * sigma));
                half m = SAMPLE_TEXTURE2D_LOD(tex, samp, uv + texel * d, 0).r;
                best = max(best, m * w);
            }
            return best;
        }
        ENDHLSL

        Pass
        {
            Name "OutlineDilateH"
            Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragH
            half4 FragH(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(Dilate(TEXTURE2D_ARGS(_BlitTexture, sampler_LinearClamp), input.texcoord, float2(1, 0)), 0, 0, 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "OutlineDilateVComposite"
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragV
            half4 FragV(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half glow = Dilate(TEXTURE2D_ARGS(_BlitTexture, sampler_LinearClamp), uv, float2(0, 1));
                half center = SAMPLE_TEXTURE2D_LOD(_OutlineMaskTex, sampler_LinearClamp, uv, 0).r;
                if (_OutlineDebug > 0.5) return half4(center, glow, 0, 0.8);   // 진단용: 빨강 = 마스크, 초록 = 번짐
                // 단단한 띠(문턱 사이를 부드럽게) + 바깥으로 옅게 번지는 빛
                half band = smoothstep(_OutlineParams.y, _OutlineParams.z, glow);
                half halo = glow * glow * _OutlineParams.w;
                half a = saturate(max(band, halo)) * (1.0 - saturate(center * 4.0));
                // 물체에 가까울수록 살짝 더 밝게
                half3 col = _OutlineColor.rgb * (0.85 + 0.35 * glow);
                return half4(col, a * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
