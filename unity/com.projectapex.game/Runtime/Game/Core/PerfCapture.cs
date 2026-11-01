using System;
using System.IO;
using Apex.Core;
using Unity.Profiling;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Performance capture during a session (docs/ISTANBUL_OVERHAUL.md, Phase 6): records every frame's time, the
    /// player's distance along the lap and the render counters (editor and development builds only) for one lap
    /// or at most <see cref="MaxSeconds"/>, then writes a CSV and a text report to persistentDataPath/perf.
    /// Started with F6 or the button in the telemetry panel; started again, it stops early and writes what it has.
    /// </summary>
    public sealed class PerfCapture
    {
        public const float MaxSeconds = 180f;
        /// <summary>Frames ignored after the start (loading hitches of the toggle itself).</summary>
        const int WarmupFrames = 10;

        readonly FrameStats stats = new FrameStats();
        ProfilerRecorder drawCalls, setPass, triangles;
        float elapsed, startDistance, travelled, lastDistance, lapLength;
        int warmup;

        public bool Running { get; private set; }
        public float Elapsed => elapsed;
        public float Travelled => travelled;
        public string LastFile { get; private set; }

        public void Start(float distance, float lapLength)
        {
            stats.Clear();
            elapsed = 0f; travelled = 0f;
            startDistance = lastDistance = distance;
            this.lapLength = lapLength;
            warmup = WarmupFrames;
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            Running = true;
        }

        static float Read(ProfilerRecorder r) => r.Valid && r.Count > 0 ? r.LastValue : -1f;

        /// <summary>Call once per rendered frame; returns true when the capture finished this frame.</summary>
        public bool Tick(float unscaledDt, float distance, string header)
        {
            if (!Running) return false;
            if (warmup > 0) { warmup--; lastDistance = distance; return false; }
            elapsed += unscaledDt;
            float step = distance - lastDistance;
            if (lapLength > 0f)
            {
                // Crossing the line wraps the distance; a large backwards jump is a reset or a wrap.
                if (step < -0.5f * lapLength) step += lapLength;
                else if (step > 0.5f * lapLength) step -= lapLength;
            }
            if (step > 0f) travelled += step;
            lastDistance = distance;
            stats.Add(unscaledDt * 1000f, distance, Read(drawCalls), Read(setPass), Read(triangles));
            if (elapsed >= MaxSeconds || (lapLength > 0f && travelled >= lapLength)) { Stop(header); return true; }
            return false;
        }

        /// <summary>Ends the capture and writes the files (also when cut short).</summary>
        public void Stop(string header)
        {
            if (!Running) return;
            Running = false;
            drawCalls.Dispose(); setPass.Dispose(); triangles.Dispose();
            if (stats.samples.Count == 0) { LastFile = null; return; }
            float target = Application.isMobilePlatform ? FrameStats.MobileTargetMs : FrameStats.PcTargetMs;
            var summary = stats.Summarize(target, lapLength);
            string dir = Path.Combine(Application.persistentDataPath, "perf");
            try
            {
                Directory.CreateDirectory(dir);
                string stem = Path.Combine(dir, $"perf_{DateTime.Now:yyyyMMdd_HHmmss}");
                File.WriteAllText(stem + ".csv", stats.Csv());
                string device = $"Gerät: {SystemInfo.deviceModel} · {SystemInfo.processorType} · {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}) · {Screen.width}×{Screen.height}";
                string report = FrameStats.Report(header + "\n" + device + $"\nStrecke gefahren: {travelled:F0} m von {lapLength:F0} m", summary);
                File.WriteAllText(stem + ".txt", report);
                LastFile = stem + ".txt";
                Debug.Log("[Apex] Leistungsmessung gespeichert: " + LastFile + "\n" + report);
            }
            catch (Exception e)
            {
                LastFile = null;
                Debug.LogWarning("[Apex] Leistungsmessung nicht gespeichert: " + e.Message);
            }
        }
    }
}
