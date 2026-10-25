using System;
using System.Collections.Generic;
using Apex.Core;

namespace Apex.Race
{
    /// <summary>
    /// Engine-independent lap and sector timing, fed with consecutive
    /// (lapDistance, simulationTime) samples from the fixed step. Line crossings are
    /// interpolated between samples, so resolution is far finer than one tick.
    /// Sectors must be passed in order for a lap to count, which defeats shortcuts
    /// and reversing over the line. Uses doubles: float time loses ms precision
    /// after a few hours.
    /// </summary>
    public sealed class LapTimer
    {
        /// <summary>Max forward progress accepted per sample; bigger jumps are teleports.</summary>
        public const float MaxStepDistance = 60f;
        /// <summary>Spacing of the reference trace used for the live delta.</summary>
        public const float TraceStep = 10f;

        public struct LapRecord
        {
            public int lap;
            public double time;
            public bool valid;
            public double[] sectors;
        }

        public event Action<int> LapStarted;
        /// <summary>(lap, time, valid, personalBest)</summary>
        public event Action<int, double, bool, bool> LapCompleted;
        /// <summary>(sectorIndex, sectorTime, deltaToBest or NaN)</summary>
        public event Action<int, double, double> SectorCompleted;
        public event Action<string> LapInvalidated;

        public readonly float trackLength;
        public readonly float[] sectorStarts;

        public int LapNumber { get; private set; }
        public bool LapActive { get; private set; }
        public bool LapValid { get; private set; } = true;
        public double LapStartTime { get; private set; }
        public int NextSector { get; private set; }
        public double[] SectorTimes { get; private set; }
        public double LastLapTime { get; private set; }
        public bool LastLapValid { get; private set; }
        public double BestLapTime { get; private set; }
        public readonly double[] bestSectorTimes;
        public readonly List<LapRecord> completedLaps = new List<LapRecord>();

        double sectorStartTime;
        List<double> currentTrace = new List<double>();
        List<double> bestTrace = new List<double>();
        float prevDistance = -1f;
        double prevTime;
        double lastTime;

        public LapTimer(float length, float[] sectors = null)
        {
            trackLength = length;
            var list = new List<float>(sectors ?? new[] { 0f });
            if (list.Count == 0 || list[0] != 0f) list.Insert(0, 0f);
            sectorStarts = list.ToArray();
            bestSectorTimes = new double[sectorStarts.Length];
            SectorTimes = new double[sectorStarts.Length];
        }

        public int SectorCount => sectorStarts.Length;

        public double CurrentLapTime(double now = -1) =>
            LapActive ? (now >= 0 ? now : lastTime) - LapStartTime : 0.0;

        /// <summary>Live delta to the best valid lap at this distance; NaN without reference.</summary>
        public double LiveDelta(float lapDistance, double now = -1)
        {
            if (!LapActive || bestTrace.Count < 2) return double.NaN;
            float x = ApexMath.Clamp(lapDistance / TraceStep, 0f, bestTrace.Count - 1);
            int i = Math.Min((int)x, bestTrace.Count - 2);
            double reference = bestTrace[i] + (bestTrace[i + 1] - bestTrace[i]) * (x - i);
            return CurrentLapTime(now) - reference;
        }

        /// <summary>Forget the previous sample after a reset/teleport; aborts the running lap.</summary>
        public void ResetPosition()
        {
            prevDistance = -1f;
            if (LapActive)
            {
                LapActive = false;
                LapInvalidated?.Invoke("Car reset");
            }
        }

        public void Invalidate(string reason)
        {
            if (LapActive && LapValid)
            {
                LapValid = false;
                LapInvalidated?.Invoke(reason);
            }
        }

        /// <summary>Feed one sample. lapDistance is measured from the start line in [0, length).</summary>
        public void Update(float lapDistance, double time)
        {
            lastTime = time;
            if (prevDistance < 0f)
            {
                prevDistance = lapDistance;
                prevTime = time;
                return;
            }
            float delta = lapDistance - prevDistance;
            if (delta < -trackLength * 0.5f) delta += trackLength;
            else if (delta > trackLength * 0.5f) delta -= trackLength;
            if (MathF.Abs(delta) > MaxStepDistance)
            {
                ResetPosition();
                prevDistance = lapDistance;
                prevTime = time;
                return;
            }
            if (delta > 0f) CheckCrossings(prevDistance, delta, prevTime, time);
            if (LapActive) RecordTrace(lapDistance, time);
            prevDistance = lapDistance;
            prevTime = time;
        }

        void CheckCrossings(float fromDist, float delta, double t0, double t1)
        {
            for (int guard = 0; guard < SectorCount; guard++)
            {
                float boundary = sectorStarts[NextSector];
                float ahead = ApexMath.PosMod(boundary - fromDist, trackLength);
                if (ahead > delta) return;
                double crossing = t0 + (t1 - t0) * (ahead / delta);
                OnBoundary(NextSector, crossing);
            }
        }

        void OnBoundary(int index, double t)
        {
            if (index == 0)
            {
                if (LapActive)
                {
                    FinishSector(SectorCount - 1, t);
                    FinishLap(t);
                }
                StartLap(t);
            }
            else if (LapActive)
            {
                FinishSector(index - 1, t);
            }
            NextSector = (index + 1) % SectorCount;
        }

        void StartLap(double t)
        {
            LapNumber++;
            LapActive = true;
            LapValid = true;
            LapStartTime = t;
            sectorStartTime = t;
            SectorTimes = new double[SectorCount];
            currentTrace = new List<double> { 0.0 };
            LapStarted?.Invoke(LapNumber);
        }

        void FinishSector(int index, double t)
        {
            double st = t - sectorStartTime;
            SectorTimes[index] = st;
            sectorStartTime = t;
            double best = bestSectorTimes[index];
            double d = best > 0 ? st - best : double.NaN;
            if (LapValid && (best <= 0 || st < best)) bestSectorTimes[index] = st;
            SectorCompleted?.Invoke(index, st, d);
        }

        void FinishLap(double t)
        {
            double lapTime = t - LapStartTime;
            LastLapTime = lapTime;
            LastLapValid = LapValid;
            bool pb = LapValid && (BestLapTime <= 0 || lapTime < BestLapTime);
            if (pb)
            {
                BestLapTime = lapTime;
                currentTrace.Add(lapTime);
                bestTrace = currentTrace;
            }
            completedLaps.Add(new LapRecord { lap = LapNumber, time = lapTime, valid = LapValid, sectors = (double[])SectorTimes.Clone() });
            LapCompleted?.Invoke(LapNumber, lapTime, LapValid, pb);
        }

        void RecordTrace(float lapDistance, double time)
        {
            int idx = (int)(lapDistance / TraceStep);
            while (currentTrace.Count <= idx && currentTrace.Count * TraceStep <= trackLength)
                currentTrace.Add(time - LapStartTime);
        }
    }
}
