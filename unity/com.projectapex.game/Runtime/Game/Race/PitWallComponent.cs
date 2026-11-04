using System.Collections.Generic;
using Apex.Core;
using Apex.Race;
using Apex.UI;
using Apex.Vehicle;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apex.Game
{
    /// <summary>
    /// Life on the pit wall (docs/PIT_LANE.md "Pitwall"): the race engineers on their stools (four per team stand, own
    /// models from PitCrewGeometry plus the headset head, seated pose PitCrewFigure.SolveSeated: typing, looking between
    /// the monitors and the track, a hand to the headset while talking) and the monitors showing live race data
    /// (PitWallDisplay: timing, the player's speed trace, tyres, weather radar), re-drawn twice a second into one shared
    /// texture. Engineers are drawn instanced and only near the camera.
    /// </summary>
    public sealed class PitWallComponent : MonoBehaviour
    {
        public RaceController race;
        public int quality = 1;
        public const float DrawDistance = 160f;
        const int Seats = 4;

        readonly List<(Matrix4x4 frame, int team)> stands = new List<(Matrix4x4, int)>();
        Texture2D screen;
        uint[] pixels;
        Color32[] colours;
        readonly PitWallDisplay.Data data = new PitWallDisplay.Data();
        float refresh, sample, clock;

        // Meshes: body parts (crew models), the engineer's head; materials per team and shared.
        readonly Mesh[] parts = new Mesh[PitCrewFigure.PartCount];
        Mesh head;
        Material dark, skin;
        readonly Dictionary<int, Material> shirts = new Dictionary<int, Material>();
        readonly PitCrewFigure.Frame[] frames = new PitCrewFigure.Frame[PitCrewFigure.PartCount];
        // Instance lists: shirts per team (torso, upper arms, forearms), shared trousers/shoes and heads.
        readonly Dictionary<int, List<Matrix4x4>[]> teamLists = new Dictionary<int, List<Matrix4x4>[]>();
        readonly List<Matrix4x4>[] sharedLists = new List<Matrix4x4>[PitCrewFigure.PartCount];
        readonly List<Matrix4x4> heads = new List<Matrix4x4>();
        readonly Matrix4x4[] batch = new Matrix4x4[256];

        public void Build(CircuitInstance ci)
        {
            foreach (var (pos, rot, team) in ci.PitWallStands) stands.Add((Matrix4x4.TRS(pos, rot, Vector3.one), team));
            if (stands.Count == 0) return;
            // Live screens: one texture for all monitors.
            screen = new Texture2D(PitWallDisplay.Width, PitWallDisplay.Height, TextureFormat.RGBA32, false) { name = "PitWallScreens", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            pixels = new uint[PitWallDisplay.Width * PitWallDisplay.Height];
            colours = new Color32[pixels.Length];
            var mat = ApexMaterials.Get(PitWallGeometry.Screen);
            foreach (var prop in new[] { "_BaseMap", "_MainTex" }) if (mat.HasProperty(prop)) mat.SetTexture(prop, screen);
            if (mat.HasProperty("_EmissionMap")) { mat.SetTexture("_EmissionMap", screen); mat.EnableKeyword("_EMISSION"); }
            // Engineers.
            int detail = quality > 0 ? 1 : 0;
            for (int p = 0; p < PitCrewFigure.PartCount; p++)
            {
                parts[p] = ToMesh("Engineer" + (PitCrewFigure.Part)p, PitCrewGeometry.Part((PitCrewFigure.Part)p, detail));
                sharedLists[p] = new List<Matrix4x4>();
            }
            head = ToMesh("EngineerHead", PitWallGeometry.EngineerHead());
            dark = new Material(EnvironmentMaterials.Solid(new Color(0.07f, 0.075f, 0.09f))) { name = "EngineerDark", enableInstancing = true };
            skin = new Material(ApexMaterials.Get(EnvironmentGeometry.Skin)) { name = "EngineerSkin", enableInstancing = true };
            foreach (var (_, team) in stands)
            {
                var tc = Roster.Teams[team];
                shirts[team] = new Material(EnvironmentMaterials.Solid(new Color(tc.r, tc.g, tc.b))) { name = "EngineerShirt" + team, enableInstancing = true };
                var lists = new List<Matrix4x4>[PitCrewFigure.PartCount];
                for (int p = 0; p < lists.Length; p++) lists[p] = new List<Matrix4x4>();
                teamLists[team] = lists;
            }
        }

        static Mesh ToMesh(string name, MeshBuilder[] slots)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>[slots.Length];
            for (int k = 0; k < slots.Length; k++)
            {
                int b = verts.Count;
                verts.AddRange(slots[k].Vertices);
                uvs.AddRange(slots[k].UVs);
                tris[k] = new List<int>();
                foreach (int i in slots[k].Triangles) tris[k].Add(i + b);
            }
            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.subMeshCount = slots.Length;
            for (int k = 0; k < slots.Length; k++) m.SetTriangles(tris[k], k);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        void Update()
        {
            if (stands.Count == 0 || race == null) return;
            float dt = Time.deltaTime;
            clock += dt;
            sample += dt;
            if (sample >= 0.5f) { sample = 0f; SampleTelemetry(); }
            refresh += dt;
            if (refresh >= 0.5f) { refresh = 0f; RefreshScreens(); }
            DrawEngineers();
        }

        void SampleTelemetry()
        {
            var p = race.Player;
            if (p == null) return;
            data.speedTrace.Add(Mathf.Max(0f, p.Sim.ForwardSpeed));
            if (data.speedTrace.Count > 120) data.speedTrace.RemoveAt(0);
            data.throttle = p.Sim.throttle;
            data.brake = p.Sim.brake;
        }

        static uint Pack(Color c) => PitWallDisplay.Rgba(c.r, c.g, c.b);

        static Color CompoundColour(TireCompound c)
        {
            switch (c)
            {
                case TireCompound.Soft: return new Color(1f, 0.3f, 0.3f);
                case TireCompound.Medium: return new Color(1f, 0.85f, 0.25f);
                case TireCompound.Hard: return new Color(0.95f, 0.95f, 0.95f);
                case TireCompound.Intermediate: return new Color(0.35f, 0.85f, 0.35f);
                default: return new Color(0.3f, 0.55f, 1f);
            }
        }

        /// <summary>Live data from the session into the screens' texture.</summary>
        void RefreshScreens()
        {
            var d = race.Director;
            data.rows.Clear();
            if (d != null)
            {
                var st = d.Standings;
                double best = double.MaxValue;
                foreach (var e in st) if (e.BestLap > 0 && e.BestLap < best) best = e.BestLap;
                for (int i = 0; i < st.Count && i < 8; i++)
                {
                    var e = st[i];
                    var rc = race.Cars[e.id];
                    var team = Roster.Teams[rc.teamIndex];
                    double gap = d.GapToLeader(e);
                    string g = i == 0 ? (e.BestLap > 0 ? ApexStyle.FormatTime(e.BestLap) : "LEAD") : double.IsNaN(gap) ? "" : $"+{gap:0.0}";
                    int sc = e.BestLap > 0 && e.BestLap <= best + 1e-6 ? 3 : e.BestLap > 0 ? 2 : 0;
                    data.rows.Add(new PitWallDisplay.Row
                    {
                        code = rc.code, gap = g.Replace("'", ":"), team = Pack(new Color(team.r, team.g, team.b)),
                        compound = Pack(CompoundColour(rc.Sim.tires[0].compound)), wear = rc.Sim.AverageTireWear(), stops = e.pitStops,
                        s1 = sc, s2 = sc == 3 ? 2 : sc, s3 = sc, player = e.isPlayer, inPit = rc.pit.InPitLane,
                    });
                }
                data.lap = d.Leader != null ? Mathf.Min(d.Leader.LapsCompleted + 1, d.rules.laps) : 0;
                data.laps = race.Type == SessionType.Race ? d.rules.laps : 0;
            }
            data.rain = race.Weather.RainIntensity;
            data.wetness = race.Weather.TrackWetness;
            data.time = clock;
            PitWallDisplay.Render(data, pixels);
            // Texture rows run bottom-up: flip while converting.
            int w = PitWallDisplay.Width, h = PitWallDisplay.Height;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    uint c = pixels[y * w + x];
                    colours[(h - 1 - y) * w + x] = new Color32((byte)c, (byte)(c >> 8), (byte)(c >> 16), 255);
                }
            screen.SetPixels32(colours);
            screen.Apply(false);
        }

        void DrawEngineers()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var camPos = cam.transform.position;
            foreach (var l in teamLists.Values) foreach (var x in l) x.Clear();
            foreach (var x in sharedLists) x.Clear();
            heads.Clear();
            int seed = 0;
            foreach (var (frame, team) in stands)
            {
                seed += 7;
                var origin = frame.GetColumn(3);
                if ((new Vector3(origin.x, origin.y, origin.z) - camPos).sqrMagnitude > DrawDistance * DrawDistance) continue;
                var lists = teamLists[team];
                for (int s = 0; s < Seats; s++)
                {
                    // Seat in the stand frame: facing −x (the desk and the track), on the floor.
                    var seat = frame * Matrix4x4.TRS(new Vector3(PitWallGeometry.SeatX + 0.05f, PitWallGeometry.Floor, PitWallGeometry.SeatZ[s]), Quaternion.Euler(0f, -90f, 0f), Vector3.one);
                    PitCrewFigure.SolveSeated(clock, seed + s, PitWallGeometry.DeskTop - 0.02f, frames);
                    for (int p = 0; p < PitCrewFigure.PartCount; p++)
                    {
                        var f = frames[p];
                        var m = seat * Matrix4x4.TRS(new Vector3(f.origin.X, f.origin.Y, f.origin.Z),
                            Quaternion.LookRotation(new Vector3(f.forward.X, f.forward.Y, f.forward.Z), new Vector3(f.up.X, f.up.Y, f.up.Z)), Vector3.one);
                        var part = (PitCrewFigure.Part)p;
                        if (part == PitCrewFigure.Part.Head) heads.Add(m);
                        else if (part == PitCrewFigure.Part.Torso || part == PitCrewFigure.Part.UpperArmL || part == PitCrewFigure.Part.UpperArmR
                                 || part == PitCrewFigure.Part.ForearmL || part == PitCrewFigure.Part.ForearmR) lists[p].Add(m);
                        else sharedLists[p].Add(m);
                    }
                }
            }
            // Shirts in the team colour (belt, cuffs and gloves dark), trousers and shoes dark, heads with headsets.
            foreach (var kv in teamLists)
                for (int p = 0; p < PitCrewFigure.PartCount; p++)
                {
                    var l = kv.Value[p];
                    if (l.Count == 0) continue;
                    Draw(parts[p], PitCrewGeometry.Overall, shirts[kv.Key], l);
                    Draw(parts[p], PitCrewGeometry.Dark, dark, l);
                }
            for (int p = 0; p < PitCrewFigure.PartCount; p++)
            {
                if (sharedLists[p].Count == 0) continue;
                Draw(parts[p], PitCrewGeometry.Overall, dark, sharedLists[p]);
                Draw(parts[p], PitCrewGeometry.Dark, dark, sharedLists[p]);
            }
            if (heads.Count > 0)
            {
                Draw(head, 0, skin, heads);
                Draw(head, 1, dark, heads);
            }
        }

        void Draw(Mesh mesh, int sub, Material mat, List<Matrix4x4> list)
        {
            if (sub >= mesh.subMeshCount || mesh.GetIndexCount(sub) == 0) return;
            for (int start = 0; start < list.Count; start += batch.Length)
            {
                int n = Mathf.Min(batch.Length, list.Count - start);
                list.CopyTo(start, batch, 0, n);
                Graphics.DrawMeshInstanced(mesh, sub, mat, batch, n, null, quality > 0 ? ShadowCastingMode.On : ShadowCastingMode.Off, true);
            }
        }
    }
}
