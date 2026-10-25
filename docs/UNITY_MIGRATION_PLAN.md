# Project Apex: Repository-Audit und Unity-Migrationsplan

Stand: 2026-10-25 · Status: **freigegeben und umgesetzt** (M0/M1 implementiert; Abnahme im Unity-Editor ausstehend) · Sprache: Deutsch

## 1. Produktarchitektur in Kürze

Project Apex ist ein originales, mobiles Formel-Simcade-Rennspiel im Querformat. Android hat Vorrang, iOS folgt über eine macOS/Xcode-Pipeline, Windows dient zum Entwickeln und Testen mit Tastatur. Der Kern besteht aus fünf Schichten. Simulation und Darstellung bleiben strikt getrennt.

| Schicht | Inhalt | Kernservices (laut Brief) |
|---|---|---|
| **Simulation** (deterministisch, FixedUpdate, ohne Engine-Abhängigkeit, wo möglich) | Reifen, Federung, Aero, Antrieb, Differenzial, Assistenzsysteme, Oberflächen, Wetter-Grip | `VehicleController`, `TireSimulation`, `SuspensionModel`, `AeroModel`, `TrackSurfaceManager` |
| **Rennlogik** | Zeitnahme, Sektoren, Tracklimits, Flaggen, Strafen, Boxenstopps, Rennwochenende, KI | `RaceTimingService`, `RaceDirector`, `PitStopManager`, `AIManager`, `CircuitManager`, `WeatherManager` |
| **Eingabe und Zustand** | ein zentraler Zustandsautomat (Boot … Replay), Finger-Ownership pro Pointer-ID, Gyro-Kalibrierung, Remapping | `GameStateController`, `InputRouter` |
| **Präsentation** | URP-Rendering, Kameras, HUD im TV-Broadcast-Stil, Audio, Replays | `HUDLayoutManager`, `AudioDirector`, `GraphicsQualityManager`, `TeamRadioController` |
| **Meta** | Speicherstände (versioniert), Garage, Karriere, Level 1–100, Einstellungen | `SaveService` |

Leitprinzipien: Fahrgefühl zuerst, keine Fake-UI, kein Pay-to-win, ehrliche Performance-Messung auf echten Geräten, nur originale oder lizenzierte Assets. Die Istanbul-Strecke ist fiktiv und als solche gekennzeichnet.

## 2. Audit des aktuellen Repositorys

**Engine: Godot 4.7.2 (GDScript), kein Unity-Projekt vorhanden.** Der vorherige Container wurde zurückgesetzt. Dieses Repository ist ein Neuaufbau aus dieser Sitzung. Es hat noch **kein Remote**: Alles existiert nur in diesem Cloud-Container und ginge beim Zurücksetzen verloren.

Gesichert als Commit `68e7fc7` und Branch `godot-prototype`.

| Bereich | Stand | Nachweis |
|---|---|---|
| Reifenmodell (kombinierter Schlupf, Lastempfindlichkeit, Reibkreis) | implementiert, getestet | 6 Unit-Tests grün |
| Motor, 8-Gang-Getriebe, Automatik ohne Gangpendeln | implementiert, getestet | 4 Unit-Tests grün |
| Rundenzeit/Sektoren (Interpolation unterhalb eines Ticks, Reihenfolgeprüfung, ungültige Runden, Live-Delta) | implementiert, getestet | 5 Unit-Tests grün |
| Spline-Streckengeometrie, Projektion, Abstandsprüfung | implementiert, getestet | 4 Unit-Tests grün |
| Raycast-Fahrzeug (Federung, ARB, LSD, Aero, TC mit kombiniertem Schlupf, ABS, Lenkhilfe) | implementiert, teilweise getestet | Ruhelage-Test grün. 0–100 km/h ca. 2,4 s und ca. 295 km/h nach 12 s laut Diagnoseprotokoll. Die vollständige Runden- und Beschleunigungs-Testsuite wurde nach dem letzten Fix **nicht mehr komplett ausgeführt** (unterbrochen). |
| Prozedurale Strecke „Kestrel Ridge“, HUD, Touch-Tasten, Pausemenü, prozeduraler Motorsound | implementiert, nur headless geprüft | keine visuelle Prüfung, kein Gerätetest |

