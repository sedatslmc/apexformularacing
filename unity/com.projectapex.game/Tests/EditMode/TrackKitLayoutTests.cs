using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.AI;
using Apex.Core;
using Apex.Tracks;
using Apex.Vehicle;
using NUnit.Framework;

namespace Apex.Tests
{
    public class TrackKitLayoutTests
    {
        static (TrackGeometry g, TrackInfrastructurePlan plan, RoadSurface surface, TrackKitLayout kit) build;

        static (TrackGeometry g, TrackInfrastructurePlan plan, RoadSurface surface, TrackKitLayout kit) Kit()
        {
            if (build.kit != null) return build;
            var def = TestData.Circuit("bosphorus_a");
            var g = TrackGeometry.FromDefinition(def);
            var setup = new VehicleSetup();
            var line = new RacingLine(g, 1.45f, setup.downforceArea * setup.airDensity * 0.5f / setup.mass);
            var plan = new TrackInfrastructurePlan(g, g.CornersFor(def), line.offsets, line.speed);
            var surface = new RoadSurface(g, def, plan.kerbs.left, plan.kerbs.right, plan.kerbs.height);
            build = (g, plan, surface, new TrackKitLayout(g, plan, surface));
            return build;
        }

        static float Lateral(TrackGeometry g, Vector3 p, float d) => Vector3.Dot(p - g.PositionAt(d), g.RightAt(d));

        [Test]
        public void BarrierChainsAreClosedAndBlocksKeepTheirLength()
        {
            var (g, _, _, kit) = Kit();
            foreach (int side in new[] { -1, 1 })
            {
                var blocks = kit.placements.FindAll(p => p.side == side && !p.transverse && (p.module == TrackKitLayout.Module.BarrierBlock || p.module == TrackKitLayout.Module.GuardrailSpan));
                blocks.Sort((a, b) => a.distance.CompareTo(b.distance));
                float covered = 0f;
                int joints = 0, open = 0, nominal = 0, all = 0;
                for (int k = 0; k < blocks.Count; k++)
                {
                    var a = blocks[k];
                    covered += a.length;
                    if (a.module == TrackKitLayout.Module.BarrierBlock)
                    {
                        Assert.That(a.length, Is.InRange(0.3f, TrackKitLayout.BlockLength * 1.25f), $"block at {a.distance:F0} m");
                        nominal += MathF.Abs(a.length / TrackKitLayout.BlockLength - 1f) < 0.1f ? 1 : 0;
                        all++;
                    }
                    var b = blocks[(k + 1) % blocks.Count];
                    var endA = a.position + a.forward * a.length * 0.5f;
                    var startB = b.position - b.forward * b.length * 0.5f;
                    joints++;
                    // Within a run the chord ends coincide; at run changes (block -> guardrail) they may step.
                    if (Vector3.Distance(endA, startB) > 0.02f) open++;
                }
                TestContext.WriteLine($"side {side}: {blocks.Count} modules, {covered:F0} m of {g.Length:F0} m, {open} steps at run changes");
                Assert.That(nominal, Is.GreaterThan(all * 0.95f), "blocks keep their precast length (arc-length spacing)");
                // Chords follow the barrier line, which is longer than the centreline outside corners.
                Assert.That(covered, Is.InRange(g.Length * 0.9f, g.Length * 1.25f), "the barrier line is covered all round");
                int steps = kit.placements.FindAll(p => p.side == side && p.transverse).Count;
                TestContext.WriteLine($"side {side}: {steps} transverse blocks close steps in the barrier line");
                Assert.That(open, Is.LessThan(joints / 20 + 2), "gaps only where the barrier type or line changes");
            }
        }

        [Test]
        public void ModulesStayOffTheRoad()
        {
            var (g, _, surface, kit) = Kit();
            foreach (var p in kit.placements)
            {
                if (p.transverse) continue;
                float lat = Lateral(g, p.position, p.distance) * p.side;
                float half = g.WidthAt(p.distance) * 0.5f;
                switch (p.module)
                {
                    case TrackKitLayout.Module.EnergyModule:
                    case TrackKitLayout.Module.TyreStack:
                        // In front of the barrier, but never on the asphalt.
                        Assert.That(lat, Is.GreaterThan(half + 0.3f), $"{p.module} at {p.distance:F0} m");
                        break;
                    default:
                        Assert.That(lat, Is.GreaterThan(kit.WallAt(p.side, p.distance) - 0.15f), $"{p.module} at {p.distance:F0} m");
                        break;
                }
                Assert.That(MathF.Abs(Vector3.Dot(p.outward, Vector3.UnitY)), Is.LessThan(1e-4f));
                Assert.That(Vector3.Dot(p.outward, g.RightAt(p.distance)) * p.side, Is.GreaterThan(0.5f), "outward points away from the track");
            }
        }

