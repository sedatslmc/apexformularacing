# Technisches Design

Stand: M0/M1 · Engine: Unity 6 LTS, C# 9, URP · Ziel: Android (später iOS), Windows zum Entwickeln

## Schichten und Assemblies
| Assembly | Abhängigkeiten | Inhalt |
|---|---|---|
| `Apex.Simulation` | keine (`noEngineReferences`) | `Vehicle/` (Reifen, Motor, Getriebe, Fahrzeugsimulation, Prüfstand), `Track/` (Spline-Geometrie, Kreisdefinition), `Race/` (Zeitnahme), `AI/` (Autopilot), `InputLogic/` (Neigung, Digitalachse, Touch-Ownership), `Core/` (Zustandsautomat, Einstellungen, JSON, Mathe) |
| `Apex.Runtime` | Simulation, Input System | Fahrzeug-Controller (Rigidbody), Strecken- und Fahrzeuggenerator, Kamera, Eingabe-Router, Rennsession, Spiel-Controller mit IMGUI-Menüs/HUD (Übergangslösung), Audio, Speichern |
| `Apex.Editor` | Simulation, Runtime | Einrichtungsmenü „Project Apex“ |
| `Apex.Tests.EditMode` | Simulation, NUnit | 43 Tests; laufen auch per `dotnet test` |

Weil die Simulation nicht von der Engine abhängt, ist sie deterministisch testbar und lässt sich ohne Unity-Editor prüfen.
Die Unity-Schicht übersetzt nur zwischen Engine und Simulation.

## Ablauf eines Physik-Schritts (120 Hz)
1. `RaceSession.FixedUpdate` (Ausführungsreihenfolge −50): Fortschritt und Zeitnahme mit dem Zustand des letzten Schritts, Tracklimits. Danach setzt die Session die Eingaben (Spieler, Autopilot oder Haltebremse).
2. `VehicleController.FixedUpdate`:
   - `Sim.PreStep`: Getriebe und Lenkung
   - Raycast pro Rad: Einfederung, Kontaktnormale, Oberfläche, Kontaktgeschwindigkeit im gelenkten Radrahmen
   - `Sim.Step`: Lasten inkl. Stabilisatoren, Antriebsstrang mit LSD, TC/ABS, Raddrehung in 6 Substeps, Reifenkräfte, Aero
   - Kräfte anwenden: Federkraft am Federbein entlang der Fahrzeug-Hochachse, Reifenkraft am Kontaktpunkt, Abtrieb an den Achsen, Luftwiderstand am Schwerpunkt
3. Unity integriert den Rigidbody. Interpolation sorgt bei 60 fps für eine ruhige Darstellung; die Kamera folgt in `LateUpdate`.

## Zustandsautomat
`GameStateMachine` ist die einzige Instanz, die Spielzustände festlegt (Boot, MainMenu, TrackSelect, CarSelect, Garage, Loading, Grid, Driving, RadioOverlay, Pause, PitApproach, PitStop, Results, Replay).
Erlaubte Übergänge und „Zurück“-Ziele sind als Tabellen hinterlegt und getestet.
`AcceptsDrivingInput` ist auch im RadioOverlay wahr: Der Funk blockiert das Fahren nie.

## Eingabe
- `InputRouter` führt Tastatur, Gamepad, Touch-Tasten und Neigung zusammen und liefert Gas, Bremse, Lenkung sowie Flanken-Aktionen.
- `TouchOwnership`: Ein Finger gehört ab dem Aufsetzen genau einem Bedienelement, bis er losgelassen wird. Nicht mehr gemeldete Finger werden freigegeben, damit nichts hängen bleibt.
- `TiltSteering`: Der Rollwinkel kommt aus dem Schwerkraftvektor im Bildschirmraum (Querformat links/rechts). Er wird relativ zur Kalibrierung gemessen, mit Totzone, Kennlinie, Glättung und Zuverlässigkeit (bei flach liegendem Gerät Rückkehr zur Mitte).
- Bei Fokusverlust oder App-Pause werden alle Eingaben gelöst, und die Session pausiert.

## Strecken-Pipeline
Die JSON-Kontrollpunkte werden per zentripetalem Catmull-Rom geschlossen und mit 2 m Abstand gleichmäßig abgetastet. Daraus entstehen:
- Tangenten, Rechtsvektoren und Krümmung
- Ribbon-Meshes (Asphalt + Randlinien, Randsteine rot/weiß, Gras-Auslauf, doppelseitige Barrieren, Schürze, Gelände, Wasser)
- MeshCollider mit `TrackSurface`-Tag

Zeitnahme und Autopilot verwenden dieselbe Geometrie.
Bekannte Grenze: Gelände und Meshes werden zur Laufzeit erzeugt (ca. 0,1–1 s je nach Gerät). Ab M2 sollen sie im Editor vorgebacken werden.

## Darstellung (Stand M1)
- Prozedurales Platzhalter-Auto und Graybox-Strecke mit URP-Lit-Materialien
- Prozeduraler Himmel, Trilight-Umgebung, linearer Nebel
- Das HUD nutzt vorerst IMGUI und wird in M4 durch ein layoutverwaltetes UI ersetzt. Gründe: Safe-Area, Kollisionsprüfung der Bereiche, weniger Allokationen pro Frame.

## Speichern
`SaveService` schreibt versioniertes JSON (`settings.json`) über eine Temp-Datei und hält eine `.bak` vor.
Ist eine Datei beschädigt, greift die Sicherung, danach gelten Standardwerte. `SettingsData.MigrateAndValidate` hebt alte Versionen an und begrenzt Werte auf gültige Bereiche.

## Performance-Leitplanken
- Physik: 4 Raycasts plus eine Simulation pro Auto und Schritt. 20 Autos bei 120 Hz entsprechen 9 600 Raycasts pro Sekunde, das ist unkritisch.
- Ziel: 60 fps auf Flaggschiff-Geräten, 30 fps als Rückfallstufe. Bisher **nicht gemessen**; erste Gerätemessung in M1/M3.
