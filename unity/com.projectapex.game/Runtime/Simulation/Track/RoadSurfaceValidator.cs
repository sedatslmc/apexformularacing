using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Core;
using Apex.Vehicle;

namespace Apex.Tracks
{
    /// <summary>
    /// Geometric validation of the physical road surface (the collider triangles of
    /// <see cref="RoadSurface"/>) against the vehicle. Samples the whole lap every 2 m and checks:
    /// surface continuity (the collider height under the centreline and across the road equals the
    /// designed height; no foreign surface such as terrain or a wall above or inside the road),
    /// longitudinal grade over 2 m and 50 m, grade change, vertical curvature against
    /// <see cref="VerticalLimits"/> at the speed cars actually reach there, cross-fall (the road is
    /// built flat across), width, and the 20 grid slots with a car standing on them.
    /// Every finding carries lap distance and world coordinates.
    /// </summary>
    public sealed class RoadSurfaceValidator
    {
        public enum Severity { Info, Warning, Critical }

        public struct Finding
        {
            public Severity severity;
            public string check;
            public float distance, lapDistance;
            public Vector3 position;
            public float value, limit;
            public string detail;
            public override string ToString() =>
                $"{severity,-8} {check,-22} lap {lapDistance,6:F0} m ({position.X:F0}, {position.Y:F1}, {position.Z:F0})  {value:G4} (limit {limit:G4}) {detail}";
        }

        public sealed class Summary
        {
            public float maxGrade2, maxGrade50, maxGradeChange, worstCurvatureUse, minClearance = float.PositiveInfinity;
            public float minCrestRadius = float.PositiveInfinity, minSagRadius = float.PositiveInfinity;
            public float maxSurfaceOffset, maxCrossFall, minWidth = float.PositiveInfinity, heightMin = float.PositiveInfinity, heightMax = float.NegativeInfinity;
            public int samples, gridSlotsOk;
        }

        public readonly List<Finding> findings = new List<Finding>();
        public readonly Summary summary = new Summary();
        readonly CircuitTestBed bed;
        readonly VerticalLimits limits;
        /// <summary>Speed (m/s) a car reaches at each sample (<see cref="VerticalLimits.DesignSpeeds"/>).</summary>
        public readonly float[] speed;

        public const float SurfaceTolerance = 0.005f;
        public const float GradeLimit = 0.12f;
        public const float Grade50Limit = 0.125f;
        /// <summary>Change of grade over 10 m beyond which the road reads as a kink.</summary>
        public const float GradeChangeLimit = 0.02f;

        public int Critical => findings.FindAll(f => f.severity == Severity.Critical).Count;

        public RoadSurfaceValidator(CircuitTestBed testBed)
        {
            bed = testBed;
            var g = bed.g;
            limits = new VerticalLimits(new VehicleSetup());
            speed = limits.DesignSpeeds(g, bed.line.speed);
            CheckProfile();
            CheckSurface();
            CheckGrid();
            findings.Sort((a, b) => b.severity != a.severity ? b.severity.CompareTo(a.severity) : (b.value / MathF.Max(b.limit, 1e-6f)).CompareTo(a.value / MathF.Max(a.limit, 1e-6f)));
        }

        float Lap(float raw) => bed.g.Wrap(raw - bed.def.startLineDistance);

        void Add(Severity s, string check, int i, float value, float limit, string detail = "")
        {
            var g = bed.g;
            findings.Add(new Finding { severity = s, check = check, distance = g.Distances[i], lapDistance = Lap(g.Distances[i]), position = g.Positions[i], value = value, limit = limit, detail = detail });
        }

