using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Apex.Game;
using Apex.Vehicle;
using UnityEngine;

// Usage: dotnet run -- <out_dir> [--check]
// Writes car_lod{0,1,2}.obj and prints statistics. With --check, exits with code 1 if a
// body material has inward-facing triangles overall (negative signed volume) or a LOD
// exceeds its triangle budget.
string outDir = args.Length > 0 ? args[0] : ".";
bool primitivesOnly = Array.IndexOf(args, "--primitives") >= 0;
bool primitiveFailed = false;
if (primitivesOnly || Array.IndexOf(args, "--check") >= 0)
{
    // Each primitive must be closed and outward-facing on its own (positive volume).
    void Vol(string name, System.Action<MeshBuilder> f)
    {
        var mb = new MeshBuilder();
        f(mb);
        float v = SignedVolumeOf(mb.ToMesh(name));
        Console.WriteLine($"primitive {name,-12} {v:F6}");
        if (v <= 0f) primitiveFailed = true;
    }
    Vol("box", mb => mb.Box(Vector3.zero, Vector3.one));
    Vol("plateYZ+x", mb => mb.PlateYZ(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) }, 1f, 0.1f));
    Vol("plateYZ-rev", mb => mb.PlateYZ(new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) }, -1f, 0.1f));
    Vol("plateXZ", mb => mb.PlateXZ(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) }, 0f, 0.1f));
    Vol("airfoil", mb => mb.Airfoil(Vector3.zero, 1f, 0.3f, 0.1f, -0.05f, 5f, 8));
    Vol("tube", mb => mb.Tube(Vector3.zero, Vector3.forward, 0.1f, 8));
    Vol("loft-desc", mb => mb.Loft(new[] { new LoftSection(1f, 0f, 0.2f, 0.2f), new LoftSection(0f, 0f, 0.2f, 0.2f) }, 12));
    Vol("loft-asc", mb => mb.Loft(new[] { new LoftSection(0f, 0f, 0.2f, 0.2f), new LoftSection(1f, 0f, 0.2f, 0.2f) }, 12));
    Vol("lathe", mb => mb.Lathe(new[] { new Vector2(-0.1f, 0.001f), new Vector2(-0.1f, 0.3f), new Vector2(0.1f, 0.3f), new Vector2(0.1f, 0.001f) }, 16));
    if (primitivesOnly) return;
}
bool check = Array.IndexOf(args, "--check") >= 0;
int[] budget = { 20000, 8000, 4000 };
bool failed = primitiveFailed;
Directory.CreateDirectory(outDir);
var setup = new VehicleSetup();
var geo = new CarGeometry(setup).BuildBody();
var inv = CultureInfo.InvariantCulture;

float SignedVolume(Mesh m) => SignedVolumeOf(m);
static float SignedVolumeOf(Mesh m)
{
    double v = 0;
    for (int i = 0; i < m.triangles.Count; i += 3)
    {
        var a = m.vertices[m.triangles[i]]; var b = m.vertices[m.triangles[i + 1]]; var c = m.vertices[m.triangles[i + 2]];
        v += Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
    }
    return (float)v;
}

