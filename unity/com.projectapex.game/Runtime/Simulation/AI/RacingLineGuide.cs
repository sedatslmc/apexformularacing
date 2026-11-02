using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Core;
using Apex.Tracks;

namespace Apex.AI
{
    /// <summary>
    /// Dynamic racing line (handling brief 2026-11, part B; docs/RACING_LINE_GUIDE.md): glowing dashes along the
    /// planned line, coloured every frame from the car's current speed against the car's own speed plan.
    /// <para>The plan is the planner line of the player's car (<see cref="RacingLine"/> built from
    /// Apex.Vehicle.CarPerformance): its grip-limited speed (corner radius, downforce, friction circle) and the
    /// backwards braking curve (real braking deceleration, grade). For the dashes ahead the guide predicts the car's
    /// speed two ways – holding the current speed, and staying on the throttle (v² + 2·a·x) – and compares them with
    /// that plan:</para>
    /// <list type="bullet">
    /// <item>Red: the plan falls below the current speed – braking is necessary here, and hard (more than 55 % of the
    /// braking the car can do there), or the car is already too fast for the next metres.</item>
    /// <item>Orange: braking needed but moderate – the end of a braking zone, trail braking into the apex.</item>
    /// <item>Yellow: a braking zone starts within the next 1.6 s, or accelerating further would be too fast (lift,
    /// hold the speed), or the corner exit is still slower than the car.</item>
    /// <item>Green: speed fits; accelerate (also from the apex on, as soon as the plan rises above the speed).</item>
    /// </list>
    /// So at 300 km/h the red starts long before the Ortaköy hairpin, at 170 km/h much later, and a car that is
    /// already slow enough sees no red at all.
    /// </summary>
    public sealed class RacingLineGuide
    {
        public enum Hint : byte { Green, Yellow, Orange, Red }
        public enum Mode { Off = 0, CornersOnly = 1, WholeLap = 2 }

        /// <summary>Dash length and spacing along the line (m).</summary>
        public const float DashLength = 3.2f, DashSpacing = 5.5f;
        /// <summary>Half width of a dash (m); with the line's 1.6 m edge margin the dashes stay well on the asphalt.</summary>
        public const float DashHalfWidth = 0.32f;
        /// <summary>Look-ahead: seconds at the current speed, clamped to a distance range (m).</summary>
        public const float LookAheadSeconds = 4.5f, LookAheadMin = 90f, LookAheadMax = 650f;
        /// <summary>Warning time before a braking zone (s).</summary>
        public const float WarnSeconds = 1.6f;
        /// <summary>Braking demand (share of the available deceleration) from which a dash is red, and from which it is orange.</summary>
        public const float RedDemand = 0.55f, OrangeDemand = 0.15f;
        /// <summary>Curvature (1/m) from which a dash belongs to a corner for <see cref="Mode.CornersOnly"/> (radius 600 m).</summary>
        public const float CornerCurvature = 1f / 600f;

        public readonly RacingLine line;
        readonly TrackGeometry g;
        /// <summary>Lap distance of each dash's centre.</summary>
        public readonly float[] dashDistance;
        /// <summary>Static corner zones (braking at full speed, the corner, the exit): shown in "corners only" mode.</summary>
        public readonly bool[] cornerZone;

        public struct Dash
        {
            public int index;
            /// <summary>Distance ahead of the car (m).</summary>
            public float ahead;
            public Hint hint;
            /// <summary>Fade 0..1 near the end of the look-ahead and right in front of the car.</summary>
            public float alpha;
        }

        public RacingLineGuide(RacingLine line)
        {
            this.line = line;
            g = line.geometry;
            int n = Math.Max(1, (int)(g.Length / DashSpacing));
            dashDistance = new float[n];
            cornerZone = new bool[n];
            float spacing = g.Length / n;
            for (int i = 0; i < n; i++) dashDistance[i] = i * spacing + 0.5f * spacing;
            // Corner zones: curvature above the threshold, or the plan below 97 % of the line's top speed (braking
            // zones at full speed, slow exits), widened by 40 m so a zone does not flicker in and out.
            var raw = new bool[n];
            for (int i = 0; i < n; i++)
            {
                float d = dashDistance[i];
                raw[i] = MathF.Abs(line.CurvatureAt(d)) > CornerCurvature || line.GripSpeedAt(d) < 0.97f * RacingLine.MaxSpeed;
            }
            int widen = (int)MathF.Ceiling(40f / spacing);
            for (int i = 0; i < n; i++)
                for (int k = -widen; k <= widen && !cornerZone[i]; k++)
                    if (raw[((i + k) % n + n) % n]) cornerZone[i] = true;
        }

        public int DashCount => dashDistance.Length;

        /// <summary>Planned speed limit (m/s) at a lap distance for the current grip (wet track, tyres).</summary>
        public float Allowed(float distance, float gripScale) =>
            MathF.Min(RacingLine.MaxSpeed, gripScale >= 0.999f ? line.GripSpeedAt(distance) : line.GripSpeedAt(distance, gripScale));