        /// <summary>Grade and vertical curvature along the centreline (as built: 2 m samples).</summary>
        void CheckProfile()
        {
            var g = bed.g;
            int n = g.Count;
            float ds = g.Spacing;
            int k10 = Math.Max(1, (int)MathF.Round(10f / ds)), k50 = (int)MathF.Round(50f / ds);
            var y = new float[n];
            for (int i = 0; i < n; i++) y[i] = g.Positions[i].Y;
            float Y(int i) => y[((i % n) + n) % n];
            // Grade per sample (central difference over ±1).
            var grade = new float[n];
            for (int i = 0; i < n; i++) grade[i] = (Y(i + 1) - Y(i - 1)) / (2f * ds);
            float G(int i) => grade[((i % n) + n) % n];
            bool lastCurv = false;
            for (int i = 0; i < n; i++)
            {
                summary.samples++;
                summary.heightMin = MathF.Min(summary.heightMin, y[i]);
                summary.heightMax = MathF.Max(summary.heightMax, y[i]);
                float g2 = MathF.Abs(G(i));
                summary.maxGrade2 = MathF.Max(summary.maxGrade2, g2);
                if (g2 > GradeLimit) Add(Severity.Critical, "grade (2 m)", i, g2, GradeLimit);
                float g50 = MathF.Abs(Y(i + k50) - Y(i)) / (k50 * ds);
                summary.maxGrade50 = MathF.Max(summary.maxGrade50, g50);
                if (g50 > Grade50Limit) Add(Severity.Critical, "grade (50 m)", i, g50, Grade50Limit);
                float dg = MathF.Abs(G(i + k10) - G(i));
                summary.maxGradeChange = MathF.Max(summary.maxGradeChange, dg);
                if (dg > GradeChangeLimit) Add(Severity.Info, "grade change (10 m)", i, dg, GradeChangeLimit, "covered by the curvature check");
                // Vertical curvature over the wheelbase scale (±4 m), + = crest.
                int h = Math.Max(1, (int)MathF.Round(4f / ds));
                float kappa = -(Y(i + h) - 2f * Y(i) + Y(i - h)) / (h * h * ds * ds);
                if (kappa > 0f) summary.minCrestRadius = MathF.Min(summary.minCrestRadius, 1f / kappa);
                else if (kappa < 0f) summary.minSagRadius = MathF.Min(summary.minSagRadius, -1f / kappa);
                float v = speed[i];
                float lim = kappa >= 0f ? limits.MaxCrest(v) : limits.MaxSag(v);
                float use = MathF.Abs(kappa) / MathF.Max(lim, 1e-6f);
                summary.worstCurvatureUse = MathF.Max(summary.worstCurvatureUse, use);
                float clearance = limits.Clearance(kappa, v);
                summary.minClearance = MathF.Min(summary.minClearance, clearance);
                bool bad = use > 1f;
                if (bad && !lastCurv)
                    Add(clearance < limits.Clearance(0f, v) - 0.002f && clearance < 0f ? Severity.Critical : Severity.Warning, kappa >= 0f ? "crest curvature" : "sag curvature", i, MathF.Abs(kappa), lim,
                        $"R {1f / MathF.Max(MathF.Abs(kappa), 1e-6f):F0} m at {v * 3.6f:F0} km/h, body clearance {clearance * 1000f:F0} mm");
                lastCurv = bad;
            }
        }

        /// <summary>The collider surface across the road and run-off: designed height, no foreign surface.</summary>
        void CheckSurface()
        {
            var g = bed.g;
            var w = bed.world;
            int n = g.Count;
            for (int i = 0; i < n; i++)
            {
                float hw = g.Widths[i] * 0.5f;
                summary.minWidth = MathF.Min(summary.minWidth, g.Widths[i]);
                float yl = float.NaN, yr = float.NaN;
                for (float lat = -hw; lat <= hw + 0.01f; lat += hw / 4f)
                {
                    float l = MathF.Max(-hw + 0.05f, MathF.Min(hw - 0.05f, lat));
                    var p = RoadSurface.P(g, i, l, 0f);
                    if (!w.SurfaceBelow(p.X, p.Z, p.Y + 3f, p.Y - 3f, out var hit))
                    {
                        Add(Severity.Critical, "missing surface", i, l, 0f, "no collider under the road");
                        continue;
                    }
                    float off = hit.point.Y - p.Y;
                    summary.maxSurfaceOffset = MathF.Max(summary.maxSurfaceOffset, MathF.Abs(off));
                    if (hit.part != "Road" && hit.part != "PitApron" && hit.part != "PitLane" && !(hit.part == "Kerbs" && MathF.Abs(l) > hw - 0.1f))
                        Add(Severity.Critical, "foreign surface", i, off, 0f, $"{hit.part} at lateral {l:F1} m");
                    else if (MathF.Abs(off) > SurfaceTolerance && hit.part == "Road")
                        Add(Severity.Critical, "surface height", i, off, SurfaceTolerance, $"lateral {l:F1} m");
                    if (l < -hw + 0.1f) yl = hit.point.Y;
                    if (l > hw - 0.1f) yr = hit.point.Y;
                }
                if (!float.IsNaN(yl) && !float.IsNaN(yr))
                {
                    float cross = MathF.Abs(yl - yr) / (2f * hw);
                    summary.maxCrossFall = MathF.Max(summary.maxCrossFall, cross);
                }
                // Terrain must stay below the road, run-off and pit surfaces (it may not poke through anywhere).
                float wl = bed.surface.wallLeft[i], wr = bed.surface.wallRight[i];
                foreach (float lat in new[] { -wl + 0.3f, -hw, 0f, hw, wr - 0.3f })
                {
                    var p = RoadSurface.P(g, i, lat, 0f);
                    if (!w.SurfaceBelow(p.X, p.Z, p.Y + 3f, p.Y - 30f, out var t, m => m.name == "Terrain")) continue;
                    if (!w.SurfaceBelow(p.X, p.Z, p.Y + 3f, p.Y - 3f, out var top, m => m.name != "Terrain" && m.name != "Barriers" && m.name != "PitWall")) continue;
                    float gap = top.point.Y - t.point.Y;
                    if (gap < 0.02f)
                        Add(gap <= 0f ? Severity.Critical : Severity.Warning, "terrain intrusion", i, -gap, 0f, $"lateral {lat:F1} m under {top.part}");
                }
            }
        }

