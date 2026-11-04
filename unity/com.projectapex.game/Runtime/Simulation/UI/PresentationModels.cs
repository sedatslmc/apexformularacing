using System;
using System.Collections.Generic;
using System.Numerics;

namespace Apex.UI
{
    // --- Loading screens ------------------------------------------------------------------------------------------

    /// <summary>Situations with their own loading screen (docs/UI_UX_OVERHAUL.md, "Race Control 2.0").</summary>
    public enum LoadContext { GameStart, Race, TimeTrial, Practice, Championship, MenuReturn, SaveGame, Career }

    public sealed class LoadStage
    {
        public string id = "", label = "";
        /// <summary>Relative cost of the stage (measured shares of the build, not a timer).</summary>
        public float weight = 1f;
        public bool done;
    }

    /// <summary>
    /// A loading screen's content and its real progress: the work is split into the actual build stages (road, pit
    /// complex, event structures, infrastructure, city, cars, lighting …); a stage counts when its code has run, so the
    /// bar never shows an invented percentage. Weights are the stages' share of the build time on the reference PC
    /// (rough, from the build logs); the bar moves in steps, not smoothly.
    /// </summary>
    public sealed class LoadingModel
    {
        public readonly LoadContext context;
        public readonly List<LoadStage> stages;

        public LoadingModel(LoadContext context, List<LoadStage> stages)
        {
            this.context = context;
            this.stages = stages;
        }

        /// <summary>Stages of loading a session: the circuit build (CircuitBuilder.BuildSteps) and the session setup.</summary>
        public static List<LoadStage> SessionStages(bool city, bool pit, bool eventIdentity)
        {
            var s = new List<LoadStage>
            {
                new LoadStage { id = "geometry", label = Loc.T("Streckengeometrie"), weight = 1f },
                new LoadStage { id = "surface", label = Loc.T("Fahrbahn und Kollision"), weight = 2f },
            };
            if (pit) s.Add(new LoadStage { id = "pit", label = Loc.T("Boxenanlage"), weight = 1.5f });
            s.Add(new LoadStage { id = "terrain", label = Loc.T("Gelände und Wasser"), weight = 1f });
            if (eventIdentity) s.Add(new LoadStage { id = "event", label = Loc.T("Boxengebäude und Startbrücke"), weight = 1f });
            s.Add(new LoadStage { id = "infrastructure", label = Loc.T("Barrieren und Streckenposten"), weight = 2f });
            if (city) s.Add(new LoadStage { id = "environment", label = Loc.T("Istanbul"), weight = 6f });
            s.Add(new LoadStage { id = "cars", label = Loc.T("Fahrzeuge und Fahrer"), weight = 1.5f });
            s.Add(new LoadStage { id = "lighting", label = Loc.T("Licht und Wetter"), weight = 0.5f });
            return s;
        }

        /// <summary>Marks a stage done (unknown ids are ignored).</summary>
        public void Complete(string id)
        {
            foreach (var s in stages) if (s.id == id) s.done = true;
        }

        /// <summary>Share of the work done (0..1), from completed stages only.</summary>
        public float Progress
        {
            get
            {
                float all = 0f, done = 0f;
                foreach (var s in stages) { all += s.weight; if (s.done) done += s.weight; }
                return all <= 0f ? 1f : done / all;
            }
        }

        /// <summary>The stage being worked on (the first not done), or "" when finished.</summary>
        public string Current
        {
            get
            {
                foreach (var s in stages) if (!s.done) return s.label;
                return "";
            }
        }

        public bool Done => Progress >= 0.999f;

        /// <summary>Headline of the screen.</summary>
        public static string Title(LoadContext c, string trackName)
        {
            switch (c)
            {
                case LoadContext.GameStart: return "APEX";
                case LoadContext.MenuReturn: return Loc.T("Zurück ins Paddock");
                case LoadContext.SaveGame: return Loc.T("Spielstand wird geladen");
                case LoadContext.Career: return Loc.T("Karriere");
                default: return (trackName ?? "").ToUpperInvariant();
            }
        }

        /// <summary>Line under the headline: the session and the place.</summary>
        public static string Subtitle(LoadContext c, string place, int round)
        {
            switch (c)
            {
                case LoadContext.Race: return place;
                case LoadContext.TimeTrial: return Loc.T("Zeitfahren") + " · " + place;
                case LoadContext.Practice: return Loc.T("Freies Training") + " · " + place;
                case LoadContext.Championship: return Loc.Format("Meisterschaft · Runde {0}", round.ToString("00")) + " · " + place;
                case LoadContext.GameStart: return "RACE BEYOND LIMITS";
                default: return "";
            }
        }

