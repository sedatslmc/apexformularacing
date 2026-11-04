using System;
using System.Numerics;
using Apex.Core;

namespace Apex.Race
{
    /// <summary>
    /// Skeleton of a pit crew member (docs/PIT_LANE.md): ten rigid body parts placed by two-bone IK from a
    /// <see cref="PitCrewChoreography.Pose"/> – a gait while walking or running, one knee down for the gun men, hands on
    /// the wheel, the gun or the jack handle. Pure maths in the figure frame (feet origin on the ground, x right, y up,
    /// z the facing), so it is tested without Unity; PitCrewComponent draws the parts (PitCrewGeometry) with these frames.
    /// </summary>
    public static class PitCrewFigure
    {
        public enum Part { Torso, Head, UpperArmL, UpperArmR, ForearmL, ForearmR, ThighL, ThighR, ShinL, ShinR }
        public const int PartCount = 10;

        /// <summary>Body dimensions (m) of a 1.80 m tall crew member; the meshes are built to them.</summary>
        public const float HipHeight = 0.93f, HipHalfWidth = 0.1f, Thigh = 0.44f, Shin = 0.43f, AnkleHeight = 0.08f,
            TorsoLength = 0.55f, ShoulderDrop = 0.08f, ShoulderHalfWidth = 0.2f, UpperArm = 0.3f, Forearm = 0.3f,
            /// <summary>Hand centre beyond the wrist end of the forearm.</summary>
            Hand = 0.06f;

        /// <summary>Frame of a part in the figure frame: origin at its joint, axes orthonormal (right, up, forward).</summary>
        public struct Frame
        {
            public Vector3 origin, right, up, forward;
        }

        /// <summary>Stride cadence (steps of one foot per second) at running speed.</summary>
        public const float Cadence = 1.7f;

        /// <summary>
        /// Solves the figure. <paramref name="handsLocal"/>: where the hands hold the object, in the figure frame (NaN.X =
        /// arms free); <paramref name="time"/>: seconds, drives the gait phase.
        /// </summary>
        public static void Solve(in PitCrewChoreography.Pose pose, Vector3 handsLocal, float time, Frame[] parts)
        {
            float run = ApexMath.Clamp01(pose.speed / 3.5f);
            float kneel = ApexMath.Clamp01(pose.kneel);
            float phase = time * Cadence * MathF.PI * 2f * (0.55f + 0.45f * run);
            float swing = MathF.Sin(phase);
            // Hip: lower when kneeling (one knee on the ground), a little lower and bouncing when running.
            float hipY = ApexMath.Lerp(HipHeight - 0.04f * run + 0.025f * run * MathF.Abs(MathF.Cos(phase)), 0.58f, kneel);
            bool holding = !float.IsNaN(handsLocal.X);
            // Lean: forward when running or kneeling, back a little when pulling the jack.
            float lean = 0.18f * run + 0.22f * kneel;
            if (pose.hold == PitCrewChoreography.Hold.Jack && holding) lean -= 0.06f;
            if (pose.hold == PitCrewChoreography.Hold.Wheel && holding) lean += 0.04f;
            var hip = new Vector3(0f, hipY, -0.06f * kneel);
            var torsoUp = new Vector3(0f, MathF.Cos(lean), MathF.Sin(lean));
            var torsoFwd = new Vector3(0f, -MathF.Sin(lean), MathF.Cos(lean));
            parts[(int)Part.Torso] = new Frame { origin = hip, right = Vector3.UnitX, up = torsoUp, forward = torsoFwd };
            var neck = hip + torsoUp * TorsoLength;
            // The head looks at the hands when holding something low, ahead otherwise.
            float nod = holding ? ApexMath.Clamp((neck.Y - handsLocal.Y) * 0.35f, 0f, 0.35f) : 0f;
            var headUp = Vector3.Normalize(new Vector3(0f, MathF.Cos(lean * 0.5f + nod), MathF.Sin(lean * 0.5f + nod)));
            parts[(int)Part.Head] = Basis(neck, headUp, Vector3.UnitZ, false);

            // Legs: a stride while moving; kneeling = left knee down behind, right foot planted ahead.
            for (int side = 0; side < 2; side++)
            {
                float sx = side == 0 ? -1f : 1f;
                var root = hip + new Vector3(sx * HipHalfWidth, 0f, 0f);
                float legSwing = (side == 0 ? swing : -swing) * 0.38f * run;
                float lift = MathF.Max(0f, (side == 0 ? 1f : -1f) * MathF.Cos(phase)) * 0.16f * run;
                var stand = new Vector3(sx * HipHalfWidth, AnkleHeight + lift, legSwing);
                var knelt = side == 0 ? new Vector3(sx * 0.13f, 0.12f, -0.5f) : new Vector3(sx * 0.14f, AnkleHeight, 0.34f);
                var foot = Vector3.Lerp(stand, knelt, kneel);
                var knee = Ik(root, foot, Thigh, Shin, new Vector3(0f, 0f, 1f), out var ankle);
                parts[(int)(side == 0 ? Part.ThighL : Part.ThighR)] = Basis(root, root - knee, Vector3.UnitZ, true);
                // The shin's forward is the foot's direction: ahead, toes down when the shin lies flat (kneeling).
                var shinFwd = Vector3.Lerp(Vector3.UnitZ, -Vector3.UnitY, kneel * (side == 0 ? 1f : 0f));
                parts[(int)(side == 0 ? Part.ShinL : Part.ShinR)] = Basis(knee, knee - ankle, shinFwd, true);
            }

            // Arms: on the held object, or swinging against the legs.
            for (int side = 0; side < 2; side++)
            {
                float sx = side == 0 ? -1f : 1f;
                var shoulder = hip + torsoUp * (TorsoLength - ShoulderDrop) + new Vector3(sx * ShoulderHalfWidth, 0f, 0f);
                Vector3 hand;
                if (holding)
                {
                    // Either side of the object: a wheel is gripped at its rim, the gun and the handle close together.
                    float spread = pose.hold == PitCrewChoreography.Hold.Wheel ? 0.3f : pose.hold == PitCrewChoreography.Hold.Jack ? 0.16f : 0.09f;
                    hand = handsLocal + new Vector3(sx * spread, 0f, 0f);
                }
                else
                {
                    float armSwing = (side == 0 ? -swing : swing) * 0.32f * run;
                    hand = shoulder + new Vector3(sx * 0.06f, -(UpperArm + Forearm) * (0.95f - 0.25f * run), armSwing + 0.04f);
                }
                // Elbows bend backwards and out.
                var elbow = Ik(shoulder, hand, UpperArm, Forearm + Hand, new Vector3(sx * 0.6f, -0.3f, -1f), out var wrist);
                parts[(int)(side == 0 ? Part.UpperArmL : Part.UpperArmR)] = Basis(shoulder, shoulder - elbow, Vector3.UnitZ, true);
                parts[(int)(side == 0 ? Part.ForearmL : Part.ForearmR)] = Basis(elbow, elbow - wrist, Vector3.UnitZ, true);
            }
        }