        /// <summary>A car settles on each grid slot: four wheels on the road, no body contact, level.</summary>
        void CheckGrid()
        {
            for (int slot = 0; slot < 20; slot++)
            {
                var (d, lat) = bed.GridSlot(slot);
                var rig = bed.NewRig(out var sim);
                bed.g.FrameAt(d, out _, out var f, out _, lat);
                rig.Place(bed.GroundAt(d, lat), f, 0.05f);
                sim.holdBrake = true;
                float maxBody = 0f;
                int minWheels = 4;
                for (int k = 0; k < 240; k++)
                {
                    rig.Step(CircuitTestBed.Dt);
                    if (k > 60) { maxBody = MathF.Max(maxBody, rig.BodyContacts); minWheels = Math.Min(minWheels, rig.WheelContacts); }
                }
                int i = bed.g.IndexAt(d);
                bool ok = maxBody == 0 && minWheels == 4 && rig.velocity.Length() < 0.05f;
                for (int w = 0; w < 4; w++) ok &= rig.wheelParts[w] == "Road";
                if (ok) summary.gridSlotsOk++;
                else Add(Severity.Critical, "grid slot", i, slot, 0f, $"wheels {minWheels}, body contacts {maxBody}, speed {rig.velocity.Length():F2} m/s, surfaces {string.Join("/", rig.wheelParts)}");
                summary.minClearance = MathF.Min(summary.minClearance, rig.MinClearance);
            }
        }

        public string Report(int maxFindings = 40)
        {
            var s = summary;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"samples {s.samples} (every {bed.g.Spacing:F2} m), height {s.heightMin:F1}..{s.heightMax:F1} m, width min {s.minWidth:F1} m");
            sb.AppendLine($"grade max {s.maxGrade2 * 100:F1} % (2 m), {s.maxGrade50 * 100:F1} % (50 m); grade change max {s.maxGradeChange * 100:F2} %/10 m");
            sb.AppendLine($"vertical radius min: crest {s.minCrestRadius:F0} m, sag {s.minSagRadius:F0} m; worst use of the vehicle limit {s.worstCurvatureUse * 100:F0} %; min body clearance {s.minClearance * 1000:F0} mm");
            sb.AppendLine($"collider surface vs design max {s.maxSurfaceOffset * 1000:F1} mm, cross-fall max {s.maxCrossFall * 100:F2} %, grid slots ok {s.gridSlotsOk}/20");
            int crit = Critical, warn = findings.FindAll(f => f.severity == Severity.Warning).Count;
            sb.AppendLine($"findings: {crit} critical, {warn} warnings, {findings.Count - crit - warn} info");
            int k = 0;
            foreach (var f in findings) if (k++ < maxFindings) sb.AppendLine("  " + f);
            return sb.ToString();
        }
    }
}
