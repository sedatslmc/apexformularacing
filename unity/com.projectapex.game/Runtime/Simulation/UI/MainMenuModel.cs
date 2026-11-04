using System;
using System.Collections.Generic;

namespace Apex.UI
{
    /// <summary>
    /// Main menu actions. <see cref="Career"/> is reserved for the career mode (docs/UI_UX_OVERHAUL.md section 13.9):
    /// the navigation knows it, but the entry is only added once <see cref="MenuContext.careerAvailable"/> is set by a
    /// working implementation.
    /// </summary>
    public enum MenuAction { QuickRace, ContinueChampionship, GrandPrix, Championship, TimeTrial, Practice, Garage, Profile, Settings, Help, Quit, Career }

    public sealed class MenuEntry
    {
        public MenuAction action;
        public string label = "", detail = "";
        /// <summary>Short explanation shown while the entry has the focus.</summary>
        public string description = "";
        /// <summary>The highlighted "start a race" entry.</summary>
        public bool primary;
    }

    /// <summary>What the main menu may show, from the real game state (no entry without a working function).</summary>
    public sealed class MenuContext
    {
        public string trackName = "";
        public int laps;
        public string timeOfDay = "";
        public bool championshipActive;
        public int championshipRound, championshipRounds;
        public string championshipTrack = "";
        /// <summary>Player's championship position (0 = unknown).</summary>
        public int championshipPosition;
        /// <summary>Personal best lap (s) on the selected track; 0 = none yet.</summary>
        public double bestLap;
        public bool profileAvailable = true;
        /// <summary>Career mode implemented (not yet: the entry stays hidden).</summary>
        public bool careerAvailable;
        /// <summary>Quit button: desktop builds only (mobile apps are closed by the OS).</summary>
        public bool canQuit = true;
    }

    /// <summary>
    /// The main menu entries (docs/UI_UX_OVERHAUL.md section 3). "RENNEN STARTEN" is the quick race with the last
    /// settings (Grand Prix, race only); a running championship adds a "continue" entry right below it. Modes that
    /// do not exist yet (career, special events, challenges, custom championships) never appear.
    /// </summary>
    public static class MainMenuModel
    {
        public static List<MenuEntry> Build(MenuContext c)
        {
            var list = new List<MenuEntry>
            {
                new MenuEntry { action = MenuAction.QuickRace, label = Loc.T("Rennen starten"), primary = true,
                    detail = $"{c.trackName} · {c.laps} {Loc.T(c.laps == 1 ? "Runde" : "Runden")} · {c.timeOfDay}" },
            };
            if (c.championshipActive)
                list.Add(new MenuEntry { action = MenuAction.ContinueChampionship, label = Loc.T("Meisterschaft fortsetzen"),
                    detail = Loc.Format("Runde {0} / {1} · {2}", c.championshipRound, c.championshipRounds, c.championshipTrack) + (c.championshipPosition > 0 ? $" · P{c.championshipPosition}" : "") });
            list.Add(new MenuEntry { action = MenuAction.GrandPrix, label = Loc.T("Grand Prix"), detail = Loc.T("Qualifying + Rennen") });
            if (!c.championshipActive)
                list.Add(new MenuEntry { action = MenuAction.Championship, label = Loc.T("Meisterschaft"), detail = Loc.T("Saison starten") });
            list.Add(new MenuEntry { action = MenuAction.TimeTrial, label = Loc.T("Zeitfahren"), detail = c.bestLap > 0 ? Loc.Format("Bestzeit {0}", FormatLap(c.bestLap)) : Loc.T("Noch keine Bestzeit") });
            list.Add(new MenuEntry { action = MenuAction.Practice, label = Loc.T("Freies Training"), detail = "" });
            list.Add(new MenuEntry { action = MenuAction.Garage, label = Loc.T("Garage"), detail = Loc.T("Lackierung · Team · Setup") });
            if (c.profileAvailable) list.Add(new MenuEntry { action = MenuAction.Profile, label = Loc.T("Fahrerprofil"), detail = Loc.T("Statistiken") });
            list.Add(new MenuEntry { action = MenuAction.Settings, label = Loc.T("Einstellungen"), detail = Loc.T("Grafik · Audio · Steuerung · HUD") });
            if (c.careerAvailable) list.Insert(1, new MenuEntry { action = MenuAction.Career, label = Loc.T("Karriere"), detail = "" });
            if (c.canQuit) list.Add(new MenuEntry { action = MenuAction.Quit, label = Loc.T("Beenden"), detail = "" });
            foreach (var e in list) e.description = Describe(e.action);
            return list;
        }