        [Test]
        public void FenceHasGatesAndNoPanelsOnMediansOrThePitWall()
        {
            var (g, plan, surface, kit) = Kit();
            int pitSide = surface.definition.pit != null ? surface.definition.pit.side : 0;
            int gates = 0, panels = 0;
            foreach (var p in kit.placements)
            {
                if (p.module == TrackKitLayout.Module.FenceGate) gates++;
                if (p.module != TrackKitLayout.Module.FencePanel && p.module != TrackKitLayout.Module.FencePost) continue;
                panels++;
                int i = g.IndexAt(p.distance);
                // End posts stand on the run boundary: check the sample just before as well.
                int h = g.IndexAt(p.distance - 2.5f);
                Assert.That(MathF.Max(plan.RunoffAt(i, p.side), plan.RunoffAt(h, p.side)), Is.GreaterThan(0.9f), $"fence on a median at {p.distance:F0} m");
                Assert.That(p.side == pitSide && surface.pitMask[i] && surface.pitMask[h], Is.False, $"fence on the pit wall at {p.distance:F0} m");
            }
            TestContext.WriteLine($"{panels} fence posts/panels, {gates} gates of {plan.gates.Count} planned");
            Assert.That(gates, Is.GreaterThan(plan.gates.Count / 2));
        }

        [Test]
        public void BannersHangInTheBrakingZonesOnly()
        {
            var (g, plan, _, kit) = Kit();
            int banners = kit.Count(TrackKitLayout.Module.Banner);
            Assert.That(banners, Is.GreaterThan(20));
            foreach (var p in kit.placements)
            {
                if (p.module != TrackKitLayout.Module.Banner) continue;
                bool inZone = false;
                foreach (var pr in plan.protections)
                {
                    float before = ApexMath.PosMod(g.Wrap(pr.from + 90f) - p.distance, g.Length);
                    if (before >= TrackKitLayout.BannerZoneEnd - 4f && before <= TrackKitLayout.BannerZoneStart + 4f) inZone = true;
                }
                Assert.That(inZone, $"banner at {p.distance:F0} m");
            }
        }

        [Test]
        public void ImpactProtectionModulesSitWherePlanned()
        {
            var (g, plan, _, kit) = Kit();
            int skipped = 0;
            foreach (var pr in plan.protections)
            {
                bool tyres = pr.kind == TrackInfrastructurePlan.ProtectionKind.TyreStack;
                float unit = tyres ? TrackKitLayout.TyreStackLength : TrackKitLayout.EnergyModuleLength;
                float need = (tyres ? TrackKitLayout.TyreStackDepth : TrackKitLayout.EnergyModuleDepth) + 0.2f;
                float len = ApexMath.PosMod(pr.to - pr.from, g.Length);
                var mods = kit.placements.FindAll(p => (p.module == TrackKitLayout.Module.TyreStack || p.module == TrackKitLayout.Module.EnergyModule)
                    && p.side == pr.side && ApexMath.PosMod(p.distance - pr.from + 1f, g.Length) < len + 2f);
                // Every stretch with room for the modules is protected...
                for (float d = 2f * unit; d < len - 2f * unit; d += g.Spacing)
                {
                    float at = g.Wrap(pr.from + d);
                    if (plan.RunoffAt(g.IndexAt(at), pr.side) < need + 0.3f) { skipped++; continue; }
                    bool near = mods.Exists(p => MathF.Abs(ApexMath.PosMod(p.distance - at + g.Length * 0.5f, g.Length) - g.Length * 0.5f) < unit * 1.5f);
                    Assert.That(near, $"protection missing at {at:F0} m");
                }
                // ...and never two modules on top of each other (merged ranges of adjacent corners).
                for (int a = 0; a < mods.Count; a++)
                    for (int b = a + 1; b < mods.Count; b++)
                        Assert.That(Vector3.Distance(mods[a].position, mods[b].position), Is.GreaterThan(unit * 0.5f), $"doubled module at {mods[a].distance:F0} m");
            }
            TestContext.WriteLine($"{skipped} samples without room for protection (narrow run-off)");
        }
    }
}
