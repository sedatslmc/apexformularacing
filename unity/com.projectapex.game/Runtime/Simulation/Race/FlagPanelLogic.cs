using System.Collections.Generic;
using Apex.Core;

namespace Apex.Race
{
    /// <summary>What a trackside LED flag panel shows.</summary>
    public enum PanelSignal
    {
        Off = 0,
        Green = 1,
        Yellow = 2,
        DoubleYellow = 3,
        Blue = 4,
        SafetyCar = 5,
        VirtualSafetyCar = 6,
    }

    /// <summary>
    /// Trackside LED flag panels driven by the actual race state (<see cref="RaceControl"/>
    /// and the car reports): SC/VSC boards while neutralised, yellow in front of an incident
    /// (double yellow when the car stands on the racing surface close ahead), green right
    /// after the incident zone and for a few seconds after a neutralisation ends, blue for
    /// a lapped car with a leader close behind. No signal is invented: without a matching
    /// race state the panel stays dark. There is no red flag rule in the game, so no red.
    /// </summary>
    public sealed class FlagPanelLogic
    {
        public float doubleYellowRange = 150f;
        public float greenAfterZone = 120f;
        public float greenAfterRestart = 8f;
        public float blueApproach = 90f;
        public float blueGapSeconds = 1.2f;

        TrackStatus lastStatus = TrackStatus.Green;
        double greenUntil = -1;

        /// <summary>Call once per update before evaluating panels.</summary>
        public void Update(RaceControl control, double now)
        {
            if (control == null) return;
            bool wasNeutral = lastStatus != TrackStatus.Green && lastStatus != TrackStatus.Yellow;
            if (wasNeutral && control.Status == TrackStatus.Green) greenUntil = now + greenAfterRestart;
            lastStatus = control.Status;
        }

        /// <summary>Signal for a panel at raw track distance `panel` (the panel faces the approaching cars).</summary>
        public PanelSignal Evaluate(float panel, float trackLength, RaceControl control, IReadOnlyList<CarReport> cars, double now)
        {
            if (control == null) return PanelSignal.Off;
            switch (control.Status)
            {
                case TrackStatus.SafetyCar:
                case TrackStatus.SafetyCarIn:
                    return PanelSignal.SafetyCar;
                case TrackStatus.VirtualSafetyCar:
                case TrackStatus.VirtualSafetyCarEnding:
                    return PanelSignal.VirtualSafetyCar;
            }
            var rules = control.rules;
            bool green = false;
            foreach (var inc in control.Incidents)
            {
                if (inc.cleared) continue;
                float ahead = ApexMath.PosMod(inc.distance - panel, trackLength);
                if (ahead <= rules.yellowBefore)
                    return inc.onTrack && ahead <= doubleYellowRange ? PanelSignal.DoubleYellow : PanelSignal.Yellow;
                float behind = ApexMath.PosMod(panel - inc.distance, trackLength);
                if (behind > rules.yellowAfter && behind <= rules.yellowAfter + greenAfterZone) green = true;
            }
            if (green || now < greenUntil) return PanelSignal.Green;
            if (cars != null && IsBlue(panel, trackLength, cars)) return PanelSignal.Blue;
            return PanelSignal.Off;
        }

        bool IsBlue(float panel, float trackLength, IReadOnlyList<CarReport> cars)
        {
            for (int i = 0; i < cars.Count; i++)
            {
                var slow = cars[i];
                if (slow.ignore || slow.inPit) continue;
                float toPanel = ApexMath.PosMod(panel - slow.distance, trackLength);
                if (toPanel > blueApproach) continue;
                for (int j = 0; j < cars.Count; j++)
                {
                    var fast = cars[j];
                    if (j == i || fast.ignore || fast.inPit) continue;
                    // A full lap (or more) ahead in race progress, physically just behind.
                    if (fast.progress - slow.progress < trackLength * 0.98) continue;
                    float gap = ApexMath.PosMod(slow.distance - fast.distance, trackLength);
                    if (gap < 1f || gap > trackLength * 0.5f) continue;
                    if (gap / System.Math.Max(fast.speed, 5f) <= blueGapSeconds) return true;
                }
            }
            return false;
        }
    }
}
