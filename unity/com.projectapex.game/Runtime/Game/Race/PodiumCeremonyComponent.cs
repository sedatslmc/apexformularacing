using System.Collections.Generic;
using Apex.Race;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apex.Game
{
    /// <summary>
    /// Draws the podium ceremony (Apex.Race.PodiumCeremony) on the circuit's podium while the results show the podium
    /// shot: three driver figures (the pit crew body models, placeholder art) in their team colours, the winner lifting
    /// the trophy, team banners on poles and confetti from two cannons. Built on <see cref="Begin"/>, removed on <see cref="End"/>.
    /// </summary>
    public sealed class PodiumCeremonyComponent : MonoBehaviour
    {
        Transform podium, trophy;
        Vector3 trophyHome;
        float t;
        bool reducedMotion;
        readonly List<Material> owned = new List<Material>();
        Mesh[] parts;
        bool[][] slots;
        Material dark;
        readonly Material[] suits = new Material[3], helmets = new Material[3];
        readonly PitCrewFigure.Frame[] frames = new PitCrewFigure.Frame[PitCrewFigure.PartCount];
        int places;
        Mesh[] banners;
        Material[] bannerMats;
        Mesh pole;
        Material poleMat;
        readonly Mesh[] confetti = new Mesh[4];
        readonly Material[] confettiMats = new Material[4];
        readonly List<Vector3>[] confettiVerts = { new List<Vector3>(), new List<Vector3>(), new List<Vector3>(), new List<Vector3>() };

        public bool Running { get; private set; }

        /// <summary>Starts the ceremony on <paramref name="podiumTransform"/> for the top three (suit colour, accent colour per place).</summary>
        public void Begin(Transform podiumTransform, IList<(Color suit, Color accent)> top, bool reduced)
        {
            End();
            if (podiumTransform == null || top == null || top.Count == 0) return;
            podium = podiumTransform;
            reducedMotion = reduced;
            places = Mathf.Min(3, top.Count);
            t = 0f;
            trophy = podium.Find("Trophy");
            if (trophy != null) trophyHome = trophy.localPosition;
            if (parts == null) BuildMeshes();
            for (int i = 0; i < places; i++)
            {
                suits[i] = Own(top[i].suit);
                helmets[i] = Own(top[i].accent);
            }
            // Banners in P2, P1, P3 order from the viewer's left, the colours of those teams.
            bannerMats = new Material[places];
            for (int i = 0; i < places; i++) bannerMats[i] = Own(top[i].suit);
            BuildConfetti();
            Running = true;
        }

        public void End()
        {
            if (!Running) return;
            Running = false;
            if (trophy != null) trophy.localPosition = trophyHome;
            foreach (var m in owned) if (m != null) Destroy(m);
            owned.Clear();
        }

        Material Own(Color c)
        {
            var m = new Material(EnvironmentMaterials.Solid(c)) { name = "Podium" };
            owned.Add(m);
            return m;
        }

        void BuildMeshes()
        {
            parts = new Mesh[PitCrewFigure.PartCount];
            slots = new bool[PitCrewFigure.PartCount][];
            for (int p = 0; p < PitCrewFigure.PartCount; p++)
            {
                var s = PitCrewGeometry.Part((PitCrewFigure.Part)p, 1);
                parts[p] = PitCrewComponent.ToMesh("Podium" + (PitCrewFigure.Part)p, s);
                slots[p] = new bool[s.Length];
                for (int k = 0; k < s.Length; k++) slots[p][k] = s[k].TriangleCount > 0;
            }
            dark = new Material(EnvironmentMaterials.Solid(new Color(0.08f, 0.085f, 0.1f))) { name = "PodiumDark" };
            poleMat = new Material(EnvironmentMaterials.Solid(new Color(0.75f, 0.77f, 0.8f))) { name = "PodiumPole" };
            var mb = new MeshBuilder();
            mb.Box(new Vector3(0f, PodiumCeremony.PoleHeight * 0.5f, 0f), new Vector3(0.06f, PodiumCeremony.PoleHeight, 0.06f));
            pole = mb.ToMesh("PodiumPole");
            banners = new Mesh[3];
            for (int b = 0; b < 3; b++) banners[b] = new Mesh { name = "PodiumBanner" };
            var colours = new[] { new Color(0.85f, 0.68f, 0.34f), new Color(0.86f, 0.1f, 0.12f), new Color(0.96f, 0.95f, 0.9f), new Color(0.1f, 0.75f, 0.75f) };
            for (int c = 0; c < 4; c++)
            {
                confetti[c] = new Mesh { name = "Confetti" };
                confetti[c].MarkDynamic();
                confettiMats[c] = new Material(EnvironmentMaterials.Solid(colours[c])) { name = "Confetti" };
            }
        }

        void BuildConfetti()
        {
            // Index buffers per colour (4 vertices, two triangles, both sides).
            var counts = new int[4];
            for (int i = 0; i < PodiumCeremony.ConfettiCount; i++) counts[PodiumCeremony.ConfettiColour(i)]++;
            for (int c = 0; c < 4; c++)
            {
                var verts = confettiVerts[c];
                verts.Clear();
                for (int k = 0; k < counts[c] * 4; k++) verts.Add(Vector3.zero);
                var tris = new int[counts[c] * 12];
                for (int q = 0; q < counts[c]; q++)
                {
                    int v = q * 4, o = q * 12;
                    tris[o] = v; tris[o + 1] = v + 1; tris[o + 2] = v + 2; tris[o + 3] = v; tris[o + 4] = v + 2; tris[o + 5] = v + 3;
                    tris[o + 6] = v; tris[o + 7] = v + 2; tris[o + 8] = v + 1; tris[o + 9] = v; tris[o + 10] = v + 3; tris[o + 11] = v + 2;
                }
                confetti[c].Clear();
                confetti[c].SetVertices(verts);
                confetti[c].SetTriangles(tris, 0);
                confetti[c].bounds = new Bounds(new Vector3(0f, 4f, 0f), new Vector3(20f, 10f, 12f));
            }
        }

        static Vector3 U(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        void LateUpdate()
        {
            if (!Running) return;
            if (podium == null) { End(); return; }
            t += Time.unscaledDeltaTime;
            var m = podium.localToWorldMatrix;
            var nan = new System.Numerics.Vector3(float.NaN, 0f, 0f);
            for (int place = 0; place < places; place++)
            {
                var feet = PodiumCeremony.Steps[place];
                var pose = new PitCrewChoreography.Pose { position = feet, yaw = PodiumCeremony.Yaw, hold = place == 0 ? PitCrewChoreography.Hold.Wheel : PitCrewChoreography.Hold.None };
                var hands = place == 0 ? PodiumCeremony.WinnerHands(t, reducedMotion) : nan;
                PitCrewFigure.Solve(pose, hands, 0f, frames);
                for (int p = 0; p < PitCrewFigure.PartCount; p++)
                {
                    var f = PitCrewFigure.ToBox(frames[p], feet, PodiumCeremony.Yaw);
                    var w = m * Matrix4x4.TRS(U(f.origin), Quaternion.LookRotation(U(f.forward), U(f.up)), Vector3.one);
                    if (slots[p][PitCrewGeometry.Overall]) Graphics.DrawMesh(parts[p], w, suits[place], 0, null, PitCrewGeometry.Overall);
                    if (slots[p][PitCrewGeometry.Dark]) Graphics.DrawMesh(parts[p], w, dark, 0, null, PitCrewGeometry.Dark);
                    if (slots[p][PitCrewGeometry.Shell]) Graphics.DrawMesh(parts[p], w, helmets[place], 0, null, PitCrewGeometry.Shell);
                }
                if (place == 0 && trophy != null) trophy.localPosition = U(PodiumCeremony.TrophyBase(PodiumCeremony.ToPodium(0, hands)));
            }
            DrawBanners(m);
            DrawConfetti(m);
        }

        readonly List<Vector3> bannerVerts = new List<Vector3>();
        int[] bannerTris;
        const int BannerCols = 10, BannerRows = 4;

        void DrawBanners(Matrix4x4 m)
        {
            // Viewer's left to right: P2, P1, P3.
            int[] order = { 1, 0, 2 };
            for (int b = 0; b < 3; b++)
            {
                int place = order[b];
                if (place >= places) continue;
                var root = new Vector3(PodiumCeremony.BannerX[b], 0.6f, PodiumCeremony.BannerZ);
                Graphics.DrawMesh(pole, m * Matrix4x4.Translate(root), poleMat, 0);
                bannerVerts.Clear();
                for (int r = 0; r <= BannerRows; r++)
                    for (int c = 0; c <= BannerCols; c++)
                    {
                        float u = c / (float)BannerCols, v = r / (float)BannerRows;
                        float wave = reducedMotion ? 0f : PodiumCeremony.BannerWave(u, v, t, b);
                        bannerVerts.Add(root + new Vector3(0.04f + u * PodiumCeremony.BannerWidth, PodiumCeremony.PoleHeight - 0.1f - (1f - v) * PodiumCeremony.BannerHeight, wave));
                    }
                if (bannerTris == null)
                {
                    var tl = new List<int>();
                    for (int r = 0; r < BannerRows; r++)
                        for (int c = 0; c < BannerCols; c++)
                        {
                            int a = r * (BannerCols + 1) + c, d = a + BannerCols + 1;
                            tl.AddRange(new[] { a, d, a + 1, a + 1, d, d + 1, a, a + 1, d, a + 1, d + 1, d });
                        }
                    bannerTris = tl.ToArray();
                }
                var mesh = banners[b];
                mesh.Clear();
                mesh.SetVertices(bannerVerts);
                mesh.SetTriangles(bannerTris, 0);
                mesh.RecalculateNormals();
                Graphics.DrawMesh(mesh, m, bannerMats[place], 0);
            }
        }

        readonly int[] fill = new int[4];

        void DrawConfetti(Matrix4x4 m)
        {
            if (reducedMotion) return;
            for (int c = 0; c < 4; c++) fill[c] = 0;
            for (int i = 0; i < PodiumCeremony.ConfettiCount; i++)
            {
                int c = PodiumCeremony.ConfettiColour(i);
                var verts = confettiVerts[c];
                int v = fill[c]++ * 4;
                if (!PodiumCeremony.Confetti(i, t, out var p, out float spin))
                {
                    for (int k = 0; k < 4; k++) verts[v + k] = new Vector3(0f, -10f, 0f);
                    continue;
                }
                var q = Quaternion.Euler(spin * 57.3f, spin * 31f, spin * 17f);
                var centre = U(p);
                var ax = q * new Vector3(0.022f, 0f, 0f);
                var ay = q * new Vector3(0f, 0f, 0.035f);
                verts[v] = centre - ax - ay; verts[v + 1] = centre + ax - ay; verts[v + 2] = centre + ax + ay; verts[v + 3] = centre - ax + ay;
            }
            for (int c = 0; c < 4; c++)
            {
                confetti[c].SetVertices(confettiVerts[c]);
                confetti[c].RecalculateNormals();
                Graphics.DrawMesh(confetti[c], m, confettiMats[c], 0, null, 0, null, ShadowCastingMode.Off, false);
            }
        }

        void OnDestroy()
        {
            End();
            if (parts != null) foreach (var p in parts) if (p != null) Destroy(p);
            foreach (var c in confetti) if (c != null) Destroy(c);
            foreach (var c in confettiMats) if (c != null) Destroy(c);
            if (banners != null) foreach (var b in banners) if (b != null) Destroy(b);
            if (pole != null) Destroy(pole);
            if (dark != null) Destroy(dark);
            if (poleMat != null) Destroy(poleMat);
        }
    }
}
