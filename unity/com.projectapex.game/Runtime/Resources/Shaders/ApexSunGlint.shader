// Window glint at golden hour for URP 17 (Unity 6): an additive layer over the facades that face the low sun
// (Apex.Environment.GoldenHour, SunGlintComponent). The facade window mask times _GlintColor, darkened where the
// facade lies in the sun's shadow and faded out with the fog (additive light must fade to nothing, not to the
// fog colour). Drawn only while the sun is low; the renderers are disabled otherwise.
Shader "Apex/SunGlint"
{
    Properties
    {
        [NoScaleOffset] _MainTex("Window mask (R = glass, per-pane brightness)", 2D) = "black" {}
        _GlintColor("Glint colour (linear, HDR)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" "IgnoreProjector" = "True" }

        Pass
        {
            Name "SunGlint"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex GlintVertex
            #pragma fragment GlintFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GlintColor;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half fog : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings GlintVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = input.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 GlintFragment(Varyings input) : SV_Target
            {
                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r;
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 color = _GlintColor.rgb * (mask * mainLight.shadowAttenuation);
                // Fog towards black: an additive layer must vanish into the haze, not tint it.
                color = MixFogColor(color, half3(0.0h, 0.0h, 0.0h), input.fog);
                return half4(color, 0.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