        /// <summary>Tips shown while loading (rotating).</summary>
        public static readonly string[] Tips =
        {
            "Reifen brauchen Temperatur, um vollen Grip aufzubauen – die ersten Kurven vorsichtig.",
            "Bremse vor engen Kurven gerade und kontrolliert, erst dann einlenken.",
            "Nutze den Windschatten auf langen Geraden – und setze dich vor der Bremszone daneben.",
            "Achte in der Boxengasse auf das Tempolimit: Wer zu schnell über die Linie fährt, bekommt eine Strafe.",
            "Passe die Reifenstrategie dem Wetter an: Intermediates ab feuchter Strecke, Regenreifen bei stehendem Wasser.",
            "Mit der Einparkhilfe schiebt die Crew das Auto auf die Marken – fahre trotzdem mittig in die Box.",
            "Die dynamische Ideallinie färbt sich rot, wenn du für die nächste Kurve zu schnell bist.",
            "Auf dem Bosporus-Kurs liegen die Kurven nah an Mauern: Tracklimits kosten Zeit, Mauern das Rennen.",
        };

        /// <summary>The tip at <paramref name="seconds"/> into the screen (a new one every 6 s, starting at a seeded tip).</summary>
        public static string TipAt(float seconds, int seed)
        {
            int n = Tips.Length;
            int i = ((seed % n) + n + (int)(Math.Max(0f, seconds) / 6f)) % n;
            return Loc.T(Tips[i]);
        }

        /// <summary>Only real loads get a loading screen; a menu change does not (the garage is a camera move).</summary>
        public static bool Wanted(LoadContext c) => c != LoadContext.Career;
    }

    // --- Title screen ---------------------------------------------------------------------------------------------

    public static class TitleScreen
    {
        public const string Tagline = "RACE BEYOND LIMITS";
        public static string Prompt(bool touch) => Loc.T(touch ? "ZUM STARTEN TIPPEN" : "DRÜCKE EINE TASTE ZUM STARTEN");
    }

    // --- Cinematics -----------------------------------------------------------------------------------------------

    /// <summary>How the camera moves in a shot.</summary>
    public enum ShotMove
    {
        /// <summary>Circles the subject (angle from → to, at radius and height).</summary>
        Orbit,
        /// <summary>Slides along the subject's forward axis past it (offset from → to), looking at it.</summary>
        Dolly,
        /// <summary>Rises over the subject while turning (a crane / drone reveal).</summary>
        Crane,
        /// <summary>Flies along the forward axis high above, looking down ahead (aerial over a straight or a bridge).</summary>
        Flyover,
    }

    /// <summary>What a shot is pointed at; the runtime resolves it to a world position and direction.</summary>
    public enum ShotSubject { PlayerCar, PlayerWheel, PlayerRear, Corner, CityFlags, Bridge, StartStraight, PitBuilding, Grid, TrackOverview, Logo }

    /// <summary>One camera shot of a cinematic sequence.</summary>
    public struct CinematicShot
    {
        public ShotSubject subject;
        public ShotMove move;
        public float duration;
        /// <summary>Distance from the subject (m), height above it (m), angles in degrees (0 = behind, 90 = right side).</summary>
        public float radius, height, angleFrom, angleTo, fov;
        /// <summary>Text card over the shot (empty = none).</summary>
        public string title, subtitle;
        /// <summary>Grid slot shown (Grid shots), 0 = pole.</summary>
        public int gridSlot;
        /// <summary>Night look (brake lights): the runtime may dim the scene for this shot.</summary>
        public bool night;
    }

