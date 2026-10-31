# Bosporus-Ufer und Ortaköy – Analyse, Küstenkorrektur, Streckenvorschlag

**Stand:** 2026-10-31.

**Status:**
- **Küstenkorrektur (Priorität 1):** implementiert und getestet (Simulation), im Unity-Editor nicht gesehen.
- **Ortaköy-Neuführung (Priorität 2):** nur Plan, **nicht umgesetzt**; die Freigabe des Product Owners steht aus.

## 1. Befund: Warum lagen Wahrzeichen so weit vom Wasser entfernt?
Geprüft habe ich gegen die OSM-Küstenlinie (`data/geo/osm/bosphorus_osm.json`, Wege `natural=coastline`):
- **Koordinaten und Projektion:** in Ordnung. Strecke, Gebäude und Wahrzeichen nutzen denselben lokalen Rahmen (Ursprung Dolmabahçe 41.03944/29.00167, x = Ost, z = Nord). Die OSM-Grundrisse von Dolmabahçe, Çırağan und der Moschee liegen exakt an der OSM-Küste.
- **Platzierung der Wahrzeichen:** in Ordnung. Die Positionen stammen aus den OSM-Grundrissen.
- **Straßenführung:** in Ordnung. Die Strecke folgt den echten Straßen (Çırağan Cd. 73–150 m landeinwärts, bebaut bis zum Wasser).
- **Falsch war die Land-/Wassergrenze im Spiel.**
  - Sie stammte aus der 0-m-Kontur des Copernicus-Höhenmodells (30 m). Über Wasser liefert dieses Modell in Ufernähe Werte über 0 m: Mischpixel, Kaianlagen, Schiffe, Rauschen.
  - Deshalb lief die Landmasse **200–450 m** in den Bosporus hinein.
  - Abstand des Spiel-Ufers zu den Wahrzeichen vorher: Dolmabahçe 381–410 m, Çırağan 320 m, Ortaköy-Moschee 250 m. Real sind es 54–93 m, 36 m und 21 m.
  - Auf diesem erfundenen Landstreifen pflanzte die Stadtgenerierung einen gleichmäßigen „Wald“ (Freiflächen-Bäume mit 30 % Dichte).

| | vorher (DEM) | nachher (OSM) |
|---|---|---|
| Ortaköy-Moschee – Ufer | 250 m | 25 m |
| Çırağan-Palast – Ufer | 320 m | 38 m |
| Dolmabahçe (Selamlık/Harem) – Ufer | 381 / 410 m | < 110 m |
| Bäume in der Stadt | 18.167 | 16.169 |

Karte: `docs/img/waterfront_coast_before_after.jpg` (oben vorher, unten nachher).

## 2. Umgesetzt: Küstenkorrektur
- **Land und Uferlinien:**
  - `tools/geo/osm_land.py` erzeugt die Landmassen und die beiden Uferlinien aus der OSM-Küstenlinie.
  - Technisch: doppelte Wege entfernt, Wege zu Ketten verbunden, offene Ketten am Auszugsrand mit Land zur Linken geschlossen, Douglas-Peucker mit 2,5 m.
  - `build_track_a.py` nutzt jetzt diesen Weg; das Höhenmodell liefert nur noch Höhen und verschiebt die Küste nicht mehr.
  - In `bosphorus_a.json` wurden nur `geo.land`, `shoreline` und `geo.shoreline_asia` ersetzt. Streckenpunkte, Höhen und Kurven sind unverändert.
- **Folgen im Spiel:** Wasserfläche, Gelände (unter Wasser −4 m) und Kollision folgen alle derselben Landmasse (`TerrainField.IsWater`). Gebäude, Bäume, Straßenmöbel und Stadtleben auf dem früheren Scheinland entfallen damit automatisch.
- **Ufer:** `Waterfront`, Darstellung `EnvironmentGeometry.WaterfrontEdge`.
  - Steinerne Kaimauer mit Abdeckstein entlang der echten Küste; die Oberkante folgt dem Gelände dahinter, mindestens 1,2 m über dem Wasser.
  - Auf der europäischen Seite eine 8 m breite gepflasterte Promenade mit Eisengeländer, Pollern (alle 18 m) und Laternen (alle 32 m, nachts leuchtend).
  - Die Promenade weicht der Strecke, Gebäuden, schmalen Molen und der Moschee-Terrasse aus.
  - Umfang: 4,5 km Kai, 3,5 km Promenade.
- **Freiflächen-Bäume:**
  - Dichte von 30 % auf 8 % gesenkt; in den ersten 40 m hinter dem Ufer gar keine mehr, damit der Blick aufs Wasser frei bleibt.
  - Echte Parks, Gärten (auch die Palastgärten), Friedhöfe und Straßenbäume bleiben unverändert.
- **Schiffsverkehr:** Die Transitrouten folgen jetzt der echten Fahrrinne (offene Wasserspanne je Querschnitt).
- **Test `TheWaterfrontLandmarksStandAtTheRealCoast`:**
  - Moschee < 40 m, Çırağan < 60 m und die Dolmabahçe-Flügel < 110 m vom Ufer
  - Kaimauern ≥ 4 km
  - Promenade nie im Wasser und nie in der Sperrzone
  - kein Baum auf dem Wasser

## 3. Ortaköy – räumliche Lage (Grundlage für den Streckenvorschlag)
- **Platz:** Der echte Ortaköy-Platz (OSM: Fußgängerfläche) ist ein 15–35 m breiter Streifen von der Moschee nach Norden zur Muallim Naci Caddesi. Ringsum liegt der dichte Basar mit 5-m-Gassen.
- **Freiraum:** `docs/img/waterfront_free.jpg` zeigt den Abstand zum nächsten Gebäude bzw. zum Wasser. Nur die Muallim Naci Cd. und der Platzstreifen bieten Platz für eine 11-m-Fahrbahn mit Barrieren.
- **Heute:** Die Ortaköy-Kehre liegt am Ende der Çırağan Cd. Die Moschee ist von dort 200 m entfernt und hinter Häusern verborgen (`docs/img/waterfront_ortakoy_now.jpg`).
- **Engstelle:** Zwischen der heutigen Kehre und dem Platz ist die Muallim Naci Cd. auf 180 m nur 10–18 m breit (Fassade zu Fassade).
- **Folge für die Planung:** Jede Kurve am Platz braucht dort Hin- und Rückweg nebeneinander.
- **Ausgeschlossen:** Eine erste Variante mit zwei Fahrbahnen hätte 13 Gebäude geschnitten, darunter die **Etz-Ahayim-Synagoge**. Diese Linienführung ist verworfen.

Der Vorschlag „Meydan-Kehre“ (Variante A) und die Untersuchung der Kurvenradien folgen in Abschnitt 4. Umgesetzt wird erst nach Freigabe.
