using System;
using Apex.Core;

namespace Apex.Race
{
    /// <summary>
    /// Stopping on the own box and leaving it again (docs/PIT_LANE.md, "Halteposition"): the approach speed profile that
    /// brings a car to rest with its origin on the box centre, the stop window the crew accepts, the crew's settling of
    /// the last centimetres (parking assist), the safe-release check against cars in the fast lane, the merge check
    /// against race traffic at the pit exit and the pit speed monitor. Pure logic, shared by RaceController and the
    /// drive tests.
    /// </summary>
    public static class PitBoxStop
    {
        /// <summary>Stop window: car origin to box centre along the lane (m), across it (m), heading (degrees).</summary>
        public const float LongTolerance = 0.5f, LatTolerance = 0.35f, YawToleranceDeg = 5f;
        /// <summary>The parking assist settles a car that stopped within these limits (m, degrees).</summary>
        public const float AssistLong = 1.2f, AssistLat = 0.7f, AssistYawDeg = 12f;
        /// <summary>Overshoot the crew pushes back by hand (m); beyond it the box is missed.</summary>
        public const float PushBackLimit = 2.5f;
        /// <summary>Deceleration of the approach profile (m/s²) and the creep speed on the last metre (m/s).</summary>
        public const float ApproachDecel = 3.2f, CreepSpeed = 0.8f;
        /// <summary>Distance before the box over which the car is already straight on the box line (m).</summary>
        public const float StraightIn = 12f;

        public enum Fit { Good, TooShort, TooFar, OffLine, Skewed }

        /// <summary>
        /// Where the car stands relative to its marks: <paramref name="toBox"/> metres still to go (negative = past the
        /// centre), <paramref name="latError"/> metres off the box line, <paramref name="yawErrorDeg"/> degrees skewed.
        /// </summary>
        public static Fit Judge(float toBox, float latError, float yawErrorDeg, float longTol = LongTolerance, float latTol = LatTolerance, float yawTol = YawToleranceDeg)
        {
            if (toBox > longTol) return Fit.TooShort;
            if (toBox < -longTol) return Fit.TooFar;
            if (MathF.Abs(latError) > latTol) return Fit.OffLine;
            if (MathF.Abs(yawErrorDeg) > yawTol) return Fit.Skewed;
            return Fit.Good;
        }

        /// <summary>Within reach of the parking assist / the crew's push (also an overshoot up to <see cref="PushBackLimit"/>).</summary>
        public static bool Settleable(float toBox, float latError, float yawErrorDeg, bool assist)
        {
            if (toBox < -PushBackLimit || toBox > (assist ? AssistLong : LongTolerance)) return false;
            if (!assist && toBox < -LongTolerance) return true;   // the crew pushes an overshooting car back by hand
            return assist
                ? MathF.Abs(latError) <= AssistLat && MathF.Abs(yawErrorDeg) <= AssistYawDeg
                : MathF.Abs(latError) <= LatTolerance && MathF.Abs(yawErrorDeg) <= YawToleranceDeg;
        }

        /// <summary>Seconds the crew takes to settle the car onto the marks (longer for a push back).</summary>
        public static float SettleTime(float toBox, float latError) =>
            0.35f + MathF.Min(1.4f, MathF.Abs(toBox) * 0.6f + MathF.Abs(latError) * 0.8f);

        /// <summary>Ease of the settling move (0..1 over the settle time), no jerk at either end.</summary>
        public static float SettleShare(float t, float duration)
        {
            float k = ApexMath.Clamp01(t / MathF.Max(duration, 1e-3f));
            return k * k * k * (k * (k * 6f - 15f) + 10f);
        }

        /// <summary>
        /// Speed cap on the approach (m/s): the pit limit, then a constant deceleration to a creep on the last metre and
        /// to zero on the centre.
        /// </summary>
        public static float ApproachSpeed(float toBox, float limit)
        {
            if (toBox <= 0.02f) return 0f;
            float brake = MathF.Sqrt(2f * ApproachDecel * toBox);
            float creep = CreepSpeed * ApexMath.Clamp01(toBox / 0.6f + 0.25f);
            return MathF.Min(limit, MathF.Max(brake, creep));
        }