    /// <summary>
    /// The game's cinematic sequences, built from in-game cameras over the real scene (no video files): the opening
    /// intro, and the race start presentation. Pure data and camera maths so the timings and poses are tested; the
    /// runtime (CinematicDirector) resolves the subjects and plays the shots, every one skippable.
    /// </summary>
    public static class CinematicScripts
    {
        /// <summary>
        /// The opening intro (first start; replayable): a car in the dark garage, the tyres, a turn-in, brake lights at
        /// night, the Turkish flags along the Bosphorus course, a flight over the bridge, the start straight, then the
        /// APEX logo with "RACE BEYOND LIMITS". Without the city (mobile, backdrop off) only the car shots.
        /// </summary>
        public static List<CinematicShot> GameIntro(bool city)
        {
            var s = new List<CinematicShot>
            {
                new CinematicShot { subject = ShotSubject.PlayerCar, move = ShotMove.Orbit, duration = 2.6f, radius = 4.2f, height = 0.6f, angleFrom = 30f, angleTo = 75f, fov = 38f, night = true },
                new CinematicShot { subject = ShotSubject.PlayerWheel, move = ShotMove.Dolly, duration = 1.6f, radius = 1.4f, height = 0.25f, angleFrom = -1.2f, angleTo = 1.2f, fov = 30f, night = true },
            };
            if (city)
            {
                s.Add(new CinematicShot { subject = ShotSubject.Corner, move = ShotMove.Orbit, duration = 2.0f, radius = 18f, height = 2.5f, angleFrom = 120f, angleTo = 150f, fov = 42f });
                s.Add(new CinematicShot { subject = ShotSubject.PlayerRear, move = ShotMove.Dolly, duration = 1.6f, radius = 3.2f, height = 0.5f, angleFrom = -0.8f, angleTo = -2.6f, fov = 34f, night = true });
                s.Add(new CinematicShot { subject = ShotSubject.CityFlags, move = ShotMove.Orbit, duration = 2.0f, radius = 30f, height = 8f, angleFrom = 200f, angleTo = 230f, fov = 40f });
                s.Add(new CinematicShot { subject = ShotSubject.Bridge, move = ShotMove.Flyover, duration = 3.0f, radius = 260f, height = 120f, angleFrom = -1f, angleTo = 1f, fov = 50f });
                s.Add(new CinematicShot { subject = ShotSubject.StartStraight, move = ShotMove.Dolly, duration = 2.2f, radius = 12f, height = 1.2f, angleFrom = -60f, angleTo = 20f, fov = 40f });
            }
            else
                s.Add(new CinematicShot { subject = ShotSubject.PlayerCar, move = ShotMove.Crane, duration = 2.4f, radius = 6f, height = 1f, angleFrom = 160f, angleTo = 210f, fov = 40f });
            s.Add(new CinematicShot { subject = ShotSubject.Logo, move = ShotMove.Orbit, duration = 2.6f, radius = 7f, height = 1.6f, angleFrom = 200f, angleTo = 215f, fov = 40f, title = "APEX", subtitle = TitleScreen.Tagline });
            return s;
        }

        /// <summary>
        /// Race start presentation: the bridge, the track from above, the start straight with the pit building and the
        /// grandstands, the event title ("BOSPHORUS GRAND PRIX · ROUND 01"), the grid with the front rows and the
        /// player's car (lower thirds), then the hand-over to the driving camera before the start lights. Time trial and
        /// practice get a short version (track and car).
        /// </summary>
        public static List<CinematicShot> RaceIntro(bool race, string eventName, int round, bool bridge, int playerSlot, int field)
        {
            var s = new List<CinematicShot>();
            string title = (eventName ?? "").ToUpperInvariant();
            if (!race)
            {
                s.Add(new CinematicShot { subject = ShotSubject.TrackOverview, move = ShotMove.Crane, duration = 2.4f, radius = 160f, height = 90f, angleFrom = 200f, angleTo = 230f, fov = 50f, title = title, subtitle = "" });
                s.Add(new CinematicShot { subject = ShotSubject.PlayerCar, move = ShotMove.Orbit, duration = 2.0f, radius = 6f, height = 1.4f, angleFrom = 140f, angleTo = 185f, fov = 42f });
                return s;
            }
            if (bridge) s.Add(new CinematicShot { subject = ShotSubject.Bridge, move = ShotMove.Flyover, duration = 2.8f, radius = 300f, height = 140f, angleFrom = -1f, angleTo = 1f, fov = 52f });
            s.Add(new CinematicShot { subject = ShotSubject.TrackOverview, move = ShotMove.Crane, duration = 2.6f, radius = 220f, height = 120f, angleFrom = 160f, angleTo = 200f, fov = 50f });
            s.Add(new CinematicShot { subject = ShotSubject.PitBuilding, move = ShotMove.Dolly, duration = 2.6f, radius = 28f, height = 9f, angleFrom = -40f, angleTo = 40f, fov = 45f, title = title, subtitle = round > 0 ? $"ROUND {round:00}" : "GRAND PRIX" });
            // The front of the grid, then the player's car.
            int front = Math.Min(field, 3);
            for (int k = 0; k < front; k++)
                if (k != playerSlot) s.Add(new CinematicShot { subject = ShotSubject.Grid, move = ShotMove.Dolly, duration = 1.4f, radius = 5f, height = 1.1f, angleFrom = 4f, angleTo = -4f, fov = 38f, gridSlot = k });
            s.Add(new CinematicShot { subject = ShotSubject.Grid, move = ShotMove.Orbit, duration = 2.2f, radius = 5.5f, height = 1.3f, angleFrom = 150f, angleTo = 200f, fov = 40f, gridSlot = Math.Max(0, playerSlot) });
            return s;
        }

