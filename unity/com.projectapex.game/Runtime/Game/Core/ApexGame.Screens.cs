using System;
using System.Collections.Generic;
using Apex.Career;
using Apex.Core;
using Apex.Race;
using Apex.UI;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// UI Toolkit screens of Phases 5 and 6 (docs/UI_UX_OVERHAUL.md): pause, results with podium shot and Grand Prix
    /// weekend, settings by category, garage with camera views, plus the cross-cutting parts: screen fade, menu
    /// music, language, colour vision, high contrast and text size. Each screen falls back to its IMGUI version
    /// when UI Toolkit cannot start.
    /// </summary>
    public sealed partial class ApexGame
    {
        PauseView pauseView;
        ResultsView resultsView;
        OptionsView settingsView, garageView;
        ScreenFade screenFade;
        MenuMusic menuMusic;
        readonly GarageOrbit garageOrbit = new GarageOrbit();
        bool backConsumed;
        int appliedLanguage = -1, hudLanguage = -1;

        /// <summary>Esc / gamepad B that no overlay screen has taken this frame.</summary>
        bool BackPressed => input.PausePressed && !backConsumed;

        void CreateScreenViews()
        {
            if (uiSounds == null) return;
            try
            {
                pauseView = new PauseView(uiSounds);
                pauseView.Chosen += OnPauseAction;
                resultsView = new ResultsView(uiSounds);
                resultsView.Chosen += OnResultAction;
                settingsView = new OptionsView(uiSounds, "ApexSettings", 30);
                settingsView.Closed += () => { settingsOpen = false; focus = 0; SaveService.SaveSettings(Settings); };
                settingsView.OptionChanged += ApplyUiSettings;
                garageView = new OptionsView(uiSounds, "ApexGarage", 11);
                garageView.Closed += () => sm.TryTransition(GameState.MainMenu);
                garageView.CameraView += v => garageOrbit.Select(v);
                garageView.CameraDrag += (dx, dy) => garageOrbit.Drag(dx, dy);
                garageView.CameraZoom += z => garageOrbit.Zoom(z);
                screenFade = new ScreenFade();
                menuMusic = new MenuMusic(gameObject);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                pauseView = null; resultsView = null; settingsView = null; garageView = null; screenFade = null;
            }
            ApplyUiSettings();
        }

        /// <summary>Language, colours, contrast, text size and volumes from the settings (after every change).</summary>
        void ApplyUiSettings()
        {
            Loc.Current = (Language)Settings.language;
            UiPalette.Current = UiPalette.For((ColourVision)Settings.colourVision);
            UiPalette.HighContrast = Settings.highContrast;
            ApexUi.TextScale = Settings.uiTextScale;
            if (uiSounds != null) uiSounds.volume = Settings.masterVolume;
            if (menuMusic != null) menuMusic.volume = Settings.musicVolume * Settings.masterVolume;
            // Rebuild open screens in the new language once; the HUD (labels built once) only outside a session.
            if (appliedLanguage >= 0 && appliedLanguage != Settings.language && settingsView != null && settingsView.Visible) ShowSettingsView();
            appliedLanguage = Settings.language;
            if (hudLanguage < 0) hudLanguage = Settings.language;
            if (hudLanguage != Settings.language && hudView != null && !InRaceState)
            {
                hudView.Dispose();
                hudView = null;
                CreateHudView();
                hudLanguage = Settings.language;
            }
        }

        static SettingsCategory CategoryOf(string label)
        {
            if (label.StartsWith("Steuerung") || label.StartsWith("Neigung") || label.StartsWith("Linkshänder") || label.StartsWith("Tasten")) return SettingsCategory.Controls;
            if (label.StartsWith("HUD") || label.StartsWith("Geschwindigkeit")) return SettingsCategory.Hud;
            if (label.StartsWith("Reduzierte") || label.StartsWith("Sprache") || label.StartsWith("Farbsehen") || label.StartsWith("Hoher Kontrast") || label.StartsWith("Textgröße"))
                return SettingsCategory.Accessibility;
            if (label.StartsWith("Lautstärke") || label.StartsWith("Musik")) return SettingsCategory.Audio;
            if (label.StartsWith("Grafik") || label.StartsWith("Menükulisse") || label.StartsWith("Umgebung") || label.StartsWith("Straßen") || label.StartsWith("Nachbearbeitung") || label.StartsWith("Bewegungsunschärfe"))
                return SettingsCategory.Graphics;
            return SettingsCategory.Driving;
        }

        /// <summary>Rows of the new accessibility and audio settings (appended to the existing settings).</summary>
        void AddUiSettingRows(List<Row> rows)
        {
            var s = Settings;
            rows.Add(new Row { label = "Musik", value = () => s.musicVolume <= 0f ? "Aus" : $"{s.musicVolume * 100f:0} %", change = d => s.musicVolume += (d == 0 ? 1 : d) * 0.1f });
            rows.Add(new Row { label = "Sprache", value = () => Loc.Name((Language)s.language), change = d => s.language = Wrap(s.language + (d < 0 ? -1 : 1), 2) });
            rows.Add(new Row { label = "Farbsehen (HUD)", value = () => UiPalette.Name((ColourVision)s.colourVision), change = d => s.colourVision = Wrap(s.colourVision + (d < 0 ? -1 : 1), 4) });
            rows.Add(new Row { label = "Hoher Kontrast", value = () => OnOff(s.highContrast), change = d => s.highContrast = !s.highContrast });
            rows.Add(new Row { label = "Textgröße (Menüs)", value = () => $"{s.uiTextScale * 100f:0} %", change = d => s.uiTextScale = Mathf.Round((s.uiTextScale + (d < 0 ? -0.1f : 0.1f)) * 10f) / 10f });
        }

        SetupOption ToOption(Row r) => new SetupOption
        {
            key = r.label, label = r.label, value = r.value,
            change = d => { ChangeSetting(r, d); ApplyUiSettings(); },
        };

        void ShowSettingsView()
        {
            var cats = new List<OptionsView.Category>();
            foreach (var c in SettingsCatalog.All)
            {
                var cat = c;
                cats.Add(new OptionsView.Category
                {
                    name = SettingsCatalog.Name(c), description = SettingsCatalog.Description(c),
                    options = () =>
                    {
                        var list = new List<SetupOption>();
                        foreach (var r in SettingsRows()) if (CategoryOf(r.label) == cat) list.Add(ToOption(r));
                        return list;
                    },
                    notes = cat == SettingsCategory.Controls && !input.TiltAvailable
                        ? (Func<List<string>>)(() => new List<string> { Loc.T("Kein Bewegungssensor erkannt – Neigung fällt auf Touch-Tasten zurück.") }) : null,
                });
            }
            settingsView.ReducedMotion = Settings.reducedMotion;
            settingsView.Show(new OptionsView.Data
            {
                caption = Loc.T("Einstellungen gelten sofort und werden gespeichert."), title = Loc.T("Einstellungen"), categories = cats, mobile = Application.isMobilePlatform,
            });
        }

        void ShowGarageView()
        {
            var paints = UnlockedPaints();
            var s = Settings;
            var setupLabels = new HashSet<string> { "Fahrprofil", "Traktionskontrolle", "ABS", "Lenkhilfe", "Automatikgetriebe", "Bremsbalance vorne" };
            var cats = new List<OptionsView.Category>
            {
                new OptionsView.Category
                {
                    name = Loc.T("Lackierung"), description = Loc.T("Nur Optik – Leistung ist in allen Modi gleich."),
                    options = () => new List<SetupOption>
                    {
                        new SetupOption { key = "paint", label = "Lackierung", value = () => PaintName(Career.paintIndex), change = d =>
                        {
                            int i = Mathf.Max(0, paints.IndexOf(Career.paintIndex));
                            Career.paintIndex = paints[Wrap(i + (d < 0 ? -1 : 1), paints.Count)];
                            SaveCareer();
                            RebuildShowroomCar();
                        } },
                    },
                    swatch = key => key == "paint" ? (Func<UiTokens.Rgb?>)(() => { var c = PlayerPaint(); return new UiTokens.Rgb(c.r, c.g, c.b); }) : null,
                    notes = () => new List<string> { Loc.Format("Level {0} · {1} von 20 Lackierungen freigeschaltet", Career.Level, paints.Count) },
                },
                new OptionsView.Category
                {
                    name = Loc.T("Team") + " · " + Loc.T("Startnummer"), description = Loc.T("Nur Optik – Leistung ist in allen Modi gleich."),
                    options = () => new List<SetupOption>
                    {
                        new SetupOption { key = "team", label = "Team", value = () => Roster.Teams[Config.playerTeam].name,
                            change = d => { Config.playerTeam = Wrap(Config.playerTeam + (d < 0 ? -1 : 1), Roster.Teams.Length); SaveConfig(); RebuildShowroomCar(); } },
                        new SetupOption { key = "number", label = "Startnummer", value = () => Career.carNumber.ToString(),
                            change = d => { Career.carNumber = Mathf.Clamp(Career.carNumber + (d == 0 ? 1 : d), 2, 99); SaveCareer(); } },
                    },
                },
                new OptionsView.Category
                {
                    name = Loc.T("Setup"), description = Loc.T("Fahrhilfen und Fahrprofil"),
                    options = () =>
                    {
                        var list = new List<SetupOption>();
                        foreach (var r in SettingsRows()) if (setupLabels.Contains(r.label)) list.Add(ToOption(r));
                        return list;
                    },
                },
                new OptionsView.Category
                {
                    name = Loc.T("Fortschritt"), description = $"Level {Career.Level} · {Career.xp} XP",
                    options = () => new List<SetupOption>(),
                    notes = () =>
                    {
                        var n = new List<string>();
                        int lvl = Career.Level;
                        if (lvl < Progression.MaxLevel) { var next = Progression.Rewards[lvl]; n.Add(Loc.Format("Nächste Belohnung (Level {0}): {1}", next.level, next.name)); }
                        var unlocked = Career.Unlocked();
                        for (int i = unlocked.Count - 1, k = 0; i >= 0 && k < 8; i--, k++) n.Add($"• Level {unlocked[i].level}: {unlocked[i].name}");
                        return n;
                    },
                },
            };
            garageView.ReducedMotion = s.reducedMotion;
            garageView.Show(new OptionsView.Data { caption = Loc.T("Garage"), title = Loc.T("Garage"), categories = cats, garage = true, mobile = Application.isMobilePlatform });
            garageOrbit.Select(GarageView.Front);
        }

        /// <summary>Garage camera from the orbit (preset views, drag, zoom).</summary>
        bool UpdateGarageCamera()
        {
            if (garageView == null || !garageView.Visible || showroom == null) return false;
            garageOrbit.Update(Time.unscaledDeltaTime, Settings.reducedMotion);
            garageOrbit.Offset(out float x, out float y, out float z);
            var car = showroomCar != null ? showroomCar.transform : showroom.transform;
            var centre = car.position + Vector3.up * 0.35f;
            var pos = centre + car.right * x + Vector3.up * y + car.forward * z;
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(centre - pos, Vector3.up));
            cam.fieldOfView = 38f;
            return true;
        }

        // --- Pause ----------------------------------------------------------------------------------------------

        void ShowPauseView()
        {
            if (pauseView == null || Race == null) return;
            var d = Race.Director;
            var p = Race.Player;
            bool race = Race.Type == SessionType.Race;
            string session = $"{Race.Circuit.Definition.name} · " + Loc.T(race ? "Rennen" : Race.Type == SessionType.Qualifying ? "Qualifying" : kind == SessionKind.TimeTrial ? "Zeitfahren" : "Training");
            string status = (race ? Loc.Format("RUNDE {0}/{1}", Mathf.Min(p.entry.LapsCompleted + 1, d.rules.laps), d.rules.laps) : Loc.Format("RUNDE {0}", Mathf.Max(1, p.entry.timer.LapNumber)))
                + " · " + Loc.Format("P{0} von {1}", p.entry.position, d.entries.Count);
            pauseView.ReducedMotion = Settings.reducedMotion;
            pauseView.Show(session, status, race, hudView != null);
        }

        void OnPauseAction(PauseAction a)
        {
            swallowClickFrame = Time.frameCount;
            navConfirm = false;
            switch (a)
            {
                case PauseAction.Resume: sm.Resume(); break;
                case PauseAction.ResetCar: sm.Resume(); Race.ResetPlayerToTrack(); break;
                case PauseAction.Restart:
                {
                    var k = kind; var t = currentType;
                    sm.TryTransition(GameState.Loading);
                    EndSession();
                    kind = k; currentType = t;
                    StartCoroutine(LoadSession(null));
                    break;
                }
                case PauseAction.Settings: settingsOpen = true; focus = 0; break;
                case PauseAction.HudEditor: hudView?.OpenEditor(); break;
                case PauseAction.Help: helpOpen = true; break;
                case PauseAction.Leave: sm.TryTransition(GameState.MainMenu); break;
            }
        }

        // --- Results --------------------------------------------------------------------------------------------

        void ShowResultsView()
        {
            var res = lastResults;
            if (resultsView == null || res == null) return;
            bool race = res.type == SessionType.Race, quali = res.type == SessionType.Qualifying;
            bool finished = res.playerFinished;
            var (title, sub) = ResultsModel.Headline(race, quali, res.playerPosition, res.rows.Count, !race || finished);
            var d = new ResultsView.Data
            {
                session = $"{res.trackName} · " + Loc.T(race ? "Rennen" : quali ? "Qualifying" : "Training"),
                title = title, sub = sub, win = race && finished && res.playerPosition == 1,
                race = race, playerRow = res.playerRow,
                xp = $"+{res.xpGained} XP",
                levelUp = res.levelAfter > res.levelBefore ? Loc.Format("LEVEL {0} ERREICHT · NEU: {1}", res.levelAfter, Progression.Rewards[res.levelAfter - 1].name) : "",
                actions = ResultsModel.Actions(quali && res.kind == SessionKind.GrandPrix, res.kind == SessionKind.Championship && Career.ChampionshipActive),
            };
            d.rows.AddRange(res.rows);
            d.teams.AddRange(res.teams);
            bool weekend = res.kind == SessionKind.GrandPrix && (quali || (race && Config.format == WeekendFormat.QualifyingAndRace));
            d.weekend = ResultsModel.Weekend(weekend, race);
            if (res.championshipLines != null)
            {
                d.standingsTitle = Loc.T(res.championshipFinished ? "MEISTERSCHAFT – ENDSTAND" : "MEISTERSCHAFT – ZWISCHENSTAND");
                d.standings = ChampionshipModel.Standings(Career.points, 10, Config.playerTeam);
            }
            var podium = FindPodium();
            d.podium = !Settings.reducedMotion && ResultsModel.ShowPodium(race, finished, res.playerPosition, podium != null);
            if (d.podium)
            {
                // Podium shot: in front of the podium on the pit building, looking at the trophy.
                raceCam.Hold(podium.TransformPoint(new Vector3(0f, 3.4f, -12f)), podium.TransformPoint(new Vector3(0f, 2.2f, 0f)));
            }
            resultsView.ReducedMotion = Settings.reducedMotion;
            resultsView.Show(d);
        }

        Transform FindPodium()
        {
            if (world == null) return null;
            foreach (var t in world.GetComponentsInChildren<Transform>(true)) if (t.name == "Podium") return t;
            return null;
        }

        void OnResultAction(ResultAction a)
        {
            swallowClickFrame = Time.frameCount;
            switch (a)
            {
                case ResultAction.ContinueToRace: StartSession(SessionKind.GrandPrix, SessionType.Race, pendingRaceGrid); break;
                case ResultAction.NextRound: StartSession(SessionKind.Championship, SessionType.Race); break;
                default: sm.TryTransition(GameState.MainMenu); break;
            }
        }

        // --- Per frame and state changes ------------------------------------------------------------------------

        /// <summary>Settings overlay (any state), fade and music; marks Esc as used when an overlay took it.</summary>
        void UpdateScreenViews()
        {
            backConsumed = false;
            float dt = Time.unscaledDeltaTime;
            screenFade?.Tick(dt);
            if (menuMusic != null)
            {
                bool menus = sm.Current == GameState.MainMenu || sm.Current == GameState.TrackSelect || sm.Current == GameState.Garage;
                menuMusic.SetPlaying(menus && Settings.musicVolume > 0f);
                menuMusic.Tick(dt);
            }
            if (settingsView == null) return;
            if (settingsOpen && !settingsView.Visible) ShowSettingsView();
            else if (!settingsOpen && settingsView.Visible) settingsView.Hide();
            if (settingsView.Visible && !helpOpen)
            {
                settingsView.Tick(dt, navUp, navDown, navLeft, navRight, navConfirm);
                if (input.PausePressed) { settingsView.Back(); backConsumed = true; }
                navUp = navDown = navLeft = navRight = navConfirm = false;   // the screen below must not react
            }
            // Pause screen: hidden while settings, help or the HUD editor are open.
            if (pauseView != null)
            {
                bool show = sm.Current == GameState.Pause && !settingsOpen && !helpOpen && !(hudView != null && hudView.Editing);
                if (show && !pauseView.Visible) ShowPauseView();
                else if (!show && pauseView.Visible) pauseView.Hide();
                if (pauseView.Visible)
                {
                    pauseView.Tick(dt, navUp, navDown, navConfirm);
                    if (input.PausePressed && !backConsumed) { pauseView.Back(); backConsumed = true; }
                }
            }
            if (resultsView != null && resultsView.Visible)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                resultsView.Tick(dt, navLeft || navUp, navRight || navDown, navConfirm, kb != null && kb.anyKey.wasPressedThisFrame);
            }
            if (garageView != null && garageView.Visible && !helpOpen)
            {
                garageView.Tick(dt, navUp, navDown, navLeft, navRight, navConfirm);
                if (input.PausePressed && !backConsumed) { garageView.Back(); backConsumed = true; }
            }
        }

        void OnScreenStateChanged(GameState from, GameState to)
        {
            // Fade on screen changes (not for pause/resume or within a running session).
            bool sessionInternal = (from == GameState.Pause || to == GameState.Pause) || (from == GameState.Grid && to == GameState.Driving)
                || to == GameState.RadioOverlay || from == GameState.RadioOverlay || to == GameState.PitApproach || to == GameState.PitStop
                || from == GameState.PitApproach || from == GameState.PitStop;
            if (!sessionInternal) screenFade?.Flash(Settings.reducedMotion);
            if (!InRaceState) ApplyUiSettings();   // e.g. a language change made in the pause menu reaches the HUD here
            if (to != GameState.Results) { resultsView?.Hide(); raceCam.ClearHold(); }
            else ShowResultsView();
            if (to != GameState.Garage) garageView?.Hide();
            else if (garageView != null) ShowGarageView();
            if (to != GameState.Pause) pauseView?.Hide();
        }

        /// <summary>F2 layout check: the UI Toolkit HUD elements in IMGUI coordinates.</summary>
        void AddHudZones()
        {
            if (hudView == null || !layoutDebug) return;
            float k = 1f / Mathf.Max(0.01f, Settings.hudScale);
            foreach (var (name, r) in hudView.VisibleRects())
                Zone(name, new Rect(r.x * k, r.y * k, r.w * k, r.h * k));
        }
    }
}
