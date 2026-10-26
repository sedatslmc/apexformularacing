// Istanbul hillside terrain for URP 17 (Unity 6): replaces the flat grey ground around the circuit.
// Three scanned CC0 layers chosen by slope (see docs/TERRAIN.md):
//   grass   flat ground, parks and verges            (projected from above)
//   soil    moderate slopes, earth with stones       (projected from above)
//   wall    steep faces: rubble-stone retaining wall (projected from the side, courses stay horizontal)
// The slope thresholds are broken up by macro noise, the grass gets a large-scale colour variation and a
// second, coarser sample far away so the tiling does not repeat visibly. Lighting is URP's own PBR path
// (shadows, SSAO, additional lights such as street lamps, probes, fog). Colours are linear vectors.
Shader "Apex/Terrain"
{
    Properties
    {
        [NoScaleOffset] _GrassMap("Grass albedo", 2D) = "grey" {}
        [NoScaleOffset] _GrassNormal("Grass normal", 2D) = "bump" {}
        [NoScaleOffset] _SoilMap("Soil albedo", 2D) = "grey" {}
        [NoScaleOffset] _SoilNormal("Soil normal", 2D) = "bump" {}
        [NoScaleOffset] _WallMap("Wall albedo", 2D) = "grey" {}
        [NoScaleOffset] _WallNormal("Wall normal", 2D) = "bump" {}
        _Tiling("1/metres per tile (grass, soil, wall, macro)", Vector) = (0.5, 0.317, 0.5, 0.027)
        _AlbedoScale("Linear albedo multipliers (grass, soil, wall)", Vector) = (0.5, 1, 1, 0)
        _Smoothness("Smoothness (grass, soil, wall, wet)", Vector) = (0.25, 0.12, 0.1, 0)
        _Slopes("Slope cosines (soil from, soil full, wall from, wall full)", Vector) = (0.95, 0.88, 0.8, 0.68)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Tiling;
            float4 _AlbedoScale;
            float4 _Smoothness;
            float4 _Slopes;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings TerrainVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = p.positionCS;
            output.positionWS = p.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
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
            #pragma vertex TerrainVertex
            #pragma fragment TerrainFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_GrassMap);   SAMPLER(sampler_GrassMap);
            TEXTURE2D(_GrassNormal);
            TEXTURE2D(_SoilMap);
            TEXTURE2D(_SoilNormal);
            TEXTURE2D(_WallMap);
            TEXTURE2D(_WallNormal);

            // One sampler for all six maps (repeat, trilinear): stays within the sampler limit of mobile GPUs.
            #define SAMPLE_LAYER(tex, uv) SAMPLE_TEXTURE2D(tex, sampler_GrassMap, uv)

            half3 UnpackLayerNormal(half4 packed)
            {
                // The CC0 normal maps are imported as normal maps (Editor/CC0TextureImporter).
                return UnpackNormal(packed);
            }

            half4 TerrainFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 positionWS = input.positionWS;
                half3 geoN = half3(normalize(input.normalWS));
                float3 toCamera = _WorldSpaceCameraPos - positionWS;
                float dist = length(toCamera);
                half3 V = half3(toCamera / max(dist, 1e-3));

                // Macro variation (grass albedo at ~37 m per tile, luminance only) breaks up tiling and slope bands.
                float2 top = positionWS.xz;
                half macro = dot(SAMPLE_LAYER(_GrassMap, top * _Tiling.w).rgb, half3(0.3h, 0.59h, 0.11h));
                half wobble = (macro - 0.35h) * 0.25h;

                // Slope weights (geoN.y = cosine of the slope), thresholds shifted by the macro noise.
                half up = geoN.y + wobble * 0.15h;
                half soil = saturate((_Slopes.x - up) / max(_Slopes.x - _Slopes.y, 1e-3));
                half wall = saturate((_Slopes.z - up) / max(_Slopes.z - _Slopes.w, 1e-3));
                half wGrass = (1.0h - soil);
                half wSoil = soil * (1.0h - wall);
                half wWall = wall;

                // Grass: near sample plus a 3.7x coarser one that takes over with distance (less visible repetition).
                float2 uvG = top * _Tiling.x;
                half far = saturate((dist - 40.0) / 120.0);
                half3 grass = lerp(SAMPLE_LAYER(_GrassMap, uvG).rgb, SAMPLE_LAYER(_GrassMap, uvG * 0.27).rgb, far * 0.6h);
                grass *= lerp(0.8h, 1.2h, saturate(macro * 1.6h));
                half3 soilCol = SAMPLE_LAYER(_SoilMap, top * _Tiling.y).rgb;

                // Wall: side projections along x and z, blended by the normal (courses horizontal).
                half2 side = abs(geoN.xz);
                side /= max(side.x + side.y, 1e-3h);
                float2 uvX = float2(positionWS.z, positionWS.y) * _Tiling.z;
                float2 uvZ = float2(positionWS.x, positionWS.y) * _Tiling.z;
                half3 wallCol = SAMPLE_LAYER(_WallMap, uvX).rgb * side.x + SAMPLE_LAYER(_WallMap, uvZ).rgb * side.y;

                half3 albedo = grass * (_AlbedoScale.x * wGrass) + soilCol * (_AlbedoScale.y * wSoil) + wallCol * (_AlbedoScale.z * wWall);

                // Normals: projection-aligned detail added to the geometric normal (UDN blend).
                half3 nG = UnpackLayerNormal(SAMPLE_LAYER(_GrassNormal, uvG));
                half3 nS = UnpackLayerNormal(SAMPLE_LAYER(_SoilNormal, top * _Tiling.y));
                half3 nWx = UnpackLayerNormal(SAMPLE_LAYER(_WallNormal, uvX));
                half3 nWz = UnpackLayerNormal(SAMPLE_LAYER(_WallNormal, uvZ));
                half detail = 1.0h - far * 0.7h;
                half3 n = geoN;
                n += half3(nG.x, 0.0h, nG.y) * (wGrass * detail);
                n += half3(nS.x, 0.0h, nS.y) * wSoil;
                // Side projections: the tangent x flips with the face direction (mirrored UVs), y stays up.
                n += half3(0.0h, nWx.y, nWx.x * sign(geoN.x)) * (wWall * side.x);
                n += half3(nWz.x * sign(geoN.z), nWz.y, 0.0h) * (wWall * side.y);
                half3 normalWS = normalize(n);

                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = V;
                inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
                float4 positionCS = TransformWorldToHClip(positionWS);
                inputData.fogCoord = ComputeFogFactor(positionCS.z);
                inputData.vertexLighting = half3(0.0h, 0.0h, 0.0h);
                inputData.bakedGI = SampleSHPixel(half3(0.0h, 0.0h, 0.0h), normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = 0.0h;
                surface.specular = half3(0.0h, 0.0h, 0.0h);
                // Rain (_Smoothness.w, set from the track wetness) makes every layer glossier.
                half dry = _Smoothness.x * wGrass + _Smoothness.y * wSoil + _Smoothness.z * wWall;
                surface.smoothness = lerp(dry, 0.7h, _Smoothness.w);
                surface.albedo *= lerp(1.0h, 0.65h, _Smoothness.w);
                surface.normalTS = half3(0.0h, 0.0h, 1.0h);
                surface.occlusion = lerp(1.0h, 0.75h, wWall);
                surface.alpha = 1.0h;

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, 1.0h);
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
            #pragma target 3.0
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            float4 ShadowVertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                positionCS = ApplyShadowClamping(positionCS);
                return positionCS;
            }

            half4 ShadowFragment() : SV_Target
            {
                return 0;
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
            #pragma vertex TerrainVertex
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
            #pragma vertex TerrainVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            half4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
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
