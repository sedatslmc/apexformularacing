// Ship wake foam for URP 17 (Unity 6): alpha-blended white water over the Bosphorus surface, built each few frames
// by ShipWakeComponent from Apex.Environment.ShipWake. Vertex alpha carries the foam strength (fading with age and
// towards the ribbon edges); a scrolling noise texture breaks it into patches. Foam is lit like a rough white
// surface by the main light (with its shadow) and the ambient probe, then fogged with the scene.
Shader "Apex/Wake"
{
    Properties
    {
        [NoScaleOffset] _NoiseTex("Foam noise (R, tiling)", 2D) = "gray" {}
        _FoamAlbedo("Foam albedo", Range(0, 1)) = 0.8
        _NoiseScale("Noise tiling (1/m)", Float) = 0.09
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-20" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Wake"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WakeVertex
            #pragma fragment WakeFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _FoamAlbedo;
                float _NoiseScale;
            CBUFFER_END

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half alpha : TEXCOORD1;
                half fog : TEXCOORD2;
            };

            Varyings WakeVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.alpha = input.color.a;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 WakeFragment(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz * _NoiseScale;
                half n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, p + _Time.y * float2(0.004, 0.003)).r;
                half n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, p * 2.7 - _Time.y * float2(0.006, -0.002)).r;
                half breakup = saturate((n1 * 0.65h + n2 * 0.35h) * 1.8h - 0.35h);
                half alpha = saturate(input.alpha * (0.35h + 0.9h * breakup));
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 up = half3(0.0h, 1.0h, 0.0h);
                half3 light = mainLight.color * (saturate(dot(up, mainLight.direction)) * mainLight.shadowAttenuation) + SampleSH(up);
                half3 color = _FoamAlbedo * light;
                color = MixFog(color, input.fog);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