        /// <summary>Seat height of a pit wall stool (top of the cushion, m above the stand's floor).</summary>
        public const float SeatHeight = 0.66f;

        /// <summary>
        /// A race engineer on a stool at the pit wall desk (figure frame, feet on the stand's floor, facing the monitors):
        /// hands on the keyboard with a light typing jitter, the head turning slowly between the monitors and the track,
        /// now and then a hand to the headset and a nod while talking on the radio. <paramref name="seed"/> varies the
        /// rhythm per person; <paramref name="deskHeight"/> is the keyboard's height above the floor.
        /// </summary>
        public static void SolveSeated(float time, int seed, float deskHeight, Frame[] parts)
        {
            float ph = seed * 1.7f;
            var hip = new Vector3(0f, SeatHeight + 0.06f, -0.04f);
            float lean = 0.12f + 0.04f * MathF.Sin(time * 0.31f + ph);
            var torsoUp = new Vector3(0f, MathF.Cos(lean), MathF.Sin(lean));
            var torsoFwd = new Vector3(0f, -MathF.Sin(lean), MathF.Cos(lean));
            parts[(int)Part.Torso] = new Frame { origin = hip, right = Vector3.UnitX, up = torsoUp, forward = torsoFwd };
            var neck = hip + torsoUp * TorsoLength;
            // Head: slow look around (monitors, then the track to the side), a nod while talking.
            float cycle = (time * 0.11f + seed * 0.37f) % 1f;
            float yaw = cycle < 0.7f ? 0.35f * MathF.Sin(time * 0.6f + ph) : 0.9f * MathF.Sin((cycle - 0.7f) / 0.3f * MathF.PI);
            bool talking = ((int)(time * 0.2f + seed * 0.5f)) % 3 == 0;
            float nod = 0.12f + (talking ? 0.06f * MathF.Sin(time * 5f + ph) : 0f);
            var lookFwd = new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
            var headUp = Vector3.Normalize(new Vector3(0f, MathF.Cos(nod), 0f) + lookFwd * MathF.Sin(nod));
            parts[(int)Part.Head] = Basis(neck, headUp, lookFwd, false);
            // Legs: thighs on the stool, feet on the footrest bar in front.
            for (int side = 0; side < 2; side++)
            {
                float sx = side == 0 ? -1f : 1f;
                var root = hip + new Vector3(sx * HipHalfWidth, 0f, 0f);
                var foot = new Vector3(sx * 0.14f, AnkleHeight + 0.18f, 0.36f);
                var knee = Ik(root, foot, Thigh, Shin, new Vector3(0f, 0.6f, 1f), out var ankle);
                parts[(int)(side == 0 ? Part.ThighL : Part.ThighR)] = Basis(root, root - knee, Vector3.UnitZ, true);
                parts[(int)(side == 0 ? Part.ShinL : Part.ShinR)] = Basis(knee, knee - ankle, Vector3.UnitZ, true);
            }
            // Arms: on the keyboard (typing), the right hand at the headset while talking.
            for (int side = 0; side < 2; side++)
            {
                float sx = side == 0 ? -1f : 1f;
                var shoulder = hip + torsoUp * (TorsoLength - ShoulderDrop) + new Vector3(sx * ShoulderHalfWidth, 0f, 0f);
                float tap = MathF.Max(0f, MathF.Sin(time * 9f + ph + side * 2.1f)) * 0.012f;
                var hand = new Vector3(sx * 0.17f, deskHeight + 0.03f + tap, 0.42f);
                if (talking && side == 1) hand = neck + new Vector3(0.13f, 0.12f, 0.02f);
                var elbow = Ik(shoulder, hand, UpperArm, Forearm + Hand, new Vector3(sx * 0.8f, -0.4f, -0.6f), out var wrist);
                parts[(int)(side == 0 ? Part.UpperArmL : Part.UpperArmR)] = Basis(shoulder, shoulder - elbow, Vector3.UnitZ, true);
                parts[(int)(side == 0 ? Part.ForearmL : Part.ForearmR)] = Basis(elbow, elbow - wrist, Vector3.UnitZ, true);
            }
        }

