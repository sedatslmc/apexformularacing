using System;
using System.Collections.Generic;
using Apex.Core;
using Apex.Vehicle;

namespace Apex.Tracks
{
    /// <summary>
    /// Driving characteristics of a circuit from one recorded lap of the AI on the real surface
    /// (<see cref="CircuitTestBed"/>): per approved corner the braking point, braking distance,
    /// entry/apex/exit speeds and steering demand; per lap the full-throttle share and the braking
    /// zones that make overtaking places (a long run in, a big speed drop).
    /// </summary>
    public sealed class CornerAnalysis
    {
        public struct Sample { public float raw, speed, throttle, brake, steer, time; }

        public struct Corner
        {
            public string name;
            public bool right;
            public float angle, apexLap;
            public float entryKmh, apexKmh, exitKmh, brakeStartLap, brakingDistance, maxSteer;
            /// <summary>Lap distance of full throttle before the braking point (the run-in).</summary>
            public float runInMetres;
            public bool overtakingPlace;
        }

        public readonly List<Sample> samples = new List<Sample>();
        public readonly List<Corner> corners = new List<Corner>();
        public double lapTime;
        public float fullThrottleShare, brakingShare, topKmh;
        readonly CircuitTestBed bed;

        public CornerAnalysis(CircuitTestBed testBed, float pace)
        {
            bed = testBed;
            var g = bed.g;
            var def = bed.def;
            int lap = 0;
            float lastLap = -1f;
            bed.trace = (d, rig) =>
            {
                float lapD = g.Wrap(d - def.startLineDistance);
                if (lastLap >= 0f && lapD < lastLap - g.Length * 0.5f) lap++;
                lastLap = lapD;
                // The grid is behind the line: crossing 1 starts the standing lap, crossing 2 the flying lap.
                if (lap == 2)
                    samples.Add(new Sample { raw = d, speed = rig.ForwardSpeed, throttle = rig.sim.throttle, brake = rig.sim.brake, steer = MathF.Abs(rig.sim.steer), time = rig.time });
            };
            var (d0, l0) = bed.GridSlot(0);
            var res = bed.Drive(d0, l0, 0f, 600f, laps: 2, pace: pace);
            bed.trace = null;
            if (res.lapTimes.Count > 1) lapTime = res.lapTimes[1];
            Analyse();
        }

        float Lap(float raw) => bed.g.Wrap(raw - bed.def.startLineDistance);

        void Analyse()
        {
            if (samples.Count == 0) return;
            int full = 0, braking = 0;
            foreach (var s in samples)
            {
                if (s.throttle > 0.95f && s.brake < 0.05f) full++;
                if (s.brake > 0.1f) braking++;
                topKmh = MathF.Max(topKmh, s.speed * 3.6f);
            }
            fullThrottleShare = full / (float)samples.Count;
            brakingShare = braking / (float)samples.Count;
            var g = bed.g;
            var marks = new List<CircuitDefinition.CornerMark>(bed.def.corners);
            marks.Sort((a, b) => Lap(a.distance).CompareTo(Lap(b.distance)));
            int n = samples.Count;
            float Rel(int i, float apexLap) => ApexMath.PosMod(Lap(samples[i].raw) - apexLap + g.Length * 0.5f, g.Length) - g.Length * 0.5f;
            foreach (var m in marks)
            {
                float apex = Lap(m.distance);
                var c = new Corner { name = m.name, right = m.right, angle = m.angle, apexLap = apex };
                // Apex speed: minimum within ±80 m of the marked apex.
                int iApex = -1;
                float vMin = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    float r = Rel(i, apex);
                    if (r < -80f || r > 80f) continue;
                    if (samples[i].speed < vMin) { vMin = samples[i].speed; iApex = i; }
                }
                if (iApex < 0) continue;
                c.apexKmh = vMin * 3.6f;
                // Braking point: walk back from the apex while braking or coasting, up to 500 m.
                int iBrake = iApex;
                for (int k = 1; k < n; k++)
                {
                    int i = (iApex - k + n) % n;
                    if (Rel(i, apex) < -500f) break;
                    if (samples[i].brake > 0.05f || samples[i].throttle < 0.5f) iBrake = i;
                    else if (Rel(iBrake, apex) - Rel(i, apex) > 15f) break;
                }
                c.entryKmh = samples[iBrake].speed * 3.6f;
                c.brakeStartLap = Lap(samples[iBrake].raw);
                c.brakingDistance = MathF.Max(0f, Rel(iApex, apex) - Rel(iBrake, apex));
                // Exit speed 150 m after the apex; steering demand within the corner.
                for (int k = 0; k < n; k++)
                {
                    int i = (iApex + k) % n;
                    if (Rel(i, apex) > 150f) { c.exitKmh = samples[i].speed * 3.6f; break; }
                }
                for (int i = 0; i < n; i++)
                {
                    float r = Rel(i, apex);
                    if (r >= Rel(iBrake, apex) && r <= 100f) c.maxSteer = MathF.Max(c.maxSteer, samples[i].steer);
                }
                // Run-in: the full-throttle stretch ending at most 30 m before the braking point
                // (the AI eases off the throttle over the last metres as it reaches its target).
                int runEnd = -1;
                for (int k = 1; k < n; k++)
                {
                    int i = (iBrake - k + n) % n;
                    bool flat = samples[i].throttle >= 0.8f && samples[i].brake <= 0.05f;
                    if (runEnd < 0)
                    {
                        if (flat) runEnd = i;
                        else if (Rel(iBrake, apex) - Rel(i, apex) > 30f) break;
                        continue;
                    }
                    if (!flat) break;
                    c.runInMetres = Rel(runEnd, apex) - Rel(i, apex);
                }
                c.overtakingPlace = c.runInMetres > 400f && c.entryKmh - c.apexKmh > 100f && c.brakingDistance > 60f;
                corners.Add(c);
            }
        }

        public string Report()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"flying lap {lapTime:F2} s, top speed {topKmh:F0} km/h, full throttle {fullThrottleShare * 100f:F0} % of the time, braking {brakingShare * 100f:F0} %");
            sb.AppendLine("| # | corner | dir | angle | brake at (lap m) | braking | entry | apex | exit (+150 m) | max steer | run-in | overtaking |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            int k = 1;
            foreach (var c in corners)
                sb.AppendLine($"| {k++} | {c.name} | {(c.right ? "R" : "L")} | {c.angle:F0}° | {c.brakeStartLap:F0} | {c.brakingDistance:F0} m | {c.entryKmh:F0} | {c.apexKmh:F0} | {c.exitKmh:F0} | {c.maxSteer * 100f:F0} % | {c.runInMetres:F0} m | {(c.overtakingPlace ? "yes" : "")} |");
            return sb.ToString();
        }
    }
}
