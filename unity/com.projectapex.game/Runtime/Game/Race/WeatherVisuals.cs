using System.Collections.Generic;
using Apex.Environment;
using Apex.Weather;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Presents the <see cref="WeatherModel"/>: sun angle and colour by time of day,
    /// light and ambient dimming with cloud cover, fog/visibility, rain particles
    /// around the camera and wet-looking track surfaces (higher smoothness, darker
    /// albedo). Cheap mobile techniques only: no planar reflections.
    /// </summary>
    public sealed class WeatherVisuals : MonoBehaviour
    {
        public WeatherModel weather;
        public TimeOfDay timeOfDay = TimeOfDay.GoldenHour;
        public Light sun;
        public Transform cameraTransform;
        /// <summary>True north in the game frame (deg); NaN for schematic circuits.</summary>
        public float trueNorthBearing = float.NaN;
        public EnvironmentInstance environment;
        /// <summary>Settings switch for the URP post-processing volume (applied live).</summary>
        public bool postProcessing = true;
        SunModel.Light light;
        PostFxStack postFx;

        ParticleSystem rain;
        ParticleSystem.EmissionModule rainEmission;
        /// <summary>Wet-able materials: base colour and smoothness, and the mean of a scanned smoothness map (-1 = none).</summary>
        readonly List<(Material mat, Color baseColor, float baseSmooth, float glossMean)> wetMaterials = new List<(Material, Color, float, float)>();
        float sunBaseIntensity = 1.25f;
        Color sunBaseColor = Color.white;

        /// <summary>Instances the road/kerb/pit materials of the circuit so they can look wet.</summary>
        public void Bind(GameObject circuitRoot)
        {
            foreach (var mr in circuitRoot.GetComponentsInChildren<MeshRenderer>())
            {
                string n = mr.gameObject.name;
                if (n != "Road" && n != "Kerbs" && n != "PitLane" && n != "PitApron") continue;
                var mats = mr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var inst = new Material(mats[i]);
                    mats[i] = inst;
                    var col = inst.HasProperty("_BaseColor") ? inst.GetColor("_BaseColor") : inst.HasProperty("_Color") ? inst.GetColor("_Color") : Color.gray;
                    float sm = inst.HasProperty("_Smoothness") ? inst.GetFloat("_Smoothness") : inst.HasProperty("_Glossiness") ? inst.GetFloat("_Glossiness") : 0.2f;
                    float glossMean = -1f;
                    if (SurfaceTextures.HasGlossMap(inst))
                    {
                        var info = SurfaceTextures.Load(n == "Kerbs" ? SurfaceTextures.Kerb : SurfaceTextures.Asphalt);
                        glossMean = info != null && info.meanSmoothness >= 0f ? info.meanSmoothness : 0.3f;
                    }
                    wetMaterials.Add((inst, col, sm, glossMean));
                }
                mr.sharedMaterials = mats;
            }
            ApplyTimeOfDay();
            CreateRain();
            var fx = new GameObject("PostFx");
            fx.transform.SetParent(transform, false);
            postFx = fx.AddComponent<PostFxStack>();
            postFx.Init(cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null, postProcessing);
        }

        void ApplyTimeOfDay()
        {
            light = SunModel.Get(timeOfDay);
            if (sun != null)
            {
                // The light shines away from the source: yaw = source azimuth + 180°.
                float az = SunModel.GameAzimuth(light, trueNorthBearing);
                sun.transform.rotation = Quaternion.Euler(light.elevation, az + 180f, 0f);
                sunBaseColor = new Color(light.r, light.g, light.b);
                sunBaseIntensity = light.intensity;
                RenderSettings.sun = sun;
            }
            var sky = RenderSettings.skybox;
            if (sky != null && sky.HasProperty("_Exposure"))
                sky.SetFloat("_Exposure", timeOfDay == TimeOfDay.Night ? 0.12f : timeOfDay == TimeOfDay.Dusk ? 0.7f : 1.3f);
            if (environment != null) environment.SetArtificialLight(light.artificial);
        }

        void CreateRain()
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            rain = go.AddComponent<ParticleSystem>();
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = rain.main;
            main.loop = true;
            main.startLifetime = 0.9f;
            main.startSpeed = 22f;
            main.startSize = 0.035f;
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.8f, 0.85f, 0.95f, 0.45f);
            var shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 40f, 1f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rainEmission = rain.emission;
            rainEmission.rateOverTime = 0f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 6f;
            var mat = Resources.Load<Material>(ApexMaterials.ResourceFolder + "/Rain");
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null) mat = new Material(shader);
            }
            if (mat != null) renderer.sharedMaterial = mat;
            rain.Play();
        }

        void LateUpdate()
        {
            if (weather == null) return;
            float cloud = weather.CloudCover;
            float rainI = weather.RainIntensity;
            float wet = weather.TrackWetness;
            if (sun != null)
            {
                sun.intensity = sunBaseIntensity * Mathf.Lerp(1f, 0.35f, cloud);
                sun.color = Color.Lerp(sunBaseColor, new Color(0.8f, 0.85f, 0.9f), cloud * 0.8f);
                sun.shadowStrength = Mathf.Lerp(0.9f, 0.3f, cloud);
            }
            RenderSettings.ambientIntensity = Mathf.Lerp(1f, 0.75f, cloud) * light.ambient;
            var clearFog = timeOfDay == TimeOfDay.Night ? new Color(0.05f, 0.07f, 0.11f)
                : timeOfDay == TimeOfDay.Dusk ? new Color(0.66f, 0.56f, 0.52f)
                : timeOfDay == TimeOfDay.GoldenHour ? new Color(0.78f, 0.74f, 0.68f) : new Color(0.72f, 0.78f, 0.84f);
            RenderSettings.fogColor = Color.Lerp(clearFog, clearFog * 0.75f + new Color(0.14f, 0.15f, 0.16f), cloud);
            RenderSettings.fogStartDistance = Mathf.Lerp(600f, 60f, rainI);
            RenderSettings.fogEndDistance = Mathf.Lerp(environment != null ? 8000f : 3500f, 900f, rainI);
            if (postFx != null)
            {
                postFx.SetEnabled(postProcessing);
                postFx.Apply(ColorGrading.Get(timeOfDay, cloud, rainI));
            }
            if (environment != null)
            {
                environment.SetWetness(wet);
                // Heavy overcast and rain switch some lights on early.
                environment.SetArtificialLight(Mathf.Max(light.artificial, cloud * 0.35f));
            }
            if (rain != null && cameraTransform != null)
            {
                rain.transform.position = cameraTransform.position + Vector3.up * 14f + cameraTransform.forward * 8f;
                rainEmission.rateOverTime = rainI * 1400f;
            }
            foreach (var (mat, baseColor, baseSmooth, glossMean) in wetMaterials)
            {
                // Wet asphalt: darker and much smoother (sky and lights reflect).
                var c = Color.Lerp(baseColor, baseColor * 0.55f, wet);
                c.a = 1f;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                float sm = Mathf.Lerp(baseSmooth, 0.88f, Mathf.Clamp01(wet * 1.4f));
                if (glossMean >= 0f)
                {
                    // Scanned smoothness map: _Smoothness scales the map and cannot raise it to a wet film.
                    // Dry: the map as scanned; wet: a uniform wet smoothness from the map's mean upwards.
                    bool dry = wet < 0.02f;
                    if (dry) mat.EnableKeyword("_METALLICSPECGLOSSMAP"); else mat.DisableKeyword("_METALLICSPECGLOSSMAP");
                    sm = dry ? 1f : Mathf.Lerp(glossMean, 0.88f, Mathf.Clamp01(wet * 1.4f));
                }
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", sm);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", sm);
            }
        }
    }
}
