using Apex.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apex.Game
{
    /// <summary>
    /// A circuit drawn from the real track data (<see cref="TrackMap.Fit"/>) as a UI Toolkit mesh: a dark casing,
    /// the line coloured by sector (or a single colour), the start line and optional corner numbers. The line can
    /// be "drawn in" with <see cref="Progress"/> (0..1) for the track-selection entrance.
    /// </summary>
    public sealed class TrackMapElement : VisualElement
    {
        TrackMapLine line;
        float width = 4f;
        bool sectors = true;
        Color single = Color.white;
        float progress = 1f;
        static readonly Color[] SectorColours = { ApexUi.C(UiTokens.Red), ApexUi.C(UiTokens.Cyan), ApexUi.C(UiTokens.Amber) };

        public TrackMapElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Generate;
        }

        /// <summary>Sets the line (fitted to this element's size), its width and whether sectors are coloured.</summary>
        public void Set(TrackMapLine l, float lineWidth, bool colourSectors, Color colour, bool cornerNumbers)
        {
            line = l;
            width = lineWidth;
            sectors = colourSectors;
            single = colour;
            Clear();
            if (cornerNumbers && l != null)
                for (int k = 0; k < l.cornerX.Length; k++)
                {
                    var n = ApexUi.Text(this, (k + 1).ToString(), ApexUi.BodyStrong, 13, ApexUi.C(UiTokens.Ivory, 0.9f), 0f, TextAnchor.MiddleCenter);
                    ApexUi.Place(n, l.cornerX[k] - 12f, l.cornerY[k] - 9f, 24f, 18f);
                }
            MarkDirtyRepaint();
        }

        public float Progress
        {
            get => progress;
            set
            {
                float v = Mathf.Clamp01(value);
                if (Mathf.Abs(v - progress) < 1e-4f) return;
                progress = v;
                MarkDirtyRepaint();
            }
        }

        void Generate(MeshGenerationContext mgc)
        {
            if (line == null || line.Count < 2) return;
            int n = line.Count;
            int segs = Mathf.Clamp(Mathf.CeilToInt(n * progress), 0, n);   // closed loop: n segments
            // Casing (wider, dark) + coloured line + start marker.
            int quads = segs * 2 + (progress >= 1f ? 1 : 0);
            if (quads == 0) return;
            var mesh = mgc.Allocate(quads * 4, quads * 6);
            var casing = ApexUi.C(UiTokens.Navy, 0.85f);
            int vtx = 0;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < segs; i++)
                {
                    int j = (i + 1) % n;
                    var a = new Vector2(line.x[i], line.y[i]);
                    var b = new Vector2(line.x[j], line.y[j]);
                    Color c = pass == 0 ? casing : sectors ? SectorColours[Mathf.Clamp(line.sector[i], 0, 2)] : single;
                    Segment(mesh, ref vtx, a, b, pass == 0 ? width + 4f : width, c);
                }
            if (progress >= 1f)
            {
                // Start/finish: a short white bar across the line.
                int j = 1 % n;
                var dir = new Vector2(line.x[j] - line.x[0], line.y[j] - line.y[0]).normalized;
                var s = new Vector2(line.startX, line.startY);
                Segment(mesh, ref vtx, s - dir * 2f, s + dir * 2f, width * 3.2f, Color.white);
            }
        }

        static void Segment(MeshWriteData mesh, ref int vertex, Vector2 a, Vector2 b, float w, Color c)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) d = Vector2.right; else d /= len;
            // Extend half a width at both ends so neighbouring segments overlap at the joints.
            a -= d * (w * 0.5f);
            b += d * (w * 0.5f);
            var nrm = new Vector2(-d.y, d.x) * (w * 0.5f);
            Color32 t = c;
            ushort i0 = (ushort)vertex;
            vertex += 4;
            mesh.SetNextVertex(new Vertex { position = new Vector3(a.x + nrm.x, a.y + nrm.y, Vertex.nearZ), tint = t });
            mesh.SetNextVertex(new Vertex { position = new Vector3(b.x + nrm.x, b.y + nrm.y, Vertex.nearZ), tint = t });
            mesh.SetNextVertex(new Vertex { position = new Vector3(b.x - nrm.x, b.y - nrm.y, Vertex.nearZ), tint = t });
            mesh.SetNextVertex(new Vertex { position = new Vector3(a.x - nrm.x, a.y - nrm.y, Vertex.nearZ), tint = t });
            // UI Toolkit expects clockwise triangles on screen (y down): a+n, b-n, b+n and a+n, a-n, b-n.
            mesh.SetNextIndex(i0); mesh.SetNextIndex((ushort)(i0 + 2)); mesh.SetNextIndex((ushort)(i0 + 1));
            mesh.SetNextIndex(i0); mesh.SetNextIndex((ushort)(i0 + 3)); mesh.SetNextIndex((ushort)(i0 + 2));
        }
    }
}
