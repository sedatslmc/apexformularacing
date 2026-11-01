using System;
using System.Collections.Generic;
using Apex.Race;

namespace Apex.UI
{
    public enum HudEventKind { Info, Notice, Flag, FastestLap, PersonalBest, Record, PositionGained, PositionLost, FinalLap, Finish, Pit, Penalty }

    public enum HudTone { Neutral, Good, Best, Warning, Danger }

    public sealed class HudEvent
    {
        public HudEventKind kind;
        public string title = "", detail = "";
        public HudTone tone;
        public int priority;
        public double posted, duration;
        public double Ends => posted + duration;
    }

    /// <summary>
    /// The event banner at the top centre (docs/UI_UX_OVERHAUL.md, race HUD): one short message at a time, a small
    /// queue behind it. A higher-priority message replaces the current one at once (a safety car beats a position
    /// change); an identical message within a few seconds is dropped instead of repeated.
    /// </summary>
    public sealed class RaceEventFeed
    {
        public const int MaxQueue = 4;
        public const double DedupeSeconds = 3.0;
        readonly List<HudEvent> queue = new List<HudEvent>();
        readonly List<HudEvent> recent = new List<HudEvent>();

        public HudEvent Current { get; private set; }

        public static int PriorityOf(HudEventKind k)
        {
            switch (k)
            {
                case HudEventKind.Flag: return 5;
                case HudEventKind.Penalty: return 4;
                case HudEventKind.Finish: case HudEventKind.Record: case HudEventKind.Notice: return 3;
                case HudEventKind.FastestLap: case HudEventKind.PersonalBest: case HudEventKind.FinalLap: case HudEventKind.Pit: return 2;
                case HudEventKind.PositionGained: case HudEventKind.PositionLost: return 1;
                default: return 0;
            }
        }

        public static double DurationOf(HudEventKind k) => k == HudEventKind.Flag || k == HudEventKind.Finish ? 4.0 : k == HudEventKind.PositionGained || k == HudEventKind.PositionLost ? 2.0 : 3.0;

        public HudEvent Post(HudEventKind kind, string title, string detail, HudTone tone, double now, double duration = -1)
        {
            foreach (var r in recent)
                if (r.kind == kind && r.title == title && r.detail == detail && now - r.posted < DedupeSeconds) return null;
            var e = new HudEvent { kind = kind, title = title, detail = detail ?? "", tone = tone, priority = PriorityOf(kind), posted = now, duration = duration > 0 ? duration : DurationOf(kind) };
            recent.Add(e);
            recent.RemoveAll(r => now - r.posted >= DedupeSeconds);
            // A newer position change supersedes an older queued one.
            if (kind == HudEventKind.PositionGained || kind == HudEventKind.PositionLost)
                queue.RemoveAll(q => q.kind == HudEventKind.PositionGained || q.kind == HudEventKind.PositionLost);
            if (Current == null || now >= Current.Ends) { Current = e; return e; }
            if (e.priority > Current.priority) { Current = e; return e; }
            queue.Add(e);
            queue.Sort((a, b) => b.priority != a.priority ? b.priority.CompareTo(a.priority) : a.posted.CompareTo(b.posted));
            if (queue.Count > MaxQueue) queue.RemoveAt(queue.Count - 1);
            return e;
        }

        /// <summary>Advances the feed; returns the message to show (null = none).</summary>
        public HudEvent Update(double now)
        {
            if (Current != null && now >= Current.Ends)
            {
                Current = null;
                if (queue.Count > 0)
                {
                    Current = queue[0];
                    queue.RemoveAt(0);
                    Current.posted = now;   // queued messages get their full time on screen
                }
            }
            return Current;
        }

        public int Queued => queue.Count;

        public void Clear() { queue.Clear(); recent.Clear(); Current = null; }
    }

    /// <summary>What the detector looks at each HUD update (the player's view of the race).</summary>
    public struct RaceSnapshot
    {
        public bool race;
        public bool racing;
        public int position, fieldSize;
        public int lap, laps;            // lap the player is on (1-based) and race length
        public bool finished;
        public TrackStatus status;
        public bool yellowHere;
    }

