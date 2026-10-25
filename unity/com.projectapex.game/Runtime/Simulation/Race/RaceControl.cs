using System;
using System.Collections.Generic;
using Apex.Core;

namespace Apex.Race
{
    public enum TrackStatus
    {
        Green = 0,
        /// <summary>Local yellow around an incident; the rest of the track is green.</summary>
        Yellow = 1,
        VirtualSafetyCar = 2,
        VirtualSafetyCarEnding = 3,
        SafetyCar = 4,
        /// <summary>Safety car comes in at the end of this lap; restart at the line.</summary>
        SafetyCarIn = 5,
    }

    [Serializable]
    public sealed class RaceControlRules
    {
        public bool safetyCar = true;
        public bool virtualSafetyCar = true;
        /// <summary>A car slower than this (m/s) for <see cref="incidentDelay"/> is an incident.</summary>
        public float stoppedSpeed = 6f;
        public float incidentDelay = 3f;
        /// <summary>Incident still active after this long: VSC (off track) or safety car (on track).</summary>
        public float neutraliseAfter = 7f;
        /// <summary>Marshals clear a stranded car after this long (s).</summary>
        public float recoveryTime = 20f;
        /// <summary>VSC speed as a fraction of the normal racing speed at that point.</summary>
        public float vscSpeedFactor = 0.6f;
        public float vscEndingTime = 4f;
        /// <summary>Safety car stays out at least this long before "in this lap".</summary>
        public float safetyCarMinTime = 45f;
        /// <summary>Local yellow zone: from this far before the incident to a bit after it.</summary>
        public float yellowBefore = 250f;
        public float yellowAfter = 60f;
        /// <summary>Time to give a position back after passing under yellow/SC/VSC.</summary>
        public float giveBackTime = 10f;
        public float illegalPassPenalty = 5f;
    }

    /// <summary>Track-relative snapshot of a car for race control.</summary>
    public struct CarReport
    {
        public int id;
        /// <summary>Raw centreline distance (wraps at the track length).</summary>
        public float distance;
        /// <summary>Race progress (laps × length + lap distance); orders cars on track.</summary>
        public double progress;
        public float speed;
        /// <summary>The car stands on the racing surface (an obstacle) rather than in the run-off.</summary>
        public bool onRacingSurface;
        public bool inPit;
        /// <summary>Retired, finished or not yet allowed to move: ignored.</summary>
        public bool ignore;
    }

    public sealed class Incident
    {
        public int carId;
        public float distance;
        public double since;
        public bool onTrack;
        public bool cleared;
    }

    /// <summary>
    /// Race control: detects stranded cars, shows local yellows, neutralises the race
    /// with a virtual safety car (incident off the racing surface) or a safety car
    /// (obstacle on track), and polices passing under yellow/VSC/SC (give the place
    /// back or take a time penalty). Engine-independent; the session feeds car
    /// reports every step and applies speed limits and the safety car vehicle.
    /// </summary>
    public sealed class RaceControl
    {
        public readonly RaceControlRules rules;
        readonly float trackLength;
        readonly List<Incident> incidents = new List<Incident>();
        readonly Dictionary<int, double> slowSince = new Dictionary<int, double>();
        readonly Dictionary<int, double> lastProgress = new Dictionary<int, double>();
        readonly List<(int passer, int victim, double deadline)> pendingGiveBack = new List<(int, int, double)>();

        public TrackStatus Status { get; private set; } = TrackStatus.Green;
        public IReadOnlyList<Incident> Incidents => incidents;
        double statusSince;
        int safetyCarStartLap = -1;
        public int SafetyCarDeployments { get; private set; }
        public int VscDeployments { get; private set; }

        /// <summary>(new status, message for the HUD).</summary>
        public event Action<TrackStatus, string> StatusChanged;
        /// <summary>Marshals removed a stranded car: the session retires it.</summary>
        public event Action<int> CarRecovered;
        /// <summary>(passer, victim) must give the place back.</summary>
        public event Action<int, int> GiveBackRequested;
        public event Action<int, double, string> PenaltyIssued;

