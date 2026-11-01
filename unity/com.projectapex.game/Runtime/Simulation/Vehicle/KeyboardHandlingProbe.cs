using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Core;
using Apex.InputLogic;

namespace Apex.Vehicle
{
    /// <summary>
    /// Handling probes driven through the real keyboard path (docs/HANDLING_KEYBOARD.md): digital keys → steering
    /// ramp (<see cref="KeyboardSteering"/>) → VehicleSimulation on the 6-DOF pad (<see cref="HandlingBenchmark"/>).
    /// Measures what a keyboard player feels: how tight the car turns with a key held, whether the front
    /// tyres are pushed past their grip peak (scrubbing understeer), how the rear reacts to taps, quick left-right
    /// changes, lifting and trail braking at speed. Force use is the tyre curve value of the combined slip
    /// (1 = at the grip peak, &gt; 1 = past it); slip use alone overstates understeer because the curve is flat
    /// near the peak.
    /// </summary>
    public sealed class KeyboardHandlingProbe
    {
        public const float Dt = HandlingBenchmark.Dt;
        readonly HandlingBenchmark bench;
        public readonly KeyboardSteering.Profile keyboard;

        public KeyboardHandlingProbe(VehicleSetup setup = null, int assistProfile = 1, KeyboardSteering.Profile keyboard = KeyboardSteering.Profile.Balanced)
        {
            bench = new HandlingBenchmark(setup, assistProfile);
            this.keyboard = keyboard;
        }

        public HandlingBenchmark Bench => bench;

        public struct Result
        {
            public float kmh;
            /// <summary>Highest front / rear combined slip relative to the peak during the manoeuvre.</summary>
            public float frontSlipMax, rearSlipMax;
            /// <summary>Front and rear slip at the end of a steady hold.</summary>
            public float frontSlipEnd, rearSlipEnd;
            public float lateralG, radius, maxBetaDeg, yawOvershootPct, endSpeedKmh, timeToHalfYawMs;
            public bool spun;
        }

        static float AxleSlip(VehicleSimulation sim, int a, int b)
        {
            var t = a < 2 ? sim.tireFront : sim.tireRear;
            return 0.5f * (t.CombinedSlip(sim.wheels[a].slipRatio, sim.wheels[a].slipAngle) + t.CombinedSlip(sim.wheels[b].slipRatio, sim.wheels[b].slipAngle));
        }

        static float Yaw(SurfaceRig r) => Vector3.Dot(r.angularVelocity, r.Up);
        static float Beta(SurfaceRig r)
        {
            float u = Vector3.Dot(r.velocity, r.Forward), v = Vector3.Dot(r.velocity, r.Right);
            return MathF.Atan2(v, MathF.Max(MathF.Abs(u), 0.5f));
        }

        /// <summary>Holds the speed with the pedals (as a player would, roughly) while the key script runs.</summary>
        static void Pedals(VehicleSimulation sim, float v, float target, bool hold)
        {
            if (!hold) return;
            float e = target - v;
            sim.throttle = ApexMath.Clamp(0.2f + e * 0.4f, 0f, 1f);
            sim.brake = e < -2f ? ApexMath.Clamp(-e * 0.08f, 0f, 0.3f) : 0f;
        }

