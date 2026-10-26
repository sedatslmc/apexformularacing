using System;
using System.Numerics;
using Apex.Vehicle;

namespace Apex.Tracks
{
    /// <summary>
    /// How sharply the road may bend vertically at a given speed, derived from the vehicle:
    /// body collider (<see cref="BodyCollider"/>, bottom on the floor line at rest), axle positions, heave stiffness
    /// and the steady-state aero compression of <see cref="VehicleSimulation"/> over speed.
    /// <list type="bullet">
    /// <item>Crest (road convex, curvature κ &gt; 0): the road rises above the chord between the
    /// axles by κ/2·(zf−z)(z−zr) and must stay below the box; the centripetal relief m·v²·κ
    /// lifts the body. The wheels must keep at least (1 − <see cref="LiftReserve"/>) of their load:
    /// v²·κ ≤ LiftReserve·(g + DF/m).</item>
    /// <item>Sag (concave, κ &lt; 0): beyond the axles the road rises by |κ|/2·(z−zf)(z−zr) towards
    /// the box ends while the extra load m·v²·|κ| compresses the springs.</item>
    /// </list>
    /// Margin: <see cref="Margin"/> to the box at every point. Curvature in 1/m.
    /// </summary>
    public sealed class VerticalLimits
    {
        public const float Margin = 0.005f;
        public const float LiftReserve = 0.5f;
        /// <summary>Clearance the road may cost where the flat-road clearance is already below the margin.</summary>
        public const float FlatRoadAllowance = 0.001f;
        public const float SpeedStep = 2.5f;
        public const float MaxSpeed = 100f;

        public readonly VehicleSetup setup;
        public readonly BodyCollider box;
        public readonly float staticClearance, zFront, zRear, boxFront, boxRear, heaveStiffness;
        // Per speed bin: compression of front/rear axle versus rest (m) and total downforce (N).
        readonly float[] dzFront, dzRear, downforce;
        /// <summary>Full-throttle acceleration on flat ground per speed bin (m/s²), measured with the vehicle model.</summary>
        readonly float[] acceleration;
        /// <summary>Top speed on flat ground (m/s), measured.</summary>
        public readonly float topSpeed;

        public VerticalLimits(VehicleSetup s)
        {
            setup = s;
            box = new BodyCollider(s);
            staticClearance = box.StaticClearance(0f);
            zFront = s.FrontAxleZ;
            zRear = s.RearAxleZ;
            boxFront = box.FrontZ;
            boxRear = box.RearZ;
            heaveStiffness = 2f * (s.springFront + s.springRear);
            int n = (int)(MaxSpeed / SpeedStep) + 1;
            dzFront = new float[n];
            dzRear = new float[n];
            downforce = new float[n];
            // Full-throttle run on flat ground: acceleration over speed and the top speed.
            acceleration = new float[n];
            {
                var sim = new VehicleSimulation(s) { assistTraction = true, assistSteering = false, speedSensitiveSteering = false };
                var rig = new FlatGroundRig(sim);
                rig.Place(Vector3.Zero, Vector3.UnitZ);
                var sum = new float[n]; var cnt = new int[n];
                float prev = 0f;
                for (int i = 0; i < 120 * 90; i++)
                {
                    sim.throttle = 1f;
                    rig.Step(1f / 120f);
                    float a = (rig.u - prev) * 120f;
                    prev = rig.u;
                    int b = Math.Min(n - 1, (int)MathF.Round(rig.u / SpeedStep));
                    sum[b] += a; cnt[b]++;
                }
                topSpeed = rig.u;
                for (int k = 0; k < n; k++) acceleration[k] = cnt[k] > 0 ? sum[k] / cnt[k] : 0f;
                // Bins the run did not reach (above top speed): no acceleration; gaps: neighbours.
                for (int k = 1; k < n; k++) if (cnt[k] == 0 && k * SpeedStep < topSpeed) acceleration[k] = acceleration[k - 1];
            }
            for (int k = 0; k < n; k++)
            {
                float v = k * SpeedStep;
                var sim = new VehicleSimulation(s) { assistTraction = true, assistSteering = false, speedSensitiveSteering = false };
                var rig = new FlatGroundRig(sim);
                rig.Place(Vector3.Zero, Vector3.UnitZ);
                if (v > 0f) rig.SetSpeed(v, 8);
                for (int i = 0; i < 240; i++)
                {
                    rig.u = v;
                    rig.v = 0f;
                    rig.yawRate = 0f;
                    sim.throttle = 0.3f;
                    rig.Step(1f / 120f);
                }
                dzFront[k] = s.rideHeightFront - sim.RideHeightFront;
                dzRear[k] = s.rideHeightRear - sim.RideHeightRear;
                downforce[k] = sim.DownforceFront + sim.DownforceRear;
            }
        }