        public RaceControl(RaceControlRules rules, float trackLength)
        {
            this.rules = rules;
            this.trackLength = trackLength;
        }

        public bool Neutralised => Status == TrackStatus.VirtualSafetyCar || Status == TrackStatus.VirtualSafetyCarEnding
                                   || Status == TrackStatus.SafetyCar || Status == TrackStatus.SafetyCarIn;
        public bool SafetyCarOut => Status == TrackStatus.SafetyCar || Status == TrackStatus.SafetyCarIn;
        public bool VscActive => Status == TrackStatus.VirtualSafetyCar || Status == TrackStatus.VirtualSafetyCarEnding;

        /// <summary>True if the distance lies in a local yellow zone (or the whole track is neutralised).</summary>
        public bool IsYellowAt(float distance)
        {
            if (Neutralised) return true;
            foreach (var inc in incidents)
            {
                if (inc.cleared) continue;
                float before = ApexMath.PosMod(inc.distance - distance, trackLength);
                float after = ApexMath.PosMod(distance - inc.distance, trackLength);
                if (before <= rules.yellowBefore || after <= rules.yellowAfter) return true;
            }
            return false;
        }

        public bool IsIncidentCar(int id)
        {
            foreach (var inc in incidents) if (!inc.cleared && inc.carId == id) return true;
            return false;
        }

        void SetStatus(TrackStatus s, double now, string message)
        {
            if (s == Status) return;
            Status = s;
            statusSince = now;
            if (s == TrackStatus.SafetyCar) SafetyCarDeployments++;
            if (s == TrackStatus.VirtualSafetyCar) VscDeployments++;
            StatusChanged?.Invoke(s, message);
        }

        /// <summary>
        /// One race-control step. leaderLaps = laps completed by the leader (for the
        /// safety car's minimum lap); call <see cref="LeaderCrossedLine"/> on each lap.
        /// </summary>
        public void Update(double now, IReadOnlyList<CarReport> cars, int leaderLaps)
        {
            DetectIncidents(now, cars);
            UpdateStatus(now, leaderLaps);
            PolicePassing(now, cars);
        }

        void DetectIncidents(double now, IReadOnlyList<CarReport> cars)
        {
            foreach (var c in cars)
            {
                var existing = incidents.Find(i => i.carId == c.id && !i.cleared);
                bool slow = !c.ignore && !c.inPit && c.speed < rules.stoppedSpeed;
                if (!slow)
                {
                    slowSince.Remove(c.id);
                    if (existing != null && (c.ignore || c.inPit || c.speed > rules.stoppedSpeed * 2.5f)) existing.cleared = true;
                    continue;
                }
                if (!slowSince.TryGetValue(c.id, out double since)) slowSince[c.id] = since = now;
                if (existing == null && now - since >= rules.incidentDelay)
                {
                    incidents.Add(new Incident { carId = c.id, distance = c.distance, since = now, onTrack = c.onRacingSurface });
                    if (Status == TrackStatus.Green) SetStatus(TrackStatus.Yellow, now, "Gelbe Flagge – Unfall voraus");
                }
                else if (existing != null)
                {
                    existing.distance = c.distance;
                    existing.onTrack |= c.onRacingSurface;
                    if (now - existing.since >= rules.recoveryTime)
                    {
                        existing.cleared = true;
                        CarRecovered?.Invoke(c.id);
                    }
                }
            }
            incidents.RemoveAll(i => i.cleared && Status == TrackStatus.Green);
        }

        bool AnyActive(out Incident oldest)
        {
            oldest = null;
            foreach (var i in incidents)
                if (!i.cleared && (oldest == null || i.since < oldest.since)) oldest = i;
            return oldest != null;
        }

