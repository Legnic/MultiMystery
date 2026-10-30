Shader "CoopDemo/PlayerContactShadow"
{
    Properties
    {
        _Color ("Shadow Color", Color) = (0, 0, 0, 0.9)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-50" }
        Pass
        {
            Name "ContactShadow"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half distanceFromCenter = length(input.uv * 2.0h - 1.0h);
                // A rounded falloff removes the flat, solid center while keeping
                // a visible core. Fade the outer rim to zero so no quad edge shows.
                half softness = exp2(-2.0h * distanceFromCenter * distanceFromCenter);
                softness *= 1.0h - smoothstep(0.7h, 1.0h, distanceFromCenter);
                return half4(_Color.rgb, _Color.a * softness);
            }
            ENDHLSL
        }
    }
}
