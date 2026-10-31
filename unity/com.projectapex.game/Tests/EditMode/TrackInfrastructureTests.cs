using System;
using Apex.Tracks;
using Apex.Vehicle;
using Apex.AI;
using NUnit.Framework;

namespace Apex.Tests
{
    public class TrackInfrastructureTests
    {
        static (CircuitDefinition, TrackGeometry, TrackInfrastructurePlan) Plan(string id)
        {
            var def = TestData.Circuit(id);
            var g = TrackGeometry.FromDefinition(def);
            var setup = new VehicleSetup();
            var line = new RacingLine(g, 1.45f, setup.downforceArea * setup.airDensity * 0.5f / setup.mass);
            return (def, g, new TrackInfrastructurePlan(g, g.CornersFor(def), line.offsets, line.speed));
        }

        [Test]
        public void KerbsSitOnTheInsideAndFitTheRunoff()
        {
            var (def, g, plan) = Plan("bosphorus_a");
            foreach (var c in g.CornersFor(def))
            {
                float inside = c.right ? plan.kerbs.right[c.index] : plan.kerbs.left[c.index];
                float runoffInside = plan.RunoffAt(c.index, c.right ? 1 : -1);
                if (runoffInside >= 1.5f) Assert.That(inside, Is.GreaterThan(0.5f), $"inside kerb at {g.Distances[c.index]:F0} m");
            }
            for (int i = 0; i < g.Count; i++)
            {
                Assert.That(plan.kerbs.left[i], Is.LessThanOrEqualTo(Math.Max(0f, plan.RunoffAt(i, -1) - 0.2f) + 1e-4f));
                Assert.That(plan.kerbs.right[i], Is.LessThanOrEqualTo(Math.Max(0f, plan.RunoffAt(i, 1) - 0.2f) + 1e-4f));
                Assert.That(plan.kerbs.height[i], Is.LessThanOrEqualTo(new Apex.Vehicle.VehicleSetup().rideHeightFront - 0.005f + 1e-4f), "kerbs stay below the body clearance (no grounding when straddled)");
            }
        }

        [Test]
        public void MarshalPostsCoverTheLapAndProtectionGuardsTheBrakingZones()
        {
            var (def, g, plan) = Plan("bosphorus_a");
            Assert.That(plan.posts.Count, Is.GreaterThanOrEqualTo(def.corners.Count));
            for (int i = 0; i < plan.posts.Count; i++)
            {
                float a = plan.posts[i].distance;
                float b = i + 1 < plan.posts.Count ? plan.posts[i + 1].distance : plan.posts[0].distance + g.Length;
                Assert.That(b - a, Is.LessThanOrEqualTo(460f), $"gap after post at {a:F0} m");
                int k = g.IndexAt(a);
                Assert.That(plan.RunoffAt(k, plan.posts[i].side), Is.GreaterThanOrEqualTo(0.5f));
            }
            // Heavy braking zones (Ortaköy square entry, Balmumcu, Beşiktaş, Maçka) get impact protection on the outside.
            foreach (string name in new[] { "Meydan Girişi", "Balmumcu", "Barbaros İnişi", "Akaretler 2" })
            {
                var c = def.corners.Find(x => x.name == name);
                bool found = plan.protections.Exists(p => p.side == (c.right ? -1 : 1) && Math.Abs(g.Wrap(p.to - c.distance) - 25f) < 30f);
                Assert.That(found, Is.True, $"protection at {c.name}");
            }
            Assert.That(plan.rubber.Count, Is.GreaterThan(1000), "line-following rubber along the whole lap");
            Assert.That(plan.gates.Count, Is.GreaterThan(30));
        }

        [Test]
        public void RubberFollowsTheRacingLineAndDarkensWhereTheTyresWork()
        {
            var def = TestData.Circuit("bosphorus_a");
            var g = TrackGeometry.FromDefinition(def);
            var setup = new VehicleSetup();
            var line = new RacingLine(g, 1.45f, setup.downforceArea * setup.airDensity * 0.5f / setup.mass);
            var plan = new TrackInfrastructurePlan(g, g.CornersFor(def), line.offsets, line.speed);
            int marks = 0;
            foreach (var r in plan.rubber)
            {
                int i = g.IndexAt(r.from);
                float hw = g.Widths[i] * 0.5f;
                Assert.That(Math.Abs(r.lateralFrom) + r.width * 0.5f, Is.LessThanOrEqualTo(hw + 0.6f), "on the asphalt");
                Assert.That(r.strength, Is.InRange(0f, 1f));
                if (r.brakeMark)
                {
                    marks++;
                    // Lock-ups happen while the line is slowing down.
                    Assert.That(line.speed[(i + 3) % g.Count], Is.LessThan(line.speed[i] + 0.5f), $"brake mark at {r.from:F0} m");
                    continue;
                }
                Assert.That(Math.Abs(r.lateralFrom - line.offsets[i]), Is.LessThanOrEqualTo(TrackInfrastructurePlan.TyreTrack + 0.45f), $"rubber near the line at {r.from:F0} m");
            }
            Assert.That(marks, Is.GreaterThanOrEqualTo(10), "lock-up streaks in the heavy braking zones");
            // T1 Ortaköy braking zone carries more rubber than the middle of the longest straight.
            var t1 = g.CornersFor(def)[0];
            int brakeZone = (t1.index - (int)(40f / g.Spacing) + g.Count) % g.Count;
            int straight = 0;
            float best = float.MaxValue;
            for (int i = 0; i < g.Count; i++)
                if (Math.Abs(g.Curvatures[i]) < best && line.speed[i] >= RacingLine.MaxSpeed - 0.5f) { best = Math.Abs(g.Curvatures[i]); straight = i; }
            Assert.That(plan.rubberIntensity[brakeZone], Is.GreaterThan(plan.rubberIntensity[straight] + 0.2f));
            Assert.That(plan.rubberIntensity[straight], Is.GreaterThanOrEqualTo(0.12f), "a light film everywhere");
        }
    }
}
