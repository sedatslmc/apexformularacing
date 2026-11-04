using System;
using System.Numerics;
using Apex.Race;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>The crew member's skeleton (Apex.Race.PitCrewFigure): joints connected, feet on the ground, hands on the job.</summary>
    public class PitCrewFigureTests
    {
        static readonly PitCrewChoreography.Car Car = PitCrewChoreography.Car.Default;
        const float T = 2.6f;

        static PitCrewFigure.Frame[] Solve(PitCrewChoreography.Pose p, float time)
        {
            var parts = new PitCrewFigure.Frame[PitCrewFigure.PartCount];
            PitCrewFigure.Solve(p, PitCrewFigure.ToFigure(p.hands, p.position, p.yaw), time, parts);
            return parts;
        }

        static PitCrewFigure.Frame P(PitCrewFigure.Frame[] f, PitCrewFigure.Part part) => f[(int)part];

        static void AssertConnected(PitCrewFigure.Frame[] f, string what)
        {
            foreach (var fr in f)
            {
                Assert.That(fr.right.Length(), Is.EqualTo(1f).Within(1e-3f), what);
                Assert.That(Vector3.Dot(fr.right, fr.up), Is.EqualTo(0f).Within(1e-3f), what);
                Assert.That(Vector3.Dot(fr.up, fr.forward), Is.EqualTo(0f).Within(1e-3f), what);
                Assert.That(Vector3.Dot(Vector3.Cross(fr.right, fr.up), fr.forward), Is.EqualTo(1f).Within(1e-3f), what + ": right-handed basis");
            }
            for (int s = 0; s < 2; s++)
            {
                var thigh = P(f, s == 0 ? PitCrewFigure.Part.ThighL : PitCrewFigure.Part.ThighR);
                var shin = P(f, s == 0 ? PitCrewFigure.Part.ShinL : PitCrewFigure.Part.ShinR);
                Assert.That(Vector3.Distance(thigh.origin - thigh.up * PitCrewFigure.Thigh, shin.origin), Is.LessThan(0.005f), what + ": knee");
                var upper = P(f, s == 0 ? PitCrewFigure.Part.UpperArmL : PitCrewFigure.Part.UpperArmR);
                var fore = P(f, s == 0 ? PitCrewFigure.Part.ForearmL : PitCrewFigure.Part.ForearmR);
                Assert.That(Vector3.Distance(upper.origin - upper.up * PitCrewFigure.UpperArm, fore.origin), Is.LessThan(0.005f), what + ": elbow");
            }
        }

        static float FootY(PitCrewFigure.Frame[] f, int side)
        {
            var shin = P(f, side == 0 ? PitCrewFigure.Part.ShinL : PitCrewFigure.Part.ShinR);
            return (shin.origin - shin.up * PitCrewFigure.Shin).Y;
        }

        static Vector3 HandAt(PitCrewFigure.Frame[] f, int side)
        {
            var fore = P(f, side == 0 ? PitCrewFigure.Part.ForearmL : PitCrewFigure.Part.ForearmR);
            return fore.origin - fore.up * (PitCrewFigure.Forearm + PitCrewFigure.Hand);
        }

        [Test]
        public void EveryRoleStaysInOnePieceWithItsFeetOnTheGroundThroughTheStop()
        {
            foreach (var r in PitCrewChoreography.Roles)
                foreach (var phase in new[] { PitCrewChoreography.Phase.Waiting, PitCrewChoreography.Phase.Ready, PitCrewChoreography.Phase.Service, PitCrewChoreography.Phase.Return })
                    for (float t = 0f; t < 3f; t += 0.1f)
                    {
                        var p = PitCrewChoreography.PoseOf(r, phase, t, T, true, Car, -1);
                        var f = Solve(p, t);
                        string what = $"{r} {phase} t={t:F1}";
                        AssertConnected(f, what);
                        // Feet: on the ground or lifted in a stride, never below it.
                        for (int s = 0; s < 2; s++)
                            Assert.That(FootY(f, s), Is.InRange(PitCrewFigure.AnkleHeight - 0.03f, 0.3f), what + $" foot {s}");
                        // Head above the hips.
                        Assert.That(P(f, PitCrewFigure.Part.Head).origin.Y, Is.GreaterThan(P(f, PitCrewFigure.Part.Torso).origin.Y + 0.4f), what);
                    }
        }

        [Test]
        public void TheGunManKneelsAtTheWheelWithTheGunOnTheNut()
        {
            var gun = PitCrewChoreography.Role.GunFL;
            var p = PitCrewChoreography.PoseOf(gun, PitCrewChoreography.Phase.Service, T * 0.5f, T, true, Car, -1);
            Assert.That(p.kneel, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(p.hold, Is.EqualTo(PitCrewChoreography.Hold.Gun));
            var f = Solve(p, T * 0.5f);
            // One knee on the ground.
            var knee = P(f, PitCrewFigure.Part.ShinL).origin;
            Assert.That(knee.Y, Is.LessThan(0.2f), "knee down");
            Assert.That(P(f, PitCrewFigure.Part.Torso).origin.Y, Is.LessThan(0.7f), "hips low");
            // Both hands on the gun at the hub.
            var target = PitCrewFigure.ToFigure(p.hands, p.position, p.yaw);
            for (int s = 0; s < 2; s++)
                Assert.That(Vector3.Distance(HandAt(f, s), target), Is.LessThan(0.15f), $"hand {s} on the gun");
            var hub = PitCrewChoreography.Hub(Car, 0);
            Assert.That(Vector3.Distance(new Vector3(p.hands.X, 0f, p.hands.Z), new Vector3(hub.X, 0f, hub.Z)), Is.LessThan(0.4f), "the gun at the hub");
        }

        [Test]
        public void TheOnManCarriesTheNewWheelInBothHandsAndFitsItToTheHub()
        {
            var on = PitCrewChoreography.Role.OnRR;
            var hub = PitCrewChoreography.Hub(Car, 3);
            var waiting = PitCrewChoreography.PoseOf(on, PitCrewChoreography.Phase.Service, T * 0.2f, T, true, Car, -1);
            Assert.That(waiting.hold, Is.EqualTo(PitCrewChoreography.Hold.Wheel));
            var f = Solve(waiting, T * 0.2f);
            var target = PitCrewFigure.ToFigure(waiting.hands, waiting.position, waiting.yaw);
            // Hands either side of the wheel (0.3 m from its centre), at chest height.
            Assert.That(target.Y, Is.InRange(0.8f, 1.1f));
            for (int s = 0; s < 2; s++)
                Assert.That(Vector3.Distance(HandAt(f, s), target), Is.InRange(0.2f, 0.4f), $"hand {s} at the rim");
            // At the end of fitting the wheel is on the hub.
            float fit = PitCrewChoreography.OnStart + (PitCrewChoreography.OnEnd - PitCrewChoreography.OnStart) * 0.75f;
            var fitting = PitCrewChoreography.PoseOf(on, PitCrewChoreography.Phase.Service, T * fit, T, true, Car, -1);
            Assert.That(Vector3.Distance(fitting.hands, hub), Is.LessThan(0.12f), "wheel on the hub");
        }

        [Test]
        public void RunningMovesTheLegsInAlternatingStrides()
        {
            var p = new PitCrewChoreography.Pose { speed = 4f, hands = PitCrewChoreography.NoHands };
            float maxDiff = 0f, minLeft = 1f, maxLeft = -1f;
            for (float t = 0f; t < 1f; t += 0.02f)
            {
                var f = Solve(p, t);
                var l = P(f, PitCrewFigure.Part.ShinL); var r = P(f, PitCrewFigure.Part.ShinR);
                float zl = (l.origin - l.up * PitCrewFigure.Shin).Z, zr = (r.origin - r.up * PitCrewFigure.Shin).Z;
                maxDiff = MathF.Max(maxDiff, MathF.Abs(zl - zr));
                minLeft = MathF.Min(minLeft, zl); maxLeft = MathF.Max(maxLeft, zl);
            }
            Assert.That(maxDiff, Is.GreaterThan(0.5f), "feet apart in a stride");
            Assert.That(maxLeft - minLeft, Is.GreaterThan(0.4f), "the left foot swings");
            // Standing still: feet side by side.
            var still = Solve(new PitCrewChoreography.Pose { hands = PitCrewChoreography.NoHands }, 0.3f);
            Assert.That(MathF.Abs(FootY(still, 0) - PitCrewFigure.AnkleHeight), Is.LessThan(0.01f));
        }

        [Test]
        public void TheJackManPushesTheHandleDownToLiftTheCar()
        {
            var jack = PitCrewChoreography.Role.FrontJack;
            var before = PitCrewChoreography.PoseOf(jack, PitCrewChoreography.Phase.Service, 0.01f, T, true, Car, -1);
            var lifted = PitCrewChoreography.PoseOf(jack, PitCrewChoreography.Phase.Service, T * 0.5f, T, true, Car, -1);
            Assert.That(lifted.hold, Is.EqualTo(PitCrewChoreography.Hold.Jack));
            Assert.That(lifted.hands.Y, Is.LessThan(before.hands.Y - 0.2f), "handle down while the car is up");
            Assert.That(PitCrewChoreography.Lift(T * 0.5f, T), Is.GreaterThan(0.05f));
        }

        [Test]
        public void TheRaceEngineerSitsAtTheDeskWithHandsOnTheKeyboard()
        {
            const float desk = 0.98f;
            var parts = new PitCrewFigure.Frame[PitCrewFigure.PartCount];
            for (float t = 0f; t < 30f; t += 0.37f)
            {
                PitCrewFigure.SolveSeated(t, 3, desk, parts);
                AssertConnected(parts, $"t={t:F1}");
                // On the stool: hips at seat height, feet on the footrest, head above the shoulders.
                Assert.That(P(parts, PitCrewFigure.Part.Torso).origin.Y, Is.EqualTo(PitCrewFigure.SeatHeight + 0.06f).Within(0.01f));
                for (int s = 0; s < 2; s++) Assert.That(FootY(parts, s), Is.InRange(0.1f, 0.4f));
                // The left hand is always on the keyboard.
                Assert.That(Vector3.Distance(HandAt(parts, 0), new Vector3(-0.17f, desk + 0.03f, 0.42f)), Is.LessThan(0.05f), $"t={t:F1}");
            }
        }

        [Test]
        public void TheCrewIsFourteenPeople()
        {
            Assert.That(PitCrewChoreography.CrewSize, Is.EqualTo(14));
            Assert.That(PitCrewChoreography.Roles.Length, Is.EqualTo(14));
            int perWheel = 0, jacks = 0;
            foreach (var r in PitCrewChoreography.Roles)
                if (r <= PitCrewChoreography.Role.OnRR) perWheel++;
                else jacks++;
            Assert.That(perWheel, Is.EqualTo(12), "three per wheel");
            Assert.That(jacks, Is.EqualTo(2), "one at the front, one at the rear");
        }
    }
}
