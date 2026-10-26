using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Core;
using Apex.Tracks;

namespace Apex.Vehicle
{
    /// <summary>
    /// Measurable handling baseline on the 6-DOF <see cref="SurfaceRig"/> over a flat test pad
    /// (same force code as the in-game VehicleController, with roll, pitch and real suspension):
    /// cornering capacity and axle balance at the limit, steering response (step and 1 Hz sine),
    /// braking and traction. Inputs are analogue unless stated; assists follow the driving
    /// profile (0 = Accessible, 1 = Balanced, 2 = Advanced, as SettingsData).
    /// </summary>
    public sealed class HandlingBenchmark
    {
        public const float Dt = 1f / 120f;
        static CollisionWorld pad;

        /// <summary>Flat asphalt pad, 6 × 6 km, centred on the origin.</summary>
        public static CollisionWorld Pad()
        {
            if (pad != null) return pad;
            var m = new SurfaceMesh("Road", 1, SurfaceType.Asphalt);
            const float half = 3000f, cell = 100f;
            for (float z = -half; z < half; z += cell)
                for (float x = -half; x < half; x += cell)
                    m.Quad(0, new Vector3(x, 0f, z), new Vector3(x + cell, 0f, z), new Vector3(x, 0f, z + cell), new Vector3(x + cell, 0f, z + cell),
                        Vector2.Zero, Vector2.UnitX, Vector2.UnitY, Vector2.One);
            pad = new CollisionWorld(new[] { m });
            return pad;
        }

        public readonly VehicleSetup setup;
        public readonly int profile;

        public HandlingBenchmark(VehicleSetup setup = null, int profile = 1)
        {
            this.setup = setup ?? new VehicleSetup();
            this.profile = profile;
        }

        public SurfaceRig NewRig(float kmh, out VehicleSimulation sim)
        {
            sim = new VehicleSimulation(setup);
            switch (profile)
            {
                case 0: sim.tractionLevel = 3; sim.steeringAssist = 1f; break;
                case 2: sim.tractionLevel = 1; sim.steeringAssist = 0f; break;
                default: sim.tractionLevel = 2; sim.steeringAssist = 0.5f; break;
            }
            sim.speedSensitiveSteering = true;
            var rig = new SurfaceRig(sim, Pad());
            rig.Place(new Vector3(-2500f, 0f, -2500f), Vector3.UnitZ);
            // Settle on the springs before setting speed.
            sim.holdBrake = true;
            for (int i = 0; i < 60; i++) rig.Step(Dt);
            sim.holdBrake = false;
            if (kmh > 0f) rig.SetSpeed(kmh / 3.6f, GearFor(kmh));
            return rig;
        }

        public int GearFor(float kmh)
        {
            int g = 1;
            while (g < 8 && kmh / 3.6f / setup.wheelRadiusRear * setup.gearbox.RatioFor(g) * 60f / ApexMath.Tau > setup.engine.limiterRpm * 0.92f) g++;
            return g;
        }

        /// <summary>Throttle/brake that hold a target speed (P controller on the pedals).</summary>
        static void HoldSpeed(VehicleSimulation sim, float v, float target)
        {
            float e = target - v;
            sim.throttle = ApexMath.Clamp(0.15f + e * 0.4f, 0f, 1f);
            sim.brake = e < -1.5f ? ApexMath.Clamp(-e * 0.1f, 0f, 0.4f) : 0f;
        }

        static float Yaw(SurfaceRig r) => Vector3.Dot(r.angularVelocity, r.Up);
        static float Beta(SurfaceRig r)
        {
            float u = Vector3.Dot(r.velocity, r.Forward), v = Vector3.Dot(r.velocity, r.Right);
            return MathF.Atan2(v, MathF.Max(MathF.Abs(u), 0.5f));
        }

        public struct Capacity
        {
            public float kmh, maxLateralG, frontUse, rearUse, steerDeg, betaDeg;
            /// <summary>Front over rear normalised slip at the limit: &gt; 1 understeer, &lt; 1 oversteer.</summary>
            public float Balance => frontUse / MathF.Max(rearUse, 1e-3f);
        }