        /// <summary>
        /// Two-bone IK: the middle joint for a chain root → end of lengths a, b, bending towards <paramref name="pole"/>.
        /// <paramref name="end"/> is the reached end point (the target, or as close as the chain reaches).
        /// </summary>
        public static Vector3 Ik(Vector3 root, Vector3 target, float a, float b, Vector3 pole, out Vector3 end)
        {
            var d = target - root;
            float len = d.Length();
            var dir = len > 1e-5f ? d / len : -Vector3.UnitY;
            float reach = ApexMath.Clamp(len, MathF.Abs(a - b) + 0.01f, a + b - 0.001f);
            end = root + dir * reach;
            float cosA = ApexMath.Clamp((a * a + reach * reach - b * b) / (2f * a * reach), -1f, 1f);
            float sinA = MathF.Sqrt(MathF.Max(0f, 1f - cosA * cosA));
            var perp = pole - Vector3.Dot(pole, dir) * dir;
            if (perp.LengthSquared() < 1e-8f) perp = Vector3.Cross(dir, Vector3.UnitX);
            perp = Vector3.Normalize(perp);
            return root + dir * (a * cosA) + perp * (a * sinA);
        }

        /// <summary>
        /// Frame at <paramref name="origin"/> whose up axis is <paramref name="up"/> (limbs: from the far joint back to
        /// this one, so the part extends along −up) and forward as close to <paramref name="forwardHint"/> as possible.
        /// </summary>
        static Frame Basis(Vector3 origin, Vector3 up, Vector3 forwardHint, bool limb)
        {
            up = up.LengthSquared() > 1e-10f ? Vector3.Normalize(up) : Vector3.UnitY;
            var f = forwardHint - Vector3.Dot(forwardHint, up) * up;
            if (f.LengthSquared() < 1e-6f)
            {
                // Hint along the bone: fall back to the vertical (toes down / face ahead).
                var alt = limb ? -Vector3.UnitY : Vector3.UnitZ;
                f = alt - Vector3.Dot(alt, up) * up;
                if (f.LengthSquared() < 1e-6f) f = Vector3.UnitZ - Vector3.Dot(Vector3.UnitZ, up) * up;
            }
            f = Vector3.Normalize(f);
            // right = up × forward (identity: Y × Z = X).
            var r = Vector3.Cross(up, f);
            return new Frame { origin = origin, right = r, up = up, forward = f };
        }