    /// <summary>
    /// Turns changes between snapshots into feed messages: flags (with "green" when a neutralisation ends),
    /// position changes (only once a position has held for <see cref="PositionHoldSeconds"/>, so side-by-side
    /// battles do not flicker), the final lap and the finish. Lap records come from the timing events directly.
    /// </summary>
    public sealed class RaceEventDetector
    {
        public const double PositionHoldSeconds = 1.5;
        bool has;
        RaceSnapshot last;
        int confirmedPosition, pendingPosition;
        double pendingSince;
        bool finalLapShown, finishShown;

        public void Reset() { has = false; finalLapShown = false; finishShown = false; }

        public static string StatusTitle(TrackStatus s, bool yellowHere)
        {
            switch (s)
            {
                case TrackStatus.Yellow: return yellowHere ? Loc.T("GELBE FLAGGE") : null;
                case TrackStatus.VirtualSafetyCar: return Loc.T("VIRTUELLES SAFETY CAR");
                case TrackStatus.VirtualSafetyCarEnding: return Loc.T("VSC ENDET");
                case TrackStatus.SafetyCar: return Loc.T("SAFETY CAR");
                case TrackStatus.SafetyCarIn: return Loc.T("SAFETY CAR KOMMT REIN");
                default: return null;
            }
        }

        public static string StatusDetail(TrackStatus s)
        {
            switch (s)
            {
                case TrackStatus.Yellow: return Loc.T("Nicht überholen");
                case TrackStatus.VirtualSafetyCar: return Loc.T("Tempo begrenzt · Abstand halten");
                case TrackStatus.VirtualSafetyCarEnding: return Loc.T("Gleich wieder Grün");
                case TrackStatus.SafetyCar: return Loc.T("Hinter dem Safety Car einreihen");
                case TrackStatus.SafetyCarIn: return Loc.T("Neustart an der Linie");
                default: return "";
            }
        }

        public void Update(RaceSnapshot s, double now, RaceEventFeed feed)
        {
            if (!has)
            {
                has = true;
                last = s;
                confirmedPosition = pendingPosition = s.position;
                pendingSince = now;
                return;
            }
            // Flags.
            bool wasFlag = StatusTitle(last.status, last.yellowHere) != null;
            string title = StatusTitle(s.status, s.yellowHere);
            if (title != null && (s.status != last.status || s.yellowHere != last.yellowHere))
                feed.Post(HudEventKind.Flag, title, StatusDetail(s.status), s.status == TrackStatus.VirtualSafetyCarEnding ? HudTone.Good : HudTone.Warning, now);
            else if (title == null && wasFlag && s.status == TrackStatus.Green && last.status != TrackStatus.Yellow)
                feed.Post(HudEventKind.Flag, Loc.T("GRÜNE FLAGGE"), Loc.T("Rennen freigegeben"), HudTone.Good, now);

            if (s.race && s.racing && !s.finished)
            {
                // Position changes, debounced.
                if (s.position != pendingPosition) { pendingPosition = s.position; pendingSince = now; }
                else if (pendingPosition != confirmedPosition && now - pendingSince >= PositionHoldSeconds && confirmedPosition > 0)
                {
                    int diff = confirmedPosition - pendingPosition;
                    feed.Post(diff > 0 ? HudEventKind.PositionGained : HudEventKind.PositionLost, $"P{pendingPosition}",
                        diff > 0 ? $"+{diff} {Loc.T(diff == 1 ? "Platz" : "Plätze")}" : $"{diff} {Loc.T(diff == -1 ? "Platz" : "Plätze")}",
                        diff > 0 ? HudTone.Good : HudTone.Danger, now);
                    confirmedPosition = pendingPosition;
                }
                if (!finalLapShown && s.laps > 1 && s.lap == s.laps && last.lap == s.laps - 1)
                {
                    finalLapShown = true;
                    feed.Post(HudEventKind.FinalLap, Loc.T("LETZTE RUNDE"), Loc.Format("P{0} von {1}", s.position, s.fieldSize), HudTone.Neutral, now);
                }
            }
            if (s.race && s.finished && !finishShown)
            {
                finishShown = true;
                feed.Post(HudEventKind.Finish, s.position == 1 ? Loc.T("SIEG") : Loc.Format("ZIEL · P{0}", s.position), s.position <= 3 ? Loc.T("Podium") : Loc.Format("von {0}", s.fieldSize), s.position <= 3 ? HudTone.Best : HudTone.Neutral, now);
            }
            if (!s.racing) { confirmedPosition = pendingPosition = s.position; pendingSince = now; }
            last = s;
        }
    }
}
