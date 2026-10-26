using System.Collections.Generic;
using Apex.Environment;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Real spot lights under the street lamps nearest ahead of the camera (<see cref="StreetLighting"/>), so the
    /// road, the cars and the barriers are lit at night instead of showing only glowing lamp heads. No shadows;
    /// the pool size follows the graphics tier (Mobile 8 .. Ultra 48). Off in daylight.
    /// </summary>
    public sealed class StreetLightPool : MonoBehaviour
    {
        public EnvironmentInstance env;
        public Transform cameraTransform;
        StreetLighting model;
        Light[] lights = new Light[0];
        readonly List<System.Numerics.Vector3> lamps = new List<System.Numerics.Vector3>();
        float lastLevel = -1f;

        public void Init(int poolSize)
        {
            foreach (var l in lights) if (l != null) Destroy(l.gameObject);
            model = new StreetLighting(poolSize);
            lights = new Light[poolSize];
            var c = StreetLighting.Color;
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("StreetLight" + i);
                go.transform.SetParent(transform, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                l.spotAngle = StreetLighting.OuterAngle;
                l.innerSpotAngle = StreetLighting.InnerAngle;
                l.range = StreetLighting.Range;
                l.color = new Color(c[0], c[1], c[2]);
                l.shadows = LightShadows.None;
                l.intensity = 0f;
                l.enabled = false;
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // straight down
                lights[i] = l;
            }
            lamps.Clear();
            if (env != null)
                foreach (var p in env.lampHeads) lamps.Add(new System.Numerics.Vector3(p.x, p.y, p.z));
        }

        void LateUpdate()
        {
            if (model == null || env == null || cameraTransform == null) return;
            float level = env.ArtificialLight;
            if (level < 0.02f)
            {
                if (lastLevel >= 0.02f) foreach (var l in lights) l.enabled = false;
                lastLevel = level;
                return;
            }
            lastLevel = level;
            var p = cameraTransform.position;
            var f = cameraTransform.forward;
            model.Update(lamps, new System.Numerics.Vector3(p.x, p.y, p.z), new System.Numerics.Vector3(f.x, f.y, f.z), Time.deltaTime);
            for (int i = 0; i < lights.Length; i++)
            {
                var slot = model.slots[i];
                var l = lights[i];
                bool on = slot.lamp >= 0 && slot.level > 0f;
                if (l.enabled != on) l.enabled = on;
                if (!on) continue;
                var q = lamps[slot.lamp];
                l.transform.position = new Vector3(q.X, q.Y, q.Z);
                l.intensity = StreetLighting.Intensity * level * slot.level;
            }
        }
    }
}
