using System;
using System.Collections.Generic;

namespace Apex.UI
{
    public enum PauseAction { Resume, ResetCar, Restart, Settings, HudEditor, Help, Leave }

    public sealed class PauseEntry
    {
        public PauseAction action;
        public string label = "";
        /// <summary>Asks for a second confirmation (leaving a race forfeits it).</summary>
        public bool confirm;
    }

    /// <summary>Pause menu entries (docs/UI_UX_OVERHAUL.md section 4): only functions the session offers.</summary>
    public static class PauseModel
    {
        public static List<PauseEntry> Entries(bool race, bool hudEditor)
        {
            var l = new List<PauseEntry> { new PauseEntry { action = PauseAction.Resume, label = Loc.T("Weiter") } };
            if (!race)
            {
                l.Add(new PauseEntry { action = PauseAction.ResetCar, label = Loc.T("Auto auf die Strecke setzen") });
                l.Add(new PauseEntry { action = PauseAction.Restart, label = Loc.T("Session neu starten") });
            }
            l.Add(new PauseEntry { action = PauseAction.Settings, label = Loc.T("Einstellungen") });
            if (hudEditor) l.Add(new PauseEntry { action = PauseAction.HudEditor, label = Loc.T("HUD anpassen") });
            l.Add(new PauseEntry { action = PauseAction.Help, label = Loc.T("Steuerung & Hilfe") });
            l.Add(new PauseEntry { action = PauseAction.Leave, label = Loc.T(race ? "Rennen aufgeben" : "Zum Hauptmenü"), confirm = race });
            return l;
        }
    }

    /// <summary>Two-step confirmation: the first press arms, the second (on the same item) confirms; anything else disarms.</summary>
    public sealed class ConfirmGate
    {
        int armed = -1;
        public bool Armed(int item) => armed == item;
        /// <summary>Returns true when the press confirms.</summary>
        public bool Press(int item)
        {
            if (armed == item) { armed = -1; return true; }
            armed = item;
            return false;
        }
        public void Disarm() => armed = -1;
    }

    public enum ResultAction { ContinueToRace, NextRound, MainMenu }

    public struct WeekendStep
    {
        public string name;
        public bool done, current;
    }

    /// <summary>Results screen: headline, Grand Prix weekend steps, podium moment and the follow-up actions.</summary>
    public static class ResultsModel
    {
        /// <summary>Big headline and sub line for the player's result.</summary>
        public static (string title, string sub) Headline(bool race, bool qualifying, int position, int field, bool finished)
        {
            if (race && !finished) return (Loc.T("NICHT IM ZIEL"), Loc.T("Rennen beendet"));
            string of = Loc.Format("P{0} von {1}", position, field);
            if (race) return (position == 1 ? Loc.T("SIEG") : position <= 3 ? Loc.T("PODIUM") : $"P{position}", of);
            if (qualifying) return (position == 1 ? Loc.T("POLE POSITION") : $"P{position}", Loc.T("Startplatz für das Rennen"));
            return (Loc.T("SESSION BEENDET"), of);
        }

        /// <summary>Weekend timeline of a Grand Prix with qualifying (empty for single sessions).</summary>
        public static List<WeekendStep> Weekend(bool grandPrixWithQualifying, bool raceDone)
        {
            var l = new List<WeekendStep>();
            if (!grandPrixWithQualifying) return l;
            l.Add(new WeekendStep { name = Loc.T("Qualifying"), done = true });
            l.Add(new WeekendStep { name = Loc.T("Startaufstellung"), done = raceDone, current = !raceDone });
            l.Add(new WeekendStep { name = Loc.T("Rennen"), done = raceDone });
            return l;
        }

        public static List<(ResultAction action, string label)> Actions(bool qualifyingOfGrandPrix, bool championshipNext)
        {
            var l = new List<(ResultAction, string)>();
            if (qualifyingOfGrandPrix) l.Add((ResultAction.ContinueToRace, Loc.T("Weiter zum Rennen")));
            if (championshipNext) l.Add((ResultAction.NextRound, Loc.T("Nächste Meisterschaftsrunde")));
            l.Add((ResultAction.MainMenu, Loc.T("Hauptmenü")));
            return l;
        }

        /// <summary>The podium shot plays for a top-3 race finish on a circuit that has a podium.</summary>
        public static bool ShowPodium(bool race, bool finished, int position, bool circuitHasPodium) =>
            race && finished && position >= 1 && position <= 3 && circuitHasPodium;

        public const float PodiumSeconds = 4f;
    }

    public enum SettingsCategory { Graphics, Controls, Driving, Audio, Hud, Accessibility }

    public static class SettingsCatalog
    {
        public static readonly SettingsCategory[] All =
            { SettingsCategory.Graphics, SettingsCategory.Controls, SettingsCategory.Driving, SettingsCategory.Audio, SettingsCategory.Hud, SettingsCategory.Accessibility };

