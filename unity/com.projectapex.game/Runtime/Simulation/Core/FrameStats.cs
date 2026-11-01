using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Apex.Core
{
    /// <summary>
    /// Frame-time recording of a performance capture (docs/ISTANBUL_OVERHAUL.md, Phase 6): one sample per rendered
    /// frame with the player's distance along the lap and, where Unity provides them, the render counters. The
    /// summary gives the averages, percentiles and 1 % lows, the hitches, and the slowest sections of the lap, so a
    /// measurement on a real device says where on the circuit the frame budget is missed.
    /// </summary>
    public sealed class FrameStats
    {
        public struct Sample
        {
            public float ms;
            /// <summary>Distance along the lap (m), negative when unknown.</summary>
            public float distance;
            /// <summary>Render counters of the frame, negative when not available (release players).</summary>
            public float drawCalls, setPass, triangles;
        }

        public struct Section
        {
            public float from, to;
            public int frames;
            public float averageMs, p95Ms;
        }

        public struct Summary
        {
            public int frames;
            public float seconds;
            public float averageMs, p50Ms, p95Ms, p99Ms, maxMs;
            public float averageFps;
            /// <summary>Frame rate of the slowest 1 % of frames (mean of their frame times).</summary>
            public float low1Fps;
            /// <summary>Frames over <see cref="HitchFactor"/> × the median and at least <see cref="HitchMinMs"/>.</summary>
            public int hitches;
            public float drawCallsAvg, drawCallsMax, setPassAvg, trianglesAvg, trianglesMax;
            /// <summary>Share of frames within the target frame time (0..1).</summary>
            public float withinTarget;
            public float targetMs;
            public List<Section> sections;
        }

        public const float HitchFactor = 2.5f, HitchMinMs = 25f;
        /// <summary>Frame budgets: 60 fps on PC, 30 fps on mobile.</summary>
        public const float PcTargetMs = 1000f / 60f, MobileTargetMs = 1000f / 30f;

        public readonly List<Sample> samples = new List<Sample>();

        public void Add(float ms, float distance = -1f, float drawCalls = -1f, float setPass = -1f, float triangles = -1f) =>
            samples.Add(new Sample { ms = ms, distance = distance, drawCalls = drawCalls, setPass = setPass, triangles = triangles });

        public void Clear() => samples.Clear();

        /// <summary>Percentile (0..100) of sorted values, linear between ranks.</summary>
        public static float Percentile(List<float> sorted, float p)
        {
            if (sorted.Count == 0) return 0f;
            float r = Math.Clamp(p, 0f, 100f) / 100f * (sorted.Count - 1);
            int i = (int)MathF.Floor(r);
            if (i >= sorted.Count - 1) return sorted[sorted.Count - 1];
            return sorted[i] + (sorted[i + 1] - sorted[i]) * (r - i);
        }

        /// <param name="lapLength">track length (m) for the section breakdown; 0 for none</param>
        /// <param name="sectionLength">length of a section (m)</param>
        public Summary Summarize(float targetMs, float lapLength = 0f, float sectionLength = 250f)
        {
            var s = new Summary { targetMs = targetMs, sections = new List<Section>() };
            int n = samples.Count;
            s.frames = n;
            if (n == 0) return s;
            var ms = new List<float>(n);
            double sum = 0;
            int within = 0;
            double dc = 0, sp = 0, tr = 0;
            int dcn = 0, spn = 0, trn = 0;
            foreach (var x in samples)
            {
                ms.Add(x.ms);
                sum += x.ms;
                if (x.ms <= targetMs + 0.5f) within++;
                if (x.drawCalls >= 0f) { dc += x.drawCalls; dcn++; s.drawCallsMax = MathF.Max(s.drawCallsMax, x.drawCalls); }
                if (x.setPass >= 0f) { sp += x.setPass; spn++; }
                if (x.triangles >= 0f) { tr += x.triangles; trn++; s.trianglesMax = MathF.Max(s.trianglesMax, x.triangles); }
            }
            ms.Sort();
            s.seconds = (float)(sum / 1000.0);
            s.averageMs = (float)(sum / n);
            s.averageFps = 1000f / MathF.Max(s.averageMs, 1e-3f);
            s.p50Ms = Percentile(ms, 50f);
            s.p95Ms = Percentile(ms, 95f);
            s.p99Ms = Percentile(ms, 99f);
            s.maxMs = ms[n - 1];
            int worst = Math.Max(1, n / 100);
            double ws = 0;
            for (int i = n - worst; i < n; i++) ws += ms[i];
            s.low1Fps = 1000f / MathF.Max((float)(ws / worst), 1e-3f);
            float hitch = MathF.Max(HitchMinMs, HitchFactor * s.p50Ms);
            foreach (var v in ms) if (v > hitch) s.hitches++;
            s.withinTarget = within / (float)n;
            s.drawCallsAvg = dcn > 0 ? (float)(dc / dcn) : -1f;
            s.setPassAvg = spn > 0 ? (float)(sp / spn) : -1f;
            s.trianglesAvg = trn > 0 ? (float)(tr / trn) : -1f;
            if (dcn == 0) s.drawCallsMax = -1f;
            if (trn == 0) s.trianglesMax = -1f;
            if (lapLength > 0f && sectionLength > 0f)
            {
                int count = Math.Max(1, (int)MathF.Ceiling(lapLength / sectionLength));
                var bins = new List<float>[count];
                for (int i = 0; i < count; i++) bins[i] = new List<float>();
                foreach (var x in samples)
                {
                    if (x.distance < 0f) continue;
                    float d = x.distance % lapLength;
                    bins[Math.Min(count - 1, (int)(d / sectionLength))].Add(x.ms);
                }
                for (int i = 0; i < count; i++)
                {
                    if (bins[i].Count == 0) continue;
                    double bs = 0;
                    foreach (var v in bins[i]) bs += v;
                    bins[i].Sort();
                    s.sections.Add(new Section
                    {
                        from = i * sectionLength,
                        to = MathF.Min(lapLength, (i + 1) * sectionLength),
                        frames = bins[i].Count,
                        averageMs = (float)(bs / bins[i].Count),
                        p95Ms = Percentile(bins[i], 95f),
                    });
                }
            }
            return s;
        }

        /// <summary>The <paramref name="count"/> sections with the highest 95th percentile, slowest first.</summary>
        public static List<Section> Slowest(Summary s, int count)
        {
            var l = new List<Section>(s.sections);
            l.Sort((a, b) => b.p95Ms.CompareTo(a.p95Ms));
            if (l.Count > count) l.RemoveRange(count, l.Count - count);
            return l;
        }

        public string Csv()
        {
            var sb = new StringBuilder("frame,ms,distance_m,draw_calls,setpass,triangles\n");
            var ci = CultureInfo.InvariantCulture;
            for (int i = 0; i < samples.Count; i++)
            {
                var x = samples[i];
                sb.Append(i).Append(',').Append(x.ms.ToString("F3", ci)).Append(',').Append(x.distance.ToString("F1", ci)).Append(',')
                  .Append(x.drawCalls.ToString("F0", ci)).Append(',').Append(x.setPass.ToString("F0", ci)).Append(',').Append(x.triangles.ToString("F0", ci)).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Plain-text report (German, for the PO) with the device and settings line given by the caller.</summary>
        public static string Report(string header, Summary s)
        {
            var ci = CultureInfo.InvariantCulture;
            string F(float v, string f = "F1") => v.ToString(f, ci);
            var sb = new StringBuilder();
            sb.AppendLine(header);
            sb.AppendLine($"Bilder: {s.frames} in {F(s.seconds)} s");
            sb.AppendLine($"Mittel: {F(s.averageMs, "F2")} ms ({F(s.averageFps)} fps), Median {F(s.p50Ms, "F2")} ms, P95 {F(s.p95Ms, "F2")} ms, P99 {F(s.p99Ms, "F2")} ms, Max {F(s.maxMs)} ms");
            sb.AppendLine($"1 %-Tief: {F(s.low1Fps)} fps, Ruckler (> {F(MathF.Max(HitchMinMs, HitchFactor * s.p50Ms))} ms): {s.hitches}");
            sb.AppendLine($"Im Ziel ({F(s.targetMs)} ms): {F(s.withinTarget * 100f)} % der Bilder");
            if (s.drawCallsAvg >= 0f)
                sb.AppendLine($"Draw Calls: Mittel {F(s.drawCallsAvg, "F0")}, Max {F(s.drawCallsMax, "F0")}; SetPass: {F(s.setPassAvg, "F0")}; Dreiecke: Mittel {F(s.trianglesAvg / 1e6f, "F2")} Mio, Max {F(s.trianglesMax / 1e6f, "F2")} Mio");
            else
                sb.AppendLine("Render-Zähler: nicht verfügbar (nur im Editor und in Development-Builds)");
            if (s.sections.Count > 0)
            {
                sb.AppendLine("Langsamste Abschnitte (P95):");
                foreach (var sec in Slowest(s, 5))
                    sb.AppendLine($"  {F(sec.from, "F0")}–{F(sec.to, "F0")} m: P95 {F(sec.p95Ms, "F2")} ms, Mittel {F(sec.averageMs, "F2")} ms ({sec.frames} Bilder)");
            }
            return sb.ToString();
        }
    }
}