        /// <summary>
        /// Constant speed, steering input ramped slowly (0.12/s) from 0 to full: the highest
        /// sustained lateral acceleration and the front/rear slip use (slip / peak slip) there.
        /// </summary>
        public Capacity CorneringCapacity(float kmh)
        {
            var r = NewRig(kmh, out var sim);
            float v0 = kmh / 3.6f;
            var cap = new Capacity { kmh = kmh };
            float best = 0f;
            var hist = new Queue<float>();
            for (int i = 0; i < 120 * 12; i++)
            {
                sim.steer = MathF.Min(1f, i * Dt * 0.12f);
                HoldSpeed(sim, r.ForwardSpeed, v0);
                r.Step(Dt);
                float ay = MathF.Abs(r.ForwardSpeed * Yaw(r));
                hist.Enqueue(ay);
                if (hist.Count > 30) hist.Dequeue();
                float mean = 0f;
                foreach (var x in hist) mean += x;
                mean /= hist.Count;
                if (MathF.Abs(Beta(r)) > 0.15f) break;   // the car let go
                if (mean > best)
                {
                    best = mean;
                    cap.maxLateralG = mean / 9.81f;
                    cap.frontUse = 0.5f * (MathF.Abs(sim.wheels[0].slipAngle) + MathF.Abs(sim.wheels[1].slipAngle)) / sim.tireFront.slipAnglePeak;
                    cap.rearUse = 0.5f * (MathF.Abs(sim.wheels[2].slipAngle) + MathF.Abs(sim.wheels[3].slipAngle)) / sim.tireRear.slipAnglePeak;
                    cap.steerDeg = MathF.Abs(sim.SteerAngle) * ApexMath.Rad2Deg;
                    cap.betaDeg = MathF.Abs(Beta(r)) * ApexMath.Rad2Deg;
                }
            }
            return cap;
        }

        public struct Response
        {
            public float kmh, t63ms, overshootPct, steadyYawDegS, steadyLateralG;
            /// <summary>Yaw-rate lag behind a 1 Hz sine steer (ms) and gain (yaw °/s per ° of road-wheel angle).</summary>
            public float sineLagMs, sineGain;
        }

        /// <summary>Steering response: step input to `fraction` of the available lock, and a 1 Hz sine of the same amplitude.</summary>
        public Response SteeringResponse(float kmh, float fraction = 0.25f)
        {
            var res = new Response { kmh = kmh };
            float v0 = kmh / 3.6f;
            {
                var r = NewRig(kmh, out var sim);
                var yaw = new List<float>();
                for (int i = 0; i < 120 * 3; i++)
                {
                    sim.steer = fraction;
                    HoldSpeed(sim, r.ForwardSpeed, v0);
                    r.Step(Dt);
                    yaw.Add(Yaw(r));
                }
                float steady = 0f;
                for (int i = yaw.Count - 60; i < yaw.Count; i++) steady += yaw[i] / 60f;
                int t63 = yaw.FindIndex(y => MathF.Abs(y) >= 0.63f * MathF.Abs(steady));
                float peak = 0f;
                foreach (var y in yaw) peak = MathF.Max(peak, MathF.Abs(y));
                res.t63ms = (t63 + 1) * Dt * 1000f;
                res.overshootPct = (peak / MathF.Max(MathF.Abs(steady), 1e-4f) - 1f) * 100f;
                res.steadyYawDegS = MathF.Abs(steady) * ApexMath.Rad2Deg;
                res.steadyLateralG = MathF.Abs(steady) * v0 / 9.81f;
            }
            {
                var r = NewRig(kmh, out var sim);
                const float f = 1f;
                float sumS = 0f, sumYS = 0f, sumYC = 0f, sumAngS = 0f, sumAngC = 0f;
                int n = 0;
                for (int i = 0; i < 120 * 6; i++)
                {
                    float t = i * Dt;
                    sim.steer = fraction * MathF.Sin(ApexMath.Tau * f * t);
                    HoldSpeed(sim, r.ForwardSpeed, v0);
                    r.Step(Dt);
                    if (t < 2f) continue;   // let it settle
                    float s = MathF.Sin(ApexMath.Tau * f * t), c = MathF.Cos(ApexMath.Tau * f * t);
                    float y = Yaw(r);
                    sumYS += y * s; sumYC += y * c;
                    sumAngS += sim.SteerAngle * s; sumAngC += sim.SteerAngle * c;
                    sumS += s * s; n++;
                }
                // Phase of yaw against the steering command, and gain against the road-wheel angle.
                float phase = MathF.Atan2(sumYC, sumYS);
                res.sineLagMs = -phase / (ApexMath.Tau * f) * 1000f;
                float yawAmp = MathF.Sqrt(sumYS * sumYS + sumYC * sumYC);
                float angAmp = MathF.Sqrt(sumAngS * sumAngS + sumAngC * sumAngC);
                res.sineGain = yawAmp / MathF.Max(angAmp, 1e-6f);
            }
            return res;
        }

