# Geodaten Bosphorus GP

| Datei | Inhalt | Herkunft / Lizenz |
|---|---|---|
| `osm/*.overpassql` | Overpass-Abfragen für die OSM-Auszüge | Projekt |
| `osm/*.osm` (nicht eingecheckt) | OSM-Rohdaten, Stand 2026-10-26 | © OpenStreetMap-Mitwirkende, ODbL 1.0 |
| `osm/bosphorus_osm.json` (nicht eingecheckt, `tools/geo/osm_import.py`) | Straßen, Küste, Gebäude, Grünflächen im lokalen Metersystem | abgeleitete Datenbank, ODbL 1.0 |
| `track_variants.json`, `track_plan_A.json` | Streckenvarianten entlang der OSM-Straßenachsen, Profil, Kurven, Sichtachsen | abgeleitete Datenbank aus OSM (ODbL 1.0) und Copernicus DEM |
| `dem/*.tif` (nicht eingecheckt, `tools/geo/fetch_dem.sh`) | Copernicus DEM GLO-30 | Copernicus WorldDEM-30-Lizenz, Quellenangabe Pflicht |

Die aus OSM abgeleiteten Dateien stehen unter der **Open Database License 1.0** (© OpenStreetMap-Mitwirkende) und werden bei Veröffentlichung des Spiels so angeboten. Reproduzierbar mit `tools/geo/osm_import.py`, `route_variants.py` und `track_analysis.py`.
