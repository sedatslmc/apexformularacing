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
            // Heavy braking zones (Ortaköy, Balmumcu, Beşiktaş, Maçka) get impact protection on the outside.
            foreach (int ci in new[] { 0, 4, 5, 7 })
            {
                var c = def.corners[ci];
                bool found = plan.protections.Exists(p => p.side == (c.right ? -1 : 1) && Math.Abs(g.Wrap(p.to - c.distance) - 25f) < 30f);
                Assert.That(found, Is.True, $"protection at {c.name}");
            }
            Assert.That(plan.rubber.Count, Is.EqualTo(def.corners.Count * 2));
            Assert.That(plan.gates.Count, Is.GreaterThan(30));
        }
    }
}