        /// <summary>
        /// Runs a key script: <paramref name="keys"/>(t) gives the digital steering key state (−1, 0, +1) at time t.
        /// Speed is held (constant-speed manoeuvre) unless <paramref name="pedals"/> overrides the pedals.
        /// </summary>
        public Result Run(float kmh, float seconds, Func<float, float> keys, Action<VehicleSimulation, float> pedals = null)
        {
            var r = bench.NewRig(kmh, out var sim);
            sim.steerSlipTarget = KeyboardSteering.SlipTarget(keyboard);
            var steering = new KeyboardSteering { profile = keyboard };
            float v0 = kmh / 3.6f;
            var res = new Result { kmh = kmh };
            float yawPeak = 0f, yawEnd = 0f, t50 = -1f;
            var yawHist = new List<float>();
            for (float t = 0f; t < seconds; t += Dt)
            {
                float key = keys(t);
                sim.steer = steering.Update(key, sim.ForwardSpeed, Dt);
                if (pedals != null) pedals(sim, t); else Pedals(sim, sim.ForwardSpeed, v0, true);
                r.Step(Dt);
                float yaw = MathF.Abs(Yaw(r));
                yawHist.Add(yaw);
                yawPeak = MathF.Max(yawPeak, yaw);
                res.frontSlipMax = MathF.Max(res.frontSlipMax, AxleSlip(sim, 0, 1));
                res.rearSlipMax = MathF.Max(res.rearSlipMax, AxleSlip(sim, 2, 3));
                res.maxBetaDeg = MathF.Max(res.maxBetaDeg, MathF.Abs(Beta(r)) * ApexMath.Rad2Deg);
                if (MathF.Abs(Beta(r)) > 0.45f) { res.spun = true; break; }
            }
            int n = yawHist.Count;
            if (n > 0)
            {
                // Steady value: mean of the last 0.3 s.
                int m = Math.Min(n, 36);
                for (int i = n - m; i < n; i++) yawEnd += yawHist[i];
                yawEnd /= m;
                for (int i = 0; i < n; i++) if (yawHist[i] >= 0.5f * yawEnd) { t50 = i * Dt; break; }
            }
            res.frontSlipEnd = AxleSlip(sim, 0, 1);
            res.rearSlipEnd = AxleSlip(sim, 2, 3);
            float speed = MathF.Max(sim.ForwardSpeed, 0.1f);
            res.lateralG = speed * yawEnd / 9.81f;
            res.radius = yawEnd > 1e-3f ? speed / yawEnd : float.PositiveInfinity;
            res.yawOvershootPct = yawEnd > 1e-3f ? MathF.Max(0f, (yawPeak / yawEnd - 1f) * 100f) : 0f;
            res.endSpeedKmh = sim.ForwardSpeed * 3.6f;
            res.timeToHalfYawMs = t50 * 1000f;
            return res;
        }

        /// <summary>Key held for the whole run.</summary>
        public Result Hold(float kmh, float seconds = 2.5f) => Run(kmh, seconds, t => 1f);

        /// <summary>A short tap, then hands off: how far the car turns and whether the rear stays calm.</summary>
        public Result Tap(float kmh, float tapSeconds = 0.12f) => Run(kmh, 2f, t => t < tapSeconds ? 1f : 0f);

        /// <summary>Quick left-right-left changes (slalom with the keys).</summary>
        public Result Slalom(float kmh, float halfPeriod = 0.6f) => Run(kmh, 4f, t => ((int)(t / halfPeriod) % 2 == 0) ? 1f : -1f);

        /// <summary>Key held into a corner, then the throttle is lifted completely after 1.2 s.</summary>
        public Result LiftOff(float kmh) => Run(kmh, 3f, t => 1f, (sim, t) =>
        {
            sim.brake = 0f;
            sim.throttle = t < 1.2f ? ApexMath.Clamp(0.2f + (kmh / 3.6f - sim.ForwardSpeed) * 0.4f, 0f, 1f) : 0f;
        });

        /// <summary>Full braking from <paramref name="kmh"/> with the key held from the start (trail braking into a corner).</summary>
        public Result TrailBrake(float kmh, float brakeSeconds = 1.2f) => Run(kmh, 2.5f, t => t > 0.25f ? 1f : 0f, (sim, t) =>
        {
            sim.throttle = t < brakeSeconds ? 0f : 0.4f;
            sim.brake = t < brakeSeconds ? 1f : 0f;
        });

        public static string Format(string name, Result r) =>
            $"  {name,-10} {r.kmh,3:F0} km/h: front slip max {r.frontSlipMax:F2} end {r.frontSlipEnd:F2}, rear max {r.rearSlipMax:F2} end {r.rearSlipEnd:F2}, " +
            $"{r.lateralG:F2} g, radius {(float.IsInfinity(r.radius) ? "-" : r.radius.ToString("F0"))} m, beta max {r.maxBetaDeg:F1}°, yaw overshoot {r.yawOvershootPct:F0} %, " +
            $"t50 {r.timeToHalfYawMs:F0} ms, end {r.endSpeedKmh:F0} km/h{(r.spun ? "  SPUN" : "")}";

        public string Report()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"keyboard probe (assist profile {bench.profile}, keyboard {keyboard})");
            foreach (float kmh in new[] { 60f, 120f, 200f, 300f }) sb.AppendLine(Format("hold", Hold(kmh)));
            foreach (float kmh in new[] { 60f, 120f, 200f, 300f }) sb.AppendLine(Format("tap", Tap(kmh)));
            foreach (float kmh in new[] { 120f, 200f, 300f }) sb.AppendLine(Format("slalom", Slalom(kmh)));
            foreach (float kmh in new[] { 200f, 280f }) sb.AppendLine(Format("lift-off", LiftOff(kmh)));
            foreach (float kmh in new[] { 150f, 250f, 300f }) sb.AppendLine(Format("trailbrake", TrailBrake(kmh)));
            return sb.ToString();
        }
    }
}
