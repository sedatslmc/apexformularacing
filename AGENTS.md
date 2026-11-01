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
tools/verify.sh          # holt Referenzen, baut Apex.Simulation, führt alle Tests aus, kompiliert Tests (NUnit 3.5), Runtime + Editor
```
- Tests einzeln: `cd build/Apex.Simulation.Tests && dotnet test`
- Fahrdynamik-Diagnose: `cd build/Apex.Tuning && dotnet run -c Release -- launch|brake|aero|validate|lap <track_id> [trace]`
  (`validate` erzeugt den Bericht für `docs/HANDLING_VALIDATION.md`).
- Fahrbahn/Kollision ohne Unity (`docs/BOSPHORUS_ROAD_FIX.md`): `cd build/Apex.Tuning && dotnet run -c Release -- surface|validate-road|limits|trace|clearance-buildings <track_id> …`
  (`clearance-buildings` listet die OSM-Gebäude, die die Strecke verdrängt: vor jeder Linienänderung prüfen).
  Ufer: `coast-audit` (Uferhöhen) und `landmark-ground` (Gelände unter Wahrzeichen); Cockpit-Sicht: `sight` (`DriverSight`); goldene Stunde: `golden` (`GoldenHour`); Ufermodell `CoastalProfile`, Stand `docs/ISTANBUL_OVERHAUL.md`.
  Fahrfläche nur über `RoadSurface` (Apex.Simulation) erzeugen: Unity-Mesh, MeshCollider, Prüfstand (`SurfaceRig`) und
  Validierung nutzen dieselben Dreiecke. Fahrzeug-Collider nur über `BodyCollider`.
- Fahrzeuggeometrie ohne Unity: `cd build/Apex.ModelPreview && dotnet run -c Release -- <out_dir>`, dann
  `python3 tools/render_car_preview.py <out_dir>/car_lod0.obj <bild.png> 0,0.47,0.02` (mit Cockpit-Ansicht).
  Unity-Konvention: Vorderseite, wenn `cross(b−a, c−a)` zum Betrachter zeigt.
- Leistungsmessung im Spiel: F6 bzw. Telemetrie-Knopf (`PerfCapture`, Bericht unter `persistentDataPath/perf`).
- PBR-Inspektionsrenders (three.js, Headless-Chromium): `tools/carrender/README.md`.
- UI-Mockups (HTML/CSS, Headless-Chromium): `OUT_DIR=docs/img node tools/uimock/shot.mjs tools/uimock/<seite>.html`. Neue UI = UI Toolkit im Code (`Runtime/Game/UI`), Modelle/Tokens in `Apex.Simulation` (`Apex.UI`); Plan und Prüfstatus: `docs/UI_UX_OVERHAUL.md`.
- Prozedurale Fahrzeuggeometrie ist Platzhalter-Art und wird nicht als fotorealistisch bezeichnet.
- In Unity: Window → General → Test Runner → EditMode (das Paket steht in `testables`).
- Die Kompiliertests laufen gegen UnityEngine-2021.3- und UnityEditor-2021.1-Referenzen. Code hinter
  `#if UNITY_6000_0_OR_NEWER` prüft erst der Unity-6-Editor.

## Strecken
- Daten: `unity/com.projectapex.game/Runtime/Resources/Tracks/*.json` (`[Ost, Nord, Höhe, Breite]` in Metern).
- **Bosphorus Grand Prix = `bosphorus_a.json`** (Variante A, reale Straßen): `tools/geo/fetch_dem.sh`, OSM-Auszüge nach
  `data/geo/osm/` (siehe `data/geo/README.md`), dann `osm_import.py` → `route_variants.py` → `track_analysis.py` →
  `build_track_a.py` (alle in `tools/geo/`). Plan/Status: `docs/BOSPHORUS_GEO_TRACK_PLAN.md`, `docs/BOSPHORUS_GP_UPGRADE.md`.
- Alter schematischer Kurs (`bosphorus_gp.json`, Archiv) wird erzeugt: `python3 tools/build_bosphorus_layout.py`. Bericht und Schema:
  `python3 tools/circuit_report.py <json> docs/img/<name>.png`.
- Tests für `bosphorus_a` (Ortaköy R20, `docs/BOSPHORUS_WATERFRONT.md` Abschnitt 5): gegen den Uhrzeigersinn, 8,0–8,3 km, 16 freigegebene Kurven,
  Steigung < 12,5 %/50 m; die alte Strecke liegt als `bosphorus_a_classic` bei (7,5–7,8 km, 13 Kurven; `build_track_a.py --layout classic`),
  Barrieren ohne Überschneidung, Fahrtests A–G auf der echten Collider-Fläche (`RoadSurfaceDriveTests`).
- Höhenprofil nie direkt aus dem DEM: `build_track_a.py` entwirft es mit `vertical_alignment.py` gegen die Fahrzeuggrenzen
  (`data/geo/bosphorus_a_limits.csv` aus `dotnet run -- limits bosphorus_a <csv>`; nach Fahrzeugänderungen neu erzeugen). Archivkurs: 4,8–5,4 km, 19–21 Kurven, Abschnitte > 60 m auseinander.

## Abnahmekriterien M1
Siehe `docs/TEST_PLAN.md`, Abschnitt „M1-Abnahme“.