        public static float LookAhead(float speed) => ApexMath.Clamp(MathF.Abs(speed) * LookAheadSeconds, LookAheadMin, LookAheadMax);

        /// <summary>
        /// Hints for the dashes ahead of a car at lap distance <paramref name="carDistance"/> driving at
        /// <paramref name="speed"/> (m/s). <paramref name="gripScale"/> is the tyre/track grip (1 dry, less in the wet).
        /// Fills <paramref name="result"/> (cleared first) in order of distance; no allocation once the list has grown.
        /// </summary>
        public void Evaluate(float carDistance, float speed, float gripScale, Mode mode, bool brakeColours, List<Dash> result)
        {
            result.Clear();
            if (mode == Mode.Off) return;
            float range = LookAhead(speed);
            int n = dashDistance.Length;
            float spacing = g.Length / n;
            int first = (int)MathF.Floor(g.Wrap(carDistance) / spacing);
            float v = MathF.Max(speed, 0f);
            float vAcc = v;
            float prevAhead = 0f;
            // First pass: hints from the plan against the predicted speed.
            for (int k = 0; k < n; k++)
            {
                int i = (first + k) % n;
                float ahead = g.Wrap(dashDistance[i] - carDistance);
                if (ahead > 0.5f * g.Length) continue;   // the dash the car is on, just behind its centre
                if (ahead < prevAhead) break;            // wrapped past the lap
                if (ahead > range) break;
                float d = dashDistance[i];
                // Speed if the driver stays on the throttle from here (until the plan stops him).
                float step = ahead - prevAhead;
                prevAhead = ahead;
                vAcc = MathF.Min(RacingLine.MaxSpeed, MathF.Sqrt(vAcc * vAcc + 2f * line.AccelAt(vAcc, d) * MathF.Max(step, 0f)));
                float allowed = Allowed(d, gripScale);
                var hint = Hint.Green;
                if (brakeColours)
                {
                    float before = Allowed(d - spacing, gripScale), after = Allowed(d + spacing, gripScale);
                    // Deceleration the plan asks for here, as a share of what the car can do at that speed.
                    float decel = MathF.Max(0f, (before * before - after * after) / (4f * spacing));
                    float demand = decel / line.BrakeDecelAt(allowed, d);
                    if (v > allowed + 0.5f)
                    {
                        // Already faster than the plan here: braking is needed – how hard says the plan's slope.
                        bool tooFastNow = v > allowed + 3f && ahead < MathF.Max(25f, v * 1f);
                        if (tooFastNow || demand >= RedDemand) hint = Hint.Red;
                        else if (demand >= OrangeDemand) hint = Hint.Orange;
                        else hint = Hint.Yellow;            // corner exit still below the current speed: hold
                    }
                    else if (vAcc > allowed + 0.5f) hint = Hint.Yellow;   // would be too fast if accelerating on: lift / hold
                    // The prediction keeps to the plan once the plan limits it (the driver brakes as shown).
                    vAcc = MathF.Min(vAcc, allowed);
                }
                result.Add(new Dash { index = i, ahead = ahead, hint = hint, alpha = 1f });
            }
            if (brakeColours)
            {
                // Yellow warning before the first braking dash: WarnSeconds at the predicted speed.
                for (int k = 0; k < result.Count; k++)
                {
                    if (result[k].hint != Hint.Orange && result[k].hint != Hint.Red) continue;
                    float start = result[k].ahead;
                    float warn = MathF.Max(35f, v * WarnSeconds);
                    for (int j = k - 1; j >= 0 && start - result[j].ahead <= warn; j--)
                        if (result[j].hint == Hint.Green) { var x = result[j]; x.hint = Hint.Yellow; result[j] = x; }
                    // Only the next braking zone gets a warning band per call; later zones get theirs as they come.
                    while (k < result.Count && result[k].hint != Hint.Green) k++;
                }
            }
            // Corners only: drop dashes outside the corner zones unless they carry a warning.
            if (mode == Mode.CornersOnly)
                result.RemoveAll(x => !cornerZone[x.index] && x.hint == Hint.Green);
            // Fades: in over the first 6 m in front of the car, out over the last 20 % of the look-ahead.
            for (int k = 0; k < result.Count; k++)
            {
                var x = result[k];
                float fadeIn = ApexMath.Clamp01((x.ahead - 4f) / 6f);
                float fadeOut = ApexMath.Clamp01((range - x.ahead) / (0.2f * range));
                x.alpha = fadeIn * fadeOut;
                result[k] = x;
            }
        }

        /// <summary>Centre of a dash on the line and the line's direction there (game frame, on the road surface).</summary>
        public void DashFrame(int index, out Vector3 centre, out Vector3 forward, out Vector3 right)
        {
            float d = dashDistance[index];
            g.FrameAt(d, out centre, out forward, out right, line.OffsetAt(d));
        }

        /// <summary>Lateral offset of the line at a dash (m, + right) and the road half width there.</summary>
        public (float offset, float halfWidth) DashLateral(int index)
        {
            float d = dashDistance[index];
            return (line.OffsetAt(d), g.WidthAt(d) * 0.5f);
        }
    }
}
