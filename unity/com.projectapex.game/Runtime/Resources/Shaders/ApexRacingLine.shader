// Dynamic racing line for URP 17 (Unity 6): glowing dashes a few centimetres above the road (RacingLineGuideComponent,
// Apex.AI.RacingLineGuide). Vertex colour = hint colour and fade; uv.x across the dash (-1..1) gives a soft glowing
// edge around a brighter core, uv.y along it (0..1) rounds the ends. Unlit (readable at night and in the rain),
// alpha-blended, no depth write, pulled towards the camera so it never sinks into the asphalt; fogged with the scene.
Shader "Apex/RacingLine"
{
    Properties
    {
        _Intensity("Glow intensity (HDR)", Range(0.5, 4)) = 1.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-30" "IgnoreProjector" = "True" }

        Pass
        {
            Name "RacingLine"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -2, -2

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LineVertex
            #pragma fragment LineFragment
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                half fog : TEXCOORD1;
            };

            Varyings LineVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = input.uv;
                o.color = input.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 LineFragment(Varyings input) : SV_Target
            {
                half across = abs(input.uv.x);
                half edge = 1.0h - smoothstep(0.55h, 1.0h, across);          // soft glow at the sides
                half ends = smoothstep(0.0h, 0.12h, input.uv.y) * smoothstep(1.0h, 0.88h, input.uv.y);
                half core = 1.0h - smoothstep(0.0h, 0.45h, across);          // brighter centre stripe
                half3 color = input.color.rgb * _Intensity * (0.75h + 0.5h * core);
                color = MixFog(color, input.fog);
                return half4(color, input.color.a * edge * ends);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
