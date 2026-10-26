using System;

namespace Apex.Core
{
    public enum GraphicsTier
    {
        Mobile,
        Medium,
        High,
        Ultra,
    }

    /// <summary>Post-process anti-aliasing (maps 1:1 onto URP's AntialiasingMode).</summary>
    public enum PostAntialiasing
    {
        None,
        Fxaa,
        Smaa,
        Taa,
    }

    /// <summary>
    /// Render settings per graphics tier (masterplan section 5). Engine-independent so the
    /// values and their invariants are tested; the runtime copies them onto QualitySettings,
    /// the URP asset, the camera and the sun. Frame-time costs are not measured yet
    /// (Phase 7); the tiers follow the usual URP cost order: render scale and MSAA, shadow
    /// distance/cascades/resolution, SSAO, realtime reflection probe updates.
    /// </summary>
    public struct GraphicsPreset
    {
        public GraphicsTier tier;
        public float renderScale;
        /// <summary>1, 2, 4 or 8. URP disables TAA when MSAA is on, so TAA tiers use 1.</summary>
        public int msaa;
        public PostAntialiasing antialiasing;
        /// <summary>0 low .. 2 high (SMAA and TAA quality).</summary>
        public int antialiasingQuality;
        public float shadowDistance;
        public int shadowCascades;
        /// <summary>Cascade ends as fractions of the shadow distance (count − 1 used).</summary>
        public float split1, split2, split3;
        public int shadowResolution;
        /// <summary>Soft shadow filter quality 0 low .. 2 high.</summary>
        public int softShadowQuality;
        public bool ssao;
        /// <summary>Reflection probe cube size and how it updates.</summary>
        public int reflectionResolution;
        /// <summary>False: sky only. True: city, track and sky.</summary>
        public bool reflectionCapturesWorld;
        /// <summary>Seconds between re-captures around the camera; 0 = only on lighting changes.</summary>
        public float reflectionInterval;
        public float lodBias;
        public bool forceAnisotropic;
        public int maxAdditionalLights;
        public bool bloomHighQuality;
        /// <summary>Content defaults the tier sets when chosen (environment density 0..2, 4K road textures).</summary>
        public int environmentDetail;
        public bool ultraTextures;

        public const int TierCount = 4;

        public static GraphicsPreset For(GraphicsTier tier)
        {
            GraphicsPreset p;
            switch (tier)
            {
                case GraphicsTier.Mobile:
                    p = new GraphicsPreset
                    {
                        renderScale = 0.85f, msaa = 1, antialiasing = PostAntialiasing.Fxaa, antialiasingQuality = 0,
                        shadowDistance = 120f, shadowCascades = 2, shadowResolution = 1024, softShadowQuality = 0,
                        ssao = false, reflectionResolution = 64, reflectionCapturesWorld = false, reflectionInterval = 0f,
                        lodBias = 1f, forceAnisotropic = false, maxAdditionalLights = 2, bloomHighQuality = false,
                        environmentDetail = 0, ultraTextures = false,
                    };
                    break;
                case GraphicsTier.Medium:
                    p = new GraphicsPreset
                    {
                        renderScale = 1f, msaa = 1, antialiasing = PostAntialiasing.Smaa, antialiasingQuality = 1,
                        shadowDistance = 200f, shadowCascades = 3, shadowResolution = 2048, softShadowQuality = 1,
                        ssao = false, reflectionResolution = 128, reflectionCapturesWorld = true, reflectionInterval = 0f,
                        lodBias = 1.25f, forceAnisotropic = false, maxAdditionalLights = 4, bloomHighQuality = false,
                        environmentDetail = 1, ultraTextures = false,
                    };
                    break;
                case GraphicsTier.High:
                    p = new GraphicsPreset
                    {
                        renderScale = 1f, msaa = 4, antialiasing = PostAntialiasing.None, antialiasingQuality = 2,
                        shadowDistance = 320f, shadowCascades = 4, shadowResolution = 2048, softShadowQuality = 2,
                        ssao = true, reflectionResolution = 256, reflectionCapturesWorld = true, reflectionInterval = 4f,
                        lodBias = 1.6f, forceAnisotropic = true, maxAdditionalLights = 6, bloomHighQuality = true,
                        environmentDetail = 2, ultraTextures = false,
                    };
                    break;
                default:
                    // TAA also calms specular shimmer of the detailed 4K normal maps at grazing angles.
                    p = new GraphicsPreset
                    {
                        renderScale = 1f, msaa = 1, antialiasing = PostAntialiasing.Taa, antialiasingQuality = 2,
                        shadowDistance = 450f, shadowCascades = 4, shadowResolution = 4096, softShadowQuality = 2,
                        ssao = true, reflectionResolution = 512, reflectionCapturesWorld = true, reflectionInterval = 1.5f,
                        lodBias = 2.2f, forceAnisotropic = true, maxAdditionalLights = 8, bloomHighQuality = true,
                        environmentDetail = 2, ultraTextures = true,
                    };
                    tier = GraphicsTier.Ultra;
                    break;
            }
            p.tier = tier;
            var s = CascadeSplits(p.shadowCascades, 1f, p.shadowDistance, 0.75f);
            p.split1 = s[0];
            p.split2 = s.Length > 1 ? s[1] : 1f;
            p.split3 = s.Length > 2 ? s[2] : 1f;
            return p;
        }

        /// <summary>
        /// Practical split scheme (blend of logarithmic and uniform splits, Zhang et al.): returns the
        /// ends of the first count − 1 cascades as fractions of <paramref name="far"/>.
        /// </summary>
        public static float[] CascadeSplits(int count, float near, float far, float lambda)
        {
            count = Math.Max(1, Math.Min(4, count));
            var r = new float[Math.Max(1, count - 1)];
            if (count == 1) { r[0] = 1f; return r; }
            for (int i = 1; i < count; i++)
            {
                float f = i / (float)count;
                float log = near * MathF.Pow(far / near, f);
                float uni = near + (far - near) * f;
                r[i - 1] = (lambda * log + (1f - lambda) * uni) / far;
            }
            return r;
        }

        /// <summary>Tier when the player chose "Auto": phones and tablets get Mobile; desktops by video memory.</summary>
        public static GraphicsTier Auto(bool mobile, int graphicsMemoryMb)
        {
            if (mobile) return GraphicsTier.Mobile;
            if (graphicsMemoryMb >= 6000) return GraphicsTier.High;
            return GraphicsTier.Medium;
        }

        public static string Name(GraphicsTier t) => t == GraphicsTier.Mobile ? "Mobil" : t == GraphicsTier.Medium ? "Mittel" : t == GraphicsTier.High ? "Hoch" : "Ultra";
    }
}
