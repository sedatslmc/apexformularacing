using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Core;
using Apex.Tracks;

namespace Apex.AI
{
    /// <summary>Driver character. All values in [0,1] unless noted.</summary>
    [Serializable]
    public struct AIPersonality
    {
        /// <summary>Fraction of the grip limit used by the speed profile (~0.82-0.97).</summary>
        public float pace;
        /// <summary>Willingness to attempt passes and defend.</summary>
        public float aggression;
        /// <summary>Lap-to-lap repeatability; low values add pace noise.</summary>
        public float consistency;
        /// <summary>Extra pace retained in the wet.</summary>
        public float wetSkill;
        /// <summary>Tire management: lower peak use, slower wear.</summary>
        public float tireSaving;

        public static AIPersonality Make(float pace, float aggression, float consistency, float wetSkill, float tireSaving) =>
            new AIPersonality { pace = pace, aggression = aggression, consistency = consistency, wetSkill = wetSkill, tireSaving = tireSaving };
    }

    /// <summary>Track-relative snapshot of a car, shared with AI drivers for traffic decisions.</summary>
    public struct CarState
    {
        public int id;
        /// <summary>Raw distance along the centreline.</summary>
        public float distance;
        /// <summary>Lateral offset (+ right).</summary>
        public float lateral;
        public float speed;
        public Vector3 position;
        public Vector3 forward;
        public bool inPit;
    }

    /// <summary>
    /// AI racing driver: follows the shared <see cref="RacingLine"/> with pure pursuit,
    /// controls speed against the line's grip-limited profile scaled by personality
    /// and conditions, and handles traffic (follow, pick a passing side, give room
    /// alongside). It only produces pedal/steering inputs for the same vehicle
    /// physics the player uses: no extra grip, no teleporting, no rubber-banding.
    /// </summary>
    public sealed class AIDriver
    {
        public readonly RacingLine line;
        public readonly AIPersonality personality;
        public float wheelbase = 3.6f;
        /// <summary>Usable lock this step. Callers should pass the vehicle's grip-limited
        /// lock (VehicleSimulation.CurrentMaxLock with the steering assist on), so the AI
        /// never steers the fronts past their peak slip angle.</summary>
        public float maxSteerRad = 22f * ApexMath.Deg2Rad;
        /// <summary>Grip multiplier from weather/tires (1 = dry, fresh).</summary>
        public float gripFactor = 1f;
        public bool wet;
        /// <summary>Overrides the racing line with a fixed lateral target (e.g. pit lane), NaN = off.</summary>
        public float forcedLateral = float.NaN;
        /// <summary>Front tire usage this step: max |front slip angle| / peak slip angle.
        /// Above ~1 the fronts are sliding (understeer) and the driver backs off.</summary>
        public float frontSlipUsage;
        /// <summary>Speed cap (m/s), e.g. pit limiter.</summary>
        public float speedCap = float.PositiveInfinity;
        /// <summary>Speed limit from race control (yellow, VSC, safety car), separate from the pit cap.</summary>
        public float controlSpeedCap = float.PositiveInfinity;
        /// <summary>Yellow flag / safety car: no passing; follow the car ahead at <see cref="followGap"/>.</summary>
        public bool noOvertaking;
        /// <summary>Following distance (m) with <see cref="noOvertaking"/>.</summary>
        public float followGap = 10f;

        public float TargetLateral { get; private set; }
        public string Intent { get; private set; } = "line";

        readonly TrackGeometry g;
        readonly Random rng;
        float passSide = 1f;
        int passTargetId = -1;
        bool initialised;
        float passCommit;
        float lapNoise = 1f;
        int hint = -1;
        int lastLapIndex = -1;

        public AIDriver(RacingLine line, AIPersonality personality, int seed)
        {
            this.line = line;
            this.personality = personality;
            g = line.geometry;
            rng = new Random(seed);
        }

        public void ResetTracking()
        {
            hint = -1;
            passCommit = 0f;
            passTargetId = -1;
            initialised = false;
        }

        float PaceFactor()
        {
            float p = personality.pace * lapNoise;
            if (wet) p *= ApexMath.Lerp(0.94f, 1f, personality.wetSkill);
            return ApexMath.Clamp(p * gripFactor, 0.4f, 1.05f);
        }