        /// <summary>The driver stands on the brakes: the remaining distance is about the stopping distance at this speed.</summary>
        public static bool BrakeNow(float toBox, float speed) => toBox <= speed * speed / (2f * 6f) + 0.04f;

        static string M(float v) => v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

        /// <summary>
        /// HUD guide on the approach to the box (box shown 1-based): distance, then on the last metres how far to go and
        /// which way to correct (<paramref name="latError"/> &gt; 0: the car is right of the box line).
        /// </summary>
        public static string Guide(int box, float toBox, float latError, bool taken)
        {
            string name = $"BOX {box + 1}";
            if (taken) return $"{name} belegt – dahinter warten";
            if (toBox > 25f) return $"{name} · {toBox:0} m";
            if (toBox < -LongTolerance) return $"{name} · {M(-toBox)} m zu weit";
            string lat = MathF.Abs(latError) <= LatTolerance ? "mittig" : $"{M(MathF.Abs(latError))} m nach {(latError > 0f ? "links" : "rechts")}";
            if (toBox <= LongTolerance) return $"{name} · STOPP · {lat}";
            return $"{name} · noch {M(toBox)} m · {lat}";
        }

        /// <summary>
        /// Safe release: is a car in the fast lane (<paramref name="otherFromEdge"/> m from the track edge) close enough
        /// behind the box (<paramref name="gapBehind"/> m before the box centre, negative = already past it) at
        /// <paramref name="otherSpeed"/> to be hit by a car pulling out now?
        /// </summary>
        public static bool BlocksRelease(float gapBehind, float otherSpeed, float otherFromEdge)
        {
            if (otherFromEdge > PitLaneLayout.Divider + 0.5f) return false;   // in the working lane: not in the way
            return gapBehind > -7f && gapBehind < 10f + 2.2f * MathF.Max(otherSpeed, 0f);
        }

        /// <summary>
        /// Merge check at the pit exit: would a race car at <paramref name="ahead"/> metres (negative = behind) with
        /// <paramref name="otherSpeed"/> and <paramref name="otherFromEdge"/> (m from the pit-side edge, negative = on the
        /// track) meet a car merging now at <paramref name="speed"/>? Cars behind count with the time they need to close up.
        /// </summary>
        public static bool BlocksMerge(float ahead, float speed, float otherSpeed, float otherFromEdge)
        {
            // Only cars on the pit side half of the track or nearer.
            if (otherFromEdge < -6f) return false;
            float closing = otherSpeed - speed;
            if (ahead >= 0f) return ahead < 9f;                       // alongside or just ahead
            float behind = -ahead;
            return behind < 8f + MathF.Max(0f, closing) * 2.2f;      // reaches the merging car within ~2 s
        }
    }

    /// <summary>
    /// Pit speed monitor: a speeding offence once a car is over the limit by more than the tolerance for longer than the
    /// grace time inside the limited zone; one offence per pit lane passage (docs/PIT_LANE.md).
    /// </summary>
    public sealed class PitSpeedMonitor
    {
        public const float Tolerance = 0.3f, Grace = 0.25f;
        float over;
        public bool Flagged { get; private set; }
        public float MaxExcess { get; private set; }

        public void Reset() { over = 0f; Flagged = false; MaxExcess = 0f; }

        /// <summary>True once, at the moment the offence is detected.</summary>
        public bool Update(bool limited, float speed, float limit, float dt)
        {
            if (!limited) { over = 0f; return false; }
            float excess = speed - limit;
            MaxExcess = MathF.Max(MaxExcess, excess);
            over = excess > Tolerance ? over + dt : 0f;
            if (Flagged || over < Grace) return false;
            Flagged = true;
            return true;
        }
    }
}
