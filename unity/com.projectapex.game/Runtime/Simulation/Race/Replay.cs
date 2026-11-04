using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Core;
using Apex.Tracks;

namespace Apex.Race
{
    /// <summary>
    /// Session replay (docs/REPLAY.md): every car's pose and speed recorded at <see cref="Rate"/> Hz, played back with
    /// interpolation (position linear, rotation slerp). Memory is bounded (<see cref="MaxSeconds"/>; the oldest frames
    /// go first): 20 cars × 20 Hz × 8 floats ≈ 13 KB per second.
    /// </summary>
    public sealed class ReplayClip
    {
        public const float Rate = 20f, MaxSeconds = 1800f;
        const int Stride = 8;

        public readonly int cars;
        readonly List<float> times = new List<float>();
        readonly List<float[]> frames = new List<float[]>();

        public ReplayClip(int cars) { this.cars = cars; }

        public int FrameCount => times.Count;
        public float Start => times.Count > 0 ? times[0] : 0f;
        public float End => times.Count > 0 ? times[times.Count - 1] : 0f;
        public float Duration => End - Start;

        /// <summary>True when a new frame is due at <paramref name="t"/> (the recorder calls this every physics step).</summary>
        public bool Due(float t) => times.Count == 0 || t - times[times.Count - 1] >= 1f / Rate - 1e-4f;

        /// <summary>Records one frame (one pose per car, in car order).</summary>
        public void Add(float t, IList<(Vector3 pos, Quaternion rot, float speed)> poses)
        {
            var f = new float[cars * Stride];
            for (int i = 0; i < cars && i < poses.Count; i++)
            {
                var (p, q, v) = poses[i];
                int k = i * Stride;
                f[k] = p.X; f[k + 1] = p.Y; f[k + 2] = p.Z;
                f[k + 3] = q.X; f[k + 4] = q.Y; f[k + 5] = q.Z; f[k + 6] = q.W;
                f[k + 7] = v;
            }
            times.Add(t);
            frames.Add(f);
            if (Duration > MaxSeconds) { times.RemoveAt(0); frames.RemoveAt(0); }
        }

        /// <summary>A car's pose at time <paramref name="t"/> (clamped to the clip).</summary>
        public (Vector3 pos, Quaternion rot, float speed) Sample(int car, float t)
        {
            if (times.Count == 0 || car < 0 || car >= cars) return (Vector3.Zero, Quaternion.Identity, 0f);
            t = ApexMath.Clamp(t, Start, End);
            int hi = times.BinarySearch(t);
            if (hi < 0) hi = ~hi;
            hi = Math.Max(1, Math.Min(times.Count - 1, hi));
            int lo = hi - 1;
            if (times.Count == 1) lo = hi = 0;
            float span = times[hi] - times[lo];
            float u = span > 1e-6f ? (t - times[lo]) / span : 0f;
            var a = frames[lo]; var b = frames[hi];
            int k = car * Stride;
            var pa = new Vector3(a[k], a[k + 1], a[k + 2]); var pb = new Vector3(b[k], b[k + 1], b[k + 2]);
            var qa = new Quaternion(a[k + 3], a[k + 4], a[k + 5], a[k + 6]); var qb = new Quaternion(b[k + 3], b[k + 4], b[k + 5], b[k + 6]);
            return (Vector3.Lerp(pa, pb, u), Quaternion.Slerp(qa, qb, u), a[k + 7] + (b[k + 7] - a[k + 7]) * u);
        }
    }

    /// <summary>Replay camera views.</summary>
    public enum ReplayCamera { Tv, Chase, Nose, Cockpit, Helicopter }

    /// <summary>
    /// Replay transport (docs/REPLAY.md): play/pause, speed steps, skipping and the camera/car selection. Real-time
    /// driven (unscaled), clamped to the clip; at the end it pauses.
    /// </summary>
    public sealed class ReplayTransport
    {
        public static readonly float[] Speeds = { 0.1f, 0.25f, 0.5f, 1f, 2f, 4f };
        public static readonly string[] CameraNames = { "TV", "Verfolger", "Nase", "Cockpit", "Helikopter" };
        public const float SkipSeconds = 10f;

        public float start, end, time;
        public int speedIndex = 3;
        public bool paused;
        public ReplayCamera camera = ReplayCamera.Tv;
        public int car, cars;

        public ReplayTransport(float start, float end, int cars, int focus)
        {
            this.start = start; this.end = Math.Max(start, end); time = start;
            this.cars = Math.Max(1, cars);
            car = Math.Max(0, Math.Min(this.cars - 1, focus));
        }

        public float Speed => Speeds[speedIndex];
        public float Progress => end - start > 1e-4f ? (time - start) / (end - start) : 1f;
        public bool AtEnd => time >= end - 1e-4f;