        void UpdateStatus(double now, int leaderLaps)
        {
            bool active = AnyActive(out var inc);
            switch (Status)
            {
                case TrackStatus.Green:
                    break;
                case TrackStatus.Yellow:
                    if (!active) SetStatus(TrackStatus.Green, now, "Grüne Flagge");
                    else if (now - inc.since >= rules.neutraliseAfter)
                    {
                        if (inc.onTrack && rules.safetyCar)
                        {
                            safetyCarStartLap = leaderLaps;
                            SetStatus(TrackStatus.SafetyCar, now, "SAFETY CAR – nicht überholen");
                        }
                        else if (rules.virtualSafetyCar)
                            SetStatus(TrackStatus.VirtualSafetyCar, now, "VIRTUELLES SAFETY CAR – Tempo reduzieren");
                    }
                    break;
                case TrackStatus.VirtualSafetyCar:
                    if (!active) SetStatus(TrackStatus.VirtualSafetyCarEnding, now, "VSC endet");
                    break;
                case TrackStatus.VirtualSafetyCarEnding:
                    if (active) SetStatus(TrackStatus.VirtualSafetyCar, now, "VIRTUELLES SAFETY CAR");
                    else if (now - statusSince >= rules.vscEndingTime) SetStatus(TrackStatus.Green, now, "Grüne Flagge – Rennen frei");
                    break;
                case TrackStatus.SafetyCar:
                    if (!active && now - statusSince >= rules.safetyCarMinTime && leaderLaps > safetyCarStartLap)
                        SetStatus(TrackStatus.SafetyCarIn, now, "Safety Car kommt in dieser Runde herein");
                    break;
                case TrackStatus.SafetyCarIn:
                    break;
            }
        }

        /// <summary>The leader crossed the start/finish line: restart after "SC in this lap".</summary>
        public void LeaderCrossedLine(double now)
        {
            if (Status == TrackStatus.SafetyCarIn)
            {
                incidents.Clear();
                SetStatus(TrackStatus.Green, now, "Grüne Flagge – Neustart");
            }
        }

        void PolicePassing(double now, IReadOnlyList<CarReport> cars)
        {
            // A pass = two running cars swap order in race progress. It is illegal when
            // made under VSC/SC or inside a yellow zone, unless the passed car is
            // stranded (the incident car) or in the pit lane.
            foreach (var a in cars)
            {
                if (a.ignore || a.inPit || !lastProgress.TryGetValue(a.id, out double aPrev)) continue;
                foreach (var b in cars)
                {
                    if (b.id == a.id || b.ignore || b.inPit || !lastProgress.TryGetValue(b.id, out double bPrev)) continue;
                    bool passed = aPrev < bPrev && a.progress > b.progress && b.progress - aPrev < trackLength * 0.5;
                    if (!passed) continue;
                    if (IsIncidentCar(b.id) || b.speed < rules.stoppedSpeed) continue;
                    if (!IsYellowAt(a.distance)) continue;
                    if (pendingGiveBack.Exists(p => p.passer == a.id && p.victim == b.id)) continue;
                    pendingGiveBack.Add((a.id, b.id, now + rules.giveBackTime));
                    GiveBackRequested?.Invoke(a.id, b.id);
                }
            }
            for (int i = pendingGiveBack.Count - 1; i >= 0; i--)
            {
                var (passer, victim, deadline) = pendingGiveBack[i];
                CarReport? pa = null, vi = null;
                foreach (var c in cars)
                {
                    if (c.id == passer) pa = c;
                    if (c.id == victim) vi = c;
                }
                if (pa == null || vi == null || vi.Value.ignore || vi.Value.inPit || pa.Value.progress < vi.Value.progress)
                {
                    pendingGiveBack.RemoveAt(i);
                    continue;
                }
                if (now >= deadline)
                {
                    pendingGiveBack.RemoveAt(i);
                    PenaltyIssued?.Invoke(passer, rules.illegalPassPenalty, "Überholen unter Gelb/Safety Car");
                }
            }
            foreach (var c in cars) lastProgress[c.id] = c.progress;
        }
    }
}