        public struct Limit { public float kmh, maxBetaDeg, lateralG; public bool spun; }

        /// <summary>Full steering input as a step at constant speed (the limit with the profile's lock): does the car stay on its tyres?</summary>
        public Limit LimitStep(float kmh)
        {
            var r = NewRig(kmh, out var sim);
            var l = new Limit { kmh = kmh };
            float v0 = kmh / 3.6f;
            float ay = 0f;
            for (int i = 0; i < 120 * 3; i++)
            {
                sim.steer = 1f;
                HoldSpeed(sim, r.ForwardSpeed, v0);
                r.Step(Dt);
                float b = MathF.Abs(Beta(r)) * ApexMath.Rad2Deg;
                l.maxBetaDeg = MathF.Max(l.maxBetaDeg, b);
                if (b > 15f) { l.spun = true; break; }
                if (i > 240) ay += MathF.Abs(r.ForwardSpeed * Yaw(r)) / 120f;
            }
            l.lateralG = ay / 9.81f;
            return l;
        }

        public struct Transient { public float kmh, maxBetaDeg; public bool spun; public string manoeuvre; }

        /// <summary>
        /// Trail braking: settle in a corner at `steer` input, then full brake while holding the steering
        /// for 2 s. A stable car slows down and may push wide; an unstable one swaps ends.
        /// </summary>
        public Transient TrailBrake(float kmh, float steer = 0.6f)
        {
            var r = NewRig(kmh, out var sim);
            var t = new Transient { kmh = kmh, manoeuvre = "trail brake" };
            float v0 = kmh / 3.6f;
            for (int i = 0; i < 120 * 3; i++)
            {
                sim.steer = steer;
                bool braking = i >= 120;
                if (braking) { sim.throttle = 0f; sim.brake = 1f; }
                else HoldSpeed(sim, r.ForwardSpeed, v0);
                r.Step(Dt);
                if (r.ForwardSpeed < 8f) break;
                float b = MathF.Abs(Beta(r)) * ApexMath.Rad2Deg;
                t.maxBetaDeg = MathF.Max(t.maxBetaDeg, b);
                if (b > 15f) { t.spun = true; break; }
            }
            return t;
        }

        /// <summary>Corner exit: settle at `steer` input at low speed, then full throttle for 2.5 s.</summary>
        public Transient PowerExit(float kmh, float steer = 0.7f)
        {
            var r = NewRig(kmh, out var sim);
            var t = new Transient { kmh = kmh, manoeuvre = "power exit" };
            float v0 = kmh / 3.6f;
            for (int i = 0; i < 120 * 3; i++)
            {
                sim.steer = steer;
                if (i >= 60) { sim.throttle = 1f; sim.brake = 0f; }
                else HoldSpeed(sim, r.ForwardSpeed, v0);
                r.Step(Dt);
                float b = MathF.Abs(Beta(r)) * ApexMath.Rad2Deg;
                t.maxBetaDeg = MathF.Max(t.maxBetaDeg, b);
                if (b > 15f) { t.spun = true; break; }
            }
            return t;
        }

        public struct Braking { public float fromKmh, toKmh, distance, time, meanG, peakG; }

