using Apex.Core;
using Apex.Vehicle;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Presentation of the placeholder car driven by the simulation: suspension
    /// links following the wheels, cockpit steering wheel with hands and arms,
    /// steering-wheel display and shift lights, rain light, brake-disc glow, tire
    /// compound colour and a little head movement. Runs in LateUpdate after the
    /// wheels were positioned by <see cref="VehicleController"/>; never touches physics.
    /// </summary>
    public sealed class CarVisuals : MonoBehaviour
    {
        public struct Link
        {
            public Transform transform;
            public Vector3 inner;
            public Transform outerSpace;
            public Vector3 outer;
            public float radius;
        }

        public VehicleController vehicle;
        public Link[] links = new Link[0];
        public Transform steeringWheel;
        public Quaternion steeringWheelBase = Quaternion.identity;
        public Transform helmet;
        public Renderer[] helmetRenderers = new Renderer[0];
        /// <summary>Arm segments: shoulder→elbow and elbow→glove, per side (0 left, 1 right).</summary>
        public Transform[] upperArms = new Transform[2];
        public Transform[] foreArms = new Transform[2];
        public Transform[] gloves = new Transform[2];
        public Vector3[] shoulders = new Vector3[2];
        public Renderer rainLight;
        public Renderer[] brakeDiscs = new Renderer[4];
        public Renderer[] compoundBands = new Renderer[0];
        public int[] compoundBandWheel = new int[0];
        public Renderer[] shiftLights = new Renderer[0];
        public TextMesh gearText;
        public TextMesh speedText;

        /// <summary>Full detail updates (display, arms) for the player's car.</summary>
        public bool detailed;
        bool firstPerson;

        readonly float[] brakeHeat = new float[4];
        readonly TireCompound[] shownCompound = { (TireCompound)(-1), (TireCompound)(-1), (TireCompound)(-1), (TireCompound)(-1) };
        MaterialPropertyBlock block;
        Vector3 lastVelocity;
        Vector2 headAccel;
        float displayTimer;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        /// <summary>Hides the helmet for the onboard camera (the camera sits inside it).</summary>
        public bool FirstPerson
        {
            get => firstPerson;
            set
            {
                if (firstPerson == value) return;
                firstPerson = value;
                foreach (var r in helmetRenderers) if (r != null) r.enabled = !value;
            }
        }

        public static Color CompoundColor(TireCompound c)
        {
            switch (c)
            {
                case TireCompound.Soft: return new Color(0.85f, 0.1f, 0.1f);
                case TireCompound.Medium: return new Color(0.95f, 0.8f, 0.1f);
                case TireCompound.Hard: return new Color(0.95f, 0.95f, 0.95f);
                case TireCompound.Intermediate: return new Color(0.15f, 0.75f, 0.2f);
                default: return new Color(0.1f, 0.35f, 0.9f);
            }
        }

        void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        void SetColor(Renderer r, Color baseColor, Color emission)
        {
            if (r == null) return;
            block.Clear();
            block.SetColor(BaseColorId, baseColor);
            block.SetColor(ColorId, baseColor);
            block.SetColor(EmissionId, emission);
            r.SetPropertyBlock(block);
        }

        void LateUpdate()
        {
            if (vehicle == null || vehicle.Sim == null) return;
            var sim = vehicle.Sim;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            UpdateLinks();
            UpdateCockpit(sim, dt);
            UpdateLights(sim, dt);
            UpdateCompound(sim);
        }

        void UpdateLinks()
        {
            var root = transform;
            foreach (var l in links)
            {
                if (l.transform == null || !l.transform.gameObject.activeInHierarchy) continue;
                Vector3 a = root.TransformPoint(l.inner);
                Vector3 b = l.outerSpace.TransformPoint(l.outer);
                Place(l.transform, a, b, l.radius);
            }
        }

        /// <summary>Stretches a unit tube (0,0,0)→(0,0,1) between two world points.</summary>
        static void Place(Transform t, Vector3 a, Vector3 b, float radius)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            t.position = a;
            t.rotation = Quaternion.LookRotation(d / len, Mathf.Abs(d.y / len) > 0.95f ? Vector3.forward : Vector3.up);
            // Parent scale is 1; local scale stretches the unit tube.
            t.localScale = new Vector3(radius, radius, len);
        }

        void UpdateCockpit(VehicleSimulation sim, float dt)
        {
            if (steeringWheel != null)
                steeringWheel.localRotation = steeringWheelBase * Quaternion.Euler(0f, 0f, -sim.SteeringWheelAngleDeg);

            // Head movement from body accelerations (lateral roll, longitudinal nod).
            var body = vehicle.Body;
            if (helmet != null && body != null)
            {
                Vector3 v = RigidbodyCompat.GetVelocity(body);
                Vector3 acc = (v - lastVelocity) / dt;
                lastVelocity = v;
                var target = new Vector2(Vector3.Dot(acc, transform.right), Vector3.Dot(acc, transform.forward));
                headAccel = Vector2.Lerp(headAccel, target, 1f - Mathf.Exp(-dt * 6f));
                float roll = Mathf.Clamp(headAccel.x / 9.81f * 3f, -9f, 9f);
                float nod = Mathf.Clamp(-headAccel.y / 9.81f * 2.5f, -8f, 8f);
                helmet.localRotation = Quaternion.Euler(nod, 0f, roll);
            }

            if (!detailed) return;
            for (int s = 0; s < 2; s++)
            {
                if (gloves[s] == null || upperArms[s] == null || foreArms[s] == null) continue;
                Vector3 shoulder = transform.TransformPoint(shoulders[s]);
                Vector3 hand = gloves[s].position;
                // Two-bone approximation: elbow out to the side and slightly down.
                Vector3 side = transform.right * (s == 0 ? -1f : 1f);
                Vector3 elbow = Vector3.Lerp(shoulder, hand, 0.5f) + side * 0.07f - transform.up * 0.06f;
                Place(upperArms[s], shoulder, elbow, 0.045f);
                Place(foreArms[s], elbow, hand, 0.038f);
            }

            displayTimer -= dt;
            if (displayTimer <= 0f)
            {
                displayTimer = 0.1f;
                if (gearText != null)
                    gearText.text = sim.gearbox.gear < 0 ? "R" : sim.gearbox.gear == 0 ? "N" : sim.gearbox.gear.ToString();
                if (speedText != null)
                    speedText.text = Mathf.RoundToInt(sim.SpeedKmh).ToString();
            }
            if (shiftLights.Length > 0)
            {
                float rpmFrac = Mathf.InverseLerp(sim.gearbox.upshiftRpm - 3000f, sim.gearbox.upshiftRpm, sim.EngineRpm);
                int lit = Mathf.RoundToInt(rpmFrac * shiftLights.Length);
                bool flash = sim.LimiterActive && Mathf.Repeat(Time.time * 8f, 1f) < 0.5f;
                for (int i = 0; i < shiftLights.Length; i++)
                {
                    float t = (float)i / Mathf.Max(1, shiftLights.Length - 1);
                    Color c = t < 0.4f ? new Color(0.1f, 1f, 0.2f) : t < 0.75f ? new Color(1f, 0.15f, 0.1f) : new Color(0.3f, 0.3f, 1f);
                    bool on = flash || (i < lit && !sim.LimiterActive);
                    SetColor(shiftLights[i], on ? c : new Color(0.05f, 0.05f, 0.05f), on ? c * 3f : Color.black);
                }
            }
        }

        void UpdateLights(VehicleSimulation sim, float dt)
        {
            if (rainLight != null)
            {
                // Rain light: steady in the wet, flashing on the pit limiter.
                bool limiter = !float.IsInfinity(sim.speedLimit);
                bool on = sim.trackWetness > 0.15f || (limiter && Mathf.Repeat(Time.time * 4f, 1f) < 0.5f);
                SetColor(rainLight, new Color(0.25f, 0.02f, 0.02f), on ? new Color(4f, 0.15f, 0.1f) : Color.black);
            }
            for (int i = 0; i < 4; i++)
            {
                var disc = brakeDiscs[i];
                if (disc == null) continue;
                var w = sim.wheels[i];
                // Brake power heats the disc; airflow cools it.
                float power = w.brakeTorque * Mathf.Abs(w.omega);
                brakeHeat[i] += power / 350000f * dt;
                brakeHeat[i] -= brakeHeat[i] * (0.25f + 0.012f * sim.Speed) * dt;
                brakeHeat[i] = Mathf.Clamp(brakeHeat[i], 0f, 2f);
                float glow = Mathf.Clamp01((brakeHeat[i] - 0.3f) / 0.9f);
                SetColor(disc, new Color(0.16f, 0.16f, 0.17f), new Color(1f, 0.35f, 0.05f) * (glow * glow * 3f));
            }
        }

        void UpdateCompound(VehicleSimulation sim)
        {
            // Recolour the sidewall bands only when a new set was fitted.
            for (int w = 0; w < 4; w++)
            {
                var c = sim.tires[w].compound;
                if (c == shownCompound[w]) continue;
                shownCompound[w] = c;
                for (int i = 0; i < compoundBands.Length; i++)
                    if (compoundBandWheel[i] == w) SetColor(compoundBands[i], CompoundColor(c), Color.black);
            }
        }
    }
}
