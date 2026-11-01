using System;
using System.Collections.Generic;

namespace Apex.UI
{
    public enum Language { Deutsch = 0, English = 1 }

    /// <summary>
    /// UI language of the APEX // RACE CONTROL screens (German source, English translation). Keys are the German
    /// texts, so a missing translation shows German instead of a key. Scope: the UI Toolkit screens and their models;
    /// track descriptions, radio replies and the interim IMGUI screens stay German (docs/UI_UX_OVERHAUL.md).
    /// </summary>
    public static class Loc
    {
        public static Language Current = Language.Deutsch;

        /// <summary>German texts requested in English without a translation (for the completeness test).</summary>
        public static readonly HashSet<string> Missing = new HashSet<string>();

        public static string T(string german)
        {
            if (Current == Language.Deutsch || string.IsNullOrEmpty(german)) return german;
            if (En.TryGetValue(german, out var en)) return en;
            Missing.Add(german);
            return german;
        }

        /// <summary>Translated format string ({0}, {1} …).</summary>
        public static string Format(string germanFormat, params object[] args) => string.Format(T(germanFormat), args);

        public static string Name(Language l) => l == Language.English ? "English" : "Deutsch";

        static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            // Main menu.
            ["Rennen starten"] = "Start race", ["Meisterschaft fortsetzen"] = "Continue championship", ["Grand Prix"] = "Grand Prix",
            ["Qualifying + Rennen"] = "Qualifying + race", ["Meisterschaft"] = "Championship", ["Saison starten"] = "Start a season",
            ["Zeitfahren"] = "Time trial", ["Noch keine Bestzeit"] = "No best time yet", ["Bestzeit {0}"] = "Best {0}",
            ["Freies Training"] = "Free practice", ["Garage"] = "Garage", ["Lackierung · Team · Setup"] = "Livery · team · setup",
            ["Fahrerprofil"] = "Driver profile", ["Statistiken"] = "Statistics", ["Beenden"] = "Quit",
            ["Runde"] = "lap", ["Runden"] = "laps", ["Runde {0} / {1} · {2}"] = "Round {0} / {1} · {2}",
            ["Hauptmenü"] = "Main menu", ["Ausgewählte Strecke"] = "Selected track", ["Letztes Ergebnis"] = "Last result",
            ["↑↓  Auswählen     Enter  Bestätigen     Esc  Zurück"] = "↑↓  Select     Enter  Confirm     Esc  Back",
            ["Rennen"] = "Race", ["Rennstarts"] = "Races", ["Siege"] = "Wins", ["Podien"] = "Podiums", ["Poles"] = "Poles", ["Level"] = "Level", ["XP"] = "XP",
            ["Ø Platz"] = "Avg. finish", ["Distanz"] = "Distance", ["Bestzeiten"] = "Best laps", ["Noch keine gewerteten Runden."] = "No timed laps yet.",
            ["Esc  Zurück"] = "Esc  Back", ["‹  Zurück"] = "‹  Back", ["ISTANBUL WIRD GELADEN"] = "LOADING ISTANBUL",
            ["WINDSCHATTEN"] = "SLIPSTREAM", ["Morgen"] = "Morning", ["Mittag"] = "Midday", ["Sonnenuntergang"] = "Sunset", ["Nacht"] = "Night", ["Goldene Stunde"] = "Golden hour",
            // Setup.
            ["Strecke & Bedingungen"] = "Track & conditions", ["Rennformat"] = "Race format", ["Feld"] = "Field", ["Regeln"] = "Rules",
            ["Runden pro Rennen"] = "Laps per race", ["KI-Gegner"] = "AI opponents", ["KI-Stärke"] = "AI strength", ["Team"] = "Team",
            ["Strecke"] = "Track", ["Tageszeit"] = "Time of day", ["Wetter"] = "Weather", ["Wetterwechsel"] = "Weather changes",
            ["Format"] = "Format", ["Nur Rennen"] = "Race only", ["Qualifying-Dauer"] = "Qualifying length", ["Startreifen"] = "Starting tyres",
            ["Reifenverschleiß"] = "Tyre wear", ["Frühstart-Strafe"] = "Jump-start penalty", ["Tracklimit-Strafen"] = "Track-limit penalties",
            ["Pflicht-Boxenstopp"] = "Mandatory pit stop", ["Safety Car / VSC"] = "Safety car / VSC", ["Schäden"] = "Damage",
            ["Technische Defekte (KI)"] = "Technical failures (AI)", ["Selten"] = "Rare", ["Häufig"] = "Frequent", ["An"] = "On", ["Aus"] = "Off",
            ["Qualifying {0} min + {1}"] = "Qualifying {0} min + {1}", ["Rennen {0} {1}"] = "Race {0} {1}", ["ohne Gegner"] = "no opponents",
            ["{0} KI-Gegner"] = "{0} AI opponents", ["Allein auf der Strecke · Bestzeit und Sektoren"] = "Alone on track · best lap and sectors",
            ["Freie Fahrt ohne Zeitlimit"] = "Free running without time limit", ["{0} Rennen · {1} {2} · {3} KI-Gegner"] = "{0} races · {1} {2} · {3} AI opponents",
            [", +1 für die schnellste Runde (Top 10)"] = ", +1 for the fastest lap (top 10)", ["Rennvorbereitung"] = "Race preparation",
            ["Saison · Apex Series"] = "Season · Apex Series", ["Nächste Runde"] = "Next round", ["Aufgeben"] = "Abandon", ["Start"] = "Start",
            ["Zurück"] = "Back", ["WIRKLICH AUFGEBEN?"] = "REALLY ABANDON?", ["Kalender"] = "Calendar", ["GEFAHREN"] = "COMPLETED",
            ["NÄCHSTE RUNDE"] = "NEXT ROUND", ["AUSSTEHEND"] = "UPCOMING", ["Punkte "] = "Points ", ["Länge"] = "Length", ["Kurven"] = "Corners",
            ["Richtung"] = "Direction", ["Uhrzeigersinn"] = "Clockwise", ["Gegen Uhrzeiger"] = "Anti-clockwise", ["Höhenunterschied"] = "Elevation change",
            ["Deine Bestzeit"] = "Your best", ["im Uhrzeigersinn"] = "clockwise", ["gegen den Uhrzeigersinn"] = "anti-clockwise",
            ["Fahrerwertung"] = "Drivers' standings", ["{0} RENNEN"] = "{0} RACES", ["Runde {0}"] = "Round {0}", ["Du"] = "You",
            ["↑↓  Auswählen     ←→  Ändern     Enter  Bestätigen     Esc  Zurück"] = "↑↓  Select     ←→  Change     Enter  Confirm     Esc  Back",
            // HUD.
            ["Positionsturm"] = "Timing tower", ["Rennstatus"] = "Race status", ["Ereignisse"] = "Events", ["Cockpit-Anzeige"] = "Dash",
            ["Reifen"] = "Tyres", ["Streckenkarte"] = "Track map", ["Zeiten"] = "Timing", ["Minimal"] = "Minimal", ["Standard"] = "Standard",
            ["Erweitert"] = "Extended", ["{0} verdeckt die Fahrbahn"] = "{0} covers the road", ["{0} überdeckt {1}"] = "{0} overlaps {1}",
            ["GELBE FLAGGE"] = "YELLOW FLAG", ["VIRTUELLES SAFETY CAR"] = "VIRTUAL SAFETY CAR", ["VSC ENDET"] = "VSC ENDING", ["SAFETY CAR"] = "SAFETY CAR",
            ["SAFETY CAR KOMMT REIN"] = "SAFETY CAR IN THIS LAP", ["Nicht überholen"] = "No overtaking", ["Tempo begrenzt · Abstand halten"] = "Speed limited · keep the gap",
            ["Gleich wieder Grün"] = "Green shortly", ["Hinter dem Safety Car einreihen"] = "Line up behind the safety car", ["Neustart an der Linie"] = "Restart at the line",
            ["GRÜNE FLAGGE"] = "GREEN FLAG", ["Rennen freigegeben"] = "Racing resumes", ["Platz"] = "place", ["Plätze"] = "places",
            ["LETZTE RUNDE"] = "FINAL LAP", ["P{0} von {1}"] = "P{0} of {1}", ["SIEG"] = "VICTORY", ["ZIEL · P{0}"] = "FINISH · P{0}", ["Podium"] = "Podium",
            ["von {0}"] = "of {0}", ["SCHNELLSTE RUNDE"] = "FASTEST LAP", ["NEUER REKORD"] = "NEW RECORD", ["PERSÖNLICHE BESTZEIT"] = "PERSONAL BEST",
            ["RUNDE {0}/{1}"] = "LAP {0}/{1}", ["RUNDE {0}"] = "LAP {0}", ["{0} AUTOS"] = "{0} CARS", ["START"] = "START", ["ZEITFAHREN"] = "TIME TRIAL",
            ["TRAINING"] = "PRACTICE", ["QUALIFYING"] = "QUALIFYING", ["ZIEL"] = "FINISH", ["Führend"] = "Leader", ["BOX"] = "PIT",
            ["Strecke {0}°C"] = "Track {0}°C", ["nass {0} %"] = "wet {0} %", ["GELB · NICHT ÜBERHOLEN"] = "YELLOW · NO OVERTAKING",
            ["VSC · TEMPO BEGRENZT"] = "VSC · SPEED LIMITED", ["SC IN DIESER RUNDE"] = "SC IN THIS LAP", ["Letzte"] = "Last", ["Beste"] = "Best",
            ["Rekord"] = "Record", ["ALTES FAHRVERHALTEN (VERGLEICH)"] = "OLD HANDLING (COMPARISON)", ["HUD anpassen"] = "Customise HUD",
            ["FAHRBAHN FREIHALTEN"] = "KEEP THE ROAD CLEAR", ["Größe {0} %   ·   Deckkraft {1} %   ·   Preset {2}"] = "Size {0} %   ·   Opacity {1} %   ·   Preset {2}",
            ["Hinweis: "] = "Note: ", ["EREIGNISSE"] = "EVENTS", ["Beispiel"] = "Example", ["TEAMFUNK"] = "TEAM RADIO",
            ["Tab / LB RB  Element   ·   Pfeile / Steuerkreuz  Verschieben (Shift schneller)   ·   Maus  Ziehen\n+ −  / Y X  Größe   ·   Bild↑ Bild↓  / RT LT  Deckkraft   ·   R  Zurücksetzen   ·   Shift+R  Alles\nEnter / Esc / B  Fertig"] =
                "Tab / LB RB  Element   ·   Arrows / d-pad  Move (Shift faster)   ·   Mouse  Drag\n+ −  / Y X  Size   ·   PgUp PgDn  / RT LT  Opacity   ·   R  Reset   ·   Shift+R  All\nEnter / Esc / B  Done",
            ["Box diese Runde"] = "Box this lap", ["Reifen wählen"] = "Choose tyres", ["Abstand"] = "Gaps", ["Strategie"] = "Strategy", ["Schaden"] = "Damage",
            ["Schließen"] = "Close", ["Reifen: {0}"] = "Tyres: {0}",
            // Pause, results.
            ["PAUSE"] = "PAUSED", ["Weiter"] = "Resume", ["Auto auf die Strecke setzen"] = "Reset car to track", ["Session neu starten"] = "Restart session",
            ["Einstellungen"] = "Settings", ["Steuerung & Hilfe"] = "Controls & help", ["Rennen aufgeben"] = "Retire from race", ["Zum Hauptmenü"] = "Main menu",
            ["WIRKLICH?  ERNEUT BESTÄTIGEN"] = "SURE?  CONFIRM AGAIN", ["NICHT IM ZIEL"] = "DID NOT FINISH", ["Rennen beendet"] = "Race over",
            ["PODIUM"] = "PODIUM", ["POLE POSITION"] = "POLE POSITION", ["Startplatz für das Rennen"] = "Grid position for the race",
            ["SESSION BEENDET"] = "SESSION OVER", ["Qualifying"] = "Qualifying", ["Startaufstellung"] = "Grid", ["Weiter zum Rennen"] = "Continue to the race",
            ["Nächste Meisterschaftsrunde"] = "Next championship round", ["Ergebnis"] = "Result", ["Pos"] = "Pos", ["Fahrer"] = "Driver",
            ["Zeit / Abstand"] = "Time / gap", ["Beste Runde"] = "Best lap", ["Stopps"] = "Stops", ["Strafe"] = "Penalty", ["Pkt."] = "Pts",
            ["Rennwochenende"] = "Race weekend", ["MEISTERSCHAFT – ZWISCHENSTAND"] = "CHAMPIONSHIP – STANDINGS", ["MEISTERSCHAFT – ENDSTAND"] = "CHAMPIONSHIP – FINAL STANDINGS",
            ["LEVEL {0} ERREICHT · NEU: {1}"] = "LEVEL {0} REACHED · NEW: {1}", ["Training"] = "Practice",
            // Settings, garage.
            ["Grafik"] = "Graphics", ["Steuerung"] = "Controls", ["Fahrverhalten"] = "Driving", ["Audio"] = "Audio", ["HUD"] = "HUD",
            ["Barrierefreiheit"] = "Accessibility", ["Qualitätsstufe, Umgebung, Effekte"] = "Quality tier, environment, effects",
            ["Eingabegerät, Neigung, Touch-Tasten"] = "Input device, tilt, touch buttons", ["Fahrhilfen und Fahrprofil"] = "Assists and driving profile",
            ["Lautstärke von Spiel und Musik"] = "Game and music volume", ["Preset, Größe, Einheiten"] = "Preset, size, units",
            ["Bewegung, Text, Kontrast, Farben, Sprache"] = "Motion, text, contrast, colours, language",
            ["Front"] = "Front", ["Seite"] = "Side", ["Heck"] = "Rear", ["Oben"] = "Top", ["Lackierung"] = "Livery", ["Startnummer"] = "Car number",
            ["Setup"] = "Setup", ["Fortschritt"] = "Progress", ["Nur Optik – Leistung ist in allen Modi gleich."] = "Looks only – performance is the same in every mode.",
            ["Kamera"] = "Camera", ["Ziehen  Drehen     Mausrad  Zoom"] = "Drag  Rotate     Wheel  Zoom",
            ["Standardsicht"] = "Standard vision", ["Deuteranopie"] = "Deuteranopia", ["Protanopie"] = "Protanopia", ["Tritanopie"] = "Tritanopia",
            // Settings rows (labels and values of the existing settings).
            ["Fahrprofil"] = "Driving profile", ["Traktionskontrolle"] = "Traction control", ["ABS"] = "ABS", ["Lenkhilfe"] = "Steering assist",
            ["Tempoabhängige Lenkung"] = "Speed-sensitive steering", ["Automatikgetriebe"] = "Automatic gearbox", ["Bremsbalance vorne"] = "Front brake bias",
            ["Fahrverhalten (Vergleich, ab nächster Session)"] = "Handling (comparison, next session)", ["Boxen-Assistent"] = "Pit assist",
            ["Neigung: max. Winkel"] = "Tilt: max. angle", ["Neigung: Empfindlichkeit"] = "Tilt: sensitivity", ["Neigung: Totzone"] = "Tilt: dead zone",
            ["Neigung: Glättung"] = "Tilt: smoothing", ["Neigung umkehren"] = "Invert tilt", ["Linkshänder-Layout"] = "Left-handed layout",
            ["Tastengröße"] = "Button size", ["Tasten-Deckkraft"] = "Button opacity", ["HUD-Größe"] = "HUD size", ["HUD-Preset (Taste H)"] = "HUD preset (H key)",
            ["Geschwindigkeit"] = "Speed", ["Reduzierte Bewegung"] = "Reduced motion", ["Grafikstufe"] = "Graphics tier",
            ["Menükulisse Istanbul (PC, ab nächstem Menü)"] = "Istanbul menu backdrop (PC, next menu)", ["Umgebungsdetails (ab nächster Session)"] = "Environment detail (next session)",
            ["Lautstärke"] = "Volume", ["Straßentexturen (ab nächster Session)"] = "Road textures (next session)", ["Nachbearbeitung (Farbe, Bloom)"] = "Post-processing (colour, bloom)",
            ["Bewegungsunschärfe"] = "Motion blur", ["Musik"] = "Music", ["Sprache"] = "Language", ["Farbsehen (HUD)"] = "Colour vision (HUD)",
            ["Hoher Kontrast"] = "High contrast", ["Textgröße (Menüs)"] = "Text size (menus)",
            ["Tastatur/Gamepad"] = "Keyboard/gamepad", ["Touch-Tasten"] = "Touch buttons", ["Neigung (Gyro)"] = "Tilt (gyro)", ["Niedrig"] = "Low", ["Mittel"] = "Medium",
            ["Hoch"] = "High", ["Extrem (PC)"] = "Extreme (PC)", ["Leicht"] = "Light", ["Voll"] = "Full", ["Aus (empfohlen mit analogen Pedalen)"] = "Off (recommended with analogue pedals)",
            ["Aus (Lenkrad/Gamepad)"] = "Off (wheel/gamepad)", ["Alt (vor Okt. 2026)"] = "Old (before Oct 2026)", ["Neu (Okt. 2026)"] = "New (Oct 2026)",
            ["– (Mobil)"] = "– (mobile)", ["– (ab Hoch)"] = "– (from High)", ["aus (reduzierte Bewegung)"] = "off (reduced motion)",
            ["Ultra (4K)"] = "Ultra (4K)", ["Standard (1K)"] = "Standard (1K)", ["Kein Bewegungssensor erkannt – Neigung fällt auf Touch-Tasten zurück."] = "No motion sensor found – tilt falls back to touch buttons.",
            ["Einsteiger"] = "Beginner", ["Ausgewogen"] = "Balanced", ["Profi"] = "Expert", ["Individuell"] = "Custom",
            ["Nächste Belohnung (Level {0}): {1}"] = "Next reward (level {0}): {1}", ["Level {0} · {1} von 20 Lackierungen freigeschaltet"] = "Level {0} · {1} of 20 liveries unlocked",
            ["Einstellungen gelten sofort und werden gespeichert."] = "Settings apply at once and are saved.",
        };
    }
}
