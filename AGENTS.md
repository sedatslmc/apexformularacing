# Entwicklerleitfaden (für Menschen und Coding-Agenten)

## Kommunikation
- Mit dem Product Owner auf **Deutsch** kommunizieren. Code, Bezeichner und Code-Kommentare auf Englisch.
- Status ehrlich angeben: **geplant / implementiert / getestet / poliert**. Kompiliert ≠ fertig.
- Vor irreversiblen oder rechtlich sensiblen Entscheidungen fragen (Engine-Wechsel, Löschen, Lizenzen).

## Architektur in einem Satz
`Apex.Simulation` (reines C#, `noEngineReferences`) enthält Physik, Zeitnahme, KI-Linie, Eingabelogik,
Zustandsautomat und Einstellungen. `Apex.Runtime` verbindet sie mit Unity (Rigidbody, Raycasts, Rendering,
Input System, UI). Details: `docs/TECHNICAL_DESIGN.md`.

## Regeln
- Neue Spiellogik möglichst in `Apex.Simulation` schreiben und mit NUnit testen (`Tests/EditMode`).
- Physik nur in `FixedUpdate` (120 Hz). Raddrehung wird in Substeps gerechnet. Keine framerate-abhängige Logik.
- Koordinaten: x = rechts/Osten, y = oben, z = vorne/Norden (Unity, linkshändig). Positive Lenkung = rechts.
  Positive Krümmung = Rechtskurve.
- Keine Fake-UI: Jeder Button führt zu einer echten Funktion. Nicht umgesetzte Modi werden nicht angezeigt.
- Unity-6-Umbenennungen über `RigidbodyCompat` kapseln (`linearVelocity`, `PhysicsMaterial` …).
- Keine F1-Marken, echten Teams, Fahrer, Sponsoren oder Lackierungen. Keine Google-Maps/Earth-Daten.
  Jedes externe Asset kommt in `docs/ASSET_LICENSES.md`.

## Prüfen
```sh
tools/verify.sh          # holt Referenzen, baut Apex.Simulation, führt alle Tests aus, kompiliert Runtime + Editor
```
- Tests einzeln: `cd build/Apex.Simulation.Tests && dotnet test`
- Fahrdynamik-Diagnose: `cd build/Apex.Tuning && dotnet run -c Release -- launch|brake|aero|validate|lap <track_id> [trace]`
  (`validate` erzeugt den Bericht für `docs/HANDLING_VALIDATION.md`).
- Fahrzeuggeometrie ohne Unity: `cd build/Apex.ModelPreview && dotnet run -c Release -- <out_dir>`, dann
  `python3 tools/render_car_preview.py <out_dir>/car_lod0.obj <bild.png> 0,0.47,0.02` (mit Cockpit-Ansicht).
  Unity-Konvention: Vorderseite, wenn `cross(b−a, c−a)` zum Betrachter zeigt.
- PBR-Inspektionsrenders (three.js, Headless-Chromium): `tools/carrender/README.md`.
- Prozedurale Fahrzeuggeometrie ist Platzhalter-Art und wird nicht als fotorealistisch bezeichnet.
- In Unity: Window → General → Test Runner → EditMode (das Paket steht in `testables`).
- Die Kompiliertests laufen gegen UnityEngine-2021.3- und UnityEditor-2021.1-Referenzen. Code hinter
  `#if UNITY_6000_0_OR_NEWER` prüft erst der Unity-6-Editor.

## Strecken
- Daten: `unity/com.projectapex.game/Runtime/Resources/Tracks/*.json` (`[Ost, Nord, Höhe, Breite]` in Metern).
- Bosporus-Layout wird erzeugt: `python3 tools/build_bosphorus_layout.py`. Bericht und Schema:
  `python3 tools/circuit_report.py <json> docs/img/<name>.png`.
- Die Tests verlangen: gegen den Uhrzeigersinn, Länge 4,8–5,4 km, 19–21 Kurven, Höhenunterschied 35–55 m,
  Mindestabstand zwischen Abschnitten > 60 m.

## Abnahmekriterien M1
Siehe `docs/TEST_PLAN.md`, Abschnitt „M1-Abnahme“.
