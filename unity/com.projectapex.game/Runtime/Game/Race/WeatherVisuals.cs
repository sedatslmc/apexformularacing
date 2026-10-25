using System.Collections.Generic;
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

        ParticleSystem rain;
        ParticleSystem.EmissionModule rainEmission;
        readonly List<(Material mat, Color baseColor, float baseSmooth)> wetMaterials = new List<(Material, Color, float)>();
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
                    wetMaterials.Add((inst, col, sm));
                }
                mr.sharedMaterials = mats;
            }
            ApplyTimeOfDay();
            CreateRain();
        }

        void ApplyTimeOfDay()
        {
            if (sun == null) return;
            // Schematic layout: east is the Bosphorus. Golden hour = low western sun
            // lighting the European shore; morning = low eastern sun.
            switch (timeOfDay)
            {
                case TimeOfDay.Morning:
                    sun.transform.rotation = Quaternion.Euler(18f, 105f, 0f);
                    sunBaseColor = new Color(1f, 0.9f, 0.8f);
                    sunBaseIntensity = 1.1f;
                    break;
                case TimeOfDay.Day:
                    sun.transform.rotation = Quaternion.Euler(55f, 160f, 0f);
                    sunBaseColor = new Color(1f, 0.97f, 0.92f);
                    sunBaseIntensity = 1.35f;
                    break;
                case TimeOfDay.Dusk:
                    sun.transform.rotation = Quaternion.Euler(6f, 255f, 0f);
                    sunBaseColor = new Color(1f, 0.6f, 0.4f);
                    sunBaseIntensity = 0.75f;
                    break;
                default:
                    sun.transform.rotation = Quaternion.Euler(16f, 245f, 0f);
                    sunBaseColor = new Color(1f, 0.8f, 0.58f);
                    sunBaseIntensity = 1.2f;
                    break;
            }
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
            RenderSettings.ambientIntensity = Mathf.Lerp(1f, 0.75f, cloud);
            RenderSettings.fogColor = Color.Lerp(new Color(0.72f, 0.78f, 0.84f), new Color(0.55f, 0.58f, 0.62f), cloud);
            RenderSettings.fogStartDistance = Mathf.Lerp(300f, 60f, rainI);
            RenderSettings.fogEndDistance = Mathf.Lerp(3500f, 900f, rainI);
            if (rain != null && cameraTransform != null)
            {
                rain.transform.position = cameraTransform.position + Vector3.up * 14f + cameraTransform.forward * 8f;
                rainEmission.rateOverTime = rainI * 1400f;
            }
            foreach (var (mat, baseColor, baseSmooth) in wetMaterials)
            {
                // Wet asphalt: darker and much smoother (sky and lights reflect).
                var c = Color.Lerp(baseColor, baseColor * 0.55f, wet);
                c.a = 1f;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                float sm = Mathf.Lerp(baseSmooth, 0.88f, Mathf.Clamp01(wet * 1.4f));
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", sm);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", sm);
            }
        }
    }
}