        /// <summary>Target speed (m/s) at a distance for this driver.</summary>
        public float TargetSpeed(float distance)
        {
            // Only the grip-limited part (corners, braking) scales with sqrt(grip usage) (v ~ sqrt(mu));
            // straights and exits are power-limited, so cold tyres or a slower driver do not lift there.
            float k = MathF.Sqrt(PaceFactor());
            float power = line.SpeedAt(distance);
            power = power >= RacingLine.MaxSpeed - 0.01f ? float.PositiveInfinity : power * MathF.Max(1f, k);
            // In the wet the grip part comes from a profile recomputed with the wet friction (keeps the downforce
            // physics); only the remaining driver pace scales by its square root.
            float grip = wet && gripFactor < 0.97f
                ? line.GripSpeedAt(distance, gripFactor) * MathF.Sqrt(PaceFactor() / gripFactor)
                : line.GripSpeedAt(distance) * k;
            float v = MathF.Min(power, grip);
            return MathF.Min(v, MathF.Min(speedCap, controlSpeedCap));
        }

        /// <summary>Highest speed now that still allows the offset path ahead (incl. braking).</summary>
        float PathSpeedLimit(float d, float lateral, float v)
        {
            float mu = line.Mu * PaceFactor();
            float dpm = line.DownforcePerMass;
            const float g0 = 9.81f;
            float horizon = MathF.Max(40f, v * 1.6f);
            float best = float.MaxValue;
            for (float s = 0f; s <= horizon; s += 5f)
            {
                float kc = g.CurvatureAt(d + s);
                float k = MathF.Abs(kc);
                if (k < 1e-4f) continue;
                // Positive curvature = right turn, whose inside is +lateral.
                float r = MathF.Max(5f, 1f / k - MathF.Sign(kc) * lateral);
                float denom = 1f / r - mu * dpm;
                float vmax = denom <= 1e-5f ? 95f : MathF.Sqrt(mu * g0 / denom);
                // Downhill reduces and uphill adds to the braking deceleration (see RacingLine).
                float decel = MathF.Max(1f, mu * (g0 + dpm * vmax * vmax) * 0.8f + g0 * g.GradeAt(d + s * 0.5f));
                best = MathF.Min(best, MathF.Sqrt(vmax * vmax + 2f * decel * s));
            }
            return best;
        }