**In Unity wiederverwendbar** (Portierung nach C#, kein Kopieren von Engine-Code):

- Physik-Mathematik: Reifenkurve, Motor- und Getriebemodell, LSD-Logik, Aero-Formeln, TC/ABS-Logik, Federungsparameter als Startwerte
- `LapTimer`-Logik und die zugehörigen Testfälle als Spezifikation
- Streckendaten im JSON-Format (`data/tracks/*.json`) und Catmull-Rom-Sampling; die Python-Werkzeuge in `tools/` sind engine-unabhängig
- Autopilot als Keim für die KI (Pure Pursuit plus Geschwindigkeitsprofil)

**Nicht wiederverwendbar:** Szenen (`.tscn`), GDScript-Knoten, Godot-Materialien und Godot-UI.

**Lizenzlage:** Alles ist prozedural erzeugt oder selbst geschrieben. Es gibt keine Fremd-Assets, also auch keine offenen Lizenzfragen.

## 3. Wichtige Einschränkung dieser Arbeitsumgebung

In diesem Cloud-Container sind **weder Unity noch .NET installiert**. Ein Unity-Editor lässt sich hier nicht sinnvoll betreiben: Er braucht Lizenzaktivierung, eine GPU und Desktop-Pakete. Folgen:

1. Unity-spezifischen Code (MonoBehaviours, URP, Input System, UI) kann ich schreiben, aber **hier nicht kompilieren oder starten**. Den Nachweis musst du auf deinem Windows-Laptop im Unity-Editor erbringen. Ich liefere dafür genaue Schritte.
2. Deshalb schlage ich vor, die **Simulation als reine C#-Bibliothek ohne Engine-Referenz** zu bauen: `Apex.Simulation`, in Unity als asmdef mit `noEngineReferences`. Diese Bibliothek kann ich hier mit dem .NET SDK (Installation nötig) **kompilieren und mit Unit-Tests prüfen**. In Unity laufen dieselben Tests im Test Runner (EditMode).
3. Kein Meilenstein gilt als erledigt, nur weil Code kompiliert.

## 4. Migrationsplan (zerstört nichts)

1. **Godot-Prototyp bleibt erhalten**: Branch `godot-prototype`, später unter `legacy/godot/`. Er dient als Referenz und als Physik-Vergleich.
2. **Neues Unity-Projekt** im Ordner `unity/ProjectApex/` im selben Repository, Branch `unity-migration`:
   - Unity 6 LTS (genaue Version im Unity Hub prüfen und in `ProjectSettings/ProjectVersion.txt` festhalten), URP-Vorlage „Universal 3D“
   - Input System Package (Touch, Gyro/`AttitudeSensor`, Tastatur, Gamepad), Test Framework
   - Android: IL2CPP, ARM64, ASTC, Querformat links/rechts; iOS erst mit Mac-Zugang
   - `.gitignore` für Unity (Library/, Temp/, Logs/, UserSettings/) und Git LFS für Binärdateien
3. **Ordnerstruktur** laut Brief: `Assets/ProjectApex/{Core,Input,Vehicle,Physics,Race,AI,Tracks,…,Tests}` mit asmdefs `Apex.Simulation` (ohne Engine), `Apex.Runtime`, `Apex.Editor`, `Apex.Tests.EditMode` und `Apex.Tests.PlayMode`.
4. **Port-Reihenfolge**: Reifen → Antrieb → Fahrzeugintegration (Raycast-Federung in `FixedUpdate`, 120 Hz plus Substeps für die Raddrehung) → Zeitnahme → Spline-Strecke → Kamera und Telemetrie → Eingabe (Tasten, Touch, Gyro).
5. **Streckendaten**: Das JSON wird per Editor-Importer zum `CircuitDefinition`-ScriptableObject, damit Python-Werkzeuge und Unity dieselbe Quelle nutzen.

## 5. Vorschlag M0 + M1 (erster Liefergegenstand nach Freigabe)

**M0 (Fundament):** Unity-Projektgerüst als Textdateien (Projekteinstellungen, Packages-Manifest, asmdefs, Ordner), Zustandsautomat `GameStateController`, Hauptmenü-Shell ohne tote Buttons (nur echte Ziele), `SaveService` mit Versionsnummer, Einrichtungsanleitung für Windows.

**M1 (Fahr-Vertical-Slice):** ein originales Auto auf einer flachen Teststrecke. Tastatur, Touch-Tasten und Gyro mit Kalibrierung. Kamera, einfache Telemetrie. `Apex.Simulation` mit Unit-Tests, die hier geprüft werden. Abnahme in deinem Unity-Editor: keine hängenden Eingaben, kein Ruckeln, Lenken, Bremsen und Beschleunigen sind vorhersehbar.

## 6. Umsetzung (Stand 2026-10-25)

- Freigabe für Unity, .NET und die Fahrtrichtung gegen den Uhrzeigersinn: erteilt.
- Umgesetzt im Branch `unity-migration`:
  - Godot nach `legacy/godot` verschoben
  - Paket `unity/com.projectapex.game` mit `Apex.Simulation`, `Apex.Runtime`, `Apex.Editor` und Tests
  - Prüf-Pipeline `tools/verify.sh`
- Abweichung vom Plan: Das Unity-Projekt liegt nicht als fertige Projektdateien im Repository. Du legst es per Hub-Vorlage an (`UNITY_SETUP.md`), weil handgeschriebene ProjectSettings fehleranfällig wären und die Vorlage die URP-Paketversionen richtig setzt.
- Offen: GitHub-Repository verbinden, damit die Arbeit nicht mit dem Cloud-Container verloren geht.