        public static float Duration(IList<CinematicShot> shots)
        {
            float t = 0f;
            foreach (var s in shots) t += s.duration;
            return t;
        }

        /// <summary>Shot index and time within it at <paramref name="t"/> seconds into the sequence (index = count when over).</summary>
        public static (int index, float local) At(IList<CinematicShot> shots, float t)
        {
            for (int i = 0; i < shots.Count; i++)
            {
                if (t < shots[i].duration) return (i, Math.Max(0f, t));
                t -= shots[i].duration;
            }
            return (shots.Count, 0f);
        }

        /// <summary>
        /// Camera pose of a shot at <paramref name="local"/> seconds: eye and look-at point, from the subject's position and
        /// its forward direction (horizontal). Moves ease in and out (no jerk at the cuts' ends).
        /// </summary>
        public static (Vector3 eye, Vector3 target) Pose(in CinematicShot s, Vector3 subject, Vector3 forward, float local)
        {
            forward.Y = 0f;
            forward = forward.LengthSquared() > 1e-6f ? Vector3.Normalize(forward) : Vector3.UnitZ;
            var right = new Vector3(forward.Z, 0f, -forward.X);
            float k = UiMotion.InOutCubic(s.duration <= 0f ? 1f : Math.Max(0f, Math.Min(1f, local / s.duration)));
            float a = s.angleFrom + (s.angleTo - s.angleFrom) * k;
            switch (s.move)
            {
                case ShotMove.Dolly:
                {
                    // Offset along the forward axis (in subject lengths of radius), at the side given by angleFrom's sign.
                    float side = s.radius;
                    var eye = subject + forward * (a * s.radius * 0.5f) + right * side + Vector3.UnitY * s.height;
                    return (eye, subject + Vector3.UnitY * (s.height * 0.4f));
                }
                case ShotMove.Crane:
                {
                    float rad = a * MathF.PI / 180f;
                    var dir = -forward * MathF.Cos(rad) + right * MathF.Sin(rad);
                    var eye = subject + dir * s.radius + Vector3.UnitY * (s.height * (0.35f + 0.65f * k));
                    return (eye, subject);
                }
                case ShotMove.Flyover:
                {
                    var eye = subject + forward * (a * s.radius) + Vector3.UnitY * s.height - right * (s.radius * 0.35f);
                    return (eye, subject + forward * (a * s.radius * 0.6f + s.radius * 0.4f));
                }
                default:
                {
                    float rad = a * MathF.PI / 180f;
                    var dir = -forward * MathF.Cos(rad) + right * MathF.Sin(rad);
                    var eye = subject + dir * s.radius + Vector3.UnitY * s.height;
                    return (eye, subject + Vector3.UnitY * MathF.Min(1f, s.height * 0.5f));
                }
            }
        }

        /// <summary>Lower third for a grid shot: position, driver, team, tyre.</summary>
        public static string GridCard(int slot, string driver, string team, string compound) =>
            $"P{slot + 1}  {driver?.ToUpperInvariant()}  ·  {team}  ·  {compound}";
    }

    // --- Race preparation flow ------------------------------------------------------------------------------------

    public enum SetupStep { Mode, Track, Conditions, Car, Summary }

    /// <summary>
    /// The race preparation as steps (mode → track → conditions → car and team → summary with the big start button).
    /// The options are the ones of SetupModel (no second rule set); this only sorts them into steps and builds the
    /// summary card.
    /// </summary>
    public static class RaceSetupFlow
    {
        public static readonly SetupStep[] Steps = { SetupStep.Mode, SetupStep.Track, SetupStep.Conditions, SetupStep.Car, SetupStep.Summary };

        public static string Name(SetupStep s)
        {
            switch (s)
            {
                case SetupStep.Mode: return Loc.T("Modus");
                case SetupStep.Track: return Loc.T("Strecke");
                case SetupStep.Conditions: return Loc.T("Bedingungen");
                case SetupStep.Car: return Loc.T("Fahrzeug & Team");
                default: return Loc.T("Übersicht");
            }
        }

        /// <summary>Which step an option (SetupModel key) belongs to.</summary>
        public static SetupStep StepOf(string key)
        {
            switch (key)
            {
                case "track": return SetupStep.Track;
                case "team":
                case "compound":
                case "livery":
                case "number":
                    return SetupStep.Car;
                default: return SetupStep.Conditions;
            }
        }

        public static List<SetupOption> OptionsFor(SetupStep step, IList<SetupOption> all)
        {
            var r = new List<SetupOption>();
            foreach (var o in all) if (StepOf(o.key) == step) r.Add(o);
            return r;
        }

