// Converted from "Shader Forge/Mobile_Vtx_Cutout" (Built-in RP, CGPROGRAM) to
// Universal Render Pipeline HLSL for Unity 6 (URP 17.x).
// Original shader used procedural (texture-less) vertex-color driven color noise
// and a per-pixel ordered-dither alpha cutout. All property names are kept
// identical so existing materials (e.g. Pine Forest_LowPoly_material.mat)
// continue to work without reassignment.
Shader "Shader Forge/Mobile_Vtx_Cutout"
{
    Properties
    {
        _ColorBase ("ColorBase", Color) = (0.6470588,0.6470588,0.6470588,1)
        _Noise_Color ("Noise_Color", Color) = (0.9338235,0.9338235,0.9338235,1)
        _Base_Noise_Amnt ("Base_Noise_Amnt", Float) = 0
        _Color_Noise_Steps ("Color_Noise_Steps", Float) = 50
        _Opacity_Noise_Steps ("Opacity_Noise_Steps", Float) = 50
        _Opacity_NoiseAmnt ("Opacity_NoiseAmnt", Float) = 0.5
        _Emissiv_Amnt ("Emissiv_Amnt", Float) = 0
        _Vtx_Color_Amnt ("Vtx_Color_Amnt", Float) = 1
        _Vtx_Opacity ("Vtx_Opacity", Float) = 1
        _All_Opacity ("All_Opacity", Float) = 1
        [HideInInspector] _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        Cull Back

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _ColorBase;
            float4 _Noise_Color;
            float _Base_Noise_Amnt;
            float _Color_Noise_Steps;
            float _Opacity_Noise_Steps;
            float _Opacity_NoiseAmnt;
            float _Emissiv_Amnt;
            float _Vtx_Color_Amnt;
            float _Vtx_Opacity;
            float _All_Opacity;
            float _Cutoff;
        CBUFFER_END

        // Same hash/posterize noise as the original Shader Forge graph.
        float Hash21(float2 uv)
        {
            float2 skewed = uv + 0.2127 + uv.x * 0.3713 * uv.y;
            float2 rnd = 4.789 * sin(489.123 * skewed);
            return frac(rnd.x * rnd.y * (1.0 + skewed.x));
        }

        float PosterizedNoise(float2 uv, float steps)
        {
            float2 p = floor(uv * steps) / (steps - 1.0);
            return Hash21(p);
        }

        // 3x3 ordered (Bayer) dither, evaluated directly from the fragment's
        // screen-space pixel position (SV_POSITION.xy in the fragment stage
        // is already in window/pixel coordinates, so no NDC round-trip needed).
        float BinaryDither3x3(float value, float2 pixelCoord)
        {
            float3x3 mtx = float3x3(
                float3(3, 7, 4) / 10.0,
                float3(6, 1, 9) / 10.0,
                float3(2, 8, 5) / 10.0);
            float2 px = floor(pixelCoord);
            int xSmp = (int)fmod(px.x, 3);
            int ySmp = (int)fmod(px.y, 3);
            float3 xVec = 1 - saturate(abs(float3(0, 1, 2) - xSmp));
            float3 yVec = 1 - saturate(abs(float3(0, 1, 2) - ySmp));
            float3 pxMult = float3(dot(mtx[0], yVec), dot(mtx[1], yVec), dot(mtx[2], yVec));
            return round(value + dot(pxMult, xVec));
        }

        float3 ComputeBaseColor(float2 uv0)
        {
            float colorNoise = PosterizedNoise(uv0, _Color_Noise_Steps);
            return colorNoise * _Noise_Color.rgb * _Base_Noise_Amnt + _ColorBase.rgb;
        }

        void ApplyDitherCutout(float2 uv0, float2 pixelCoord, float vertexAlpha)
        {
            float opacityNoise = PosterizedNoise(uv0, _Opacity_Noise_Steps);
            float ditherInput = lerp((vertexAlpha * _Vtx_Opacity) * opacityNoise * _Opacity_NoiseAmnt, _All_Opacity, vertexAlpha);
            clip(BinaryDither3x3(ditherInput - 1.5, pixelCoord));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv0        : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv0        : TEXCOORD0;
                float4 color      : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS   : TEXCOORD3;
                float3 vertexSH   : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
                float fogCoord    : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.uv0 = v.uv0;
                o.color = v.color;

                VertexPositionInputs posInputs = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(v.normalOS);

                o.positionWS = posInputs.positionWS;
                o.normalWS = normInputs.normalWS;
                o.positionCS = posInputs.positionCS;

                OUTPUT_SH(o.normalWS, o.vertexSH);
                o.shadowCoord = GetShadowCoord(posInputs);
                o.fogCoord = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                ApplyDitherCutout(i.uv0, i.positionCS.xy, i.color.a);

                float3 normalWS = normalize(i.normalWS);
                Light mainLight = GetMainLight(i.shadowCoord);
                float3 attenColor = mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation);

                float NdotL = max(0.0, dot(normalWS, mainLight.direction));
                float3 directDiffuse = NdotL * attenColor;

                #if defined(_ADDITIONAL_LIGHTS)
                    uint additionalLightsCount = GetAdditionalLightsCount();
                    for (uint lightIndex = 0u; lightIndex < additionalLightsCount; lightIndex++)
                    {
                        Light light = GetAdditionalLight(lightIndex, i.positionWS);
                        float3 lightAtten = light.color * (light.distanceAttenuation * light.shadowAttenuation);
                        directDiffuse += max(0.0, dot(normalWS, light.direction)) * lightAtten;
                    }
                #endif

                float3 indirectDiffuse = i.vertexSH;

                float3 baseColor = ComputeBaseColor(i.uv0);
                float3 diffuseColor = baseColor * i.color.rgb * _Vtx_Color_Amnt;
                float3 diffuse = (directDiffuse + indirectDiffuse) * diffuseColor;
                float3 emissive = baseColor * i.color.rgb * _Emissiv_Amnt;

                float3 finalColor = diffuse + emissive;
                finalColor = MixFog(finalColor, i.fogCoord);
                return half4(finalColor, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv0        : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv0        : TEXCOORD0;
                float4 color      : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.uv0 = v.uv0;
                o.color = v.color;

                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                o.positionCS = ApplyShadowClamping(positionCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                ApplyDitherCutout(i.uv0, i.positionCS.xy, i.color.a);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0        : TEXCOORD0;
                float2 uv1        : TEXCOORD1;
                float2 uv2        : TEXCOORD2;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv0        : TEXCOORD0;
                float4 color      : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                o.positionCS = MetaVertexPosition(v.positionOS, v.uv1, v.uv2, unity_LightmapST, unity_DynamicLightmapST);
                o.uv0 = v.uv0;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                MetaInput o = (MetaInput)0;
                float3 baseColor = ComputeBaseColor(i.uv0);
                o.Emission = baseColor * i.color.rgb * _Emissiv_Amnt;
                o.Albedo = baseColor * i.color.rgb * _Vtx_Color_Amnt;
                return MetaFragment(o);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
