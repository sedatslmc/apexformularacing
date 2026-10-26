// Bosphorus water for URP 17 (Unity 6). Physical inputs come from Apex.Environment.WaterOptics and are
// set per frame by WaterSurfaceComponent; see docs/WATER_SHADER.md.
//   - three scrolling normal-map layers (deep-water drift speeds), detail fades with distance
//   - Schlick Fresnel with F0 of sea water; reflection from the scene's reflection probe
//   - sun glitter: GGX with the Cox-Munk roughness of the wind-driven sea; distant pixels carry the
//     unresolved wave slopes as roughness, which widens the glitter path instead of aliasing
//   - body colour deep -> shallow and foam along quays from a coast-distance texture
//   - per-pixel fog and shadow coordinates (the strait is one very large quad)
// Colours are passed as vectors in linear space (Color properties would be gamma-converted).
Shader "Apex/Water"
{
    Properties
    {
        [NoScaleOffset] _NormalMap("Wave normals (RGB = xyz * 0.5 + 0.5)", 2D) = "bump" {}
        [NoScaleOffset] _CoastTex("Coast distance (R = distance / range)", 2D) = "white" {}
        _CoastRect("Coast rect (minX, minZ, 1/sizeX, 1/sizeZ)", Vector) = (0, 0, 0, 0)
        _WaveA("Wave A (uv offset xy, 1/tile, strength)", Vector) = (0, 0, 0.015625, 0.5)
        _WaveB("Wave B (uv offset xy, 1/tile, strength)", Vector) = (0, 0, 0.0434783, 0.35)
        _WaveC("Wave C (uv offset xy, 1/tile, strength)", Vector) = (0, 0, 0.117647, 0.25)
        _Roughness("GGX roughness (Cox-Munk)", Range(0.02, 0.6)) = 0.17
        _F0("Fresnel F0", Range(0, 0.1)) = 0.021
        _DeepColor("Deep body colour (linear)", Vector) = (0.006, 0.03, 0.04, 0)
        _ShallowColor("Shallow body colour (linear)", Vector) = (0.02, 0.075, 0.07, 0)
        _FoamAlbedo("Foam albedo", Range(0, 1)) = 0.75
        _FoamWidth("Foam band (m)", Float) = 4
        _ShallowWidth("Shallow band (m)", Float) = 45
        _CoastMaxDistance("Coast texture range (m)", Float) = 64
        _DetailDistance("Wave detail fade distance (m)", Float) = 350
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "IgnoreProjector" = "True" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _CoastRect;
            float4 _WaveA;
            float4 _WaveB;
            float4 _WaveC;
            float4 _DeepColor;
            float4 _ShallowColor;
            half _Roughness;
            half _F0;
            half _FoamAlbedo;
            half _FoamWidth;
            half _ShallowWidth;
            half _CoastMaxDistance;
            half _DetailDistance;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings WaterVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = p.positionCS;
            output.positionWS = p.positionWS;
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WaterVertex
            #pragma fragment WaterFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_NormalMap);  SAMPLER(sampler_NormalMap);
            TEXTURE2D(_CoastTex);   SAMPLER(sampler_CoastTex);

            // Surface slope (dh/dx, dh/dz) of one layer; the texture stores the normal as xyz * 0.5 + 0.5.
            half2 WaveSlope(float2 p, float4 w)
            {
                half3 n = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, p * w.z + w.xy).rgb * 2.0h - 1.0h;
                return n.xy * w.w / max(n.z, 0.2h);
            }

            half SchlickFresnel(half f0, half cosTheta)
            {
                half m = 1.0h - saturate(cosTheta);
                half m2 = m * m;
                return f0 + (1.0h - f0) * m2 * m2 * m;
            }

            half4 WaterFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 positionWS = input.positionWS;
                float2 p = positionWS.xz;
                float3 toCamera = _WorldSpaceCameraPos - positionWS;
                float dist = length(toCamera);
                half3 V = half3(toCamera / max(dist, 1e-3));

                // Waves: near pixels resolve the layers; far pixels keep 30 % of the slope and move the rest
                // into roughness (the normal map's mips average the slopes out anyway).
                half detail = saturate(1.0h - half(dist / _DetailDistance));
                half2 slope = WaveSlope(p, _WaveA) + WaveSlope(p, _WaveB) + WaveSlope(p, _WaveC);
                slope *= lerp(0.3h, 1.0h, detail);
                half3 N = normalize(half3(slope.x, 1.0h, slope.y));
                half alpha = lerp(_Roughness * 0.45h, _Roughness, 1.0h - detail);
                half perceptualRoughness = sqrt(alpha);

                // Coast distance (m): white default texture / zero rect = open water.
                float2 coastUv = (p - _CoastRect.xy) * _CoastRect.zw;
                half coast = SAMPLE_TEXTURE2D(_CoastTex, sampler_CoastTex, coastUv).r * _CoastMaxDistance;

                half NdotV = saturate(dot(N, V));
                half F = SchlickFresnel(_F0, NdotV);

                // Sky and city from the reflection probe; reflections below the horizon are folded up.
                half3 R = reflect(-V, N);
                R.y = abs(R.y);
                float2 screenUv = GetNormalizedScreenSpaceUV(input.positionCS);
                half3 environment = GlossyEnvironmentReflection(R, positionWS, perceptualRoughness, 1.0h, screenUv);

                // Sun glitter: URP's minimalist GGX (same normalisation as Lit) with Fresnel at the half vector.
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
                half atten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half3 L = mainLight.direction;
                half3 H = SafeNormalize(L + V);
                half NdotL = saturate(dot(N, L));
                half NdotH = saturate(dot(N, H));
                half LdotH = saturate(dot(L, H));
                half a2 = alpha * alpha;
                half d = NdotH * NdotH * (a2 - 1.0h) + 1.00001h;
                half specular = a2 / ((d * d) * max(0.1h, LdotH * LdotH) * (alpha * 4.0h + 2.0h));
                specular = min(specular, 100.0h);
                half3 glitter = mainLight.color * (atten * NdotL * specular * SchlickFresnel(_F0, LdotH));

                // Water body: light that enters, scatters and leaves (deep blue-green, lighter near the shore).
                half3 ambientUp = SampleSHPixel(half3(0.0h, 0.0h, 0.0h), half3(0.0h, 1.0h, 0.0h));
                half3 incoming = ambientUp + mainLight.color * (atten * saturate(L.y));
                half shallow = 1.0h - saturate(coast / _ShallowWidth);
                half3 body = lerp(half3(_DeepColor.rgb), half3(_ShallowColor.rgb), shallow * shallow);
                half3 color = body * incoming * (1.0h - F) + environment * F + glitter;

                // Foam along quay walls and shores, broken up by two differently scaled samples of the wave texture.
                half band = 1.0h - saturate(coast / _FoamWidth);
                half n1 = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, p * 0.37 + _WaveC.xy * 2.0).r;
                half n2 = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, p * 0.11 - _WaveB.xy).g;
                half pattern = n1 * n2 * 2.2h;
                half foam = saturate((band * pattern - 0.3h) * 4.0h) * lerp(0.5h, 1.0h, detail);
                color = lerp(color, _FoamAlbedo * incoming, foam);

                // Per-pixel fog: the strait is one quad, vertex fog would be interpolated across kilometres.
                float4 positionCS = TransformWorldToHClip(positionWS);
                color = MixFog(color, ComputeFogFactor(positionCS.z));
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WaterVertex
            #pragma fragment DepthFragment

            half DepthFragment(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WaterVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            // SSAO only needs the flat surface normal.
            half4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                float3 normalWS = float3(0.0, 1.0, 0.0);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
                return half4(PackFloat2To888(remapped), 0.0);
            #else
                return half4(normalWS, 0.0);
            #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
