# Asset- und Lizenzverzeichnis

Stand: Inhalte sind selbst erstellt oder prozedural erzeugt; Ausnahme sind die unten aufgeführten CC0-Texturen (Poly Haven). Poly Haven bittet um Nennung als Gefälligkeit: in die Credits aufnehmen.

| Asset | Herkunft | Lizenz | Hinweis |
|---|---|---|---|
| Fahrzeug „APEX A01“ (prozedural: `CarGeometry`, 3 LODs, ~55k/14k/5,5k Dreiecke), Carbon-Gewebe-Textur (prozedural) | selbst erstellt (Code) | Projekteigentum | originales, fiktives Design; Referenzbilder nur für Proportionen studiert, keine Geometrie, Marken oder Lackierungen übernommen; nicht fotorealistisch. Premium-Modell: siehe `VEHICLE_ASSET_SPEC.md` (noch nicht beauftragt) |
| three.js 0.160.0 (nur Entwicklungswerkzeug `tools/carrender`, nicht im Spiel) | npm | MIT | wird nicht eingecheckt (`npm install`) |
| Strecken Testoval, Bosphorus GP (Graybox) | selbst erstellt (Daten und Generator) | Projekteigentum | Streckenverlauf fiktiv; keine Google-Maps/Earth- oder OSM-Daten |
| Copernicus DEM GLO-30 (Kacheln N41E028, N41E029; `tools/geo/fetch_dem.sh`, nicht eingecheckt) | AWS Open Data `copernicus-dem-30m` | Copernicus WorldDEM-30, frei, Quellenangabe Pflicht | Hinweis in den Credits: „Produced using Copernicus WorldDEM-30 © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018 provided under COPERNICUS by the European Union and ESA; all rights reserved“ plus Haftungsausschluss laut Lizenz (Wortlaut vor Release aus der Lizenz-PDF übernehmen) |
| OpenStreetMap-Auszüge (geplant, `data/geo/osm/`) | OpenStreetMap | ODbL 1.0 | Credits: „© OpenStreetMap-Mitwirkende“. Abgeleitete Geodaten (`bosphorus_osm.json`, daraus erzeugte Streckendaten) bei Veröffentlichung unter ODbL anbieten (ODbL 4.6); Spiel selbst = Produced Work. Keine Rechtsberatung, vor Release juristisch prüfen |
| Bosporus-Geoplatzierung (`tools/bosphorus_geo.py`) | Einzelkoordinaten von Wahrzeichen aus Wikipedia, Structurae, Pleiades (Fakten, keine Kartenwerke); Uferlinien und Landmassen von Hand genähert | Projekteigentum | ungenaue Näherungen sind in `docs/BOSPHORUS_ENVIRONMENT_PLAN.md` markiert; vor finaler Art ggf. OSM-Küstenlinie (ODbL, Namensnennung) |
| Bosporus-Umgebung: Stadt, Wahrzeichen-Silhouetten, Tribünen, Fähren, Bäume, Zuschauer, Möwen, Flagge, Fassaden-/Wasser-/Zaun-Texturen, Ambient-Sounds | prozedural im Code erzeugt | Projekteigentum | Platzhalter-Art, nicht fotorealistisch; Sponsoren auf Bannern sind erfundene Namen; Straßennamen sind reale Ortsnamen (Fakten) |
| Bosphorus-GP-Identität: Emblem, İznik-inspirierte Fliese, Startportal, Boxengebäude, Zeitturm, Rennleitung, Podium, Pokal, türkische Banner | prozedural im Code erzeugt (`EventArt`, `EventGeometry`) | Projekteigentum | eigenes, fiktives Event ohne Bezug zu realen Rennserien, Teams oder Sponsoren; Schrift = eingebaute Unity-Schrift; Platzhalter-Art |
| CC0-Texturset `asphalt` (1024 px, 2 m je Kachel): Fahrbahn | [Poly Haven „Asphalt Track“](https://polyhaven.com/a/asphalt_track), Dimitrios Savva | CC0 1.0 | `Resources/Textures/CC0/asphalt`, importiert 2026-10-26 mit `tools/textures/import_cc0.py` (Herkunft und Prüfsummen in `source.json`); im Spiel auf Albedo 0,07 kalibriert |
| CC0-Texturset `runoff` (1024 px, 2,1 m je Kachel): Auslauf | [Poly Haven „Clean Asphalt“](https://polyhaven.com/a/clean_asphalt), Dimitrios Savva | CC0 1.0 | `Resources/Textures/CC0/runoff`, importiert 2026-10-26; im Spiel auf Albedo 0,09 kalibriert |
| CC0-Texturset `kerb` (1024 px, 3 m je Kachel): Kerbs, rot/weiß eingefärbt | [Poly Haven „Concrete Floor Worn 001“](https://polyhaven.com/a/concrete_floor_worn_001), Dimitrios Savva, Rico Cilliers | CC0 1.0 | `Resources/Textures/CC0/kerb`, importiert 2026-10-26, mit Lackbasis (`PaintBase`) |
| Materialien, Himmel | Unity-Standard-Shader (URP Lit, Skybox/Procedural) | Unity-Lizenz | keine Texturen von Dritten |
| Motor- und Reifensound | prozedural synthetisiert | Projekteigentum | keine Aufnahmen |
| Schrift (UI) | Unity-Standard-Font | Unity-Lizenz | wird in M4 durch eine lizenzierte Schrift ersetzt (Lizenz hier eintragen) |

## Regeln für neue Assets
- Wahrzeichen-Modelle werden als Prefab unter `Runtime/Resources/Landmarks/<id>` abgelegt (Konvention und Budgets: `docs/VISUAL_OVERHAUL.md`). Jedes Prefab braucht hier eine eigene Zeile.
- Pro Asset: Quelle, Autor, Lizenz (mit Link), Datum, Polygon- und Texturbudget, Änderungen.
- OpenStreetMap-Daten (ODbL): Attribution im Spiel; Share-Alike für abgeleitete Datenbanken. Vor kommerzieller Nutzung rechtlich prüfen lassen.
- Verboten: Google-Maps/Earth-Geometrie oder -Texturen, F1-Marken, echte Teams, Fahrer, Sponsoren, Lackierungen, Assets aus anderen Spielen, nicht lizenzierte Musik.
- Landmarken (Bosporus-Brücke, Moscheen, Galata-Turm): eigene Modelle nach lizenzierten Referenzfotos oder gekaufte Modelle mit kommerzieller Lizenz. Respektvoll und referenzgetreu darstellen.