        /// <summary>Full-throttle acceleration (m/s²) on flat ground at speed v.</summary>
        public float Acceleration(float v) => MathF.Max(0f, Lerp(acceleration, v));

        /// <summary>
        /// Speed a car can actually reach at each sample: the racing line's corner and braking limits, and on
        /// the way out the measured full-throttle acceleration of the vehicle minus the grade. Then the
        /// fastest speed within ±60 m and 5 % reserve, because the limits must hold for every car passing.
        /// </summary>
        public float[] DesignSpeeds(TrackGeometry g, float[] lineSpeed)
        {
            int n = g.Count;
            var v = (float[])lineSpeed.Clone();
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    float a = Acceleration(v[i]) - 9.81f * g.GradeAt(g.Distances[i]);
                    v[j] = MathF.Min(v[j], MathF.Sqrt(MathF.Max(0f, v[i] * v[i] + 2f * MathF.Max(0.2f, a) * g.Spacing)));
                }
            var outV = new float[n];
            int win = (int)MathF.Round(60f / g.Spacing);
            for (int i = 0; i < n; i++)
            {
                float m = 0f;
                for (int o = -win; o <= win; o++) m = MathF.Max(m, v[((i + o) % n + n) % n]);
                outV[i] = MathF.Min(m * 1.05f, topSpeed);
            }
            return outV;
        }

        float Lerp(float[] a, float v)
        {
            float x = Math.Clamp(v, 0f, MaxSpeed) / SpeedStep;
            int i = Math.Min((int)x, a.Length - 2);
            float t = x - i;
            return a[i] + (a[i + 1] - a[i]) * t;
        }

        public float Downforce(float v) => Lerp(downforce, v);

        /// <summary>Box clearance (m) at body position z on a flat road at speed v (aero compression and pitch).</summary>
        public float FlatClearance(float z, float v)
        {
            float f = Lerp(dzFront, v), r = Lerp(dzRear, v);
            float t = (z - zRear) / (zFront - zRear);
            return box.StaticClearance(z) - (r + (f - r) * t);
        }

        /// <summary>Smallest clearance (m) along the box on a road of vertical curvature κ (convex &gt; 0) at speed v.</summary>
        public float Clearance(float kappa, float v)
        {
            float relief = setup.mass * v * v * kappa / heaveStiffness;   // + lifts the body on a crest
            float best = float.PositiveInfinity;
            for (int i = 0; i <= 26; i++)
            {
                float z = boxRear + (boxFront - boxRear) * i / 26f;
                // Road height above the chord through the two contact points.
                float rise = 0.5f * kappa * (zFront - z) * (z - zRear);
                best = MathF.Min(best, FlatClearance(z, v) + relief - rise);
            }
            return best;
        }

        /// <summary>Largest convex (crest) curvature allowed at speed v.</summary>
        public float MaxCrest(float v)
        {
            float lift = LiftReserve * (9.81f + Downforce(v) / setup.mass) / MathF.Max(v * v, 1f);
            return MathF.Min(lift, Solve(v, +1));
        }

        /// <summary>Largest concave (sag) curvature magnitude allowed at speed v.</summary>
        public float MaxSag(float v) => Solve(v, -1);

        /// <summary>
        /// Curvature at which the clearance reaches the target (sign +1 crest, -1 sag). The target is
        /// <see cref="Margin"/>; where the car already runs closer than that on a flat road (aero
        /// compression near top speed, a property of the vehicle) the road may cost at most
        /// <see cref="FlatRoadAllowance"/> more.
        /// </summary>
        float Solve(float v, int sign)
        {
            float lo = 0f, hi = 0.2f;
            float target = MathF.Min(Margin, Clearance(0f, v) - FlatRoadAllowance);
            bool Ok(float k) => Clearance(sign * k, v) > target;
            if (Ok(hi)) return hi;
            for (int it = 0; it < 40; it++)
            {
                float mid = 0.5f * (lo + hi);
                if (Ok(mid)) lo = mid; else hi = mid;
            }
            return lo;
        }
    }
}