        public Braking Brake(float fromKmh, float toKmh)
        {
            var r = NewRig(fromKmh, out var sim);
            var b = new Braking { fromKmh = fromKmh, toKmh = toKmh };
            float prev = r.ForwardSpeed;
            for (int i = 0; i < 120 * 15 && r.ForwardSpeed > toKmh / 3.6f; i++)
            {
                sim.brake = 1f;
                sim.throttle = 0f;
                sim.steer = 0f;
                r.Step(Dt);
                b.distance += r.ForwardSpeed * Dt;
                b.time += Dt;
                b.peakG = MathF.Max(b.peakG, (prev - r.ForwardSpeed) / Dt / 9.81f);
                prev = r.ForwardSpeed;
            }
            b.meanG = (fromKmh - toKmh) / 3.6f / MathF.Max(b.time, 1e-3f) / 9.81f;
            return b;
        }

        public struct Traction { public float fromKmh, toKmh, time, tcShare; }

        public Traction Accelerate(float fromKmh, float toKmh)
        {
            var r = NewRig(fromKmh, out var sim);
            var t = new Traction { fromKmh = fromKmh, toKmh = toKmh };
            int tc = 0, steps = 0;
            for (int i = 0; i < 120 * 30 && r.ForwardSpeed < toKmh / 3.6f; i++)
            {
                sim.throttle = 1f;
                sim.brake = 0f;
                sim.steer = 0f;
                r.Step(Dt);
                t.time += Dt;
                steps++;
                if (sim.TcActive) tc++;
            }
            t.tcShare = steps > 0 ? tc / (float)steps : 0f;
            return t;
        }

        /// <summary>Compact report of all measurements.</summary>
        public string Report()
        {
            var sb = new System.Text.StringBuilder();
            string[] names = { "Accessible", "Balanced", "Advanced" };
            sb.AppendLine($"profile {names[Math.Clamp(profile, 0, 2)]}");
            foreach (float kmh in new[] { 80f, 150f, 250f })
            {
                var c = CorneringCapacity(kmh);
                sb.AppendLine($"  cornering {kmh,3:F0} km/h: max lateral {c.maxLateralG:F2} g, balance front/rear slip use {c.frontUse:F2}/{c.rearUse:F2} (= {c.Balance:F2}), road-wheel angle {c.steerDeg:F1}°, slip angle {c.betaDeg:F1}°");
            }
            foreach (float kmh in new[] { 80f, 150f, 250f })
            {
                var s = SteeringResponse(kmh);
                var lim = LimitStep(kmh);
                sb.AppendLine($"  limit step {kmh,3:F0} km/h (100 % input): {(lim.spun ? "SPIN" : "stable")}, max slip angle {lim.maxBetaDeg:F1}°, {lim.lateralG:F2} g");
                sb.AppendLine($"  steering {kmh,3:F0} km/h (25 % input): t63 {s.t63ms:F0} ms, overshoot {s.overshootPct:F0} %, steady {s.steadyYawDegS:F1} °/s / {s.steadyLateralG:F2} g; 1 Hz sine: yaw lag {s.sineLagMs:F0} ms, gain {s.sineGain:F2}");
            }
            foreach (float kmh in new[] { 120f, 200f, 260f })
            {
                var tb = TrailBrake(kmh);
                sb.AppendLine($"  trail brake {kmh,3:F0} km/h (60 % steer + full brake): {(tb.spun ? "SPIN" : "stable")}, max slip angle {tb.maxBetaDeg:F1}°");
            }
            foreach (float kmh in new[] { 60f, 100f })
            {
                var pe = PowerExit(kmh);
                sb.AppendLine($"  power exit {kmh,3:F0} km/h (70 % steer + full throttle): {(pe.spun ? "SPIN" : "stable")}, max slip angle {pe.maxBetaDeg:F1}°");
            }
            foreach (var (a, b) in new[] { (300f, 80f), (200f, 60f), (100f, 0.5f) })
            {
                var br = Brake(a, b);
                sb.AppendLine($"  braking {a:F0}->{b:F0} km/h: {br.distance:F0} m, {br.time:F2} s, mean {br.meanG:F2} g, peak {br.peakG:F2} g");
            }
            foreach (var (a, b) in new[] { (0.5f, 100f), (0.5f, 200f), (60f, 200f) })
            {
                var t = Accelerate(a, b);
                sb.AppendLine($"  traction {a:F0}->{b:F0} km/h: {t.time:F2} s (TC active {t.tcShare * 100f:F0} % of the time)");
            }
            return sb.ToString();
        }
    }
}
