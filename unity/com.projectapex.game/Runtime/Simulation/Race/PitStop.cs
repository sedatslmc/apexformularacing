using System;
using Apex.Vehicle;

namespace Apex.Race
{
    /// <summary>
    /// Pit stop service: stationary time from crew performance with rare,
    /// configurable mistakes; tire change state for the vehicle.
    /// </summary>
    public sealed class PitCrew
    {
        public float baseServiceTime = 2.6f;
        public float spread = 0.35f;
        /// <summary>Probability of a slow wheel (0 = never).</summary>
        public float mistakeChance = 0.04f;
        readonly Random rng;

        public PitCrew(int seed)
        {
            rng = new Random(seed);
        }

        /// <summary>Returns the stationary time (s) and whether a mistake happened.</summary>
        public (float time, bool mistake) ServiceTime(bool changeTires)
        {
            if (!changeTires) return (1.2f, false);
            float t = baseServiceTime + (float)(rng.NextDouble() * spread);
            bool mistake = rng.NextDouble() < mistakeChance;
            if (mistake) t += 1.5f + (float)rng.NextDouble() * 2.5f;
            return (t, mistake);
        }
    }

    /// <summary>Per-car pit request and stop state machine.</summary>
    public sealed class PitState
    {
        public enum Stage { None, Requested, InLane, Stopped, Leaving }

        public Stage stage = Stage.None;
        public TireCompound nextCompound = TireCompound.Medium;
        public bool changeTires = true;
        public float serviceRemaining;
        public float serviceTotal;
        public bool lastMistake;

        /// <summary>At rest on the box, the crew settling the car onto the marks (stage InLane; service has not started).</summary>
        public bool settling;
        public float settleTime, settleDuration;
        /// <summary>Service done, but the release is held: a car in the fast lane (PitBoxStop.BlocksRelease).</summary>
        public bool releaseHeld;
        /// <summary>How the car stood on its marks when it came to rest (for the HUD guide).</summary>
        public PitBoxStop.Fit fit = PitBoxStop.Fit.TooShort;
        /// <summary>Metres after the entry where the merge onto the track began (NaN: not yet; held back by traffic).</summary>
        public float mergeFrom = float.NaN;
        /// <summary>Pit lane speed check for this passage.</summary>
        public readonly PitSpeedMonitor speed = new PitSpeedMonitor();

        public bool InPitLane => stage == Stage.InLane || stage == Stage.Stopped || stage == Stage.Leaving;

        /// <summary>Back to normal racing (keeps the next compound).</summary>
        public void Finish()
        {
            stage = Stage.None;
            settling = false;
            releaseHeld = false;
            mergeFrom = float.NaN;
            fit = PitBoxStop.Fit.TooShort;
            speed.Reset();
        }

        public void Request(TireCompound compound)
        {
            if (stage == Stage.None || stage == Stage.Requested)
            {
                stage = Stage.Requested;
                nextCompound = compound;
            }
        }

        public void Cancel()
        {
            if (stage == Stage.Requested) stage = Stage.None;
        }
    }
}
