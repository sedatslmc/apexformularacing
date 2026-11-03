using System;
using System.Numerics;
using Apex.Race;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>Pit lane lanes and the pit crew's stop (docs/PIT_LANE.md).</summary>
    public class PitCrewTests
    {
        static readonly PitCrewChoreography.Car Car = PitCrewChoreography.Car.Default;
        const float BoxCentre = 7.5f;

        [Test]
        public void CarsUseTheFastLaneAndTurnInOnlyForTheirBox()
        {
            Assert.That(PitLaneLayout.FromEdge(200f, false, BoxCentre), Is.EqualTo(PitLaneLayout.FastLaneCentre));
            Assert.That(PitLaneLayout.FromEdge(0f, false, BoxCentre), Is.EqualTo(BoxCentre).Within(1e-4f), "on the box line at the box");
            float mid = PitLaneLayout.FromEdge(PitLaneLayout.TurnIn * 0.5f, false, BoxCentre);
            Assert.That(mid, Is.InRange(PitLaneLayout.FastLaneCentre + 0.5f, BoxCentre - 0.5f), "a smooth turn-in");
            Assert.That(PitLaneLayout.FromEdge(-PitLaneLayout.PullOut - 1f, true, BoxCentre), Is.EqualTo(PitLaneLayout.FastLaneCentre).Within(1e-4f), "back in the fast lane");
            // A car in the fast lane stays on the fast-lane side of the divider.
            Assert.That(PitLaneLayout.FastLaneCentre + PitLaneLayout.CarHalfWidth, Is.LessThan(PitLaneLayout.Divider));
        }

        [Test]
        public void NoCrewMemberStandsInTheFastLane()
        {
            // Box frame x is across the lane; the fast lane is on the far side from the garage. Garage on the left
            // (pit side -1): the fast lane lies at x > +(box centre - fast lane edge).
            int garage = -1;
            float fastLaneEdgeX = BoxCentre - (PitLaneLayout.FastLaneCentre + PitLaneLayout.CarHalfWidth);   // m from box centre
            foreach (var r in PitCrewChoreography.Roles)
                foreach (var phase in new[] { PitCrewChoreography.Phase.Waiting, PitCrewChoreography.Phase.Ready, PitCrewChoreography.Phase.Service, PitCrewChoreography.Phase.Return })
                    for (float t = 0f; t < 3f; t += 0.05f)
                    {
                        var p = PitCrewChoreography.PoseOf(r, phase, t, 2.6f, true, Car, garage);
                        // Distance from the box centre towards the fast lane (away from the garage), plus 0.3 m body.
                        float towardsFast = -garage * p.position.X + 0.3f;
                        Assert.That(towardsFast, Is.LessThan(fastLaneEdgeX), $"{r} {phase} t={t:F2}");
                    }
        }

        [Test]
        public void TheStopRunsInOrderAndFitsTheServiceTime()
        {
            const float T = 2.6f;
            int wheelsOff = 0;
            for (int w = 0; w < 4; w++)
            {
                var hub = PitCrewChoreography.Hub(Car, w);
                var off = (PitCrewChoreography.Role)(w * 3 + 1);
                var on = (PitCrewChoreography.Role)(w * 3 + 2);
                var gun = (PitCrewChoreography.Role)(w * 3);
                // Old wheel comes off before the new one goes on; the new one is on before the jacks drop.
                Assert.That(PitCrewChoreography.OffEnd, Is.LessThanOrEqualTo(PitCrewChoreography.OnStart));
                Assert.That(PitCrewChoreography.OnEnd, Is.LessThan(PitCrewChoreography.JacksDown));
                var mid = PitCrewChoreography.PoseOf(off, PitCrewChoreography.Phase.Service, T * 0.3f, T, true, Car, -1);
                Assert.That(mid.carriesWheel, Is.True, "off man carries the old wheel");
                var waitOn = PitCrewChoreography.PoseOf(on, PitCrewChoreography.Phase.Service, T * 0.2f, T, true, Car, -1);
                Assert.That(waitOn.carriesWheel, Is.True, "on man holds the new wheel");
                var fitted = PitCrewChoreography.PoseOf(on, PitCrewChoreography.Phase.Service, T * 0.9f, T, true, Car, -1);
                Assert.That(fitted.carriesWheel, Is.False, "new wheel fitted");
                var kneel = PitCrewChoreography.PoseOf(gun, PitCrewChoreography.Phase.Service, T * 0.5f, T, true, Car, -1);
                Assert.That(kneel.kneel, Is.GreaterThan(0.9f), "gun man kneels at the wheel");
                Assert.That(Vector3.Distance(kneel.position, new Vector3(hub.X, 0f, hub.Z)), Is.LessThan(0.8f));
                if (PitCrewChoreography.WheelOffHub(T * 0.5f, T, true)) wheelsOff++;
            }
            Assert.That(wheelsOff, Is.EqualTo(4));
            Assert.That(PitCrewChoreography.WheelOffHub(T * 0.05f, T, true), Is.False);
            Assert.That(PitCrewChoreography.WheelOffHub(T * 0.95f, T, true), Is.False, "all wheels back on before release");
            Assert.That(PitCrewChoreography.WheelOffHub(T * 0.5f, T, false), Is.False, "repair only: wheels stay on");
            Assert.That(PitCrewChoreography.ReleaseGreen(T * 0.5f, T), Is.False);
            Assert.That(PitCrewChoreography.ReleaseGreen(T, T), Is.True);
            // Jacks: car up during the work, down at release.
            Assert.That(PitCrewChoreography.Lift(T * 0.5f, T), Is.GreaterThan(0.04f));
            Assert.That(PitCrewChoreography.Lift(T, T), Is.EqualTo(0f));
            // A long stop (nut stuck, new nose) stretches the same choreography.
            var late = PitCrewChoreography.PoseOf(PitCrewChoreography.Role.OnFL, PitCrewChoreography.Phase.Service, 7f * 0.2f, 7f, true, Car, -1);
            Assert.That(late.carriesWheel, Is.True);
        }

        [Test]
        public void TheCrewIsOutOfTheWayBeforeTheCarArrivesAndBackInTheGarageAfter()
        {
            foreach (var r in PitCrewChoreography.Roles)
            {
                var ready = PitCrewChoreography.PoseOf(r, PitCrewChoreography.Phase.Ready, PitCrewChoreography.RunTime + 0.1f, 2.6f, true, Car, -1);
                var work = PitCrewChoreography.WorkSpot(Car, r);
                // Ready spots are not on the car's path: it rolls in from behind (−z) up to its stop (nose at Car.nose).
                bool onPath = Math.Abs(ready.position.X) < 1.0f + 0.3f && ready.position.Z < Car.nose + 0.3f;
                Assert.That(onPath, Is.False, $"{r} waits on the car's path");
                var home = PitCrewChoreography.PoseOf(r, PitCrewChoreography.Phase.Return, PitCrewChoreography.ReturnTime, 2.6f, true, Car, -1);
                Assert.That(Vector3.Distance(home.position, PitCrewChoreography.WaitSpot(r, -1)), Is.LessThan(0.01f), $"{r} back at the garage");
            }
        }
    }
}