for (int lod = 0; lod < CarGeometry.LodCount; lod++)
{
    var parts = new List<(string mat, Mesh mesh)>();
    foreach (var group in new[] { geo.body, geo.frontWing, geo.rearWing })
        foreach (var kv in group[lod]) parts.Add((kv.Key, kv.Value.ToMesh(kv.Key)));
    // Wheels at static ride height.
    for (int i = 0; i < 4; i++)
    {
        bool front = i < 2;
        var mount = setup.MountPosition(i, out _);
        float r = front ? setup.wheelRadiusFront : setup.wheelRadiusRear;
        float w = front ? 0.3f : 0.405f;
        var centre = new Vector3(mount.X, r - setup.cogHeight, mount.Z);
        var tire = new MeshBuilder { matrix = new Matrix4x4 { offset = centre } };
        CarGeometry.TireMesh(tire, r, w, 0.229f, lod);
        parts.Add(("Tire", tire.ToMesh("Tire")));
        var rim = new MeshBuilder { matrix = new Matrix4x4 { offset = centre } };
        CarGeometry.RimMesh(rim, w, 0.229f, lod);
        parts.Add(("Rim", rim.ToMesh("Rim")));
        var band = new MeshBuilder { matrix = new Matrix4x4 { offset = centre } };
        CarGeometry.CompoundBand(band, r, w, 0.229f, lod);
        parts.Add(("Belt", band.ToMesh("Band")));
        if (lod == 0)
        {
            var disc = new MeshBuilder { matrix = new Matrix4x4 { offset = centre } };
            CarGeometry.BrakeDisc(disc, 0.16f, lod);
            parts.Add(("Rim", disc.ToMesh("Disc")));
        }
        if (lod == 0)
        {
            var up = new MeshBuilder { matrix = new Matrix4x4 { offset = centre } };
            CarGeometry.Upright(up, r, w, i % 2 == 0, front, lod);
            parts.Add(("Carbon", up.ToMesh("Upright")));
            var links = new MeshBuilder();
            foreach (var (inner, outer, rad) in CarGeometry.Links(setup, i)) links.Tube(inner, centre + outer, rad, 6);
            parts.Add(("Carbon", links.ToMesh("Links")));
        }
    }
    if (lod == 0)
    {
        var sw = CarGeometry.SteeringWheelPosition(setup);
        var carbon = new MeshBuilder { matrix = new Matrix4x4 { offset = sw } };
        var grips = new MeshBuilder { matrix = new Matrix4x4 { offset = sw } };
        var disp = new MeshBuilder { matrix = new Matrix4x4 { offset = sw } };
        CarGeometry.SteeringWheel(carbon, grips, disp);
        parts.Add(("Carbon", carbon.ToMesh("Wheel"))); parts.Add(("Interior", grips.ToMesh("Grips"))); parts.Add(("Display", disp.ToMesh("Display")));
        foreach (float gs in new[] { -1f, 1f })
        {
            var glove = new MeshBuilder { matrix = new Matrix4x4 { offset = sw + new Vector3(gs * 0.15f, 0f, -0.03f) } };
            CarGeometry.GloveMesh(glove);
            parts.Add(("Glove", glove.ToMesh("Glove")));
        }
        var helmetPos = new Vector3(0f, -setup.cogHeight + 0.76f, setup.FrontAxleZ - 2.05f);
        var shell = new MeshBuilder { matrix = new Matrix4x4 { offset = helmetPos } };
        var visor = new MeshBuilder { matrix = new Matrix4x4 { offset = helmetPos } };
        CarGeometry.HelmetMesh(shell, visor);
        parts.Add(("Helmet", shell.ToMesh("Helmet"))); parts.Add(("Visor", visor.ToMesh("Visor")));
    }
    var sb = new StringBuilder();
    int vBase = 1, tris = 0;
    foreach (var (mat, mesh) in parts)
    {
        sb.AppendLine($"o {mesh.name}_{mat}");
        sb.AppendLine($"usemtl {mat}");
        foreach (var v in mesh.vertices) sb.AppendLine(string.Format(inv, "v {0:F4} {1:F4} {2:F4}", v.x, v.y, v.z));
        for (int i = 0; i < mesh.triangles.Count; i += 3)
            sb.AppendLine($"f {mesh.triangles[i] + vBase} {mesh.triangles[i + 1] + vBase} {mesh.triangles[i + 2] + vBase}");
        vBase += mesh.vertices.Count;
        tris += mesh.triangles.Count / 3;
    }
    File.WriteAllText(Path.Combine(outDir, $"car_lod{lod}.obj"), sb.ToString());
    Console.WriteLine($"LOD{lod}: {tris} triangles, {vBase - 1} vertices, {parts.Count} parts (budget {budget[lod]})");
    if (tris > budget[lod]) failed = true;
    foreach (var group in new[] { geo.body, geo.frontWing, geo.rearWing })
    foreach (var kv in group[lod])
        if (SignedVolume(kv.Value.ToMesh(kv.Key)) <= 0f)
        {
            Console.WriteLine($"  inward-facing body mesh: LOD{lod} {kv.Key}");
            failed = true;
        }
    if (lod == 0)
        foreach (var kv in geo.body[0])
            Console.WriteLine(string.Format(inv, "  body {0,-9} tris {1,6}  signed volume {2:F4} m³", kv.Key, kv.Value.TriangleCount, SignedVolume(kv.Value.ToMesh(kv.Key))));
}
if (check && failed)
{
    Console.WriteLine("model check: FAILED");
    Environment.Exit(1);
}
if (check) Console.WriteLine("model check: OK");