        public void Step(float realDt)
        {
            if (paused) return;
            time = Math.Min(end, time + realDt * Speed);
            if (AtEnd) paused = true;
        }

        public void TogglePause()
        {
            if (paused && AtEnd) time = start;   // play again from the start
            paused = !paused;
        }

        public void Skip(float seconds) => time = ApexMath.Clamp(time + seconds, start, end);
        public void Faster() => speedIndex = Math.Min(Speeds.Length - 1, speedIndex + 1);
        public void Slower() => speedIndex = Math.Max(0, speedIndex - 1);
        public void NextCamera() => camera = (ReplayCamera)(((int)camera + 1) % CameraNames.Length);
        public void NextCar(int step) => car = ((car + step) % cars + cars) % cars;

        public static string Clock(float seconds)
        {
            int s = (int)MathF.Max(0f, seconds);
            return $"{s / 60}:{s % 60:00}";
        }

        public string SpeedLabel => Speed >= 1f ? $"{Speed:0}×" : $"{Speed:0.##}×";
    }

    /// <summary>
    /// TV coverage for replays: camera sites along the circuit (behind the barrier, raised on a platform, alternating
    /// to the outside of the bends) and the cut logic – stay on a camera while it frames the car well, cut to the next
    /// one ahead when the car runs out of it. Pure geometry, tested without Unity.
    /// </summary>
    public static class TvDirector
    {
        public const float Spacing = 140f, Height = 5f, MaxRange = 170f;

        public struct Site
        {
            public Vector3 position;
            public float distance;   // along the lap
        }

        /// <summary>
        /// Sites every <see cref="Spacing"/> m, 3 m behind the barrier on the outside of the local bend (falls back to the
        /// inside, then skips the spot, when that would put the camera on another part of the circuit).
        /// </summary>
        public static List<Site> Sites(TrackGeometry g, float[] wallLeft, float[] wallRight)
        {
            var s = new List<Site>();
            for (float d = 0f; d < g.Length - 1f; d += Spacing)
            {
                int i = g.IndexAt(d);
                // Outside of the bend gives the classic "car coming towards the camera through the corner" shot.
                int preferred = g.CurvatureAt(d) > 0f ? -1 : 1;
                foreach (int side in new[] { preferred, -preferred })
                {
                    float wall = side < 0 ? (wallLeft != null ? wallLeft[i] : g.Widths[i] * 0.5f + 3f) : (wallRight != null ? wallRight[i] : g.Widths[i] * 0.5f + 3f);
                    var p = g.PositionAt(d) + g.RightAt(d) * (side * (wall + 3f)) + Vector3.UnitY * Height;
                    if (!ClearOfRoad(g, p, 2f)) continue;
                    s.Add(new Site { position = p, distance = d });
                    break;
                }
            }
            return s;
        }

        /// <summary>True when <paramref name="p"/> is at least <paramref name="margin"/> m off every part of the road (plan view).</summary>
        public static bool ClearOfRoad(TrackGeometry g, Vector3 p, float margin)
        {
            for (int i = 0; i < g.Count; i++)
            {
                float r = g.Widths[i] * 0.5f + margin;
                if (TrackGeometry.FlatDist2(g.Positions[i], p) < r * r) return false;
            }
            return true;
        }

        /// <summary>
        /// The camera for a car at lap distance <paramref name="carDistance"/>: keeps <paramref name="current"/> while the
        /// car is within range and not yet far past it, else the first site at or ahead of the car (the car drives towards it).
        /// </summary>
        public static int Choose(IList<Site> sites, float length, float carDistance, Vector3 carPos, int current)
        {
            if (sites.Count == 0) return -1;
            if (current >= 0 && current < sites.Count)
            {
                var c = sites[current];
                float past = ApexMath.PosMod(carDistance - c.distance, length);   // > 0 once the car has passed it
                if (past > length * 0.5f) past -= length;
                if (Vector3.Distance(c.position, carPos) < MaxRange && past < 45f) return current;
            }
            int best = -1;
            float bestAhead = float.MaxValue;
            for (int i = 0; i < sites.Count; i++)
            {
                float ahead = ApexMath.PosMod(sites[i].distance - carDistance, length);
                if (ahead < bestAhead) { bestAhead = ahead; best = i; }
            }
            return best;
        }

        /// <summary>Field of view (degrees) that keeps a car framed at <paramref name="range"/> metres (zoom of a TV lens).</summary>
        public static float Fov(float range) => ApexMath.Clamp(2f * MathF.Atan(9f / MathF.Max(range, 1f)) * 180f / MathF.PI, 7f, 55f);
    }
}