        public (float throttle, float brake, float steer) Drive(in CarState self, IReadOnlyList<CarState> others, float dt)
        {
            var proj = g.Project(self.position, hint);
            hint = proj.index;
            float d = proj.distance;
            float v = MathF.Max(self.speed, 0f);
            int lapIndex = (int)(d / MathF.Max(g.Length, 1f) * 4f);
            if (lapIndex != lastLapIndex)
            {
                // New quarter-lap: draw pace noise from consistency.
                lastLapIndex = lapIndex;
                float spread = (1f - personality.consistency) * 0.025f;
                lapNoise = 1f + ((float)rng.NextDouble() * 2f - 1f) * spread;
            }

            float wantSpeed = MathF.Min(TargetSpeed(d + v * 0.25f), TargetSpeed(d + 4f));
            float lineOffset = line.OffsetAt(d + 6f + v * 0.25f);
            float halfWidth = g.WidthAt(d) * 0.5f - 1.4f;
            Intent = "line";
            float desired = lineOffset;

            // --- Traffic ----------------------------------------------------------------
            passCommit = noOvertaking ? 0f : MathF.Max(0f, passCommit - dt);
            float nearestGap = float.MaxValue;
            CarState ahead = default;
            bool hasAhead = false;
            bool passTargetSeen = false;
            float passTargetLateral = 0f;
            float alongsideLateral = float.NaN;
            foreach (var o in others)
            {
                if (o.id == self.id || o.inPit != self.inPit) continue;
                float gap = ApexMath.PosMod(o.distance - d, g.Length);
                if (gap > g.Length * 0.5f) gap -= g.Length;
                float dl = o.lateral - proj.lateral;
                if (o.id == passTargetId && gap > -8f && gap < 70f)
                {
                    passTargetSeen = true;
                    passTargetLateral = o.lateral;
                }
                if (MathF.Abs(gap) < 7f && MathF.Abs(dl) < 3.4f)
                    alongsideLateral = o.lateral;
                // Without passing, any car ahead counts (queue behind it, whatever the line).
                bool inLane = noOvertaking ? gap < 150f : gap < 60f && MathF.Abs(dl) < 2.6f;
                if (gap > 0f && inLane && gap < nearestGap)
                {
                    nearestGap = gap;
                    ahead = o;
                    hasAhead = true;
                }
            }

            if (hasAhead)
            {
                float closing = v - ahead.speed;
                float timeToContact = closing > 0.1f ? (nearestGap - 7f) / closing : float.MaxValue;
                // Start passes on straights (or when much faster), not mid-corner.
                float upcoming = 0f;
                for (float s2 = 0f; s2 <= 80f; s2 += 10f) upcoming = MathF.Max(upcoming, MathF.Abs(line.curvature[g.IndexAt(d + s2)]));
                bool straight = upcoming < 1f / 300f;
                bool wantsPass = !noOvertaking && (closing > 1f || nearestGap < 18f) && personality.aggression > 0.15f
                                 && (straight || closing > 10f * (1.3f - personality.aggression));
                if (wantsPass && passCommit <= 0f)
                {
                    // Pick the side with more room next to the car ahead.
                    float roomLeft = ahead.lateral + halfWidth;
                    float roomRight = halfWidth - ahead.lateral;
                    if (MathF.Max(roomLeft, roomRight) > 3.4f)
                    {
                        passSide = roomRight > roomLeft ? 1f : -1f;
                        passTargetId = ahead.id;
                        passCommit = ApexMath.Lerp(3f, 1.5f, personality.aggression);
                        passTargetSeen = true;
                        passTargetLateral = ahead.lateral;
                    }
                }
                if (noOvertaking)
                {
                    // Queue: close up to the follow gap, never faster than the car ahead plus a margin.
                    float follow = ahead.speed + (nearestGap - followGap) * 0.4f;
                    if (follow < wantSpeed)
                    {
                        wantSpeed = MathF.Max(follow, 0f);
                        Intent = "queue";
                    }
                }
                else if (timeToContact < 1.4f || nearestGap < 9f)
                {
                    // Blocked: match speed with a safety margin.
                    float follow = ahead.speed + (nearestGap - 10f) * 0.5f;
                    wantSpeed = MathF.Min(wantSpeed, MathF.Max(follow, 0f));
                    Intent = "follow";
                }
            }
            if (passCommit > 0f && passTargetSeen)
            {
                // Lateral target follows the car being passed, not a fixed line offset.
                desired = passTargetLateral + passSide * 3.4f;
                Intent = "pass";
            }
            else if (passCommit <= 0f)
            {
                passTargetId = -1;
            }
            if (!float.IsNaN(alongsideLateral))
            {
                // Side by side: keep at least a car width of room.
                float away = MathF.Sign(proj.lateral - alongsideLateral);
                if (away == 0f) away = passSide;
                float minLat = alongsideLateral + away * 3.2f;
                desired = away > 0f ? MathF.Max(desired, minLat) : MathF.Min(desired, minLat);
                if (Intent == "line") Intent = "alongside";
            }
            if (!float.IsNaN(forcedLateral)) desired = forcedLateral;
            else desired = ApexMath.Clamp(desired, -halfWidth, halfWidth);
            // Smooth lateral changes so the car never jinks.
            if (!initialised)
            {
                TargetLateral = proj.lateral;
                initialised = true;
            }
            TargetLateral = ApexMath.MoveToward(TargetLateral, desired, (float.IsNaN(forcedLateral) ? 4f : 6f) * dt);
            // Off the racing line (passing, defending, pit lane) the line's speed profile
            // no longer applies: limit speed by the curvature of the path actually driven
            // (centreline radius shifted by the lateral offset), including braking for it.
            float offLine = MathF.Max(MathF.Abs(TargetLateral - line.OffsetAt(d)), MathF.Abs(proj.lateral - line.OffsetAt(d)));
            if (offLine > 1.5f)
                wantSpeed = MathF.Min(wantSpeed, PathSpeedLimit(d, MathF.Max(MathF.Abs(TargetLateral), MathF.Abs(proj.lateral)) * MathF.Sign(TargetLateral), v));
            // Understeering: lift/brake until the fronts grip again.
            if (frontSlipUsage > 1.0f)
            {
                wantSpeed = MathF.Min(wantSpeed, v - (frontSlipUsage - 0.95f) * 40f);
                Intent = "understeer";
            }

            // --- Steering: pure pursuit plus lateral-error feedback ---------------------
            float lookahead = 6f + v * 0.3f;
            var target = g.PositionAt(d + lookahead) + g.RightAt(d + lookahead) * TargetLateral;
            var fwd = Vector3.Normalize(new Vector3(self.forward.X, 0f, self.forward.Z));
            var right = new Vector3(fwd.Z, 0f, -fwd.X);
            var to = target - self.position;
            float lx = Vector3.Dot(to, right);
            float lz = Vector3.Dot(to, fwd);
            float dist2 = MathF.Max(lx * lx + lz * lz, 1f);
            float wheelAngle = MathF.Atan(2f * lx / dist2 * wheelbase);
            // Feedback on the lateral error compensates understeer at high speed.
            float latErr = TargetLateral - proj.lateral;
            wheelAngle += ApexMath.Clamp(latErr * 0.004f, -0.03f, 0.03f);
            float steer = ApexMath.Clamp(wheelAngle / maxSteerRad, -1f, 1f);

            // --- Speed control ---------------------------------------------------------------
            float err = wantSpeed - v;
            float throttle = ApexMath.Clamp01(err * 0.35f + 0.15f);
            float brake = ApexMath.Clamp01(-err * 0.18f);
            if (brake > 0.05f) throttle = 0f;
            return (throttle, brake, steer);
        }
    }
}