        /// <summary>The modes offered in the first step.</summary>
        public static readonly SetupMode[] Modes = { SetupMode.GrandPrix, SetupMode.TimeTrial, SetupMode.Practice };

        public static string ModeName(SetupMode m) => m == SetupMode.GrandPrix ? Loc.T("Grand Prix / Schnelles Rennen") : SetupModel.Title(m);

        public static string ModeDescription(SetupMode m)
        {
            switch (m)
            {
                case SetupMode.GrandPrix: return Loc.T("Ein Rennen gegen KI-Gegner, auf Wunsch mit Qualifying. Rundenzahl, Wetter, Feld und Regeln stellst du ein.");
                case SetupMode.TimeTrial: return Loc.T("Allein auf der Strecke gegen die Uhr: Bestzeit und Sektoren werden gespeichert.");
                case SetupMode.Practice: return Loc.T("Freie Fahrt ohne Zeitlimit zum Kennenlernen der Strecke und des Autos.");
                default: return "";
            }
        }

        /// <summary>The summary card before the start (label, value), from the real configuration.</summary>
        public static List<(string label, string value)> Summary(SetupMode mode, Apex.Core.SessionConfig c, string trackName, string place, string car)
        {
            var r = new List<(string, string)>
            {
                (Loc.T("Strecke"), trackName + (string.IsNullOrEmpty(place) ? "" : " · " + place)),
                (Loc.T("Modus"), mode == SetupMode.GrandPrix ? Loc.T(c.format == Apex.Core.WeekendFormat.QualifyingAndRace ? "Qualifying + Rennen" : "Rennen") : SetupModel.Title(mode)),
            };
            if (mode == SetupMode.GrandPrix)
            {
                r.Add((Loc.T("Runden"), c.laps.ToString()));
                r.Add((Loc.T("Gegner"), c.aiCount.ToString()));
            }
            if (mode != SetupMode.TimeTrial) r.Add((Loc.T("Wetter"), Apex.Weather.WeatherModel.Name(c.weather)));
            r.Add((Loc.T("Tageszeit"), SetupModel.TimeName(c.timeOfDay)));
            r.Add((Loc.T("Fahrzeug"), car));
            r.Add((Loc.T("Team"), Apex.Core.Roster.Teams[c.playerTeam].name));
            return r;
        }

        /// <summary>Short explanation of a setting (shown under the focused option).</summary>
        public static string Explain(string key)
        {
            switch (key)
            {
                case "track": return Loc.T("Die Rennstrecke. Länge, Kurven und Bestzeit stehen rechts.");
                case "time": return Loc.T("Tageszeit des Rennens: Sonnenstand, Licht und Schatten.");
                case "weather": return Loc.T("Wetter beim Start. Nässe kostet Grip und verlangt andere Reifen.");
                case "dynamicWeather": return Loc.T("Das Wetter kann sich während des Rennens ändern.");
                case "format": return Loc.T("Nur das Rennen, oder zuerst ein Qualifying, das die Startaufstellung bestimmt.");
                case "laps": return Loc.T("Anzahl der Rennrunden.");
                case "qualifying": return Loc.T("Dauer des Qualifyings.");
                case "compound": return Loc.T("Reifenmischung beim Start: weich ist schnell, hält aber kürzer.");
                case "wear": return Loc.T("Wie schnell die Reifen abbauen (1× realistisch, höher für kurze Rennen mit Stopps).");
                case "ai": return Loc.T("Anzahl der KI-Gegner im Feld.");
                case "difficulty": return Loc.T("Tempo der KI-Fahrer in Prozent ihres Könnens.");
                case "team": return Loc.T("Dein Team: Farben, Box und Teamkollege.");
                case "falseStarts": return Loc.T("Zeitstrafe für Bewegung vor dem Erlöschen der Startlichter.");
                case "trackLimits": return Loc.T("Strafe nach wiederholtem Verlassen der Strecke.");
                case "pitStop": return Loc.T("Mindestens ein Boxenstopp ist Pflicht, sonst gibt es eine Strafe.");
                case "safetyCar": return Loc.T("Bei Unfällen kommen Gelbphasen, virtuelles Safety Car oder Safety Car.");
                case "damage": return Loc.T("Kollisionen beschädigen Flügel, Aufhängung und Reifen.");
                case "failures": return Loc.T("KI-Autos können mit technischem Defekt ausfallen.");
                case "livery": return Loc.T("Lackierung deines Autos.");
                case "number": return Loc.T("Deine Startnummer.");
                default: return "";
            }
        }
    }
}
