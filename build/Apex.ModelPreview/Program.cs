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
if (Array.IndexOf(args, "--flag") >= 0)
{
    // Writes the flag raster as a binary PPM for visual checks.
    int h = 400;
    var px = Apex.Environment.TurkishFlagDesign.Rasterize(h);
    int w = px.Length / 4 / h;
    using var fs = File.Create(Path.Combine(outDir, "turkish_flag.ppm"));
    var header = Encoding.ASCII.GetBytes($"P6 {w} {h} 255\n");
    fs.Write(header, 0, header.Length);
    for (int j = h - 1; j >= 0; j--)
        for (int i = 0; i < w; i++) { int k = (j * w + i) * 4; fs.WriteByte(px[k]); fs.WriteByte(px[k + 1]); fs.WriteByte(px[k + 2]); }
    return;
}
if (Array.IndexOf(args, "--art") >= 0)
{
    // Event emblem (on grey) and the İznik tile (2x2, to check the seams) as binary PPMs.
    void WritePpm(string name, float[] px, int n, int repeat, float bg)
    {
        int size = n * repeat;
        using var fs = File.Create(Path.Combine(outDir, name));
        var header = Encoding.ASCII.GetBytes($"P6 {size} {size} 255\n");
        fs.Write(header, 0, header.Length);
        for (int j = size - 1; j >= 0; j--)
            for (int i = 0; i < size; i++)
            {
                int k = ((j % n) * n + (i % n)) * 4;
                for (int c = 0; c < 3; c++) fs.WriteByte((byte)Math.Round(255f * (px[k + c] * px[k + 3] + bg * (1f - px[k + 3]))));
            }
    }
    WritePpm("event_emblem.ppm", Apex.Environment.EventArt.Emblem(512), 512, 1, 0.5f);
    WritePpm("iznik_tile.ppm", Apex.Environment.EventArt.IznikTile(256), 256, 2, 0f);
    return;
}
if (Array.IndexOf(args, "--landmarks") >= 0)
{
    // One OBJ per landmark, each at the origin, for inspection renders.
    void Export(string name, System.Action<LandmarkGeometry> build)
    {
        var lg = new LandmarkGeometry();
        build(lg);
        var sbl = new StringBuilder();
        int vb = 1, tr = 0;
        foreach (var kv in lg.parts)
        {
            sbl.AppendLine($"o {kv.Key}\nusemtl {kv.Key}");
            var mbx = kv.Value;
            var ns = mbx.ComputeNormals();
            foreach (var v in mbx.Vertices) sbl.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F3} {1:F3} {2:F3}", v.x, v.y, v.z));
            foreach (var nn in ns) sbl.AppendLine(string.Format(CultureInfo.InvariantCulture, "vn {0:F3} {1:F3} {2:F3}", nn.x, nn.y, nn.z));
            var ts = mbx.Triangles;
            for (int i = 0; i < ts.Count; i += 3) sbl.AppendLine($"f {ts[i] + vb}//{ts[i] + vb} {ts[i + 1] + vb}//{ts[i + 1] + vb} {ts[i + 2] + vb}//{ts[i + 2] + vb}");
            vb += mbx.VertexCount;
            tr += ts.Count / 3;
            float vol = SignedVolumeOf(mbx.ToMesh(kv.Key));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  {0,-14} {1,-12} tris {2,6} volume {3,12:F1}", name, kv.Key, ts.Count / 3, vol));
        }
        File.WriteAllText(Path.Combine(outDir, name + ".obj"), sbl.ToString());
        Console.WriteLine($"{name}: {tr} triangles");
    }
    Export("bridge", l => l.Bridge(new Vector3(0f, 30f, 0f), new Vector3(1560f, 20f, 0f), -0.4f));
    Export("ortakoy", l => l.OrtakoyMosque());
    Export("dolmabahce", l => { l.Palace(284f, 48f, 18f, true); });
    Export("galata", l => l.GalataTower(63f));
    Export("maiden", l => l.MaidensTower());
    Export("sultanahmet", l => l.ImperialMosque(11.7f, 26f, 6, 64f, LandmarkGeometry.Stone));
    Export("hagiasophia", l => l.ImperialMosque(15.5f, 34f, 4, 60f, LandmarkGeometry.HagiaPlaster));
    Export("ferry", l => l.Ferry());
    Export("freighter", l => l.Freighter());
    return;
}
if (Array.IndexOf(args, "--monument") >= 0)
{
    // Monumental flag at its bosphorus_a site: flag_site.obj (installation LOD0, cloth at 6 m/s wind, terrain patch,
    // recentred on the pole) and turkish_flag.ppm for the cloth texture.
    var ic = CultureInfo.InvariantCulture;
    string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../unity/com.projectapex.game/Runtime/Resources/Tracks/bosphorus_a.json"));
    var def = Apex.Tracks.CircuitDefinition.FromJson(json);
    var g = Apex.Tracks.TrackGeometry.FromDefinition(def);
    var field = new Apex.Environment.TerrainField(g, def);
    var (fx, fz) = def.monumentalFlag.Value;
    MonumentalFlagGeometry.Levels(field.Height, fx, fz, out float baseY, out float terrace, out float deck);
    var parts = new Dictionary<string, MeshBuilder>();
    MeshBuilder Get(string k) { if (!parts.TryGetValue(k, out var m)) parts[k] = m = new MeshBuilder(); return m; }
    MonumentalFlagGeometry.Build(k => Get(k), 0, terrace, deck);
    float poleTop = MonumentalFlagGeometry.PoleTop(deck) - 0.4f;
    float L = Apex.Environment.MonumentalFlagSite.FlagLength, H = L / Apex.Environment.TurkishFlagDesign.AspectRatio;
    int nu = 46, nv = 31;
    var cloth = Get("TurkishFlag");
    Vector3 C(int i, int j)
    {
        var p = Apex.Environment.FlagCloth.Evaluate(i / (nu - 1f), j / (nv - 1f), L, H, 6f, 3.7f, 0.4f);
        return new Vector3(p.X + MonumentalFlagGeometry.TopRadius + 0.05f, poleTop + p.Y, p.Z);
    }
    for (int j = 0; j < nv - 1; j++)
        for (int i = 0; i < nu - 1; i++)
        {
            var a = C(i, j); var b = C(i + 1, j); var c = C(i + 1, j + 1); var d = C(i, j + 1);
            var n = Vector3.Cross(b - a, d - a);
            cloth.QuadFacing(a, b, c, d, new Vector2(i / (nu - 1f), 1f - j / (nv - 1f)), new Vector2((i + 1) / (nu - 1f), 1f - j / (nv - 1f)),
                new Vector2((i + 1) / (nu - 1f), 1f - (j + 1) / (nv - 1f)), new Vector2(i / (nu - 1f), 1f - (j + 1) / (nv - 1f)), n);
        }
    // Terrain patch (5 m grid, 160 m) around the site.
    var terr = Get("Ground");
    for (float z0 = -80f; z0 < 80f; z0 += 5f)
        for (float x0 = -80f; x0 < 80f; x0 += 5f)
        {
            Vector3 T(float x, float z) => new Vector3(x, field.Height(fx + x, fz + z) - baseY, z);
            terr.QuadFacing(T(x0, z0), T(x0 + 5f, z0), T(x0 + 5f, z0 + 5f), T(x0, z0 + 5f), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
        }
    var sb = new StringBuilder();
    int vb = 1;
    foreach (var kv in parts)
    {
        sb.AppendLine($"o {kv.Key}\nusemtl {kv.Key}");
        foreach (var v in kv.Value.Vertices) sb.AppendLine(string.Format(ic, "v {0:F3} {1:F3} {2:F3}", v.x, v.y, v.z));
        foreach (var uv in kv.Value.UVs) sb.AppendLine(string.Format(ic, "vt {0:F4} {1:F4}", uv.x, uv.y));
        var ts = kv.Value.Triangles;
        for (int i = 0; i < ts.Count; i += 3) sb.AppendLine($"f {ts[i] + vb}/{ts[i] + vb} {ts[i + 1] + vb}/{ts[i + 1] + vb} {ts[i + 2] + vb}/{ts[i + 2] + vb}");
        vb += kv.Value.VertexCount;
        Console.WriteLine($"  {kv.Key,-14} {kv.Value.TriangleCount,6} tris");
    }
    Directory.CreateDirectory(outDir);
    File.WriteAllText(Path.Combine(outDir, "flag_site.obj"), sb.ToString());
    Console.WriteLine(string.Format(ic, "site {0:F0},{1:F0}: base {2:F1} m, terrace +{3:F1} m, pole top +{4:F1} m", fx, fz, baseY, terrace, poleTop));
    return;
}
if (Array.IndexOf(args, "--kit") >= 0)
{
    // Circuit kit previews: kit_catalog_lod{0,1,2}.obj (every module side by side) and
    // kit_t1_lod{0,1,2}.obj (the real placements and road surface around T1 Ortaköy, recentred).
    var ic = CultureInfo.InvariantCulture;
    void Write(string file, Dictionary<string, MeshBuilder> parts)
    {
        var sb = new StringBuilder();
        int vb = 1, total = 0;
        foreach (var kv in parts)
        {
            if (kv.Value.TriangleCount == 0) continue;
            sb.AppendLine($"o {kv.Key}\nusemtl {kv.Key}");
            foreach (var v in kv.Value.Vertices) sb.AppendLine(string.Format(ic, "v {0:F4} {1:F4} {2:F4}", v.x, v.y, v.z));
            foreach (var uv in kv.Value.UVs) sb.AppendLine(string.Format(ic, "vt {0:F4} {1:F4}", uv.x, uv.y));
            var ts = kv.Value.Triangles;
            for (int i = 0; i < ts.Count; i += 3) sb.AppendLine($"f {ts[i] + vb}/{ts[i] + vb} {ts[i + 1] + vb}/{ts[i + 1] + vb} {ts[i + 2] + vb}/{ts[i + 2] + vb}");
            vb += kv.Value.VertexCount;
            total += kv.Value.TriangleCount;
        }
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, file), sb.ToString());
        Console.WriteLine($"{file}: {total} triangles");
    }
    MeshBuilder Get(Dictionary<string, MeshBuilder> parts, string k) { if (!parts.TryGetValue(k, out var m)) parts[k] = m = new MeshBuilder(); return m; }
    var modules = (Apex.Tracks.TrackKitLayout.Module[])Enum.GetValues(typeof(Apex.Tracks.TrackKitLayout.Module));
    for (int lod = 0; lod < 3; lod++)
    {
        var parts = new Dictionary<string, MeshBuilder>();
        float x = 0f;
        foreach (var m in modules)
        {
            var one = new Dictionary<string, MeshBuilder>();
            TrackKitGeometry.Module(m, k => Get(one, k), lod, 0);
            int tris = 0;
            foreach (var kv in one) { float ox = x; Get(parts, kv.Key).Append(kv.Value, v => new Vector3(v.x + ox, v.y, v.z)); tris += kv.Value.TriangleCount; }
            Console.WriteLine($"  LOD{lod} {m,-14} {tris,6} tris");
            x += m == Apex.Tracks.TrackKitLayout.Module.TyreStack ? 2.2f : 1.8f;
        }
        foreach (var (name, build) in new (string, Action<TrackKitGeometry.Parts>)[] { ("MarshalPost", B => TrackKitGeometry.MarshalPost(B, lod)), ("FlagPanel", B => TrackKitGeometry.FlagPanelHousing(B, lod)) })
        {
            var one = new Dictionary<string, MeshBuilder>();
            build(k => Get(one, k));
            int tris = 0;
            foreach (var kv in one) { float ox = x + 1.5f; Get(parts, kv.Key).Append(kv.Value, v => new Vector3(v.x + ox, v.y, v.z)); tris += kv.Value.TriangleCount; }
            Console.WriteLine($"  LOD{lod} {name,-14} {tris,6} tris");
            x += 3.6f;
        }
        Write($"kit_catalog_lod{lod}.obj", parts);
    }
    // T1 Ortaköy: lap metres 1700-2250 (the premium vertical slice).
    string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../unity/com.projectapex.game/Runtime/Resources/Tracks/bosphorus_a.json"));
    var def = Apex.Tracks.CircuitDefinition.FromJson(json);
    var g = Apex.Tracks.TrackGeometry.FromDefinition(def);
    var su = new Apex.Vehicle.VehicleSetup();
    var line = new Apex.AI.RacingLine(g, 1.45f, su.downforceArea * su.airDensity * 0.5f / su.mass);
    var plan = new Apex.Tracks.TrackInfrastructurePlan(g, g.CornersFor(def), line.offsets, line.speed);
    var surface = new Apex.Tracks.RoadSurface(g, def, plan.kerbs.left, plan.kerbs.right, plan.kerbs.height);
    var kit = new Apex.Tracks.TrackKitLayout(g, plan, surface);
    float from = g.Wrap(1700f + def.startLineDistance), to = g.Wrap(2250f + def.startLineDistance);
    bool InWindow(float d) => Apex.Core.ApexMath.PosMod(d - from, g.Length) < Apex.Core.ApexMath.PosMod(to - from, g.Length);
    var c0 = g.PositionAt(g.Wrap(2150f + def.startLineDistance));
    Vector3 Local(System.Numerics.Vector3 v) => new Vector3(v.X - c0.X, v.Y - c0.Y, v.Z - c0.Z);
    var cache = new Dictionary<(Apex.Tracks.TrackKitLayout.Module, int, int), Dictionary<string, MeshBuilder>>();
    for (int lod = 0; lod < 3; lod++)
    {
        var parts = new Dictionary<string, MeshBuilder>();
        int placed = 0;
        foreach (var p in kit.placements)
        {
            if (!InWindow(p.distance)) continue;
            var key = (p.module, lod, p.variant);
            if (!cache.TryGetValue(key, out var mod)) { mod = new Dictionary<string, MeshBuilder>(); TrackKitGeometry.Module(p.module, k => Get(mod, k), lod, p.variant); cache[key] = mod; }
            var f = TrackKitGeometry.Transform(p);
            foreach (var kv in mod) Get(parts, kv.Key).Append(kv.Value, v => { var w = f(v); return new Vector3(w.x - c0.X, w.y - c0.Y, w.z - c0.Z); });
            placed++;
        }
        // Road, kerbs and run-off of the same stretch (the RoadSurface triangles).
        foreach (var (sm, mats) in new[] { (surface.road, new[] { "Asphalt", "Line" }), (surface.kerbs, new[] { "KerbRed", "KerbWhite" }), (surface.runoff, new[] { "RunoffAsphalt" }) })
            for (int s = 0; s < sm.triangles.Length; s++)
            {
                var mb = Get(parts, mats[Math.Min(s, mats.Length - 1)]);
                var tri = sm.triangles[s];
                for (int i = 0; i < tri.Count; i += 3)
                {
                    var a = sm.vertices[tri[i]]; var b = sm.vertices[tri[i + 1]]; var c = sm.vertices[tri[i + 2]];
                    var pr = g.Project((a + b + c) / 3f);
                    if (!InWindow(pr.distance) || MathF.Abs(pr.lateral) > 40f) continue;
                    mb.TriangleFacing(Local(a), Local(b), Local(c), new Vector2(sm.uvs[tri[i]].X, sm.uvs[tri[i]].Y), new Vector2(sm.uvs[tri[i + 1]].X, sm.uvs[tri[i + 1]].Y), new Vector2(sm.uvs[tri[i + 2]].X, sm.uvs[tri[i + 2]].Y), Vector3.up);
                }
            }
        if (lod == 0)
        {
            Console.WriteLine($"T1 window: {placed} module placements");
            foreach (float lap in new[] { 1900f, 2000f, 2080f, 2150f, 2200f })
            {
                float d = g.Wrap(lap + def.startLineDistance);
                var p0 = Local(g.PositionAt(d)); var t = g.TangentAt(d); var r = g.RightAt(d);
                Console.WriteLine(string.Format(ic, "view lap {0}: pos {1:F1},{2:F1},{3:F1} fwd {4:F2},{5:F2} right {6:F2},{7:F2} width {8:F1}", lap, p0.x, p0.y, p0.z, t.X, t.Z, r.X, r.Z, g.WidthAt(d)));
            }
        }
        Write($"kit_t1_lod{lod}.obj", parts);
    }
    return;
}
if (Array.IndexOf(args, "--environment") >= 0)
{
    // Bosphorus environment as one OBJ (terrain, road, city, trees, landmarks) plus budget stats.
    int qa = Array.IndexOf(args, "--quality");
    int quality = qa >= 0 && qa + 1 < args.Length ? int.Parse(args[qa + 1]) : 1;
    int ti = Array.IndexOf(args, "--track");
    string trackId = ti >= 0 && ti + 1 < args.Length ? args[ti + 1] : "bosphorus_a";
    string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../unity/com.projectapex.game/Runtime/Resources/Tracks/" + trackId + ".json"));
    var def = Apex.Tracks.CircuitDefinition.FromJson(json);
    var g = Apex.Tracks.TrackGeometry.FromDefinition(def);
    var field = new Apex.Environment.TerrainField(g, def);
    var parts = new Dictionary<string, MeshBuilder>();
    MeshBuilder B(string k) { if (!parts.TryGetValue(k, out var m)) parts[k] = m = new MeshBuilder(); return m; }
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var city = new Apex.Environment.CityLayout(field, def, new Apex.Environment.CityLayout.Options { quality = quality });
    Console.WriteLine($"city: {city.lots.Count} lots, {city.trees.Count} trees, {sw.ElapsedMilliseconds} ms");
    int[] bands = new int[3];
    foreach (var lot in city.lots) { bands[lot.band]++; EnvironmentGeometry.Building(B, lot, lot.band == 0); }
    Console.WriteLine($"  bands near/mid/far: {bands[0]}/{bands[1]}/{bands[2]}");
    int cityTris = 0; foreach (var kv in parts) cityTris += kv.Value.TriangleCount;
    Console.WriteLine($"  city triangles (LOD0): {cityTris}");
    // Near terrain on a 24 m grid (the game uses 12 m), far terrain, quays.
    const float c = 24f;
    for (float z = field.MinZ; z < field.MaxZ - 1f; z += c)
        for (float x = field.MinX; x < field.MaxX - 1f; x += c)
        {
            var a = new Vector3(x, field.Height(x, z), z); var b = new Vector3(x + c, field.Height(x + c, z), z);
            var cc = new Vector3(x + c, field.Height(x + c, z + c), z + c); var d = new Vector3(x, field.Height(x, z + c), z + c);
            B("Ground").QuadFacing(a, b, cc, d, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
        }
    float cx = (field.MinX + field.MaxX) * 0.5f, cz = (field.MinZ + field.MaxZ) * 0.5f;
    EnvironmentGeometry.FarTerrain(B(EnvironmentGeometry.FarGround), field, cx, cz, 6400f);
    Console.WriteLine($"  far terrain triangles: {parts[EnvironmentGeometry.FarGround].TriangleCount}");
    EnvironmentGeometry.QuayWall(B(EnvironmentGeometry.Quay), def.shoreline, true);
    EnvironmentGeometry.QuayWall(B(EnvironmentGeometry.Quay), def.farShoreline, false);
    // Road ribbon.
    for (int i = 0; i < g.Count; i++)
    {
        int j = (i + 1) % g.Count;
        Vector3 P(int k, float lat) { var p = g.Positions[k] + g.Rights[k] * lat; return new Vector3(p.X, p.Y + 0.05f, p.Z); }
        float hi = g.Widths[i] * 0.5f, hj = g.Widths[j] * 0.5f;
        B("Asphalt").QuadFacing(P(i, -hi), P(i, hi), P(j, hj), P(j, -hj), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
    }
    // Street-circuit infrastructure: same plan and geometry code as the Unity builder.
    {
        var su = new VehicleSetup();
        var rl = new Apex.AI.RacingLine(g, 1.45f, su.downforceArea * su.airDensity * 0.5f / su.mass);
        var plan = new Apex.Tracks.TrackInfrastructurePlan(g, g.CornersFor(def), rl.offsets, rl.speed);
        Console.WriteLine($"infrastructure: {plan.posts.Count} marshal posts/panels, {plan.protections.Count} protection zones ({plan.protections.FindAll(x => x.kind == Apex.Tracks.TrackInfrastructurePlan.ProtectionKind.TyreStack).Count} tyre), {plan.gates.Count} gates, {plan.rubber.Count} rubber zones, {plan.patches.Count} patches, {plan.manholes.Count} manholes, {plan.laneDashes.Count} lane dashes");
        int nS = g.Count;
        var wl = new float[nS]; var wr = new float[nS];
        bool InPit(int i) => def.pit != null && def.pit.Contains(g.Distances[i], g.Length);
        for (int i = 0; i < nS; i++)
        {
            float hw = g.Widths[i] * 0.5f;
            wl[i] = InPit(i) && def.pit.side < 0 ? hw + 13.5f : hw + MathF.Max(plan.RunoffAt(i, -1), plan.kerbs.left[i] + 0.2f);
            wr[i] = InPit(i) && def.pit.side > 0 ? hw + 13.5f : hw + MathF.Max(plan.RunoffAt(i, 1), plan.kerbs.right[i] + 0.2f);
        }
        Vector3 Q(int i, float lat, float dy) => TrackInfrastructureGeometry.P(g, i, lat, dy);
        for (int i = 0; i < nS; i++)
        {
            int j = (i + 1) % nS;
            foreach (int side in new[] { -1, 1 })
            {
                float[] k = side < 0 ? plan.kerbs.left : plan.kerbs.right;
                float[] w = side < 0 ? wl : wr;
                float hi = g.Widths[i] * 0.5f, hj = g.Widths[j] * 0.5f;
                // Run-off (paved) between kerb and barrier.
                B("RunoffAsphalt").QuadFacing(Q(i, side * (hi + k[i]), -0.1f), Q(i, side * w[i], -0.1f), Q(j, side * w[j], -0.1f), Q(j, side * (hj + k[j]), -0.1f), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
                if (k[i] > 0f || k[j] > 0f)
                {
                    string km = ((int)(g.Distances[i] / 2.5f)) % 2 == 0 ? "KerbRed" : "KerbWhite";
                    float yi = plan.kerbs.height[i], yj = plan.kerbs.height[j];
                    B(km).QuadFacing(Q(i, side * hi, 0.05f), Q(i, side * (hi + k[i] * 0.5f), 0.05f + yi), Q(j, side * (hj + k[j] * 0.5f), 0.05f + yj), Q(j, side * hj, 0.05f), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
                    B(km).QuadFacing(Q(i, side * (hi + k[i] * 0.5f), 0.05f + yi), Q(i, side * (hi + k[i]), 0.0f), Q(j, side * (hj + k[j]), 0.0f), Q(j, side * (hj + k[j] * 0.5f), 0.05f + yj), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
                }
            }
        }
        // Barriers, fence, banners and impact protection: the circuit kit at its mid detail level (as seen from afar).
        {
            var surf = new Apex.Tracks.RoadSurface(g, def, plan.kerbs.left, plan.kerbs.right, plan.kerbs.height);
            var kitLayout = new Apex.Tracks.TrackKitLayout(g, plan, surf);
            var kitCache = new Dictionary<(Apex.Tracks.TrackKitLayout.Module, int), Dictionary<string, MeshBuilder>>();
            foreach (var kp in kitLayout.placements)
            {
                if (!kitCache.TryGetValue((kp.module, kp.variant), out var mod))
                {
                    mod = new Dictionary<string, MeshBuilder>();
                    var local = mod;
                    TrackKitGeometry.Module(kp.module, k2 => { if (!local.TryGetValue(k2, out var m2)) local[k2] = m2 = new MeshBuilder(); return m2; }, 1, kp.variant);
                    kitCache[(kp.module, kp.variant)] = mod;
                }
                var f = TrackKitGeometry.Transform(kp);
                foreach (var kv in mod) B(kv.Key).Append(kv.Value, f);
            }
        }
        foreach (var pa in plan.patches) TrackInfrastructureGeometry.RoadDecal(B(TrackInfrastructureGeometry.Patch), g, g.Wrap(pa.distance), g.Wrap(pa.distance + pa.length), pa.lateral - pa.width * 0.5f, pa.lateral + pa.width * 0.5f, 0.07f, 0f);
        foreach (var (f, t2, lat) in plan.laneDashes) TrackInfrastructureGeometry.RoadDecal(B(TrackInfrastructureGeometry.Dash), g, g.Wrap(f), g.Wrap(t2), lat - 0.07f, lat + 0.07f, 0.072f, 0f);
        foreach (var z in plan.rubber) TrackInfrastructureGeometry.RoadDecal(B(TrackInfrastructureGeometry.Rubber), g, z.from, z.to, z.lateralFrom, z.lateralTo, 0.075f, 25f);
        foreach (var (d, lat) in plan.manholes) TrackInfrastructureGeometry.ManholeCover(B(TrackInfrastructureGeometry.Manhole), Q(g.IndexAt(d), lat, 0.074f), 0.38f);
        foreach (var po in plan.posts)
        {
            int i = g.IndexAt(po.distance);
            float[] w = po.side < 0 ? wl : wr;
            var fwd = new Vector3(g.Tangents[i].X, 0f, g.Tangents[i].Z).normalized;
            var rot = Quaternion.LookRotation(-fwd);
            void Place(Action<TrackKitGeometry.Parts> build, Vector3 at)
            {
                var tmp = new Dictionary<string, MeshBuilder>();
                MeshBuilder T(string key) { if (!tmp.TryGetValue(key, out var mb2)) tmp[key] = mb2 = new MeshBuilder(); return mb2; }
                build(T);
                foreach (var kv in tmp)
                {
                    var v = kv.Value.Vertices; var tr = kv.Value.Triangles;
                    for (int q = 0; q < tr.Count; q += 3)
                    {
                        Vector3 a = at + rot * v[tr[q]], b = at + rot * v[tr[q + 1]], c = at + rot * v[tr[q + 2]];
                        B(kv.Key).TriangleFacing(a, b, c, Vector2.zero, Vector2.zero, Vector2.zero, Vector3.Cross(c - a, b - a));
                    }
                }
            }
            Place(T2 => TrackKitGeometry.MarshalPost(T2, 1), Q(i, po.side * (w[i] + 2.3f), -0.12f));
            Place(T2 => TrackKitGeometry.FlagPanelHousing(T2, 1), Q(i, po.side * (w[i] + 0.75f), -0.12f));
        }
        // Event identity: gantry, pit building with podium and trophy, timing tower, race control (EventLayout as in the game).
        if (def.identity == Apex.Race.EventIdentity.Bosphorus)
        {
            var ev = new Apex.Environment.EventLayout(g, def, field, wl, wr, 13.5f);
            void PutEv(Action<EventGeometry.Parts> build, Vector3 at, float yaw, Vector3 local = default, float localYaw = 0f)
            {
                var tmp = new Dictionary<string, MeshBuilder>();
                MeshBuilder T(string key) { if (!tmp.TryGetValue(key, out var mb2)) tmp[key] = mb2 = new MeshBuilder(); return mb2; }
                build(T);
                var q = Quaternion.Euler(0f, yaw, 0f) ;
                var ql = Quaternion.Euler(0f, localYaw, 0f);
                foreach (var kv in tmp)
                {
                    var v = kv.Value.Vertices; var tr = kv.Value.Triangles;
                    for (int t = 0; t < tr.Count; t += 3)
                    {
                        Vector3 W(Vector3 p) => at + q * (local + ql * p);
                        Vector3 a = W(v[tr[t]]), b = W(v[tr[t + 1]]), c = W(v[tr[t + 2]]);
                        B(kv.Key).TriangleFacing(a, b, c, Vector2.zero, Vector2.zero, Vector2.zero, Vector3.Cross(c - a, b - a));
                    }
                }
            }
            Vector3 U(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
            g.FrameAt(def.startLineDistance, out var sp, out var sf, out _);
            PutEv(T => EventGeometry.Gantry(T, g.WidthAt(def.startLineDistance)), U(sp), Apex.Environment.EventLayout.Yaw(sf));
            if (ev.pitBuilding.placed)
            {
                var at = U(ev.pitBuilding.position);
                PutEv(T => EventGeometry.PitBuilding(T, ev.pitLength, ev.bayZ, def.pit.boxSpacing), at, ev.pitBuilding.yaw);
                var podiumAt = new Vector3(EventGeometry.PitDepth * 0.5f, EventGeometry.PitRoof, ev.podiumZ);
                PutEv(EventGeometry.Podium, at, ev.pitBuilding.yaw, podiumAt, -90f);
                PutEv(T => { EventGeometry.Trophy(T); }, at, ev.pitBuilding.yaw, podiumAt + Quaternion.Euler(0f, -90f, 0f) * EventGeometry.TrophyOnPodium, -90f);
            }
            if (ev.timingTower.placed) PutEv(EventGeometry.TimingTower, U(ev.timingTower.position), ev.timingTower.yaw);
            if (ev.raceControl.placed) PutEv(EventGeometry.RaceControl, U(ev.raceControl.position), ev.raceControl.yaw);
            Console.WriteLine($"  start line ({sp.X:F0}, {sp.Y:F1}, {sp.Z:F0}) heading {Apex.Environment.EventLayout.Yaw(sf):F0}°, pit building at ({ev.pitBuilding.position.X:F0}, {ev.pitBuilding.position.Z:F0}) yaw {ev.pitBuilding.yaw:F0}, tower ({ev.timingTower.position.X:F0}, {ev.timingTower.position.Z:F0}), race control ({ev.raceControl.position.X:F0}, {ev.raceControl.position.Z:F0})");
            Console.WriteLine($"event: pit building {ev.pitBuilding.placed} ({ev.pitLength:F0} m, {ev.bayZ.Count} bays), timing tower {ev.timingTower.placed}, race control {ev.raceControl.placed}, {ev.flags.Count} event flags");
            foreach (var (fp, _, _) in ev.flags) Console.WriteLine($"  flag at ({fp.X:F0}, {fp.Z:F0})");
        }
    }
    // Trees (instanced in the game).
    foreach (var t in city.trees)
    {
        var trunk = new MeshBuilder(); var crown = new MeshBuilder();
        EnvironmentGeometry.TreeMesh(t.kind, trunk, crown);
        void Copy(MeshBuilder src, string key)
        {
            var v = src.Vertices; var tr = src.Triangles;
            Vector3 W(Vector3 p) => new Vector3(t.x + p.x * t.height, t.ground + p.y * t.height, t.z + p.z * t.height);
            for (int i = 0; i < tr.Count; i += 3)
            {
                Vector3 a = W(v[tr[i]]), b = W(v[tr[i + 1]]), cc = W(v[tr[i + 2]]);
                B(key).TriangleFacing(a, b, cc, Vector2.zero, Vector2.zero, Vector2.zero, Vector3.Cross(cc - a, b - a));
            }
        }
        Copy(trunk, EnvironmentGeometry.Bark);
        Copy(crown, t.kind == 1 ? EnvironmentGeometry.Pine : t.kind == 2 ? EnvironmentGeometry.Cypress : EnvironmentGeometry.Foliage);
    }
    // Landmarks (same placement rules as EnvironmentBuilder, simplified).
    float north = def.trueNorthBearing;
    void Put(LandmarkGeometry lg, Vector3 pos, float yawDeg)
    {
        var q = Quaternion.Euler(0f, yawDeg, 0f);
        foreach (var kv in lg.parts)
        {
            var v = kv.Value.Vertices; var tr = kv.Value.Triangles;
            for (int i = 0; i < tr.Count; i += 3)
            {
                Vector3 a = pos + q * v[tr[i]], b = pos + q * v[tr[i + 1]], cc = pos + q * v[tr[i + 2]];
                B(kv.Key).TriangleFacing(a, b, cc, Vector2.zero, Vector2.zero, Vector2.zero, Vector3.Cross(cc - a, b - a));
            }
        }
    }
    Vector3 LP(string id) { var l = def.FindLandmark(id); return new Vector3(l.east, field.Height(l.east, l.north), l.north); }
    Vector3 Inland(Vector3 p, float dir, float clear)
    {
        for (float s = 0; s < 400; s += 5) { var x = p + new Vector3(dir * s, 0, 0); if (!field.IsWater(x.x, x.z) && field.land.CoastDistance(x.x, x.z) >= clear) { x.y = field.Height(x.x, x.z); return x; } }
        return p;
    }
    float ShoreYaw(List<(float east, float north)> sh, float z) => MathF.Atan2(Apex.Environment.TerrainField.ShoreX(sh, z + 60) - Apex.Environment.TerrainField.ShoreX(sh, z - 60), 120f) * Mathf.Rad2Deg;
    float qibla = (151f + north) % 360f;
    { var lg = new LandmarkGeometry(); lg.Bridge(LP("bridge_eu"), LP("bridge_as"), -0.4f); Put(lg, Vector3.zero, 0f); }
    { var lg = new LandmarkGeometry(); lg.OrtakoyMosque(); lg.B(LandmarkGeometry.Stone).Box(new Vector3(0, -3, 0), new Vector3(46, 6, 46)); var p = LP("ortakoy_mosque"); p.y = MathF.Max(p.y, 1.6f); Put(lg, p, qibla); }
    { var lg = new LandmarkGeometry(); lg.Palace(284f, 48f, 18f, true); Put(lg, Inland(LP("dolmabahce"), -1, 40), ShoreYaw(def.shoreline, def.FindLandmark("dolmabahce").north)); }
    { var lg = new LandmarkGeometry(1); lg.Palace(160f, 30f, 14f, false); Put(lg, Inland(LP("ciragan"), -1, 28), ShoreYaw(def.shoreline, def.FindLandmark("ciragan").north)); }
    { var lg = new LandmarkGeometry(1); lg.Palace(65f, 40f, 15f, false); Put(lg, Inland(LP("beylerbeyi"), 1, 30), ShoreYaw(def.farShoreline, def.FindLandmark("beylerbeyi").north)); }
    { var lg = new LandmarkGeometry(1); lg.GalataTower(63f); Put(lg, LP("galata"), 0f); }
    { var lg = new LandmarkGeometry(1); lg.MaidensTower(); var p = LP("maidens_tower"); p.y = -0.4f; Put(lg, p, 90f + north); }
    { var lg = new LandmarkGeometry(1); lg.ImperialMosque(11.7f, 26f, 6, 64f, LandmarkGeometry.Stone); Put(lg, LP("sultan_ahmed"), qibla); }
    { var lg = new LandmarkGeometry(1); lg.ImperialMosque(15.5f, 34f, 4, 60f, LandmarkGeometry.HagiaPlaster); Put(lg, LP("hagia_sophia"), 122f + north); }
    var sbe = new StringBuilder();
    int vb = 1, total = 0;
    foreach (var kv in parts)
    {
        var mbx = kv.Value;
        if (mbx.TriangleCount == 0) continue;
        sbe.AppendLine($"o {kv.Key}\nusemtl {kv.Key}");
        foreach (var v in mbx.Vertices) sbe.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F2} {1:F2} {2:F2}", v.x, v.y, v.z));
        foreach (var uv in mbx.UVs) sbe.AppendLine(string.Format(CultureInfo.InvariantCulture, "vt {0:F4} {1:F4}", uv.x, uv.y));
        var ts = mbx.Triangles;
        for (int i = 0; i < ts.Count; i += 3) sbe.AppendLine($"f {ts[i] + vb}/{ts[i] + vb} {ts[i + 1] + vb}/{ts[i + 1] + vb} {ts[i + 2] + vb}/{ts[i + 2] + vb}");
        vb += mbx.VertexCount;
        total += mbx.TriangleCount;
    }
    foreach (var kv in parts) if (kv.Value.TriangleCount > 3000) Console.WriteLine($"  {kv.Key,-18} {kv.Value.TriangleCount,7} tris");
    Directory.CreateDirectory(outDir);
    File.WriteAllText(Path.Combine(outDir, "environment.obj"), sbe.ToString());
    Console.WriteLine($"environment.obj: {total} triangles (preview: near terrain at 24 m, trees as meshes)");
    return;
}
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
    Vol("vlathe", mb => LandmarkGeometry.VerticalLathe(mb, Vector3.zero, new[] { new Vector2(0f, 0.001f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0.001f) }, 16));
    if (primitivesOnly) return;
}
bool check = Array.IndexOf(args, "--check") >= 0;
int[] budget = { 60000, 15000, 6000 };
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
    var parts = new List<(string mat, MeshBuilder mb, string name)>();
    foreach (var group in new[] { geo.body, geo.frontWing, geo.rearWing })
        foreach (var kv in group[lod]) parts.Add((kv.Key, kv.Value, kv.Key));
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
        parts.Add(("Tire", tire, "Tire"));
        var rim = new MeshBuilder { matrix = new Matrix4x4 { offset = centre } };
        CarGeometry.RimMesh(rim, w, 0.229f, lod);
        parts.Add(("Rim", rim, "Rim"));
        var hub = new MeshBuilder { matrix = new Matrix4x4 { offset = centre } };
        CarGeometry.HubMesh(hub, w, 0.229f, i % 2 == 1, lod);
        parts.Add(("CarAccent", hub, "Hub"));
        var band = new MeshBuilder { matrix = new Matrix4x4 { offset = centre } };
        CarGeometry.CompoundBand(band, r, w, 0.229f, lod);
        parts.Add(("Belt", band, "Band"));
        if (lod == 0)
        {
            var disc = new MeshBuilder { matrix = new Matrix4x4 { offset = centre } };
            CarGeometry.BrakeDisc(disc, 0.16f, lod);
            parts.Add(("Rim", disc, "Disc"));
        }
        if (lod == 0)
        {
            var up = new MeshBuilder { matrix = new Matrix4x4 { offset = centre } };
            CarGeometry.Upright(up, r, w, i % 2 == 0, front, lod);
            parts.Add(("Carbon", up, "Upright"));
            var links = new MeshBuilder();
            foreach (var (inner, outer, rad) in CarGeometry.Links(setup, i)) links.Tube(inner, centre + outer, rad, 6);
            parts.Add(("Carbon", links, "Links"));
        }
    }
    if (lod == 0)
    {
        var sw = CarGeometry.SteeringWheelPosition(setup);
        var carbon = new MeshBuilder { matrix = new Matrix4x4 { offset = sw } };
        var grips = new MeshBuilder { matrix = new Matrix4x4 { offset = sw } };
        var disp = new MeshBuilder { matrix = new Matrix4x4 { offset = sw } };
        CarGeometry.SteeringWheel(carbon, grips, disp);
        parts.Add(("Carbon", carbon, "Wheel")); parts.Add(("Interior", grips, "Grips")); parts.Add(("Display", disp, "Display"));
        foreach (float gs in new[] { -1f, 1f })
        {
            var glove = new MeshBuilder { matrix = new Matrix4x4 { offset = sw + new Vector3(gs * 0.14f, 0f, -0.01f) } };
            CarGeometry.GloveMesh(glove, gs);
            parts.Add(("Glove", glove, "Glove"));
        }
        var helmetPos = new Vector3(0f, -setup.cogHeight + 0.76f, setup.FrontAxleZ - 2.05f);
        var shell = new MeshBuilder { matrix = new Matrix4x4 { offset = helmetPos } };
        var visor = new MeshBuilder { matrix = new Matrix4x4 { offset = helmetPos } };
        CarGeometry.HelmetMesh(shell, visor);
        parts.Add(("Helmet", shell, "Helmet")); parts.Add(("Visor", visor, "Visor"));
    }
    var sb = new StringBuilder();
    int vBase = 1, tris = 0;
    foreach (var (mat, mb, name) in parts)
    {
        sb.AppendLine($"o {name}_{mat}");
        sb.AppendLine($"usemtl {mat}");
        var vs = mb.Vertices;
        var ns = mb.ComputeNormals();
        foreach (var v in vs) sb.AppendLine(string.Format(inv, "v {0:F4} {1:F4} {2:F4}", v.x, v.y, v.z));
        foreach (var n in ns) sb.AppendLine(string.Format(inv, "vn {0:F3} {1:F3} {2:F3}", n.x, n.y, n.z));
        foreach (var uv in mb.UVs) sb.AppendLine(string.Format(inv, "vt {0:F4} {1:F4}", uv.x, uv.y));
        var ts = mb.Triangles;
        for (int i = 0; i < ts.Count; i += 3)
        {
            int a0 = ts[i] + vBase, a1 = ts[i + 1] + vBase, a2 = ts[i + 2] + vBase;
            sb.AppendLine($"f {a0}/{a0}/{a0} {a1}/{a1}/{a1} {a2}/{a2}/{a2}");
        }
        vBase += vs.Count;
        tris += ts.Count / 3;
    }
    File.WriteAllText(Path.Combine(outDir, $"car_lod{lod}.obj"), sb.ToString());
    Console.WriteLine($"LOD{lod}: {tris} triangles, {vBase - 1} vertices, {parts.Count} parts (budget {budget[lod]})");
    if (tris > budget[lod]) failed = true;
    // Body materials are open patches of one closed surface: check the sum per group.
    foreach (var (gname, group) in new[] { ("body", geo.body), ("frontWing", geo.frontWing), ("rearWing", geo.rearWing) })
    {
        float sum = 0f;
        foreach (var kv in group[lod]) sum += SignedVolume(kv.Value.ToMesh(kv.Key));
        if (sum <= 0f)
        {
            Console.WriteLine($"  inward-facing group: LOD{lod} {gname} ({sum:F4} m³)");
            failed = true;
        }
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