        /// <summary>One-line description of an entry (shown under the menu for the focused entry).</summary>
        public static string Describe(MenuAction a)
        {
            switch (a)
            {
                case MenuAction.QuickRace: return Loc.T("Direkt ins Rennen mit deinen letzten Einstellungen – oder Schritt für Schritt vorbereiten.");
                case MenuAction.ContinueChampionship: return Loc.T("Weiter mit der nächsten Runde deiner laufenden Saison.");
                case MenuAction.GrandPrix: return Loc.T("Ein Rennwochenende: Qualifying, Startaufstellung und Rennen mit allen Regeln.");
                case MenuAction.Championship: return Loc.T("Eine Saison über mehrere Strecken mit Fahrerwertung und Punkten.");
                case MenuAction.TimeTrial: return Loc.T("Allein gegen die Uhr: Bestzeit und Sektoren werden gespeichert.");
                case MenuAction.Practice: return Loc.T("Freie Fahrt ohne Zeitlimit, um Strecke und Auto kennenzulernen.");
                case MenuAction.Garage: return Loc.T("Lackierung, Team, Startnummer und Setup – mit dem Auto auf der Bühne.");
                case MenuAction.Profile: return Loc.T("Deine Statistiken: Rennen, Siege, Podien, Bestzeiten.");
                case MenuAction.Settings: return Loc.T("Grafik, Audio, Steuerung, Fahrhilfen, HUD und Barrierefreiheit.");
                case MenuAction.Quit: return Loc.T("Spiel beenden.");
                case MenuAction.Career: return Loc.T("Deine Motorsportkarriere.");
                default: return "";
            }
        }

        public static string FormatLap(double t)
        {
            if (t <= 0 || double.IsNaN(t)) return "-:--.---";
            long ms = (long)Math.Round(t * 1000.0);
            return $"{ms / 60000}:{(ms / 1000) % 60:00}.{ms % 1000:000}";
        }
    }

    public enum UiScreen { Boot, MainMenu, Profile, Settings, Help }

    /// <summary>
    /// Screen stack of the menu layer with per-screen focus memory: Back returns to the previous screen with its
    /// focus where it was. The game state machine (GameStateMachine) stays authoritative for sessions; this only
    /// orders the overlay screens inside the menu.
    /// </summary>
    public sealed class UiNavigator
    {
        readonly List<UiScreen> stack = new List<UiScreen>();
        readonly Dictionary<UiScreen, int> focus = new Dictionary<UiScreen, int>();

        public UiNavigator(UiScreen root) { stack.Add(root); }

        public UiScreen Current => stack[stack.Count - 1];
        public int Depth => stack.Count;
        public bool CanGoBack => stack.Count > 1;
        /// <summary>Raised with (from, to) on every change of the current screen.</summary>
        public event Action<UiScreen, UiScreen> Changed;

        public int Focus(UiScreen s) => focus.TryGetValue(s, out int f) ? f : 0;
        public void SetFocus(int index) { focus[Current] = Math.Max(0, index); }

        /// <summary>Moves the focus by <paramref name="delta"/> within <paramref name="count"/> items, wrapping.</summary>
        public int MoveFocus(int delta, int count)
        {
            if (count <= 0) return 0;
            int f = ((Focus(Current) + delta) % count + count) % count;
            focus[Current] = f;
            return f;
        }

        public void Push(UiScreen s)
        {
            if (s == Current) return;
            var from = Current;
            stack.Add(s);
            Changed?.Invoke(from, s);
        }

        /// <summary>Back one screen; false at the root (the caller decides, e.g. a quit prompt).</summary>
        public bool Back()
        {
            if (!CanGoBack) return false;
            var from = Current;
            stack.RemoveAt(stack.Count - 1);
            Changed?.Invoke(from, Current);
            return true;
        }

        /// <summary>Replaces the whole stack with a new root (e.g. Boot → MainMenu), focus memory kept.</summary>
        public void Reset(UiScreen root)
        {
            var from = Current;
            stack.Clear();
            stack.Add(root);
            if (from != root) Changed?.Invoke(from, root);
        }
    }
}