        public static string Name(SettingsCategory c)
        {
            switch (c)
            {
                case SettingsCategory.Graphics: return Loc.T("Grafik");
                case SettingsCategory.Controls: return Loc.T("Steuerung");
                case SettingsCategory.Driving: return Loc.T("Fahrverhalten");
                case SettingsCategory.Audio: return Loc.T("Audio");
                case SettingsCategory.Hud: return Loc.T("HUD");
                default: return Loc.T("Barrierefreiheit");
            }
        }

        public static string Description(SettingsCategory c)
        {
            switch (c)
            {
                case SettingsCategory.Graphics: return Loc.T("Qualitätsstufe, Umgebung, Effekte");
                case SettingsCategory.Controls: return Loc.T("Eingabegerät, Neigung, Touch-Tasten");
                case SettingsCategory.Driving: return Loc.T("Fahrhilfen und Fahrprofil");
                case SettingsCategory.Audio: return Loc.T("Lautstärke von Spiel und Musik");
                case SettingsCategory.Hud: return Loc.T("Preset, Größe, Einheiten");
                default: return Loc.T("Bewegung, Text, Kontrast, Farben, Sprache");
            }
        }
    }

    public enum GarageView { Front, Side, Rear, Top }

    /// <summary>
    /// Garage camera: preset views plus free orbit (drag to rotate, wheel to zoom). Angles in degrees around the car
    /// (0 = in front of the car looking back at it), pitch above the horizon, distance in metres.
    /// </summary>
    public sealed class GarageOrbit
    {
        public const float MinDistance = 3.2f, MaxDistance = 9f, MinPitch = 2f, MaxPitch = 80f;
        public float yaw = 35f, pitch = 14f, distance = 6f;
        float targetYaw = 35f, targetPitch = 14f, targetDistance = 6f;

        public static string Name(GarageView v)
        {
            switch (v)
            {
                case GarageView.Front: return Loc.T("Front");
                case GarageView.Side: return Loc.T("Seite");
                case GarageView.Rear: return Loc.T("Heck");
                default: return Loc.T("Oben");
            }
        }

        public static void Preset(GarageView v, out float yaw, out float pitch, out float distance)
        {
            switch (v)
            {
                case GarageView.Front: yaw = 35f; pitch = 12f; distance = 6f; break;
                case GarageView.Side: yaw = 90f; pitch = 6f; distance = 6.5f; break;
                case GarageView.Rear: yaw = 205f; pitch = 14f; distance = 6f; break;
                default: yaw = 90f; pitch = 72f; distance = 7.5f; break;
            }
        }

        public void Select(GarageView v) => Preset(v, out targetYaw, out targetPitch, out targetDistance);

        /// <summary>Pointer drag in pixels: 0.25° per pixel; the pitch stays above the floor.</summary>
        public void Drag(float dx, float dy)
        {
            targetYaw += dx * 0.25f;
            targetPitch = Math.Max(MinPitch, Math.Min(MaxPitch, targetPitch + dy * 0.25f));
        }

        public void Zoom(float steps) => targetDistance = Math.Max(MinDistance, Math.Min(MaxDistance, targetDistance - steps * 0.5f));

        /// <summary>Eases towards the target (the shortest way round); reduced motion jumps.</summary>
        public void Update(float dt, bool reducedMotion)
        {
            float k = reducedMotion ? 1f : 1f - (float)Math.Exp(-dt * 8f);
            float dyaw = ((targetYaw - yaw) % 360f + 540f) % 360f - 180f;
            yaw = ((yaw + dyaw * k) % 360f + 360f) % 360f;
            pitch += (targetPitch - pitch) * k;
            distance += (targetDistance - distance) * k;
        }

        public float TargetYaw => targetYaw;
        public float TargetPitch => targetPitch;
        public float TargetDistance => targetDistance;

        /// <summary>Camera offset from the car centre (x right, y up, z forward of the car).</summary>
        public void Offset(out float x, out float y, out float z)
        {
            double a = yaw * Math.PI / 180.0, p = pitch * Math.PI / 180.0;
            x = (float)(Math.Sin(a) * Math.Cos(p) * distance);
            y = (float)(Math.Sin(p) * distance);
            z = (float)(Math.Cos(a) * Math.Cos(p) * distance);
        }
    }

    /// <summary>Start sequence camera: a sweep around the car that blends into the driving camera.</summary>
    public static class IntroCamera
    {
        public const float Seconds = 3.2f;

        /// <summary>Blend weight towards the driving camera (0 = intro pose, 1 = driving camera).</summary>
        public static float Blend(float t) => UiMotion.InOutCubic(Math.Max(0f, Math.Min(1f, (t - 1.2f) / (Seconds - 1.2f))));

        /// <summary>Intro orbit angle (deg, 0 = in front of the car) and height over time.</summary>
        public static void Pose(float t, out float angleDeg, out float distance, out float height)
        {
            float u = Math.Max(0f, Math.Min(1f, t / Seconds));
            angleDeg = 150f - 120f * UiMotion.OutCubic(u);
            distance = 7.5f - 1.5f * u;
            height = 1.2f + 0.6f * u;
        }
    }
}