        /// <summary>A figure-frame part placed in the box frame (figure standing at <paramref name="feet"/>, facing <paramref name="yaw"/>).</summary>
        public static Frame ToBox(in Frame f, Vector3 feet, float yaw) => new Frame
        {
            origin = feet + PitCrewChoreography.Rotate(f.origin, yaw),
            right = PitCrewChoreography.Rotate(f.right, yaw),
            up = PitCrewChoreography.Rotate(f.up, yaw),
            forward = PitCrewChoreography.Rotate(f.forward, yaw),
        };

        /// <summary>The things a crew member holds, as frames in the box frame (PitCrewGeometry builds them to these).</summary>
        public struct Props
        {
            /// <summary>Wheel: centre, axle along forward.</summary>
            public bool wheel; public Frame wheelFrame;
            /// <summary>Wheel gun: grip, socket along forward.</summary>
            public bool gun; public Frame gunFrame;
            /// <summary>Jack: the lifting head at the saddle (facing the man) and the handle from it to the hands.</summary>
            public bool jack; public Frame jackHead, jackHandle; public float handleLength;
        }

        static Frame Upright(Vector3 origin, Vector3 forward)
        {
            forward.Y = 0f;
            forward = forward.LengthSquared() > 1e-8f ? Vector3.Normalize(forward) : Vector3.UnitZ;
            return new Frame { origin = origin, up = Vector3.UnitY, forward = forward, right = Vector3.Cross(Vector3.UnitY, forward) };
        }

        /// <summary>The props of a role in this pose (service time <paramref name="t"/> for the jack's lift).</summary>
        public static Props PropsOf(PitCrewChoreography.Role r, in PitCrewChoreography.Pose p, PitCrewChoreography.Phase phase, float t, float serviceTotal, PitCrewChoreography.Car car)
        {
            var props = new Props();
            var figFwd = PitCrewChoreography.Rotate(Vector3.UnitZ, p.yaw);
            int wheel = r <= PitCrewChoreography.Role.OnRR ? (int)r / 3 : -1;
            switch (p.hold)
            {
                case PitCrewChoreography.Hold.Wheel:
                {
                    var hub = PitCrewChoreography.Hub(car, wheel);
                    float k = 1f - ApexMath.Clamp01((Vector3.Distance(p.hands, hub) - 0.05f) / 0.55f);
                    var axle = new Vector3(MathF.Sign(hub.X), 0f, 0f);
                    // Carried upright in front (axle along the facing), turned onto the car's axle at the hub.
                    var fwd = Vector3.Lerp(figFwd, axle, k);
                    props.wheel = true;
                    props.wheelFrame = Upright(p.hands, fwd);
                    break;
                }
                case PitCrewChoreography.Hold.Gun:
                {
                    var hub = PitCrewChoreography.Hub(car, wheel);
                    var toNut = new Vector3(-MathF.Sign(hub.X), 0f, 0f);
                    props.gun = true;
                    props.gunFrame = Upright(p.hands, Vector3.Lerp(figFwd, toNut, ApexMath.Clamp01(p.kneel)));
                    break;
                }
                case PitCrewChoreography.Hold.Jack:
                {
                    var saddle = phase == PitCrewChoreography.Phase.Service
                        ? PitCrewChoreography.JackSaddle(r, car, PitCrewChoreography.Lift(t, serviceTotal))
                        : p.position + PitCrewChoreography.Rotate(new Vector3(0f, 0.12f, 1.05f), p.yaw);
                    var along = p.hands - saddle;
                    props.handleLength = along.Length();
                    var dir = props.handleLength > 1e-4f ? along / props.handleLength : Vector3.UnitY;
                    props.jack = true;
                    props.jackHead = Upright(saddle, along);
                    var side = Vector3.Cross(Vector3.UnitY, dir);
                    if (side.LengthSquared() < 1e-6f) side = Vector3.UnitX;
                    side = Vector3.Normalize(side);
                    props.jackHandle = new Frame { origin = saddle, forward = dir, right = side, up = Vector3.Cross(dir, side) };
                    break;
                }
            }
            return props;
        }

        /// <summary>The hands' target in the figure frame from a box-frame point (figure at <paramref name="feet"/>, facing <paramref name="yaw"/>).</summary>
        public static Vector3 ToFigure(Vector3 boxPoint, Vector3 feet, float yaw)
        {
            if (float.IsNaN(boxPoint.X)) return boxPoint;
            var d = boxPoint - feet;
            return PitCrewChoreography.Rotate(new Vector3(d.X, d.Y, d.Z), -yaw);
        }
    }
}
